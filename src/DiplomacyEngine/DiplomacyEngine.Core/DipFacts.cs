using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>
/// What the engine says, as facts in the shared fact model. A condition in a document is about a <i>pair</i>: the first realm (the proposer, the attacker, the one that would hold a casus belli) and the second. It reads
/// <c>step</c>, <c>season</c>, <c>regard</c> (the first's regard of the second), <c>reverse</c>, <c>ratio</c> (the first's share of the two realms' might, per mille), <c>at_war</c>, <c>treaties</c> (active between them),
/// <c>coalition</c> (the first is in a coalition against the second), <c>intel</c> (the first's level of knowledge of the second, 0 to 4), <c>treaty.&lt;type&gt;</c>, <c>cb.&lt;id&gt;</c> (the first holds it against the second) and,
/// for each realm as <c>first.</c> and <c>second.</c>, <c>stability</c>, <c>legitimacy</c>, <c>trust</c>, <c>fear</c>, <c>honour</c>, <c>infamy</c>, <c>might</c>, <c>wars</c> and <c>treaties</c>. Any other id is the host's.
/// To the other layers the same numbers are offered with a prefix, <c>diplomacy.&lt;realm&gt;.stability</c> and <c>diplomacy.&lt;a&gt;.&lt;b&gt;.regard</c>.
/// </summary>
public static class DipFacts
{
    public const long Saturate = 1_000_000_000;

    public static readonly IReadOnlyList<string> PairNames = ["step", "season", "regard", "reverse", "ratio", "at_war", "treaties", "coalition", "intel"];
    public static readonly IReadOnlyList<string> RealmNames = ["stability", "legitimacy", "trust", "fear", "honour", "infamy", "might", "wars", "treaties"];
    private static readonly string[] Prefixes = ["first.", "second.", "treaty.", "cb."];

    public static bool IsReserved(string id) => PairNames.Contains(id) || Prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));

    private static FactDefinition RealmFact(string name) => name switch
    {
        "stability" or "legitimacy" => new FactDefinition(FactKind.Integer, 0, 1000),
        "trust" or "fear" or "honour" => new FactDefinition(FactKind.Integer, -1000, 1000),
        "infamy" => new FactDefinition(FactKind.Integer, 0, 1000),
        "might" => new FactDefinition(FactKind.Integer, 0, Saturate),
        "wars" => new FactDefinition(FactKind.Integer, 0, 64),
        "treaties" => new FactDefinition(FactKind.Integer, 0, Saturate),
        "band" => new FactDefinition(FactKind.String),
        _ => new FactDefinition(FactKind.Integer, 0, 0),
    };

    public static IReadOnlyDictionary<string, FactDefinition> Pair(IReadOnlyDictionary<string, FactDefinition> host, IEnumerable<string> types, IEnumerable<string> casusBelli)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal)
        {
            ["step"] = new(FactKind.Integer, 0, Saturate), ["season"] = new(FactKind.String), ["regard"] = new(FactKind.Integer, -1000, 1000), ["reverse"] = new(FactKind.Integer, -1000, 1000),
            ["ratio"] = new(FactKind.Integer, 0, 1000), ["at_war"] = new(FactKind.Boolean), ["treaties"] = new(FactKind.Integer, 0, Saturate), ["coalition"] = new(FactKind.Boolean), ["intel"] = new(FactKind.Integer, 0, 4),
        };
        foreach (var name in RealmNames)
        {
            facts["first." + name] = RealmFact(name);
            facts["second." + name] = RealmFact(name);
        }
        foreach (var type in types) facts["treaty." + type] = new FactDefinition(FactKind.Boolean);
        foreach (var cb in casusBelli) facts["cb." + cb] = new FactDefinition(FactKind.Boolean);
        foreach (var (id, definition) in host) facts[id] = definition;
        return facts;
    }

    /// <summary>The facts the other layers can read of a realm (<c>diplomacy.&lt;realm&gt;.stability</c>) and of each pair (<c>diplomacy.&lt;a&gt;.&lt;b&gt;.regard</c>), under <paramref name="prefix"/>.</summary>
    public static IReadOnlyDictionary<string, FactDefinition> Offered(DipSet set, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(set);
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var realm in set.Realms)
        {
            foreach (var name in RealmNames) facts[prefix + "diplomacy." + realm.Id + "." + name] = RealmFact(name);
            facts[prefix + "diplomacy." + realm.Id + ".band"] = RealmFact("band");
            foreach (var other in set.Realms)
            {
                if (other.Index == realm.Index) continue;
                var at = prefix + "diplomacy." + realm.Id + "." + other.Id + ".";
                facts[at + "regard"] = new FactDefinition(FactKind.Integer, -1000, 1000);
                facts[at + "reverse"] = new FactDefinition(FactKind.Integer, -1000, 1000);
                facts[at + "ratio"] = new FactDefinition(FactKind.Integer, 0, 1000);
                facts[at + "at_war"] = new FactDefinition(FactKind.Boolean);
                facts[at + "treaties"] = new FactDefinition(FactKind.Integer, 0, Saturate);
                facts[at + "coalition"] = new FactDefinition(FactKind.Boolean);
                facts[at + "intel"] = new FactDefinition(FactKind.Integer, 0, 4);
                foreach (var type in set.Types) facts[at + "treaty." + type.Id] = new FactDefinition(FactKind.Boolean);
            }
        }
        return facts;
    }
}
