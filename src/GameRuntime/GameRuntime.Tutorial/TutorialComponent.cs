using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

namespace GameRuntime;

/// <summary>
/// The tutorial and the first-run journey of one player as one game component. It owns no clock of its own: the tick of each Update is the only time there is, an <see cref="ITutorialClock"/> turns it into a
/// step, and the tutorial moves to that step in the Update and nowhere else. A request event (observe something the player did, touch, start, skip, replay, dismiss a tip, the journey's language, next, back, skip,
/// entry, replay, an intent, a setting, a progress query) only queues; the component answers it inside its own Update, in the order the requests arrived, with events (a progress query is answered last, after the tutorial has looked at what the player did this tick). Then the tutorial is evaluated against the
/// host facts of that moment, and what that changed (steps, hints, tips, skills, the phase) is raised as events, at most <c>maxEvents</c> per Update: the rest is owed and follows in the next ones, in order, none is
/// lost. Every event is deferred (delivered after the Update returns). When the evaluation would add to an owed queue that is already longer than <c>maxOwed</c>, time waits (nothing moves) until the host has taken
/// the events. A change the player's saved progress holds is followed by <see cref="TutorialSaveNeeded"/>.
/// </summary>
/// <remarks>
/// One component holds one player's tutorial. A game with more than one (two local players) adds a component for each, with its own id and its own <c>channel</c>; a component answers the requests of its channel
/// only. A request is answered by exactly one component: the one that serves its channel, or the <see cref="TutorialChannels"/> registry, which every component is built with and which must be installed on the
/// session (<see cref="ChannelRegistry.Install"/>), refuses it with <see cref="TutorialChannels.UnknownChannel"/> when nobody serves the channel.
/// </remarks>
public sealed partial class TutorialComponent : IGameComponent
{
    public const string NotQuiet = "TUT_NOT_QUIET";

    /// <summary>While more events than this are owed, time waits.</summary>
    public const int DefaultMaxOwed = 20_000;

    /// <summary>The refusal for a request whose kind is none of <see cref="TutorialRequestKind"/>.</summary>
    public const string UnknownKind = "TUT_UNKNOWN_KIND";

    /// <summary>The request id on the refusal of the clock itself (a step that goes backwards) and of the evaluation (a host fact the document needs and the host did not give).</summary>
    public const string ClockRequestId = "clock";
    public const string EvaluateRequestId = "evaluate";

    private readonly List<TutorialPending> _pending = new();
    private readonly Queue<Action<IEventPort>> _owed = new();
    private readonly ITutorialClock _clock;
    private readonly Func<IReadOnlyDictionary<string, FactValue>> _hostFacts;
    private readonly int _maxEvents;
    private readonly int _maxOwed;
    private readonly TutorialJournal? _journal;
    private IEventPort? _events;
    private long _lastTick;
    private long _answered;
    private string _clockFailure = "";
    private string _evaluateFailure = "";
    private string _journaledFacts = "";
    private long _journaledStep = -1;

