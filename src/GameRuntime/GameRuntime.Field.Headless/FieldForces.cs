using CombatEngine;
using GameContent.Catalog;

namespace GameRuntime.Field.Headless;

/// <summary>
/// The two armies of a run: the combat rules, one combat group per squad, the character definitions generated from the same catalog
/// formations, and the group ids of each side. Headcount, role, training and equipment come from the catalog formation once and reach
/// both the combat group and the squad from there.
/// </summary>
public sealed record FieldForces(CombatRules Rules, IReadOnlyList<Group> Groups, IReadOnlyList<string> SideA, IReadOnlyList<string> SideB,
    string FormationA, string FormationB, int SquadsA, int SquadsB, string CharactersDocument, string Source);

/// <summary>The character vocabulary this game gives its people: which entities they use and how stats and skills grow with training.</summary>
public static class FieldTemplate
{
    public static CharacterTemplate For() => new(
        IndividualEntity: "field.person", SquadEntity: "field.squad", GivenNames: "field.names.given", FamilyNames: "field.names.family",
        Stats:
        [
            new StatPlan("field.stat.strength", new TrainingRanges(new LevelRange(5, 10), new LevelRange(8, 14), new LevelRange(10, 16), new LevelRange(12, 18))),
            new StatPlan("field.stat.courage", new TrainingRanges(new LevelRange(10, 50), new LevelRange(30, 80), new LevelRange(50, 90), new LevelRange(70, 100))),
        ],
        Skills: [new StatPlan("field.skill.shooting", new TrainingRanges(new LevelRange(0, 2), new LevelRange(1, 4), new LevelRange(2, 5), new LevelRange(3, 5)))],
        Traits: [new TraitWeight("field.trait.brave", 3), new TraitWeight("field.trait.careful", 2), new TraitWeight("field.trait.steady", 2)],
        TraitMin: 0, TraitMax: 2, MaxSpawn: 20_000, MaxSquads: 1000);
}

/// <summary>The one place the field game chooses its forces, read from the catalog packs and the rules file at run time.</summary>
public static class FieldForceSource
{
    public static string DefaultRulesPath => Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json");
    public static string DefaultPacksPath => Path.Combine(AppContext.BaseDirectory, "Packs");

    public static FieldForces Load(FieldOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rules = CombatRulesLoader.Load(File.ReadAllText(options.RulesPath ?? DefaultRulesPath));
        rules.TerrainType(options.Terrain);
        var source = new DirectoryCatalogSource(options.PacksPath ?? DefaultPacksPath);
        var setA = CatalogLoader.Load(source, options.PackA!).Required();
        var setB = CatalogLoader.Load(source, options.PackB!).Required();
        Check(setA, options.PackA!, options.FormationA!);
        Check(setB, options.PackB!, options.FormationB!);

        var groups = new List<Group>();
        var sideA = new List<string>();
        var sideB = new List<string>();
        for (var i = 1; i <= options.SquadsA; i++) { sideA.Add("a" + i); groups.Add(CatalogCombat.Group(setA, rules, options.FormationA!, "a" + i, 1)); }
        for (var i = 1; i <= options.SquadsB; i++) { sideB.Add("b" + i); groups.Add(CatalogCombat.Group(setB, rules, options.FormationB!, "b" + i, 2)); }

        // One document for both formations, whichever pack each comes from: generated per pack, then joined.
        var documentA = System.Text.Json.Nodes.JsonNode.Parse(CatalogCharacters.Document(setA, [options.FormationA!], FieldTemplate.For()))!.AsObject();
        var documentB = System.Text.Json.Nodes.JsonNode.Parse(CatalogCharacters.Document(setB, [options.FormationB!], FieldTemplate.For()))!.AsObject();
        var merged = new System.Text.Json.Nodes.JsonObject
        {
            ["character_archetypes"] = Join(documentA, documentB, "character_archetypes"),
            ["character_formations"] = Join(documentA, documentB, "character_formations"),
        };
        return new FieldForces(rules, groups, sideA, sideB, options.FormationA!, options.FormationB!, options.SquadsA!.Value, options.SquadsB!.Value,
            merged.ToJsonString(), $"a={options.SquadsA}x{options.FormationA}@{options.PackA} b={options.SquadsB}x{options.FormationB}@{options.PackB}");
    }

    private static System.Text.Json.Nodes.JsonArray Join(System.Text.Json.Nodes.JsonObject a, System.Text.Json.Nodes.JsonObject b, string section)
    {
        var joined = new System.Text.Json.Nodes.JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in a[section]!.AsArray().Concat(b[section]!.AsArray()))
            if (seen.Add(node!["id"]!.GetValue<string>())) joined.Add(node.DeepClone());
        return joined;
    }

    private static void Check(CatalogSet set, string pack, string formation)
    {
        if (!set.TryUnit(formation, out _))
            throw new FieldUsageException($"'{formation}' is not a formation of pack {pack}; it has: " + string.Join(", ", set.Root.Units.Select(u => u.Resource).Order(StringComparer.Ordinal)));
    }
}
