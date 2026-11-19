using System.Globalization;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The calendar as one game component. It owns no clock of its own: the tick of each Update is the only time there is, and the calendar moves by the time scale (a rational number of calendar ticks per game tick)
/// in the Update and nowhere else. A request event (scale, pause, skip, follow an actor, interrupt, where) only queues; the component answers it inside its own Update, in the order the requests arrived, with
/// events. Then time moves, and what it crossed is raised as events: the day, the part of the day, the season, the holidays; then the actors whose schedule put them in a new block are announced, at most
/// <c>maxAnnouncements</c> per Update (the rest stay owed and follow in the next ones, none is lost). The facts a schedule reads that the world owns (weather, a story flag) come from the host delegate at the moment of each Update.
/// </summary>
/// <remarks>
/// One component holds one calendar. A game with more than one (a world calendar and a dungeon with its own clock) adds a component for each, with its own id and its own <c>channel</c>; a component answers the
/// requests of its channel only. A request is answered by exactly one component: the one that serves its channel, or the <see cref="CalendarChannels"/> registry, which every component is built with and which must be installed on the
/// session (<see cref="CalendarChannels.Install"/>), refuses it with <see cref="CalendarChannels.UnknownChannel"/> when nobody serves the channel.
/// </remarks>
public sealed partial class CalendarComponent : IGameComponent
{
    public const string NotQuiet = "CAL_NOT_QUIET";

    /// <summary>The request id on the refusal of the clock itself (a tick that goes backwards) and of the announcement of schedule changes (a fact the schedules need and the host did not give).</summary>
    public const string ClockRequestId = "clock";
    public const string AnnounceRequestId = "announce";

    private readonly List<CalendarPending> _pending = new();
    private readonly Func<IReadOnlyDictionary<string, FactValue>> _hostFacts;
    private readonly int _maxAnnouncements;
    private readonly CalendarJournal? _journal;
    private IEventPort? _events;
    private long _lastTick;
    private long _answered;
    private bool _dirty = true;
    private long _announcedMinute = long.MinValue;
    private string _announcedFacts = "";
    private string _announceFailure = "";
    private CalendarContext? _context;
    private long _contextInstant = long.MinValue;
    private string _journaledFacts = "";

    /// <param name="hostFacts">The facts the document declares for the host, as they are now; called once per Update.</param>
    /// <param name="maxAnnouncements">The most actors announced in one Update.</param>
    /// <param name="journal">When given, every request is written to it with the tick it was applied at, and the host facts whenever they change, so the session can be replayed.</param>
    public CalendarComponent(CalendarRuntime runtime, Func<IReadOnlyDictionary<string, FactValue>> hostFacts, CalendarChannels channels, string id = "calendar", string channel = "", int maxAnnouncements = 1000, CalendarJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
        ArgumentNullException.ThrowIfNull(hostFacts);
        _hostFacts = hostFacts;
        ArgumentNullException.ThrowIfNull(channels);
        Channels = channels;
        if (maxAnnouncements < 1) throw new ArgumentException("at least one actor must be announced per Update", nameof(maxAnnouncements));
        _maxAnnouncements = maxAnnouncements;
        _journal = journal;
        Id = id;
        Channel = channel;
        _lastTick = runtime.LastTick;
    }

    public string Id { get; }

    /// <summary>The channel whose requests this component answers and which it writes on everything it raises; empty for the only calendar of a game.</summary>
    public string Channel { get; }

    /// <summary>The registry this component is served through and refused through; the one installed on its session by <see cref="CalendarChannels.Install"/>.</summary>
    public CalendarChannels Channels { get; }

    /// <summary>The calendar this component answers from; its state is the calendar's state.</summary>
    public CalendarRuntime Runtime { get; }

    /// <summary>How many requests have been answered, refused ones included.</summary>
    public long Answered => _answered;

    /// <summary>True when no request is waiting for the next Update. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0;

    public string Fingerprint() => $"calendar tick={_lastTick} state={CalendarSave.StateDigest(Runtime)[..16]} answered={_answered} pending={_pending.Count}";

    /// <summary>
    /// The facts of the calendar at its instant together with the host's: what a dialogue condition or an AI sensor reads. A dialogue document declares them as external facts and reads them as <c>ext.</c> followed by the
    /// id, so a dialogue component is given <c>Facts("ext.")</c>.
    /// </summary>
    public IReadOnlyDictionary<string, FactValue> Facts(string prefix = "")
    {
        var all = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var (id, value) in CalendarFacts.Read(Runtime.Set, Runtime.Set.System.MomentOf(Runtime.Instant))) all[prefix + id] = value;
        foreach (var (id, value) in _hostFacts()) all[prefix + id] = value;
        return all;
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        Channels.Serve(Channel, Id);
        CalendarRequestPort.Connect(context.Events, Queue);
    }

    /// <summary>Takes a request that is addressed to this component's channel; a request for another channel is that channel's, not this one's.</summary>
    private void Queue(string requestChannel, CalendarPending request)
    {
        if (requestChannel == Channel) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        if (!Channels.RunsOn(events)) throw new InvalidOperationException("the calendar channels registry of component '" + Id + "' does not run on this session (it was installed on another one, or its session was not initialised), so requests for channels nobody serves could not be answered");
        var tick = context.Tick;
        _context = null;
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
        TimeStep? step = null;
        try { step = Runtime.Step(tick); }
        catch (EngineRefusedException refused)
        {
            // A tick that goes backwards or jumps too far is the host's mistake: it is published with its code and the calendar has not moved.
            events.Emit(new CalendarRefused(Id, Channel, ClockRequestId, tick, refused.Code));
        }
        if (step is not null) Publish(events, tick, step);
        Announce(events, tick, facts);
    }

}
