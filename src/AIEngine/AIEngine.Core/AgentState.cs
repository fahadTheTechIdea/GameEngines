namespace AIEngine;

/// <summary>Everything one agent remembers: its blackboard, the memory of its running nodes, what it knows and when it thinks next.</summary>
internal sealed class AgentState(string id, BehaviorDef behavior, AiSet set)
{
    public string Id { get; } = id;
    public BehaviorDef Behavior { get; } = behavior;
    public long Due { get; set; }
    public long RollCounter { get; set; }

    /// <summary>True when the agent is being explained: its next think is recorded. Diagnostic only; never saved and never part of a digest.</summary>
    public bool Traced { get; set; }

    public ThinkTrace? LastTrace { get; set; }
    public Val[] Keys { get; } = set.Keys.Select(k => k.Initial).ToArray();
    public long[] Slots { get; } = new long[behavior.SlotCount];
    public PerceptStore[] Knowledge { get; } = set.Percepts.Select(_ => new PerceptStore()).ToArray();
}