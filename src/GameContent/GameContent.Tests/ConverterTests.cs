using System.Text.Json.Nodes;
using AlchemyEngine;
using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>The capability matrix as tests: each feature is converted, reduced with a note, or refused with a code, never dropped.</summary>
public sealed class ConverterTests
{
    private static CatalogSet Set(string resources, string recipes = "", string stations = "", string skills = "") =>
        Load(Single(resources, recipes, stations, skills)).Required();

    private static readonly string Bench = """{ "id": "t.bench", "name": "Bench", "levels": 3 }""";
    private static readonly string Craft = """{ "id": "t.craft", "name": "Craft", "max": 9 }""";

    private static IEnumerable<string> Codes(ConversionReport report, ConversionLevel level) => report.Notes.Where(n => n.Level == level).Select(n => n.Code);

    // ---- Alchemy ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AlchemyTurnsInputsToolsAndStationsIntoACombination()
    {
        var resources = Res("t.a") + "," + Res("t.pick", cls: "tool", unit: "{each}", durability: """{ "uses": 50 }""") + "," + Res("t.b", origin: "crafted") + "," + Res("t.slag", origin: "crafted");
        var recipe = Rec("t.smelt", kind: "smelt", inputs: Line("t.a", "3"), tools: """{ "resource": "t.pick", "uses": 50 }""", outputs: Line("t.b", "1"), byproducts: Line("t.slag", "2"),
            station: """{ "id": "t.bench", "level": 2 }""", skill: """{ "id": "t.craft", "level": 4 }""", environment: "\"heat\"");
        var conversion = CatalogToAlchemy.Convert(Set(resources, recipe, Bench, Craft));
        var alchemy = conversion.Content.Recipes.Single();
        Assert.Equal(["t.a"], alchemy.Inputs);
        Assert.Equal(["t.pick"], alchemy.Catalysts);
        Assert.Equal(["t.b", "t.slag"], alchemy.Outputs);
        Assert.Equal(["at-bench-l2", "heat", "skill-craft-4"], alchemy.Environment);
        var reduced = Codes(conversion.Report, ConversionLevel.Reduced).ToHashSet();
        Assert.Contains(AlchemyConversionCodes.Amounts, reduced);
        Assert.Contains(AlchemyConversionCodes.Duration, reduced);
        Assert.Contains(AlchemyConversionCodes.ToolsAsCatalysts, reduced);
        Assert.Contains(AlchemyConversionCodes.ByproductsAsOutputs, reduced);
        Assert.Contains(AlchemyConversionCodes.StationAsEnvironment, reduced);
        Assert.Contains(AlchemyConversionCodes.SkillAsEnvironment, reduced);
        // The rules accept the combination only when the game offers the environment it needs.
        var rules = new AlchemyRules(conversion.Content);
        Assert.Equal(CombineOutcome.NoReaction, rules.WhatDoes(["t.a", "t.pick"], []).Outcome);
        Assert.Equal(CombineOutcome.Reacted, rules.WhatDoes(["t.a", "t.pick"], ["at-bench-l2", "heat", "skill-craft-4"]).Outcome);
    }