    /// <param name="clock">How the tick becomes a step.</param>
    /// <param name="hostFacts">The facts the document declares for the host, as they are now; called once per Update.</param>
    /// <param name="maxEvents">The most change events raised in one Update; the rest are owed.</param>
    /// <param name="journal">When given, every request is written to it with the tick it was applied at, and the host facts and the step whenever they change, so the session can be replayed.</param>
    /// <param name="maxOwed">While more events than this are owed, time waits.</param>
    public TutorialComponent(TutorialRuntime runtime, ITutorialClock clock, Func<IReadOnlyDictionary<string, FactValue>> hostFacts, TutorialChannels channels, string id = "tutorial", string channel = "",
        int maxEvents = 2000, TutorialJournal? journal = null, int maxOwed = DefaultMaxOwed)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(hostFacts);
        ArgumentNullException.ThrowIfNull(channels);
        Runtime = runtime;
        _clock = clock;
        _hostFacts = hostFacts;
        Channels = channels;
        if (maxEvents < 1) throw new ArgumentException("at least one event must be raised per Update", nameof(maxEvents));
        if (maxOwed < maxEvents) throw new ArgumentException("the owed queue holds at least what one Update raises", nameof(maxOwed));
        _maxEvents = maxEvents;
        _maxOwed = maxOwed;
        _journal = journal;
        Id = id;
        Channel = channel;
    }

    public string Id { get; }

    /// <summary>The channel whose requests this component answers and which it writes on everything it raises; empty for the only player of a game.</summary>
    public string Channel { get; }

    /// <summary>The registry this component is served through and refused through; the one installed on its session.</summary>
    public TutorialChannels Channels { get; }

    /// <summary>The tutorial this component answers from; its state is theirs.</summary>
    public TutorialRuntime Runtime { get; }

    /// <summary>The tick of the latest Update; what a checkpoint taken now belongs to.</summary>
    public long Tick => _lastTick;

    /// <summary>How many requests have been answered, refused ones included.</summary>
    public long Answered => _answered;

    /// <summary>How many change events are waiting to be raised in the next Updates.</summary>
    public int Owed => _owed.Count;

    /// <summary>True when no request is waiting for the next Update and no change event is owed. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0 && _owed.Count == 0;

    public string Fingerprint() => $"tutorial tick={_lastTick} step={Runtime.Step} state={Runtime.StateDigest()[..16]} answered={_answered} pending={_pending.Count} owed={_owed.Count}";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        Channels.Serve(Channel, Id);
        TutorialRequestPort.Connect(context.Events, Queue);
    }

    /// <summary>Takes a request that is addressed to this component's channel; a request for another channel is that channel's, not this one's.</summary>
    private void Queue(string requestChannel, TutorialPending request)
    {
        if (requestChannel == Channel) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        if (!Channels.RunsOn(events)) throw new InvalidOperationException("the tutorial channels registry of component '" + Id + "' does not run on this session (it was installed on another one, or its session was not initialised), so requests for channels nobody serves could not be answered");
        var tick = context.Tick;
        _lastTick = tick;
        var facts = _hostFacts();
        _journal?.WriteFacts(tick, facts, ref _journaledFacts);
        var waits = _owed.Count >= _maxOwed;
        if (!waits) MoveTime(events, tick);
        var persist = false;
        var queries = new List<TutorialPending>();
        if (_pending.Count > 0)
        {
            var requests = _pending.ToArray();
            _pending.Clear();
            foreach (var request in requests)
            {
                _journal?.WriteRequest(tick, Channel, request);
                if (request.Kind == TutorialRequestNames.NameOf(TutorialRequestKind.Progress)) queries.Add(request);
                else persist |= Answer(events, tick, request);
            }
        }
        if (!waits) persist |= Live(events, tick, facts);
        foreach (var query in queries) Answer(events, tick, query);       // a query is answered after the tutorial has looked at what the player did this tick
        if (persist) _owed.Enqueue(port => port.Emit(new TutorialSaveNeeded(Id, Channel, tick)));
        for (var raised = 0; raised < _maxEvents && _owed.Count > 0; raised++) _owed.Dequeue()(events);
    }

    /// <summary>Moves the tutorial to the step the clock gives for this tick, before the requests of the tick are answered, so what the player did is stamped with it.</summary>
    private void MoveTime(IEventPort events, long tick)
    {
        var target = _clock.StepAt(tick);
        _journal?.WriteStep(tick, target, ref _journaledStep);
        try
        {
            Runtime.MoveTo(target);
            _clockFailure = "";
        }
        catch (EngineRefusedException refused) { Report(events, tick, ClockRequestId, refused, ref _clockFailure); }
    }

    /// <summary>Evaluates the tutorial against the facts, and queues what that changed. Returns whether the player's saved progress changed.</summary>
    private bool Live(IEventPort events, long tick, IReadOnlyDictionary<string, FactValue> facts)
    {
        try
        {
            var changes = Runtime.Evaluate(facts);
            _evaluateFailure = "";
            Owe(tick, changes);
            return changes.Persist;
        }
        catch (EngineRefusedException refused)
        {
            Report(events, tick, EvaluateRequestId, refused, ref _evaluateFailure);
            return false;
        }
    }

    /// <summary>A step that goes backwards, or a fact the document needs and the host did not give, is the host's mistake: it is published once per kind of failure (until the same call works again) and the tutorial has not moved.</summary>
    private void Report(IEventPort events, long tick, string requestId, EngineRefusedException refused, ref string last)
    {
        if (refused.Code != last) events.Emit(new TutorialRefused(Id, Channel, requestId, tick, refused.Code));
        last = refused.Code;
    }
}
