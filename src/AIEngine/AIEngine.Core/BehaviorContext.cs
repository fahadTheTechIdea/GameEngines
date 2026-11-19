namespace AIEngine;

/// <summary>What is counted while one behaviour is read: its node ids, its node count and the memory slots its nodes claim.</summary>
internal sealed class BehaviorContext(string behavior)
{
    public const int MaxNodes = 512;
    public const int MaxDepth = 12;

    public string Behavior { get; } = behavior;
    public HashSet<string> NodeIds { get; } = new(StringComparer.Ordinal);
    public List<bool> PersistentSlots { get; } = new();
    public List<long> SlotMaxima { get; } = new();
    public ReadScope Scope { get; } = new();

    public int NodeCount => NodeIds.Count;

    /// <summary>Claims a slot; a persistent slot keeps its value between thinks, the other is cleared when its node stops running. <paramref name="maximum"/> is the largest value the node ever stores, which a loaded save is checked against.</summary>
    public int Claim(bool persistent, long maximum)
    {
        PersistentSlots.Add(persistent);
        SlotMaxima.Add(maximum);
        return PersistentSlots.Count - 1;
    }
}