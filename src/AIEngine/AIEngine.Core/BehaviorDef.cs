namespace AIEngine;

/// <summary>
/// A tree an agent runs: how often it thinks, its root and the memory slots its nodes claim. <see cref="MaxCost"/> is the most work units one think can
/// cost, worked out from the document alone; a think never spends more.
/// </summary>
public sealed class BehaviorDef(string id, int every, Node root, IReadOnlyList<bool> persistentSlots, IReadOnlyList<long> slotMaxima, long maxCost, int nodeCount)
{
    public string Id { get; } = id;
    public int Every { get; } = every;
    public Node Root { get; } = root;

    /// <summary>One entry per memory slot: true when the slot keeps its value between thinks (a utility choice, a cooldown), false when it is cleared the moment its node stops running.</summary>
    public IReadOnlyList<bool> PersistentSlots { get; } = persistentSlots;

    /// <summary>The largest value each slot ever holds (all slots start at 0).</summary>
    public IReadOnlyList<long> SlotMaxima { get; } = slotMaxima;

    public int SlotCount => PersistentSlots.Count;
    public long MaxCost { get; } = maxCost;
    public int NodeCount { get; } = nodeCount;
}