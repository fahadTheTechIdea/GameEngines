using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// The world of weather, hazards, diseases and random events as one game component. It owns no clock of its own: the tick of each Update is the only time there is, an <see cref="IWorldClock"/> turns it into a step and a
/// season for every step, and the world lives up to that step in the Update and nowhere else. A request event (an intervention, a storyteller, a difficulty, a hazard, an outbreak, a card, a weather or disease query)
/// only queues; the component answers it inside its own Update, in the order the requests arrived, with events. What the world did in the steps it lived (seasons, weather, hazards, outbreaks, cards, the director, the
/// effects it asks of other layers) is raised as events in the order it happened, at most <c>maxEvents</c> per Update: the rest is owed and follows in the next ones, in order, none is lost. Every event is deferred
/// (delivered after the Update returns). When the owed queue is already longer than <c>maxOwed</c>, time waits (the world does not move) until the host has taken the events: back-pressure instead of unbounded memory.
/// </summary>
/// <remarks>
/// One component holds one world. A game with more than one (parallel worlds) adds a component for each, with its own id and its own <c>channel</c>; a component answers the requests of its channel only. A request is
/// answered by exactly one component: the one that serves its channel, or the <see cref="WorldChannels"/> registry, which every component is built with and which must be installed on the session
/// (<see cref="ChannelRegistry.Install"/>), refuses it with <see cref="WorldChannels.UnknownChannel"/> when nobody serves the channel. The other layers do not call this component: they listen to its events or read its
/// facts through the bridges.
/// </remarks>
public sealed partial class WorldComponent : IGameComponent
{
    public const string NotQuiet = "WE_NOT_QUIET";

    /// <summary>While more events than this are owed, time waits.</summary>
    public const int DefaultMaxOwed = 20_000;

    /// <summary>The most steps one Update lives unless told otherwise: a year of days.</summary>
    public const int DefaultMaxStepsPerUpdate = 366;

    /// <summary>The refusal for a request whose kind is none of <see cref="WorldRequestKind"/>.</summary>
    public const string UnknownKind = "WE_UNKNOWN_KIND";

    /// <summary>The request id on the refusal of the clock itself (a step that goes backwards, or a season it cannot name) and of the advance (a host fact the document declares and the host did not give).</summary>
    public const string ClockRequestId = "clock";
    public const string AdvanceRequestId = "advance";

    private readonly List<WorldPending> _pending = new();
    private readonly Queue<Action<IEventPort>> _owed = new();
    private readonly IWorldClock _clock;
    private readonly Func<IReadOnlyDictionary<string, FactValue>> _hostFacts;
    private readonly int _maxEvents;
    private readonly int _maxOwed;
    private readonly int _maxStepsPerUpdate;
    private readonly WorldJournal? _journal;
    private IEventPort? _events;
    private long _lastTick;
    private long _answered;
    private long _held;
    private string _clockFailure = "";
    private string _advanceFailure = "";
    private string _journaledFacts = "";
    private long _journaledStep = long.MinValue;

