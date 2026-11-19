using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The relationships of a village as one game component. It owns no clock of its own: the tick of each Update is the only time there is, an <see cref="IRelationshipClock"/> turns it into a step, and the
/// relationships move to that step in the Update and nowhere else. A request event (add an actor, say what it is doing, an interaction, a grant, a memory, a need, an axis, a bond, a mood or opinion query) only
/// queues; the component answers it inside its own Update, in the order the requests arrived, with events. Then time moves, and what the steps changed is raised as events (moods, needs, states, bonds, memories,
/// moodlets, evictions), at most <c>maxEvents</c> per Update: the rest is owed and follows in the next ones, in order, none is lost. Every event is deferred (delivered after the Update returns). The facts the
/// decay rules read that the world owns (the season, the weather) come from the host delegate at the moment of each Update.
/// </summary>
/// <remarks>
/// One component holds one set of relationships. A game with more than one (the village and a ship) adds a component for each, with its own id and its own <c>channel</c>; a component answers the requests of its
/// channel only. A request is answered by exactly one component: the one that serves its channel, or the <see cref="RelationshipChannels"/> registry, which every component is built with and which must be
/// installed on the session (<see cref="ChannelRegistry.Install"/>), refuses it with <see cref="RelationshipChannels.UnknownChannel"/> when nobody serves the channel.
/// </remarks>
public sealed partial class RelationshipComponent : IGameComponent
{
    public const string NotQuiet = "REL_NOT_QUIET";

    /// <summary>The request id on the refusal of the clock itself (a step that goes backwards) and of the advance (a host fact the decay rules need and the host did not give).</summary>
    public const string ClockRequestId = "clock";
    public const string AdvanceRequestId = "advance";

    private readonly List<RelationshipPending> _pending = new();
    private readonly Queue<Action<IEventPort>> _owed = new();
    private readonly IRelationshipClock _clock;
    private readonly Func<IReadOnlyDictionary<string, FactValue>> _hostFacts;
    private readonly int _maxEvents;
    private readonly RelationshipJournal? _journal;
    private IEventPort? _events;
    private long _lastTick;
    private long _answered;
    private string _advanceFailure = "";
    private string _journaledFacts = "";
    private long _journaledStep = -1;

    /// <param name="clock">How the tick becomes a step.</param>
    /// <param name="hostFacts">The facts the document declares for the host, as they are now; called once per Update.</param>
    /// <param name="maxEvents">The most change events raised in one Update; the rest are owed.</param>
    /// <param name="journal">When given, every request is written to it with the tick it was applied at, and the host facts and the step whenever they change, so the session can be replayed.</param>
    public RelationshipComponent(RelationshipRuntime runtime, IRelationshipClock clock, Func<IReadOnlyDictionary<string, FactValue>> hostFacts, RelationshipChannels channels, string id = "relationship", string channel = "", int maxEvents = 2000, RelationshipJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        ArgumentNullException.ThrowIfNull(hostFacts);
        _hostFacts = hostFacts;
        ArgumentNullException.ThrowIfNull(channels);
        Channels = channels;
        if (maxEvents < 1) throw new ArgumentException("at least one event must be raised per Update", nameof(maxEvents));
        _maxEvents = maxEvents;
        _journal = journal;
        Id = id;
        Channel = channel;
    }

    public string Id { get; }

    /// <summary>The channel whose requests this component answers and which it writes on everything it raises; empty for the only village of a game.</summary>
    public string Channel { get; }

    /// <summary>The registry this component is served through and refused through; the one installed on its session.</summary>
    public RelationshipChannels Channels { get; }

    /// <summary>The relationships this component answers from; its state is theirs.</summary>
    public RelationshipRuntime Runtime { get; }

    /// <summary>The tick of the latest Update; what a checkpoint taken now belongs to.</summary>
    public long Tick => _lastTick;

    /// <summary>How many requests have been answered, refused ones included.</summary>
    public long Answered => _answered;

    /// <summary>How many change events are waiting to be raised in the next Updates.</summary>
    public int Owed => _owed.Count;

    /// <summary>True when no request is waiting for the next Update and no change event is owed. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0 && _owed.Count == 0;

    public string Fingerprint() => $"relationship tick={_lastTick} step={Runtime.Step} state={Runtime.StateDigest()[..16]} answered={_answered} pending={_pending.Count} owed={_owed.Count}";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        Channels.Serve(Channel, Id);
        RelationshipRequestPort.Connect(context.Events, Queue);
    }

    /// <summary>Takes a request that is addressed to this component's channel; a request for another channel is that channel's, not this one's.</summary>
    private void Queue(string requestChannel, RelationshipPending request)
    {
        if (requestChannel == Channel) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        if (!Channels.RunsOn(events)) throw new InvalidOperationException("the relationship channels registry of component '" + Id + "' does not run on this session (it was installed on another one, or its session was not initialised), so requests for channels nobody serves could not be answered");
        var tick = context.Tick;
        _lastTick = tick;
        var facts = _hostFacts();
        _journal?.WriteFacts(tick, facts, ref _journaledFacts);
        if (_pending.Count > 0)
        {
            var requests = _pending.ToArray();
            _pending.Clear();
            foreach (var request in requests)
            {
                _journal?.WriteRequest(tick, Channel, request);
                Answer(events, tick, request, facts);
            }
        }
        Live(events, tick, facts);
        for (var raised = 0; raised < _maxEvents && _owed.Count > 0; raised++) _owed.Dequeue()(events);
    }

    /// <summary>Moves the relationships to the step the clock gives for this tick, and queues what that changed.</summary>
    private void Live(IEventPort events, long tick, IReadOnlyDictionary<string, FactValue> facts)
    {
        var target = _clock.StepAt(tick);
        _journal?.WriteStep(tick, target, ref _journaledStep);
        var from = Runtime.Step;
        try
        {
            var changes = Runtime.Advance(target, facts);
            _advanceFailure = "";
            if (Runtime.Step == from) return;
            events.Emit(new RelationshipStepped(Id, Channel, tick, from, Runtime.Step, Runtime.Owed(target)));
            Owe(tick, changes);
        }
        catch (EngineRefusedException refused)
        {
            Report(events, tick, refused);
        }
    }

    /// <summary>A step that goes backwards, or a fact the decay rules need and the host did not give, is the host's mistake: it is published once per kind of failure and the relationships have not moved.</summary>
    private void Report(IEventPort events, long tick, EngineRefusedException refused)
    {
        if (refused.Code != _advanceFailure) events.Emit(new RelationshipRefused(Id, Channel, refused.Code == RelationshipCodes.StepOrder ? ClockRequestId : AdvanceRequestId, tick, refused.Code));
        _advanceFailure = refused.Code;
    }
}
