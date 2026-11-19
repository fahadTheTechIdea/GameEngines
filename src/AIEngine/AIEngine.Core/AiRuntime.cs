using EngineCommon;

namespace AIEngine;

/// <summary>
/// The agents of one AI document. The host tells them what it wants them to know (<see cref="Perceive"/>, <see cref="SetKey"/>), then calls <see cref="Step"/>
/// with the tick and a budget of work units; due agents think, oldest due first, until the next one would not fit, and what they decided leaves as intents.
/// Nothing here reads a clock or the world: a step is a function of the state, the tick and the budget.
/// </summary>
public sealed partial class AiRuntime
{
    public const long TickLimit = 1_000_000_000_000;

    private readonly SortedDictionary<string, AgentState> _agents = new(StringComparer.Ordinal);
    private readonly SortedSet<(long Due, string Id)> _queue = new(DueOrder.Instance);
    private readonly ThinkContext _context = new();
    private readonly TraceRecorder _recorder = new();
    private long _lastTick;

    public AiRuntime(AiSet set, ulong seed)
    {
        Set = set ?? throw new ArgumentNullException(nameof(set));
        Seed = seed;
    }

    public AiSet Set { get; }
    public ulong Seed { get; }

    /// <summary>A runtime continuing from a verified image of a state. The caller has already checked the image against the set.</summary>
    internal static AiRuntime Restore(AiSet set, ulong seed, AiImage image)
    {
        var runtime = new AiRuntime(set, seed) { _lastTick = image.LastTick };
        foreach (var row in image.Agents)
        {
            var agent = new AgentState(row.Id, set.Behaviors[row.Behavior], set) { Due = row.Due, RollCounter = row.Roll };
            for (var i = 0; i < set.Keys.Count; i++) agent.Keys[i] = Unpack(row.Keys[i].Value);
            for (var i = 0; i < row.Slots.Count; i++) agent.Slots[i] = row.Slots[i];
            foreach (var know in row.Knowledge)
            {
                set.TryPercept(know.Kind, out var kind);
                agent.Knowledge[kind.Index].Add(new Percept(kind, know.Id, know.Owner, know.Seen, know.Values.Select(Unpack).ToArray()));
            }
            runtime._agents[row.Id] = agent;
            runtime._queue.Add((agent.Due, agent.Id));
        }
        return runtime;
    }

    private static Val Unpack(ImageValue value) => value.Text is null ? Val.Of(value.Number) : Val.Of(value.Text);

    /// <summary>The tick of the latest step; calls with an earlier tick are refused.</summary>
    public long LastTick => _lastTick;

    public IReadOnlyCollection<string> Agents => _agents.Keys;

    internal IReadOnlyDictionary<string, AgentState> States => _agents;

    private AgentState Agent(string id) =>
        _agents.TryGetValue(id, out var agent) ? agent : throw new EngineRefusedException(AiCodes.UnknownAgent, "there is no agent '" + id + "'");

    /// <summary>The tick the agent thinks next.</summary>
    public long Due(string agent) => Agent(agent).Due;

    public string BehaviorOf(string agent) => Agent(agent).Behavior.Id;

    /// <summary>Adds an agent running a behaviour. Its first think is a seeded number of ticks after <paramref name="tick"/>, fewer than its think interval, so agents added together do not all think on one tick.</summary>
    public void Add(string id, string behavior, long tick)
    {
        CheckTick(tick);
        if (id.Length == 0 || id.Length > 128 || id.Any(ch => !(ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch is '-' or '.' or '_')))
            throw new EngineRefusedException(AiCodes.Format, "agent id '" + id + "' may only use a-z, 0-9, hyphen, dot and underscore, 1 to 128 characters");
        if (_agents.ContainsKey(id)) throw new EngineRefusedException(AiCodes.DuplicateAgent, "agent '" + id + "' exists");
        if (!Set.Behaviors.TryGetValue(behavior, out var def)) throw new EngineRefusedException(AiCodes.UnknownBehavior, "there is no behaviour '" + behavior + "'");
        var agent = new AgentState(id, def, Set) { Due = tick + DeterministicRoll.Below(Seed, "ai-offset:" + id, 0, def.Every) };
        _agents[id] = agent;
        _queue.Add((agent.Due, id));
    }

