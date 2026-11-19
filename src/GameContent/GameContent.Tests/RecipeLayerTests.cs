using EconomyEngine;
using EngineCommon;
using GameContent.Catalog;
using GameRuntime;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>One recipe, one layer: a game that composes the SimEngine world and the economy document of the same pack is refused when a recipe would run in both, and accepted when the ownership declaration splits them.</summary>
public sealed class RecipeLayerTests
{
    private static string Priced(string id, string origin, decimal price) => Res(id, cls: origin == "natural" ? "raw" : "intermediate", origin: origin, properties: "\"base-price\": " + price);

    private static CatalogSet Pack() => Load(Single(Priced("t.a", "natural", 4) + "," + Priced("t.b", "crafted", 5) + "," + Priced("t.c", "crafted", 6),
        Rec("t.one", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")) + "," + Rec("t.two", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")))).Required();

    private static readonly EconomyConversionOptions Options = new(1000, 1m, GoodTuning.Town, []);

    private static EconomySet Economy(EconomyConversion conversion)
    {
        var goods = conversion.Goods.ToList();
        var world = new System.Text.Json.Nodes.JsonObject
        {
            ["format"] = 6, ["id"] = "w", ["seed"] = 1, ["time"] = new System.Text.Json.Nodes.JsonObject { ["stepsPerDay"] = 1, ["periodSteps"] = 1 },
            ["limits"] = new System.Text.Json.Nodes.JsonObject { ["maxShipments"] = 10, ["maxLoans"] = 0, ["maxShocks"] = 1, ["history"] = 2 },
            ["price"] = new System.Text.Json.Nodes.JsonObject { ["responseMin"] = 200, ["responseMax"] = 3000, ["settlePeriods"] = 4, ["bandMargin"] = 0, ["noise"] = 0, ["arrearsPenalty"] = 300 },
            ["routing"] = new System.Text.Json.Nodes.JsonObject { ["mode"] = "direct" },
            ["currencies"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["id"] = "c", ["key"] = "k.c", ["reference"] = true, ["anchor"] = 1_000_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 } },
            ["bands"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["id"] = "b", ["key"] = "k.b", ["from"] = 0 } },
            ["schedules"] = new System.Text.Json.Nodes.JsonArray(), ["policies"] = new System.Text.Json.Nodes.JsonArray(), ["modifiers"] = new System.Text.Json.Nodes.JsonArray(), ["shocks"] = new System.Text.Json.Nodes.JsonArray(),
            ["routes"] = new System.Text.Json.Nodes.JsonArray(), ["loans"] = new System.Text.Json.Nodes.JsonArray(), ["facts"] = new System.Text.Json.Nodes.JsonArray(),
            ["markets"] = new System.Text.Json.Nodes.JsonArray
            {
                new System.Text.Json.Nodes.JsonObject
                {
                    ["id"] = "m", ["key"] = "k.m", ["currency"] = "c", ["treasury"] = 100, ["staff"] = 0, ["spread"] = 0,
                    ["pairs"] = new System.Text.Json.Nodes.JsonArray(goods.Select(g => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["good"] = g, ["production"] = 0, ["consumption"] = 0, ["stock"] = 0, ["cover"] = 0, ["capacity"] = 1000 }).ToArray()),
                    ["policies"] = new System.Text.Json.Nodes.JsonArray(), ["budget"] = new System.Text.Json.Nodes.JsonArray(),
                    ["lines"] = new System.Text.Json.Nodes.JsonArray(conversion.Recipes.Select(r => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["recipe"] = r, ["capacity"] = 5 }).ToArray()),
                },
            },
        };
        return EconomyLoader.Load(conversion.Compose(world.ToJsonString()));
    }

    [Fact]
    public void AGameThatComposesTheSameRecipesIntoBothLayersIsRefusedAndNamesThem()
    {
        var set = Pack();
        var sim = CatalogToSim.Convert(set);
        var economy = Economy(CatalogToEconomy.Convert(set, Options));
        var refusal = Assert.Throws<EngineRefusedException>(() => EconomyRecipeLayers.Check(economy, SimWorld.Content(sim)));
        Assert.Equal(EconomyRecipeLayers.DoubleOwner, refusal.Code);
        Assert.Contains("t.one", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("t.two", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOwnershipDeclarationSplitsThePackSoEachRecipeRunsInOneLayerAndTheGameIsAccepted()
    {
        var set = Pack();
        var ownership = RecipeOwnership.Parse("""{ "format": 1, "owners": { "t.one": "sim", "t.two": "economy" } }""");
        var sim = CatalogToSim.Convert(set, ownership);
        var economy = Economy(CatalogToEconomy.Convert(set, Options with { Ownership = ownership }));
        Assert.Equal(["t.two"], economy.Recipes.Select(r => r.Id).ToArray());
        EconomyRecipeLayers.Check(economy, SimWorld.Content(sim));   // no refusal
        // Declaring both to the economy and still composing t.one into the sim is the mistake the check exists for: the sim conversion without the declaration takes it again.
        Assert.Equal(EconomyRecipeLayers.DoubleOwner, Assert.Throws<EngineRefusedException>(() => EconomyRecipeLayers.Check(economy, SimWorld.Content(CatalogToSim.Convert(set)))).Code);
        Assert.Throws<ArgumentNullException>(() => EconomyRecipeLayers.Check(null!, SimWorld.Content(sim)));
    }
}
