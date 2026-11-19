namespace GameRuntime;

/// <summary>
/// The input the outside world holds for the game, like XNA's Keyboard.GetState or Godot's Input singleton: a table of
/// which actions are held plus the presses and releases since the last poll. The host, a script or a bot writes it between
/// Updates; it never touches game state. A component reads it inside its own Update (<see cref="Poll"/>, <see cref="IsHeld"/>),
/// so nothing a writer does has any effect until the next Update.
/// </summary>
public sealed class InputState
{
    private readonly Func<long> _nextPollTick;
    private readonly Action<long, InputEdgeRaised>? _onWrite;
    private readonly List<InputEdgeRaised> _edges = new();
    private readonly Dictionary<string, string> _held = new(StringComparer.Ordinal);

    /// <param name="nextPollTick">The tick whose Update will first see a write made now (the clock's tick + 1).</param>
    /// <param name="onWrite">Called for every write with that tick, so a journal can record the input and its delivery tick.</param>
    public InputState(Func<long> nextPollTick, Action<long, InputEdgeRaised>? onWrite = null)
    {
        _nextPollTick = nextPollTick ?? throw new ArgumentNullException(nameof(nextPollTick));
        _onWrite = onWrite;
    }

    /// <summary>True when nothing is waiting to be polled and no action is held; a checkpoint is only consistent then.</summary>
    public bool IsQuiet => _edges.Count == 0 && _held.Count == 0;

    public int PendingEdges => _edges.Count;

    /// <summary>The action goes down: one just-pressed edge, and it is held until released.</summary>
    public void Press(string action, string argument = "") => Write(new InputEdgeRaised(RequireAction(action), argument ?? "", true));

    public void Release(string action) => Write(new InputEdgeRaised(RequireAction(action), "", false));

    /// <summary>Whether the action is held right now, and the argument it was pressed with.</summary>
    public bool IsHeld(string action, out string argument) => _held.TryGetValue(action, out argument!);

    /// <summary>Takes the edges written since the last poll, oldest first. Each edge is returned once.</summary>
    public IReadOnlyList<InputEdgeRaised> Poll()
    {
        if (_edges.Count == 0) return Array.Empty<InputEdgeRaised>();
        var taken = _edges.ToArray();
        _edges.Clear();
        return taken;
    }

    /// <summary>Applies an edge that arrived as an event (a journal replay delivers inputs this way). It is not recorded again.</summary>
    internal void Apply(in InputEdgeRaised edge) => Record(edge);

    private void Write(InputEdgeRaised edge)
    {
        _onWrite?.Invoke(_nextPollTick(), edge);
        Record(edge);
    }

    private void Record(in InputEdgeRaised edge)
    {
        _edges.Add(edge);
        if (edge.Pressed) _held[edge.Action] = edge.Argument;
        else _held.Remove(edge.Action);
    }

    private static string RequireAction(string action) =>
        string.IsNullOrWhiteSpace(action) ? throw new ArgumentException("an input action needs a name", nameof(action)) : action;
}

/// <summary>
/// Puts input edges that were delivered as events (by <c>IGameRuntime.Submit</c>, for example from a journal) into the
/// input state at the start of their tick, so a component polling it in that tick sees them.
/// </summary>
public sealed class InputStateBridge(InputState state, string id = "input") : IGameComponent
{
    public string Id { get; } = id;
    public void Init(in ComponentContext context) => context.Events.Connect<InputEdgeRaised>(edge => state.Apply(edge));
    public void Update(in UpdateContext context) { }
}
