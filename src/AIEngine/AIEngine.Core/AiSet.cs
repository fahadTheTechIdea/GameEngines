namespace AIEngine;

/// <summary>A loaded AI document: the declarations and the behaviours, plus the digest of the exact text they were read from.</summary>
public sealed class AiSet(string id, string digest, IReadOnlyList<KeyDef> keys, IReadOnlyList<PerceptKindDef> percepts, IReadOnlyDictionary<string, IntentDef> intents,
    IReadOnlyDictionary<string, BehaviorDef> behaviors)
{
    public string Id { get; } = id;
    public string Digest { get; } = digest;

    /// <summary>Keys in id order; a key's index is its place here.</summary>
    public IReadOnlyList<KeyDef> Keys { get; } = keys;

    /// <summary>Percept kinds in id order; a kind's index is its place here.</summary>
    public IReadOnlyList<PerceptKindDef> Percepts { get; } = percepts;

    public IReadOnlyDictionary<string, IntentDef> Intents { get; } = intents;
    public IReadOnlyDictionary<string, BehaviorDef> Behaviors { get; } = behaviors;

    /// <summary>The largest work-unit bound of any behaviour: a step's budget must cover it, so any agent can always be thought.</summary>
    public long MaxCost { get; } = behaviors.Values.Max(b => b.MaxCost);

    private readonly Dictionary<string, KeyDef> _keys = keys.ToDictionary(k => k.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, PerceptKindDef> _percepts = percepts.ToDictionary(p => p.Id, StringComparer.Ordinal);

    public bool TryKey(string id, out KeyDef key) => _keys.TryGetValue(id, out key!);
    public bool TryPercept(string id, out PerceptKindDef kind) => _percepts.TryGetValue(id, out kind!);
}