    /// <param name="clock">How the tick becomes a step and each step a season.</param>
    /// <param name="hostFacts">The facts the document declares for the host, as they are now; called once per Update.</param>
    /// <param name="maxEvents">The most change events raised in one Update; the rest are owed.</param>
    /// <param name="journal">When given, every request is written to it with the tick it was applied at, and the host facts and the step whenever they change, so the session can be replayed.</param>
    /// <param name="maxOwed">While more events than this are owed, time waits.</param>
    /// <param name="maxStepsPerUpdate">The most steps the world lives in one Update. A clock that is ahead (a skip, or time that waited for the host to take the events) is caught up this many steps at a time, so one Update never lives a year at once.</param>
    public WorldComponent(WorldRuntime runtime, IWorldClock clock, Func<IReadOnlyDictionary<string, FactValue>> hostFacts, WorldChannels channels, string id = "world", string channel = "",
        int maxEvents = 2000, WorldJournal? journal = null, int maxOwed = DefaultMaxOwed, int maxStepsPerUpdate = DefaultMaxStepsPerUpdate)
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
        if (maxStepsPerUpdate < 1 || maxStepsPerUpdate > WorldRuntime.MaxAdvance) throw new ArgumentException("an Update lives 1 to " + WorldRuntime.MaxAdvance + " steps", nameof(maxStepsPerUpdate));
        _maxEvents = maxEvents;
        _maxOwed = maxOwed;
        _maxStepsPerUpdate = maxStepsPerUpdate;
        _journal = journal;
        Id = id;
        Channel = channel;
    }

    public string Id { get; }

    /// <summary>The channel whose requests this component answers and which it writes on everything it raises; empty for the only world of a game.</summary>
    public string Channel { get; }

    /// <summary>The registry this component is served through and refused through; the one installed on its session.</summary>
    public WorldChannels Channels { get; }

    /// <summary>The world this component answers from; its state is theirs.</summary>
    public WorldRuntime Runtime { get; }

    /// <summary>The tick of the latest Update; what a checkpoint taken now belongs to.</summary>
    public long Tick => _lastTick;

    /// <summary>How many requests have been answered, refused ones included.</summary>
    public long Answered => _answered;

    /// <summary>How many change events are waiting to be raised in the next Updates.</summary>
    public int Owed => _owed.Count;

    /// <summary>How many Updates the world waited because the owed queue was full (back-pressure).</summary>
    public long Held => _held;

    /// <summary>True when no request is waiting for the next Update and no change event is owed. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0 && _owed.Count == 0;

    public string Fingerprint() => $"world tick={_lastTick} step={Runtime.Step} state={Runtime.StateDigest()[..16]} answered={_answered} pending={_pending.Count} owed={_owed.Count}";

    /// <summary>The world as facts for the other layers (see <see cref="WorldRuntime.Facts"/>): the one way a bridge reads the state.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts(string prefix, string region) => Runtime.Facts(prefix, region);

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        Channels.Serve(Channel, Id);
        WorldRequestPort.Connect(context.Events, Queue);
    }

    /// <summary>Takes a request that is addressed to this component's channel; a request for another channel is that channel's, not this one's.</summary>
    private void Queue(string requestChannel, WorldPending request)
    {
        if (requestChannel == Channel) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        if (!Channels.RunsOn(events)) throw new InvalidOperationException("the world channels registry of component '" + Id + "' does not run on this session (it was installed on another one, or its session was not initialised), so requests for channels nobody serves could not be answered");
        var tick = context.Tick;
        _lastTick = tick;
        var facts = _hostFacts();
        _journal?.WriteFacts(tick, facts, ref _journaledFacts);
        if (_owed.Count >= _maxOwed) _held++;
        else MoveTime(events, tick, facts);
        if (_pending.Count > 0)
        {
            var requests = _pending.ToArray();
            _pending.Clear();
            foreach (var request in requests)
            {
                _journal?.WriteRequest(tick, Channel, request);
                Answer(events, tick, request);
            }
        }
        for (var raised = 0; raised < _maxEvents && _owed.Count > 0; raised++) _owed.Dequeue()(events);
    }

    /// <summary>Lives the world to the step the clock gives for this tick, before the requests of the tick are answered, so what a request does is stamped with the new step.</summary>
    private void MoveTime(IEventPort events, long tick, IReadOnlyDictionary<string, FactValue> facts)
    {
        var from = Runtime.Step;
        var wanted = _clock.StepAt(tick);
        var target = wanted > from ? Math.Min(wanted, from + _maxStepsPerUpdate) : wanted;          // a clock that is far ahead is caught up a bounded number of steps an Update
        var runs = new List<SeasonRun>();
        var clockFault = false;
        try
        {
            if (target > from)
                for (var step = from + 1; step <= target; step++)
                {
                    var season = _clock.SeasonAt(step);
                    if (runs.Count > 0 && runs[^1].Season == season) runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
                    else runs.Add(new SeasonRun(step, season, 1));
                }
        }
        catch (EngineRefusedException refused)
        {
            // The clock could not say what season a step is in (a calendar asked beyond its range): the fault is published, the world does not move, and the journal keeps the step with no seasons.
            Report(events, tick, ClockRequestId, refused, ref _clockFailure);
            clockFault = true;
            runs.Clear();
        }
        _journal?.WriteStep(tick, target, runs, ref _journaledStep);
        if (clockFault) return;
        try
        {
            var changes = Runtime.Advance(target, _clock.SeasonAt, facts);
            _clockFailure = "";
            _advanceFailure = "";
            Owe(tick, changes);
        }
        catch (EngineRefusedException refused)
        {
            // A step that goes backwards, an unknown season or a host fact the document needs and the host did not give is the host's mistake: it is published once per kind of failure (until the same call works again) and the world has not moved.
            if (refused.Code is WorldCodes.FactMissing or WorldCodes.FactInvalid) Report(events, tick, AdvanceRequestId, refused, ref _advanceFailure);
            else Report(events, tick, ClockRequestId, refused, ref _clockFailure);
        }
    }

    private void Report(IEventPort events, long tick, string requestId, EngineRefusedException refused, ref string last)
    {
        if (refused.Code != last) events.Emit(new WorldRefused(Id, Channel, requestId, tick, refused.Code));
        last = refused.Code;
    }
}
