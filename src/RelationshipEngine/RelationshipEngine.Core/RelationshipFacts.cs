using ScenarioEngine.Contracts;

namespace RelationshipEngine;

/// <summary>
/// What the relationship engine says about an actor, or about two actors, as facts in the shared fact model (boolean, integer, string). The same facts feed interaction preconditions and rules, dialogue
/// conditions (declare them as external facts) and AI sensors. The names are fixed: <c>actor.</c> and <c>target.</c> for the two actors (<c>mood</c>, <c>mood_state</c>, <c>activity</c>, <c>community</c>, <c>community_size</c>, <c>standing</c>, <c>need.&lt;id&gt;</c>,
/// <c>trait.&lt;id&gt;</c>), <c>edge.</c> for the actor's opinion of the target and <c>reverse.</c> for the target's opinion of the actor (<c>&lt;axis&gt;</c>, <c>state.&lt;ladder&gt;</c>, <c>bond.&lt;id&gt;</c>,
/// <c>memory.&lt;id&gt;</c>, <c>known</c>, <c>same_community</c>). Any other id is the host's.
/// </summary>
public static class RelationshipFacts
{
    public const string Actor = "actor.";
    public const string Target = "target.";
    public const string Edge = "edge.";
    public const string Reverse = "reverse.";

    /// <summary>True when the id begins with one of the four prefixes the engine owns.</summary>
    public static bool IsReserved(string id) =>
        id.StartsWith(Actor, StringComparison.Ordinal) || id.StartsWith(Target, StringComparison.Ordinal) || id.StartsWith(Edge, StringComparison.Ordinal) || id.StartsWith(Reverse, StringComparison.Ordinal);

    /// <summary>The names an axis id may not take, because an edge fact with that name already means something else.</summary>
    public static bool AxisNameClashes(string axisId) =>
        axisId == "known" || axisId == "same_community" || axisId.StartsWith("state.", StringComparison.Ordinal) || axisId.StartsWith("bond.", StringComparison.Ordinal) || axisId.StartsWith("memory.", StringComparison.Ordinal);

    /// <summary>
    /// Every fact the engine itself offers about an actor, and, when a target is given, about the pair, each id prefixed by <paramref name="prefix"/> (a dialogue document declares them as external facts under
    /// <c>ext.</c>). The host's own facts are not included.
    /// </summary>
    public static Dictionary<string, FactValue> Read(RelationshipRuntime runtime, string actorId, string? targetId = null, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var actor = runtime.Actor(actorId);
        var target = targetId is null ? null : runtime.Actor(targetId);
        var none = new Dictionary<string, FactValue>();
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var id in runtime.Set.Facts.Keys)
        {
            if (!IsReserved(id)) continue;
            if (target is null && !id.StartsWith(Actor, StringComparison.Ordinal)) continue;
            facts[prefix + id] = runtime.Resolve(id, actor, target ?? actor, none);
        }
        return facts;
    }

    /// <summary>Every fact a document with these declarations offers, each with its type and range, plus the host's own.</summary>
    public static IReadOnlyDictionary<string, FactDefinition> Definitions(IReadOnlyList<AxisDef> axes, IReadOnlyList<NeedDef> needs, IReadOnlyList<TraitDef> traits, IReadOnlyList<LadderDef> ladders,
        IReadOnlyList<BondDef> bonds, IReadOnlyList<MemoryDef> memories, MoodDef mood, LimitsDef limits, IReadOnlyDictionary<string, FactDefinition> hostFacts)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var prefix in new[] { Actor, Target })
        {
            facts[prefix + "mood"] = new FactDefinition(FactKind.Integer, mood.Min, mood.Max);
            facts[prefix + "mood_state"] = new FactDefinition(FactKind.String);
            facts[prefix + "activity"] = new FactDefinition(FactKind.String);
            facts[prefix + "community"] = new FactDefinition(FactKind.String);
            facts[prefix + "community_size"] = new FactDefinition(FactKind.Integer, 1, limits.MaxActors);
            facts[prefix + "standing"] = new FactDefinition(FactKind.Integer, 0, RelationshipRuntime.StandingMax);
            foreach (var need in needs) facts[prefix + "need." + need.Id] = new FactDefinition(FactKind.Integer, 0, need.Max);
            foreach (var trait in traits) facts[prefix + "trait." + trait.Id] = new FactDefinition(FactKind.Boolean);
        }
        foreach (var prefix in new[] { Edge, Reverse })
        {
            facts[prefix + "known"] = new FactDefinition(FactKind.Boolean);
            facts[prefix + "same_community"] = new FactDefinition(FactKind.Boolean);
            foreach (var axis in axes) facts[prefix + axis.Id] = new FactDefinition(FactKind.Integer, axis.Min, axis.Max);
            foreach (var ladder in ladders) facts[prefix + "state." + ladder.Id] = new FactDefinition(FactKind.String);
            foreach (var bond in bonds) facts[prefix + "bond." + bond.Id] = new FactDefinition(FactKind.Boolean);
            foreach (var memory in memories) facts[prefix + "memory." + memory.Id] = new FactDefinition(FactKind.Integer, 0, limits.MaxMemoriesPerEdge);
        }
        foreach (var (id, definition) in hostFacts) facts[id] = definition;
        return facts;
    }
}