    public void Remove(string id)
    {
        var agent = Agent(id);
        _queue.Remove((agent.Due, id));
        _agents.Remove(id);
    }

    private void CheckTick(long tick)
    {
        if (tick < _lastTick) throw new EngineRefusedException(AiCodes.TickOrder, "tick " + tick + " is before the latest step " + _lastTick);
        if (tick > TickLimit) throw new EngineRefusedException(AiCodes.TickOrder, "tick " + tick + " is beyond " + TickLimit);
    }

    /// <summary>
    /// Lets the agents that are due think. <paramref name="budget"/> counts work units (one for each node run and each percept a query looks at) and must cover
    /// the largest behaviour; a think is only started when the remaining budget covers that agent behaviour bound, so a step never spends more than its budget.
    /// An agent that was due but did not fit stays due and goes first next step.
    /// </summary>
    public StepReport Step(long tick, long budget)
    {
        CheckTick(tick);
        if (budget < Set.MaxCost) throw new EngineRefusedException(AiCodes.Budget, "a budget of " + budget + " is below the largest behaviour bound " + Set.MaxCost);
        _lastTick = tick;
        var intents = new List<Intent>();
        var remaining = budget;
        var thought = 0;
        long longest = 0;
        while (_queue.Count > 0)
        {
            var first = _queue.Min;
            if (first.Due > tick) break;
            var agent = _agents[first.Id];
            if (agent.Behavior.MaxCost > remaining) break;
            _queue.Remove(first);
            remaining -= Think(agent, tick, intents);
            longest = Math.Max(longest, tick - first.Due);
            agent.Due = tick + agent.Behavior.Every;
            _queue.Add((agent.Due, agent.Id));
            thought++;
        }
        // Counting what was left waiting walks the queue, so it is only done when something was: a step that served everyone allocates nothing for it.
        var deferred = 0;
        if (_queue.Count > 0 && _queue.Min.Due <= tick)
            foreach (var waiting in _queue)
            {
                if (waiting.Due > tick) break;
                deferred++;
            }
        return new StepReport(tick, intents, thought, budget - remaining, deferred, longest);
    }

    /// <summary>
    /// Starts or stops explaining an agent: while on, each think of it is recorded and <see cref="Explain"/> returns the last one. Recording does not change a decision, a digest or a save.
    /// </summary>
    public void Trace(string agent, bool on)
    {
        var state = Agent(agent);
        state.Traced = on;
        if (!on) state.LastTrace = null;
    }

    /// <summary>Why the agent did what it did on its last think, or null when it is not being explained or has not thought since it began to be.</summary>
    public ThinkTrace? Explain(string agent) => Agent(agent).LastTrace;

    private long Think(AgentState agent, long tick, List<Intent> intents)
    {
        Forget(agent, tick);
        _context.Begin(agent, tick, Seed, intents);
        if (agent.Traced)
        {
            _recorder.Start();
            _context.Trace = _recorder;
        }
        agent.Behavior.Root.Run(_context);
        if (agent.Traced)
        {
            agent.LastTrace = _recorder.Finish(agent.Id, agent.Behavior.Id, tick, _context.Spent);
            _context.Trace = null;
        }
        // A node that is not running forgets its place: the slots that keep a place only while running are cleared, the ones that keep a choice are left.
        for (var slot = 0; slot < agent.Slots.Length; slot++)
            if (!agent.Behavior.PersistentSlots[slot] && !_context.Alive[slot]) agent.Slots[slot] = 0;
        return _context.Spent;
    }

    /// <summary>Drops what the agent has not been told about for longer than its kind memory.</summary>
    private static void Forget(AgentState agent, long tick)
    {
        foreach (var known in agent.Knowledge)
        {
            if (known.Count == 0) continue;
            List<string>? stale = null;
            foreach (var percept in known.Values)
                if (tick - percept.Seen > percept.Kind.Memory) (stale ??= new List<string>()).Add(percept.Id);
            if (stale is null) continue;
            foreach (var id in stale) known.Remove(id);
        }
    }
}