    [Fact]
    public void AlchemyRepresentsGatheringConsumptionAndLootInsteadOfRefusingThem()
    {
        var resources = Res("t.a") + "," + Res("t.pick", cls: "tool", unit: "{each}", durability: """{ "uses": 50 }""") + "," + Res("t.ore", origin: "crafted") + "," + Res("t.berry", origin: "crafted") + "," + Res("t.gem", origin: "crafted") + "," + Res("t.b", origin: "crafted");
        var recipes = string.Join(",",
            Rec("t.mine", kind: "extract", tools: """{ "resource": "t.pick", "uses": 50 }""", outputs: Line("t.ore", "1"), station: """{ "id": "t.bench", "level": 1 }"""),
            Rec("t.forage", kind: "harvest", outputs: Line("t.berry", "1")),
            Rec("t.eat", kind: "consume", inputs: Line("t.berry", "1")),
            Rec("t.find-gem", kind: "loot", outputs: "", drops: """{ "resource": "t.gem", "amount": 1, "chance": "1/4" }"""),
            Rec("t.good", kind: "craft", inputs: Line("t.a", "2"), outputs: Line("t.b", "1")));
        var conversion = CatalogToAlchemy.Convert(Set(resources + "," + Res("t.pick2", cls: "tool", unit: "{each}", origin: "crafted", durability: """{ "uses": 5 }""").Replace("t.pick2", "t.pick-spare"), recipes + "," + Rec("t.make-pick-spare", inputs: Line("t.a", "1"), outputs: Line("t.pick-spare", "1")), Bench));
        var content = conversion.Content;
        var codes = conversion.Report.Notes.ToLookup(n => n.Subject, n => n.Code);
        Assert.Contains(AlchemyConversionCodes.GatheringAsToolUse, codes["t.mine"]);
        Assert.Contains(AlchemyConversionCodes.HandCatalyst, codes["t.forage"]);
        Assert.Contains(AlchemyConversionCodes.HandCatalyst, codes["t.find-gem"]);
        Assert.Contains(AlchemyConversionCodes.ConsumptionOnly, codes["t.eat"]);
        Assert.Contains(AlchemyConversionCodes.DropAsRecipeChance, codes["t.find-gem"]);
        Assert.Empty(conversion.Report.Refusals);
        var rules = new AlchemyRules(content);
        var mined = rules.WhatDoes(["t.pick"], ["at-bench"]);
        Assert.Equal(CombineOutcome.Reacted, mined.Outcome);
        Assert.Equal(["t.ore"], mined.Produced);
        Assert.Equal(["t.pick"], mined.Kept);
        Assert.Equal(["t.berry"], rules.WhatDoes([CatalogToAlchemy.Hand], ["method-forage"]).Produced);   // hands alone fit forage and find-gem, so each carries a method tag
        Assert.Empty(rules.WhatDoes(["t.berry"], []).Produced);
        Assert.Equal(CombineOutcome.Reacted, rules.WhatDoes(["t.berry"], []).Outcome);
        Assert.Equal((1, 4), (content.Recipes.Single(r => r.Id == "t.find-gem").ChanceNumerator, content.Recipes.Single(r => r.Id == "t.find-gem").ChanceDenominator));
    }

