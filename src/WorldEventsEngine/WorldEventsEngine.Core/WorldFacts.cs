using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

/// <summary>
/// What the engine says about the world, as facts in the shared fact model (boolean, integer, string). A condition in a document reads them by their short names: the world's (<c>season</c>, <c>regime</c>,
/// <c>step</c>, <c>stress</c>, <c>phase</c>, <c>storyteller</c>, <c>difficulty</c>, <c>hazards_active</c>) and, in a region's condition, the region's (<c>temp</c>, <c>precip</c>, <c>wetness</c>, <c>wind</c>, <c>weather</c>,
/// <c>soil</c>, <c>river</c>, <c>snow</c>, <c>dry_steps</c>, <c>population</c>, <c>hazard.&lt;id&gt;</c> (0 none, else the rung number), <c>disease.&lt;id&gt;.infected</c> / <c>.prevalence</c> and <c>mod.&lt;id&gt;</c>). Any other id is
/// the host's. To the other layers the same facts are offered with a prefix and the region's id in front of the region's (<c>world.season</c>, <c>world.vale.temp</c>), by <see cref="WorldRuntime"/>.
/// </summary>
public static class WorldFacts
{
    /// <summary>The largest value a count or an amount reaches; a bigger one saturates here (the shared fact model's limit).</summary>
    public const long Saturate = 1_000_000_000;

    public const int MaxTemperature = 10_000;

    public static readonly IReadOnlyList<string> GlobalNames = ["season", "regime", "step", "stress", "phase", "storyteller", "difficulty", "hazards_active"];
    public static readonly IReadOnlyList<string> RegionNames = ["temp", "precip", "wetness", "wind", "weather", "soil", "river", "snow", "dry_steps", "population"];
    private static readonly string[] Prefixes = ["hazard.", "disease.", "mod."];

    /// <summary>True when the id is one the engine gives (a host fact may not take it).</summary>
    public static bool IsReserved(string id) => GlobalNames.Contains(id) || RegionNames.Contains(id) || Prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));

    public static IReadOnlyDictionary<string, FactDefinition> Global(IReadOnlyDictionary<string, FactDefinition> host)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal)
        {
            ["season"] = new(FactKind.String), ["regime"] = new(FactKind.String), ["step"] = new(FactKind.Integer, 0, Saturate), ["stress"] = new(FactKind.Integer, 0, 1000), ["phase"] = new(FactKind.String),
            ["storyteller"] = new(FactKind.String), ["difficulty"] = new(FactKind.String), ["hazards_active"] = new(FactKind.Integer, 0, Saturate),
        };
        foreach (var (id, definition) in host) facts[id] = definition;
        return facts;
    }

    public static IReadOnlyDictionary<string, FactDefinition> Local(IReadOnlyList<(string Id, int Rungs)> hazards, IReadOnlyList<string> diseases, IReadOnlyList<ModifierDef> modifiers, IReadOnlyDictionary<string, FactDefinition> global)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal)
        {
            ["temp"] = new(FactKind.Integer, -MaxTemperature, MaxTemperature), ["precip"] = new(FactKind.Integer, 0, Saturate), ["wetness"] = new(FactKind.Integer, 0, Saturate), ["wind"] = new(FactKind.Integer, 0, Saturate),
            ["weather"] = new(FactKind.String), ["soil"] = new(FactKind.Integer, 0, 1000), ["river"] = new(FactKind.Integer, 0, Saturate), ["snow"] = new(FactKind.Integer, 0, Saturate),
            ["dry_steps"] = new(FactKind.Integer, 0, Saturate), ["population"] = new(FactKind.Integer, 0, Saturate),
        };
        foreach (var (id, definition) in global) facts[id] = definition;
        foreach (var hazard in hazards) facts["hazard." + hazard.Id] = new FactDefinition(FactKind.Integer, 0, hazard.Rungs);
        foreach (var disease in diseases)
        {
            facts["disease." + disease + ".infected"] = new FactDefinition(FactKind.Integer, 0, Saturate);
            facts["disease." + disease + ".prevalence"] = new FactDefinition(FactKind.Integer, 0, 1000);
        }
        foreach (var modifier in modifiers) facts["mod." + modifier.Id] = new FactDefinition(FactKind.Integer, modifier.Minimum, modifier.Maximum);
        return facts;
    }

    /// <summary>Every fact the other layers can read of a world, under <paramref name="prefix"/>: the world's, and each region's under its id. Dialogue documents declare the ones they read as external facts with these ids.</summary>
    public static IReadOnlyDictionary<string, FactDefinition> Offered(WorldSet set, string prefix = "")
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var name in GlobalNames) facts[prefix + "world." + name] = set.GlobalFacts[name];
        foreach (var region in set.Regions)
            foreach (var (name, definition) in set.LocalFacts)
                if (!set.GlobalFacts.ContainsKey(name)) facts[prefix + "world." + region.Id + "." + name] = definition;
        return facts;
    }
}
