namespace GameRuntime;

public enum RuntimeState { Created, Running, Ended, Faulted, Disposed }

/// <summary>Receives every failure the session catches. One reporter for log, audit and exceptions.</summary>
public interface IRuntimeReporter
{
    void Report(string code, string message, Exception? exception);
}

/// <summary>The thin host contract: Init, Update, a clock and an event surface.</summary>
public interface IGameRuntime : IDisposable
{
    RuntimeState State { get; }
    void Init();
    /// <summary>Runs the ticks due for the host's elapsed time; returns how many ran.</summary>
    int Update(long elapsedMicroseconds);
    IRuntimeClock Clock { get; }
    /// <summary>Queues an input event; it is delivered at the start of the next tick, in submission order.</summary>
    void Submit<T>(in T inputEvent) where T : struct;
    /// <summary>Subscribe to events; they arrive after the tick's drain, never during it.</summary>
    void Subscribe<T>(Action<T> handler) where T : struct;
}

/// <summary>
/// The reference host: one instance per session, the only Init/Update and the only clock. Each due tick it updates every
/// component in registered order and drains the events each one emitted before the next component runs. Any exception
/// stops the session (fail-stop) and is reported once.
/// </summary>
public sealed class RuntimeSession : IGameRuntime
{
    private readonly record struct Slot(IGameComponent Component, int Order);

    private readonly List<Slot> _components = new();
    private readonly List<Action> _input = new();
    private readonly RuntimeEventBus _bus;
    private readonly RuntimeClock _clock;
    private readonly IRuntimeReporter _reporter;
    private bool _endedRequested;

    public RuntimeSession(RuntimeClock clock, IRuntimeReporter reporter, int eventBudgetPerDrain = 10_000)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        _bus = new RuntimeEventBus(eventBudgetPerDrain);
        foreach (var declare in RuntimeEventCatalog.Standard) declare(this);
    }

    public RuntimeState State { get; private set; } = RuntimeState.Created;
    public IRuntimeClock Clock => _clock;

    /// <summary>Declares an event owned by a component before Init. The standard catalog is declared already.</summary>
    public void DeclareEvent<T>(string id, int version) where T : struct
    {
        RequireState(RuntimeState.Created, "events are declared before Init");
        _bus.Declare<T>(id, version);
    }

    /// <summary>Registers a component. The order must be unique, as SimEngine's systems require.</summary>
    public void AddComponent(IGameComponent component, int order)
    {
        ArgumentNullException.ThrowIfNull(component);
        RequireState(RuntimeState.Created, "components are added before Init");
        foreach (var slot in _components)
        {
            if (slot.Order == order) throw new InvalidOperationException("components '" + slot.Component.Id + "' and '" + component.Id + "' share order " + order);
            if (slot.Component.Id == component.Id) throw new InvalidOperationException("component id '" + component.Id + "' used twice");
        }
        _components.Add(new Slot(component, order));
        _components.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public void Init()
    {
        RequireState(RuntimeState.Created, "Init succeeds once");
        try
        {
            _bus.Freeze();
            _bus.Connect<SessionEnded>(_ => _endedRequested = true);
            var context = new ComponentContext(_bus);
            foreach (var slot in _components) slot.Component.Init(context);
            _bus.CloseConnections();
            _bus.Drain();
            State = RuntimeState.Running;
        }
        catch (Exception exception)
        {
            Fail(exception, "init");
            throw;
        }
    }

    public int Update(long elapsedMicroseconds)
    {
        if (State == RuntimeState.Created) throw new InvalidOperationException("Init the runtime before Update");
        if (State == RuntimeState.Disposed) throw new ObjectDisposedException(nameof(RuntimeSession));
        if (State != RuntimeState.Running) return 0;
        var due = _clock.Earn(elapsedMicroseconds);
        var ran = 0;
        var current = "session";
        try
        {
            while (ran < due && State == RuntimeState.Running)
            {
                var tick = _clock.Advance();
                var context = new UpdateContext(tick, _clock.TickMicroseconds, _clock.Alpha, tick * RuntimeClock.MicrosecondsPerSecond / _clock.TicksPerSecond);
                current = "input";
                var queued = _input.ToArray();
                _input.Clear();
                foreach (var deliver in queued) deliver();
                _bus.Drain();
                foreach (var slot in _components)
                {
                    current = slot.Component.Id;
                    slot.Component.Update(context);
                    _bus.Drain();
                }
                current = "session";
                _bus.Emit(new TickSettled(tick));
                _bus.Drain();
                ran++;
                if (_endedRequested)
                {
                    State = RuntimeState.Ended;
                    _clock.DiscardPending();
                }
            }
        }
        catch (Exception exception)
        {
            Fail(exception, current);
        }
        _bus.DeliverOutbound();
        return ran;
    }

    public void Submit<T>(in T inputEvent) where T : struct
    {
        if (State is RuntimeState.Disposed or RuntimeState.Faulted or RuntimeState.Ended)
            throw new InvalidOperationException("the session is not accepting input in state " + State);
        var value = inputEvent;
        _input.Add(() => _bus.Emit(value));
    }

    public void Subscribe<T>(Action<T> handler) where T : struct => _bus.ConnectHost(handler);

    public void Dispose()
    {
        State = RuntimeState.Disposed;
    }

    private void Fail(Exception exception, string component)
    {
        // The reporter is called directly first, so the failure being recorded cannot depend on the bus that failed.
        State = RuntimeState.Faulted;
        _clock.DiscardPending();
        _reporter.Report("RUNTIME_FAULT", "Session stopped in '" + component + "' at tick " + _clock.Tick + ": " + exception.Message, exception);
        try
        {
            _bus.Emit(new SessionFaulted(_clock.Tick, component, exception.Message));
        }
        catch (InvalidOperationException emitFailure)
        {
            _reporter.Report("RUNTIME_FAULT_EVENT", "The fault event could not be raised: " + emitFailure.Message, emitFailure);
        }
    }

    private void RequireState(RuntimeState expected, string message)
    {
        if (State != expected) throw new InvalidOperationException(message + " (state " + State + ")");
    }
}

/// <summary>The declared standard events; each has exactly one owner and a schema version.</summary>
public static class RuntimeEventCatalog
{
    internal static readonly Action<RuntimeSession>[] Standard =
    {
        s => s.DeclareEvent<ObservationPosted>("runtime.observation-posted", 1),
        s => s.DeclareEvent<MissionCommandRequested>("runtime.mission-command-requested", 1),
        s => s.DeclareEvent<MissionEvent>("runtime.mission-event", 1),
        s => s.DeclareEvent<EffectRequested>("runtime.effect-requested", 1),
        s => s.DeclareEvent<CommandResolved>("runtime.command-resolved", 1),
        s => s.DeclareEvent<TickSettled>("runtime.tick-settled", 1),
        s => s.DeclareEvent<SessionFaulted>("runtime.session-faulted", 1),
        s => s.DeclareEvent<SessionEnded>("runtime.session-ended", 1),
        s => s.DeclareEvent<InputEdgeRaised>("runtime.input-edge-raised", 1),
    };
}