    [Fact]
    public void ADropBesideAProductBecomesItsOwnReactionOverTheSameVessel()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + Res("t.c", origin: "crafted");
        var recipes = Rec("t.hunt", kind: "craft", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), drops: """{ "resource": "t.c", "amount": 1, "chance": "1/2" }""");
        var conversion = CatalogToAlchemy.Convert(Set(resources, recipes));
        Assert.Empty(conversion.Report.Refusals);
        Assert.Contains(conversion.Report.Notes, n => n.Code == AlchemyConversionCodes.DropAsSeparateReaction && n.Subject == "t.hunt.drop-c");
        var rules = new AlchemyRules(conversion.Content);
        Assert.Equal(["t.b"], rules.WhatDoes(["t.a"], ["method-hunt"]).Produced);
        var drop = conversion.Content.Recipes.Single(r => r.Id == "t.hunt.drop-c");
        Assert.Equal((1, 2), (drop.ChanceNumerator, drop.ChanceDenominator));
        Assert.Equal(["t.c"], drop.Outputs);
    }

    [Fact]
    public void AlchemyRefusesOnlyAVesselOverTheLimitAndWhatOnlyItCouldMake()
    {
        var inputs = string.Join(",", Enumerable.Range(1, 9).Select(i => Line("t.i" + i, "1")));
        var resources = string.Join(",", Enumerable.Range(1, 9).Select(i => Res("t.i" + i))) + "," + Res("t.big", origin: "crafted") + "," + Res("t.next", origin: "crafted");
        var recipes = Rec("t.too-many", inputs: inputs, outputs: Line("t.big", "1")) + "," + Rec("t.use-big", inputs: Line("t.big", "1"), outputs: Line("t.next", "1"));
        var refusals = CatalogToAlchemy.Convert(Set(resources, recipes)).Report.Refusals.ToDictionary(n => n.Subject, n => n.Code);
        Assert.Equal(AlchemyConversionCodes.VesselTooBig, refusals["t.too-many"]);
        Assert.Equal(AlchemyConversionCodes.Unreachable, refusals["t.big"]);
        Assert.Equal(AlchemyConversionCodes.Unreachable, refusals["t.use-big"]);
    }

    [Fact]
    public void SpoilageBecomesAReactionNeedingTheTimePassesTag()
    {
        var resources = Res("t.a", perishable: """{ "ticks": 100, "result": "t.rot" }""") + "," + Res("t.rot", origin: "crafted");
        var conversion = CatalogToAlchemy.Convert(Set(resources, Rec("t.keep", inputs: Line("t.a", "1"), outputs: Line("t.rot", "1"))));
        Assert.Contains(conversion.Report.Notes, n => n.Code == AlchemyConversionCodes.SpoilageAsReaction && n.Subject == "t.a.spoil");
        Assert.Equal(["t.rot"], new AlchemyRules(conversion.Content).WhatDoes(["t.a"], ["time-passes", "method-a.spoil"]).Produced);   // it shares a vessel with t.keep, so it also carries a method tag
    }

    [Fact]
    public void AlternateRecipesForOneCombinationGetMethodTagsAndAllSurvive()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + Res("t.c", origin: "crafted") + "," + Res("t.d", origin: "crafted");
        var recipes = Rec("t.alpha", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")) + "," + Rec("t.beta", inputs: Line("t.a", "5"), outputs: Line("t.c", "1")) +
            "," + Rec("t.gamma", inputs: Line("t.a", "5"), outputs: Line("t.d", "1"), environment: "\"heat\"") + "," + Rec("t.reach", inputs: Line("t.b", "1"), outputs: Line("t.d", "1"));
        var conversion = CatalogToAlchemy.Convert(Set(resources, recipes));
        Assert.Empty(conversion.Report.Refusals);
        Assert.Equal(4, conversion.Content.Recipes.Count);
        var tagged = conversion.Report.Notes.Where(n => n.Code == AlchemyConversionCodes.MethodTag).Select(n => n.Subject).Order().ToArray();
        Assert.Equal(["t.alpha", "t.beta", "t.gamma"], tagged);   // heat alone does not separate gamma from the others, so all three collide
        var rules = new AlchemyRules(conversion.Content);
        Assert.Equal("t.alpha", rules.WhatDoes(["t.a"], ["method-alpha"]).RecipeId);
        Assert.Equal("t.beta", rules.WhatDoes(["t.a"], ["method-beta"]).RecipeId);
        Assert.Equal("t.gamma", rules.WhatDoes(["t.a"], ["method-gamma", "heat"]).RecipeId);
        // Recipes whose environments already differ are left alone.
        var split = Rec("t.alpha", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), environment: "\"cold\"") + "," + Rec("t.beta", inputs: Line("t.a", "1"), outputs: Line("t.c", "1"), environment: "\"heat\"") + "," + Rec("t.reach", inputs: Line("t.b", "1"), outputs: Line("t.d", "1"));
        Assert.DoesNotContain(CatalogToAlchemy.Convert(Set(resources, split)).Report.Notes, n => n.Code == AlchemyConversionCodes.MethodTag);
    }

    private static SimConversion Sim(string resources, string recipes, string stations = "", string skills = "") => CatalogToSim.Convert(Set(resources, recipes, stations, skills));

    private static string Refusal(SimConversion conversion, string recipe) => conversion.Report.Refusals.Single(n => n.Subject == recipe).Code;

    [Fact]
    public void SimRefusesEachFeatureItCannotRunWithItsOwnCode()
    {
        var resources = Res("t.a") + "," + Res("t.g", grade: """{ "scheme": "quality", "levels": ["lo", "hi"] }""") + "," + Res("t.b", origin: "crafted") + "," + Res("t.c", origin: "crafted");
        var recipes = string.Join(",",
            Rec("t.drops", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), drops: """{ "resource": "t.c", "amount": 1, "chance": "1/2" }"""),
            Rec("t.range", inputs: Line("t.a", "1"), outputs: Line("t.b", """{ "min": 1, "max": 3 }""")),
            Rec("t.skill", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), skill: """{ "id": "t.craft", "level": 1 }"""),
            Rec("t.env", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), environment: "\"night\""),
            Rec("t.quality", inputs: Line("t.g", "1"), outputs: Line("t.b", "1"), quality: "\"inherit-input\""),
            Rec("t.level", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), station: """{ "id": "t.bench", "level": 2 }"""),
            Rec("t.nooutputs", kind: "consume", inputs: Line("t.a", "1")),
            Rec("t.instant", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), duration: """{ "value": 0, "unit": "s" }"""),
            Rec("t.precise", inputs: Line("t.a", "0.1234567891"), outputs: Line("t.b", "1")),
            Rec("t.fine", inputs: Line("t.a", "1"), outputs: Line("t.c", "1")));
        var conversion = Sim(resources, recipes, Bench, Craft);
        Assert.Equal(SimConversionCodes.Drops, Refusal(conversion, "t.drops"));
        Assert.Equal(SimConversionCodes.Range, Refusal(conversion, "t.range"));
        Assert.Equal(SimConversionCodes.Skill, Refusal(conversion, "t.skill"));
        Assert.Equal(SimConversionCodes.Environment, Refusal(conversion, "t.env"));
        Assert.Equal(SimConversionCodes.Quality, Refusal(conversion, "t.quality"));
        Assert.Equal(SimConversionCodes.NoOutputs, Refusal(conversion, "t.nooutputs"));
        Assert.Equal(SimConversionCodes.Instant, Refusal(conversion, "t.instant"));
        Assert.Equal(SimConversionCodes.Precision, Refusal(conversion, "t.precise"));
        Assert.Equal(["t.fine", "t.level"], conversion.Transformations.Order(StringComparer.Ordinal).ToArray());   // a station level is a second processor entity, not a refusal
    }


    [Fact]
    public void SimRunsAnExtractionThatConsumesNothing()
    {
        var resources = Res("t.pick", cls: "tool", unit: "{each}", durability: """{ "uses": 50 }""") + "," + Res("t.ore", origin: "crafted");
        var conversion = Sim(resources, Rec("t.mine", kind: "extract", tools: """{ "resource": "t.pick", "uses": 50 }""", outputs: Line("t.ore", "2")));
        Assert.Equal(["t.mine"], conversion.Transformations);
        Assert.Empty(conversion.Report.Refusals);
        Assert.NotNull(SimWorld.Compose(conversion));
    }


    [Fact]
    public void SimHoldsALargeEnergyInputInTheStationStore()
    {
        // 500 kW.h is 1.8 million kJ in the base unit; the generated store must hold a whole batch of it.
        var resources = Res("t.power", cls: "energy", unit: "kW.h") + "," + Res("t.a") + "," + Res("t.b", origin: "crafted");
        var conversion = Sim(resources, Rec("t.smelt", kind: "smelt", inputs: Line("t.a", "2") + "," + Line("t.power", "500"), outputs: Line("t.b", "1")));
        Assert.Equal(["t.smelt"], conversion.Transformations);
        Assert.NotNull(SimWorld.Compose(conversion));
    }
    [Fact]
    public void SimStillRefusesATransformationWithNoResult()
    {
        var conversion = Sim(AB, Rec("t.eat", kind: "consume", inputs: Line("t.a", "1")) + "," + Rec("t.make-b", inputs: Line("t.a", "2"), outputs: Line("t.b", "1")));
        Assert.Equal(SimConversionCodes.NoOutputs, Refusal(conversion, "t.eat"));
    }
    [Fact]
    public void SimReducesResourceFeaturesItHasNoModelForAndSaysSo()
    {
        var resources = Res("t.a", perishable: """{ "ticks": 100, "result": "t.rot" }""") + "," + Res("t.rot", origin: "crafted") + "," + Res("t.g", grade: """{ "scheme": "rarity", "levels": ["x", "y"] }""");
        var conversion = Sim(resources, Rec("t.fine", inputs: Line("t.a", "1"), outputs: Line("t.rot", "1")));
        var reduced = conversion.Report.Notes.Where(n => n.Level == ConversionLevel.Reduced).ToList();
        Assert.Contains(reduced, n => n.Code == SimConversionCodes.Perishable && n.Subject == "t.a");
        Assert.Contains(reduced, n => n.Code == SimConversionCodes.Grade && n.Subject == "t.g");
        Assert.Contains(reduced, n => n.Code == SimConversionCodes.Stack);
        Assert.Contains(reduced, n => n.Code == SimConversionCodes.DiscreteUnits);
    }

    [Fact]
    public void SimCarriesToolsCatalystsByproductsAndUnitsExactly()
    {
        var resources = Res("t.ore", unit: "g") + "," + Res("t.pick", cls: "tool", unit: "{each}", durability: """{ "uses": 200 }""") + "," + Res("t.seal", cls: "equipment", unit: "{each}") + "," + Res("t.ingot", origin: "crafted") + "," + Res("t.slag", origin: "crafted");
        var recipe = Rec("t.smelt", kind: "smelt", inputs: Line("t.ore", "500"), catalysts: Line("t.seal", "1"), tools: """{ "resource": "t.pick", "uses": 200 }""",
            outputs: Line("t.ingot", "0.2"), byproducts: Line("t.slag", "0.1"), duration: """{ "value": 1.5, "unit": "min" }""");
        var conversion = Sim(resources, recipe);
        var transformation = JsonNode.Parse(conversion.Files["recipes.json"])!["transformations"]![0]!;
        Assert.Equal(0.5m, (decimal)transformation["inputs"]!["t.ore"]!);               // 500 g is 0.5 kg in the base unit
        Assert.Equal(1m, (decimal)transformation["tools"]!["t.pick"]!);                 // held while it runs
        Assert.Equal(1m, (decimal)transformation["tools"]!["t.seal"]!);                 // a catalyst is held and not worn
        Assert.Equal(0.005m, (decimal)transformation["tool_consumption"]!["t.pick"]!);  // 1/200 of a pick per run
        Assert.Null(transformation["tool_consumption"]!["t.seal"]);
        Assert.Equal(0.2m, (decimal)transformation["outputs"]!["t.ingot"]!);
        Assert.Equal(0.1m, (decimal)transformation["outputs"]!["t.slag"]!);             // the byproduct is an ordinary output
        Assert.Equal(3, (int)transformation["duration"]!["numerator"]!);                // 1.5 minutes as 3/2
        Assert.Equal(2, (int)transformation["duration"]!["denominator"]!);
        Assert.Equal("minute", (string)transformation["duration"]!["unit"]!);
        Assert.Equal("catalog.feed", (string)transformation["input_ports"]!["t.ore"]!);
        Assert.Equal("catalog.product", (string)transformation["output_ports"]!["t.slag"]!);
        // The engine reads it.
        Assert.NotNull(SimWorld.Compose(conversion));
    }


    [Fact]
    public void ALeveledStationBecomesOneEntityPerLevelAndAHigherLevelRunsTheLowerRecipes()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + Res("t.c", origin: "crafted");
        var recipes = Rec("t.basic", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), station: """{ "id": "t.bench", "level": 1 }""") + "," + Rec("t.fancy", inputs: Line("t.a", "2"), outputs: Line("t.c", "1"), station: """{ "id": "t.bench", "level": 2 }""");
        var conversion = Sim(resources, recipes, Bench);
        Assert.Equal(["catalog.at.t.bench", "catalog.at.t.bench.l2"], conversion.Stations.ToArray());
        Assert.Contains(conversion.Report.Notes, n => n.Code == SimConversionCodes.StationLevelsAsEntities && n.Subject == "t.bench");
        var entities = JsonNode.Parse(conversion.Files["world.json"])!["entities"]!.AsArray();
        string[] Runs(string id) => entities.Single(e => (string)e!["id"]! == id)!["components"]!["transformer"]!["transformations"]!.AsArray().Select(n => (string)n!).ToArray();
        Assert.Equal(["t.basic"], Runs("catalog.at.t.bench"));
        Assert.Equal(["t.basic", "t.fancy"], Runs("catalog.at.t.bench.l2"));
        Assert.NotNull(SimWorld.Compose(conversion));
    }
    [Fact]
    public void SimGivesEachStationItsOwnProcessorAndHandCraftsOneShared()
    {
        var resources = Res("t.a") + "," + Res("t.b", origin: "crafted") + "," + Res("t.c", origin: "crafted");
        var recipes = Rec("t.on-bench", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), station: """{ "id": "t.bench", "level": 1 }""") + "," + Rec("t.by-hand", inputs: Line("t.a", "2"), outputs: Line("t.c", "1"));
        var conversion = Sim(resources, recipes, Bench);
        Assert.Equal(["catalog.at.t.bench", CatalogToSim.HandEntity], conversion.Stations.Order(StringComparer.Ordinal).ToArray().Reverse().ToArray().Reverse().ToArray());
        Assert.NotNull(SimWorld.Compose(conversion));
    }
}
