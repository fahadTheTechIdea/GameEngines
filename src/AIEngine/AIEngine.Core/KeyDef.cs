namespace AIEngine;

/// <summary>A blackboard key: a typed value every agent has. <see cref="HostWrites"/> says who may change it: the host (what the agent is told about itself) or the agent (its own memory).</summary>
public sealed record KeyDef(ValueDef Value, Val Initial, bool HostWrites)
{
    public int Index => Value.Index;
    public string Id => Value.Id;
}