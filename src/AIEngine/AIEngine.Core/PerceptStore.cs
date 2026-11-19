namespace AIEngine;

/// <summary>
/// What one agent knows of one kind, in id order, with a second index by owner so a query for the things one owner holds looks only at those and not at everything of the
/// kind. Both indexes always agree: a percept is in both or in neither.
/// </summary>
internal sealed class PerceptStore
{
    private readonly SortedDictionary<string, Percept> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedDictionary<string, Percept>> _byOwner = new(StringComparer.Ordinal);

    public int Count => _byId.Count;

    /// <summary>Everything, in id order.</summary>
    public SortedDictionary<string, Percept>.ValueCollection Values => _byId.Values;

    public bool TryGetValue(string id, out Percept percept) => _byId.TryGetValue(id, out percept!);

    public void Add(Percept percept)
    {
        _byId.Add(percept.Id, percept);
        if (percept.Owner.Length == 0) return;
        if (!_byOwner.TryGetValue(percept.Owner, out var owned)) _byOwner[percept.Owner] = owned = new SortedDictionary<string, Percept>(StringComparer.Ordinal);
        owned.Add(percept.Id, percept);
    }

    public bool Remove(string id)
    {
        if (!_byId.Remove(id, out var percept)) return false;
        if (percept.Owner.Length > 0 && _byOwner.TryGetValue(percept.Owner, out var owned) && owned.Remove(id) && owned.Count == 0) _byOwner.Remove(percept.Owner);
        return true;
    }

    public int CountOwnedBy(string owner) => _byOwner.TryGetValue(owner, out var owned) ? owned.Count : 0;

    /// <summary>What one owner holds, in id order, or null when it holds nothing.</summary>
    public SortedDictionary<string, Percept>.ValueCollection? OwnedBy(string owner) => _byOwner.TryGetValue(owner, out var owned) ? owned.Values : null;
}