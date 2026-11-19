namespace AIEngine;

/// <summary>One thing an agent knows: what the host last told it, and when. The values are aligned with the attributes of its kind.</summary>
internal sealed class Percept(PerceptKindDef kind, string id, string owner, long seen, Val[] values)
{
    public PerceptKindDef Kind { get; } = kind;
    public string Id { get; } = id;
    public string Owner { get; } = owner;
    public long Seen { get; set; } = seen;
    public Val[] Values { get; } = values;
}