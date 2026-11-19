using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>One item of the percept loop a node is inside: the percept and its place in the loop.</summary>
internal readonly record struct ScopeFrame(Percept Item, int Index);

/// <summary>
/// One think of one agent. It is reused from think to think (nothing in it survives one): the tick, the agent, the work spent so far, the
/// intents raised, the percept loops being walked and the scratch the evaluations use.
/// </summary>
internal sealed class ThinkContext
{
    private readonly Stack<List<Percept>> _lists = new();

    public AgentState Agent { get; set; } = null!;
    public long Tick { get; set; }
    public ulong Seed { get; set; }
    public long Spent { get; private set; }

    /// <summary>Set while the think is being explained; null otherwise, so an ordinary think pays for a null check at each node and nothing more.</summary>
    public TraceRecorder? Trace { get; set; }
    public List<Intent> Intents { get; set; } = null!;
    public List<ScopeFrame> Scope { get; } = new();
    public Dictionary<string, FactValue> Scratch { get; } = new(StringComparer.Ordinal);

    /// <summary>Set by a node that reports Running, per slot; slots whose node did not run are cleared when the think ends.</summary>
    public bool[] Alive { get; set; } = Array.Empty<bool>();

    public void Begin(AgentState agent, long tick, ulong seed, List<Intent> intents)
    {
        Agent = agent;
        Tick = tick;
        Seed = seed;
        Intents = intents;
        Spent = 0;
        Scope.Clear();
        if (Alive.Length < agent.Behavior.SlotCount) Alive = new bool[Math.Max(agent.Behavior.SlotCount, Alive.Length * 2)];
        System.Array.Clear(Alive, 0, agent.Behavior.SlotCount);
    }

    public void Charge(long units) => Spent += units;

    public List<Percept> Rent() => _lists.Count > 0 ? _lists.Pop() : new List<Percept>();

    public void Return(List<Percept> list)
    {
        list.Clear();
        _lists.Push(list);
    }

    public FactValue Fact(FactRef reference)
    {
        switch (reference.Source)
        {
            case FactSource.Key: return Agent.Keys[reference.Index].ToFact(reference.Kind);
            case FactSource.ItemAttr: return Scope[^1].Item.Values[reference.Index].ToFact(reference.Kind);
            case FactSource.ItemId: return FactValue.From(Scope[^1].Item.Id);
            case FactSource.ItemOwner: return FactValue.From(Scope[^1].Item.Owner);
            case FactSource.ItemIndex: return FactValue.From((long)Scope[^1].Index);
            case FactSource.Tick: break;
        }
        return FactValue.From(Tick);
    }
}