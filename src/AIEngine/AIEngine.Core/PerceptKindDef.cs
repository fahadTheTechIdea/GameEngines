namespace AIEngine;

/// <summary>
/// A kind of thing an agent can know about. Each attribute has an <see cref="Unknowns"/> value that an absent percept reads as, so not knowing
/// is stated in the document and never guessed. A percept is forgotten <see cref="Memory"/> ticks after it was last perceived.
/// </summary>
public sealed class PerceptKindDef(int index, string id, IReadOnlyList<ValueDef> attrs, IReadOnlyList<Val> unknowns, int capacity, int ownerCapacity, int memory, bool owned)
{
    public int Index { get; } = index;
    public string Id { get; } = id;
    public IReadOnlyList<ValueDef> Attrs { get; } = attrs;
    public IReadOnlyList<Val> Unknowns { get; } = unknowns;
    public int Capacity { get; } = capacity;

    /// <summary>For an owned kind, the most things one owner can hold; 0 for a kind without owners. A query for what one owner holds is bounded by this, not by the capacity of the whole kind.</summary>
    public int OwnerCapacity { get; } = ownerCapacity;

    /// <summary>The most percepts a query of this kind can look at: those of one owner when it asks for one, otherwise all.</summary>
    public int ScanBound(bool byOwner) => byOwner ? OwnerCapacity : Capacity;
    public int Memory { get; } = memory;
    public bool Owned { get; } = owned;

    private readonly Dictionary<string, ValueDef> _byId = attrs.ToDictionary(a => a.Id, StringComparer.Ordinal);

    public bool TryAttr(string id, out ValueDef attr) => _byId.TryGetValue(id, out attr!);
}