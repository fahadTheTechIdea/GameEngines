namespace GameRuntime;

/// <summary>
/// The reference host's event delivery. Synchronous, single-threaded and ordered: events emitted during a component's
/// Update (or a handler) are queued and delivered in publication order, to handlers in connection order, once that
/// Update returns. Host subscribers receive the same events afterwards, outside any drain.
/// </summary>
internal sealed class RuntimeEventBus : IEventPort
{
    private readonly record struct Declaration(string Id, int Version);

    private readonly Dictionary<Type, Declaration> _declared = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Dictionary<Type, List<Delegate>> _hostHandlers = new();
    private readonly Queue<Action> _queue = new();
    private readonly List<Action> _outbound = new();
    private readonly int _budget;
    private bool _connectOpen = true;
    private bool _frozen;

    public RuntimeEventBus(int budgetPerDrain)
    {
        if (budgetPerDrain < 1) throw new ArgumentOutOfRangeException(nameof(budgetPerDrain));
        _budget = budgetPerDrain;
    }

    public void Declare<T>(string id, int version) where T : struct
    {
        if (_frozen) throw new InvalidOperationException("event declarations are frozen");
        if (string.IsNullOrWhiteSpace(id) || version < 1) throw new ArgumentException("an event needs an id and a positive schema version");
        foreach (var existing in _declared)
            if (existing.Value.Id == id) throw new InvalidOperationException("event id '" + id + "' is declared twice");
        if (!_declared.TryAdd(typeof(T), new Declaration(id, version)))
            throw new InvalidOperationException("event payload " + typeof(T).Name + " is declared twice");
    }

    public void Freeze() { _frozen = true; }
    public void CloseConnections() { _connectOpen = false; }

    public void Connect<T>(Action<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_connectOpen) throw new InvalidOperationException("components connect to events during Init only");
        RequireDeclared<T>();
        Add(_handlers, handler);
    }

    /// <summary>Host-side subscription; delivered after the tick's drain, never during it.</summary>
    public void ConnectHost<T>(Action<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(handler);
        RequireDeclared<T>();
        Add(_hostHandlers, handler);
    }

    public void Emit<T>(in T evt) where T : struct
    {
        if (!_frozen) throw new InvalidOperationException("events are emitted after composition is frozen");
        RequireDeclared<T>();
        var value = evt;
        _queue.Enqueue(() => Deliver(_handlers, value));
        _outbound.Add(() => Deliver(_hostHandlers, value));
    }

    /// <summary>Delivers queued events to components until the queue is empty. A budget overflow throws.</summary>
    public void Drain()
    {
        var delivered = 0;
        while (_queue.Count != 0)
        {
            if (++delivered > _budget)
            {
                _queue.Clear();
                throw new InvalidOperationException("event budget exceeded in one drain: " + _budget);
            }
            _queue.Dequeue()();
        }
    }

    /// <summary>Delivers everything emitted since the last call to host subscribers.</summary>
    public void DeliverOutbound()
    {
        var batch = _outbound.ToArray();
        _outbound.Clear();
        foreach (var delivery in batch) delivery();
    }

    private void RequireDeclared<T>() where T : struct
    {
        if (!_declared.ContainsKey(typeof(T))) throw new InvalidOperationException("event " + typeof(T).Name + " is not declared");
    }

    private static void Add<T>(Dictionary<Type, List<Delegate>> map, Action<T> handler)
    {
        if (!map.TryGetValue(typeof(T), out var list)) map[typeof(T)] = list = new List<Delegate>();
        list.Add(handler);
    }

    private static void Deliver<T>(Dictionary<Type, List<Delegate>> map, T value)
    {
        if (!map.TryGetValue(typeof(T), out var list)) return;
        foreach (var handler in list.ToArray()) ((Action<T>)handler)(value);
    }
}
