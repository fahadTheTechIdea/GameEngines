using System.Text.Json.Nodes;
using AlchemyEngine;
using EconomyEngine;
using GameContent.Catalog;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>
/// Discovery and production together: a recipe authored once in the catalog is found by the alchemy book (the discovery layer), which unlocks it in the economy (the production layer), which then runs it with the
/// amounts, durations and lines of its document. The economy keeps the unlocked set as state; alchemy keeps what the player knows.
/// </summary>
public sealed class CatalogBridgeTests
{
    private static string Res(string id, string cls, string origin, decimal price) =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "class": "{{cls}}", "unit": "kg", "origin": "{{origin}}", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": { "base-price": {{price}} } }""";

    private static string Rec(string id, string inputs, string outputs) =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "kind": "smelt", "inputs": [{{inputs}}], "catalysts": [], "tools": [], "outputs": [{{outputs}}], "byproducts": [], "drops": [], "station": null, "skill": null, "environment": [], "duration": { "value": 1, "unit": "s" }, "quality": null, "reverse": null }""";

    private static string Line(string resource, int amount) => $$"""{ "resource": "{{resource}}", "amount": {{amount}} }""";

    /// <summary>ore + coal -> steel (found by combining), and steel -> tools (open from the start).</summary>
    private static CatalogSet Pack()
    {
        var resources = string.Join(",", Res("t.ore", "raw", "natural", 4), Res("t.coal", "raw", "natural", 3), Res("t.steel", "intermediate", "crafted", 14), Res("t.tools", "product", "crafted", 20));
        var recipes = Rec("t.smelt", Line("t.ore", 2) + "," + Line("t.coal", 1), Line("t.steel", 1)) + "," + Rec("t.smith", Line("t.steel", 2), Line("t.tools", 3));
        var files = new Dictionary<string, string>
        {
            ["t/pack.json"] = """{ "format": 1, "id": "t", "version": "1.0.0", "title": "t", "language": "en", "world": "timeless", "genres": ["test"], "dependencies": [], "license": { "spdx": "CC0-1.0", "provenance": "original", "author": "tests" }, "locales": [], "documents": ["doc.json"] }""",
            ["t/doc.json"] = "{ \"format\": 1, \"pack\": \"t\", \"resources\": [" + resources + "], \"recipes\": [" + recipes + "] }",
        };
        return CatalogLoader.Load(new MemoryCatalogSource(files), "t").Required();
    }

    private static string World(IEnumerable<string> goods)
    {
        var list = goods.ToList();
        var root = new JsonObject
        {
            ["format"] = 6, ["id"] = "chain", ["seed"] = 1,
            ["time"] = new JsonObject { ["stepsPerDay"] = 1, ["periodSteps"] = 1 },
            ["limits"] = new JsonObject { ["maxShipments"] = 10, ["maxLoans"] = 0, ["maxShocks"] = 1, ["history"] = 2 },
            ["price"] = new JsonObject { ["responseMin"] = 200, ["responseMax"] = 3000, ["settlePeriods"] = 4, ["bandMargin"] = 0, ["noise"] = 0, ["arrearsPenalty"] = 300 },
            ["routing"] = new JsonObject { ["mode"] = "direct" },
            ["currencies"] = new JsonArray { new JsonObject { ["id"] = "c", ["key"] = "k.c", ["reference"] = true, ["anchor"] = 1_000_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 } },
            ["bands"] = new JsonArray { new JsonObject { ["id"] = "b", ["key"] = "k.b", ["from"] = 0 } },
            ["schedules"] = new JsonArray(), ["policies"] = new JsonArray(), ["modifiers"] = new JsonArray(), ["shocks"] = new JsonArray(), ["routes"] = new JsonArray(), ["loans"] = new JsonArray(), ["facts"] = new JsonArray(),
            ["markets"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "m", ["key"] = "k.m", ["currency"] = "c", ["treasury"] = 100, ["staff"] = 0, ["spread"] = 0,
                    ["pairs"] = new JsonArray(list.Select(g => (JsonNode)new JsonObject
                    {
                        ["good"] = g, ["production"] = 0, ["consumption"] = 0, ["stock"] = g == "t.ore" ? 100 : g == "t.coal" ? 30 : 0, ["cover"] = 0, ["capacity"] = 1_000_000,
                    }).ToArray()),
                    ["policies"] = new JsonArray(), ["budget"] = new JsonArray(),
                    ["lines"] = new JsonArray { new JsonObject { ["recipe"] = "t.smelt", ["capacity"] = 40 }, new JsonObject { ["recipe"] = "t.smith", ["capacity"] = 10 } },
                },
            },
        };
        return root.ToJsonString();
    }

    private static EconomySet LockedSmelt()
    {
        var conversion = CatalogToEconomy.Convert(Pack(), new EconomyConversionOptions(1000, 1m, GoodTuning.Town, ["t.smelt"]));
        Assert.Equal(["t.smelt", "t.smith"], conversion.Recipes);
        return EconomyLoader.Load(conversion.Compose(World(conversion.Goods)));
    }

    [Fact]
    public void ARecipeTheBookDiscoversUnlocksProductionThatTheEconomyThenRunsWhileTheOpenRecipeNeverWaited()
    {
        var economySet = LockedSmelt();
        Assert.True(economySet.RecipeById["t.smelt"].Locked);
        Assert.False(economySet.RecipeById["t.smith"].Locked);

        var alchemy = CatalogToAlchemy.Convert(Pack());
        var book = new DiscoveryBook(alchemy.Content);
        var alchemist = new AlchemyComponent(new AlchemyRules(alchemy.Content), book, 7);
        var unlocks = new EconomyRecipeUnlocks("economy-unlocks", economySet);
        var host = new EcoHost(set: economySet, extra: [(alchemist, 5), (unlocks, 9)], declare: AlchemyEventCatalog.Declare);
        long Stock(string good) => host.Runtime.Markets[0].Pairs[host.Runtime.PairIndex(0, host.Runtime.GoodIndex(good))].Stock;

        // Before any discovery the locked recipe does not run, whatever the stock: the economy waits.
        host.Tick(3);
        Assert.Equal((100L, 30L, 0L), (Stock("t.ore"), Stock("t.coal"), Stock("t.steel")));
        Assert.Empty(host.Of<EconomyRecipeUnlocked>());
        Assert.DoesNotContain("t.smelt", book.Recipes);

        // The player combines ore and coal and finds the recipe: the book learns it, the bridge asks, the economy unlocks it.
        host.Session.Submit(new CombineRequested("try-1", "t.ore+t.coal", ""));
        host.Tick(3);
        Assert.Contains("t.smelt", book.Recipes);
        var unlocked = Assert.Single(host.Of<EconomyRecipeUnlocked>());
        Assert.Equal("t.smelt", unlocked.Recipe);
        Assert.True(host.Runtime.IsUnlocked(economySet.RecipeById["t.smelt"].Index));
        Assert.Empty(host.Of<EconomyRefused>());

        // The economy runs it with the document's numbers: 30 runs of 2 ore + 1 coal (the coal is the limit) make 30 steel, and the open recipe turns them into 45 tools (15 runs of 2 steel -> 3 tools).
        host.Tick(10);
        Assert.Equal((40L, 0L, 0L, 45L), (Stock("t.ore"), Stock("t.coal"), Stock("t.steel"), Stock("t.tools")));
        Assert.Empty(host.Runtime.Batches);
        Assert.Equal(LineLimit.Input, host.Runtime.Markets[0].Lines[0].Limit);
    }

    [Fact]
    public void ADiscoveryTheEconomyHasNoLockedRecipeForIsIgnoredAndAskedForOnlyOnce()
    {
        var set = LockedSmelt();
        var unlocks = new EconomyRecipeUnlocks("u", set);
        var host = new EcoHost(set: set, extra: [(unlocks, 9)], declare: AlchemyEventCatalog.Declare);
        host.Session.Submit(new DiscoveryMade("x", 1, "element", "t.steel"));      // an element, not a recipe
        host.Session.Submit(new DiscoveryMade("x", 1, "recipe", "t.smith"));       // a recipe the economy never locked
        host.Session.Submit(new DiscoveryMade("x", 1, "recipe", "t.unknown"));     // a recipe the economy does not have
        host.Tick(2);
        Assert.Empty(host.Of<EconomyApplied>());
        Assert.Empty(host.Of<EconomyRefused>());
        Assert.True(unlocks.IsQuiet);
        host.Session.Submit(new DiscoveryMade("y", 3, "recipe", "t.smelt"));
        host.Session.Submit(new DiscoveryMade("external:sim", 3, "recipe", "t.smelt"));   // told twice, asked once
        host.Tick(2);
        Assert.Single(host.Of<EconomyApplied>());
        Assert.Single(host.Of<EconomyRecipeUnlocked>());
    }
}
