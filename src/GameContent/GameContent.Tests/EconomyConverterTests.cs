using System.Text.Json.Nodes;
using EconomyEngine;
using GameContent.Catalog;
using Xunit;
using static GameContent.Tests.PackBuilder;

namespace GameContent.Tests;

/// <summary>
/// The catalog to Economy converter: each feature is converted, reduced with a note or refused with a code; the generated goods and recipes compose into an economy document that EconomyEngine loads and runs; a recipe is
/// owned by one layer; the shipped packs convert and load.
/// </summary>
public sealed class EconomyConverterTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("catalog-economy-");

    public void Dispose() => _dir.Delete(true);

    private static string Priced(string id, decimal price, string cls = "raw", string origin = "natural", string unit = "kg", string tags = "") =>
        Res(id, cls: cls, unit: unit, origin: origin, tags: tags, properties: "\"base-price\": " + price.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static CatalogSet Set(string resources, string recipes, string stations = "", string skills = "") => Load(Single(resources, recipes, stations, skills)).Required();

    private static readonly EconomyConversionOptions OneHourPeriods = new(1000, 3600m, GoodTuning.Town, []);

    private static ConversionNote Note(EconomyConversion conversion, string subject, string code) => Assert.Single(conversion.Report.Notes, n => n.Subject == subject && n.Code == code);

    private static readonly string Bench = """{ "id": "t.bench", "name": "Bench", "levels": 3 }""";
    private static readonly string Craft = """{ "id": "t.craft", "name": "Craft", "max": 9 }""";

    // ---- the chain, end to end ----------------------------------------------------------------------------------------------------------

    private static string World(IEnumerable<string> goods, IEnumerable<(string Market, string Recipe, int Capacity)> lines, IReadOnlyDictionary<string, long>? stocks = null)
    {
        var goodList = goods.ToList();
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
        };
        var markets = new JsonArray();
        foreach (var market in lines.Select(l => l.Market).Distinct().Order(StringComparer.Ordinal))
            markets.Add(new JsonObject
            {
                ["id"] = market, ["key"] = "k.m." + market, ["currency"] = "c", ["treasury"] = 100, ["staff"] = 0, ["spread"] = 0,
                ["pairs"] = new JsonArray(goodList.Select(g => (JsonNode)new JsonObject
                {
                    ["good"] = g, ["production"] = 0, ["consumption"] = 0, ["stock"] = stocks is not null && stocks.TryGetValue(g, out var stock) ? stock : 0, ["cover"] = 0, ["capacity"] = 1_000_000,
                }).ToArray()),
                ["policies"] = new JsonArray(), ["budget"] = new JsonArray(),
                ["lines"] = new JsonArray(lines.Where(l => l.Market == market).Select(l => (JsonNode)new JsonObject { ["recipe"] = l.Recipe, ["capacity"] = l.Capacity }).ToArray()),
            });
        root["markets"] = markets;
        return root.ToJsonString();
    }

    private static CatalogSet SteelPack() => Set(
        Priced("t.ore", 4) + "," + Priced("t.coal", 3) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Priced("t.tools", 20, "product", "crafted", "{each}"),
        Rec("t.smelt", kind: "smelt", inputs: Line("t.ore", "2") + "," + Line("t.coal", "1"), outputs: Line("t.steel", "1"), duration: """{ "value": 60, "unit": "s" }""") + "," +
        Rec("t.smith", inputs: Line("t.steel", "2"), outputs: Line("t.tools", "3"), duration: """{ "value": 2, "unit": "h" }"""));

    [Fact]
    public void AChainConvertsComposesLoadsAndRunsTheWayItWasWorkedByHand()
    {
        var conversion = CatalogToEconomy.Convert(SteelPack(), OneHourPeriods);
        Assert.Equal(["t.coal", "t.ore", "t.steel", "t.tools"], conversion.Goods);
        Assert.Equal(["t.smelt", "t.smith"], conversion.Recipes);
        var goods = JsonNode.Parse(conversion.GoodsJson)!.AsArray();
        Assert.Equal([3000L, 4000L, 14000L, 20000L], goods.Select(g => g!["base"]!.GetValue<long>()).ToArray());
        var recipes = JsonNode.Parse(conversion.RecipesJson)!.AsArray();
        Assert.Equal([1, 2], recipes.Select(r => r!["duration"]!.GetValue<int>()).ToArray());   // 60 s rounds up to one hour-long period; 2 h are two periods
        Assert.Equal(("t.coal", 1L, "t.ore", 2L), (recipes[0]!["inputs"]![0]!["good"]!.GetValue<string>(), recipes[0]!["inputs"]![0]!["amount"]!.GetValue<long>(), recipes[0]!["inputs"]![1]!["good"]!.GetValue<string>(), recipes[0]!["inputs"]![1]!["amount"]!.GetValue<long>()));   // inputs are listed by good id
        Assert.All(recipes, r => Assert.False(r!["locked"]!.GetValue<bool>()));
        Assert.Equal("t.ore", conversion.Names["good.t.ore"]);
        Note(conversion, "t.smelt", EconomyConversionCodes.DurationRounded);
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.smith" && n.Code == EconomyConversionCodes.DurationRounded);   // 7200 s is exactly two periods

        // The generated arrays and a world compose into a document EconomyEngine reads, and the chain runs as it does by hand (ore 100, coal 30; smelt 40 runs, smith 10 runs).
        var text = conversion.Compose(World(conversion.Goods, [("m", "t.smelt", 40), ("m", "t.smith", 10)], new Dictionary<string, long> { ["t.ore"] = 100, ["t.coal"] = 30 }));
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(text));
        long Stock(string good) => runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex(good))].Stock;
        for (var period = 0; period < 4; period++) runtime.Advance(runtime.Step + 1, new Dictionary<string, ScenarioEngine.Contracts.FactValue>());
        Assert.Equal((40L, 0L, 0L, 30L), (Stock("t.ore"), Stock("t.coal"), Stock("t.steel"), Stock("t.tools")));
        Assert.Equal(5, Assert.Single(runtime.Batches).Runs);   // 5 runs of smith are on their way: 15 tools
    }

    [Fact]
    public void ComposeRefusesAWorldThatAlreadyHasGoodsOrRecipesAndSomethingThatIsNotAnObject()
    {
        var conversion = CatalogToEconomy.Convert(SteelPack(), OneHourPeriods);
        Assert.Throws<ArgumentException>(() => conversion.Compose("[]"));
        Assert.Throws<ArgumentException>(() => conversion.Compose("{ \"goods\": [] }"));
        Assert.Throws<ArgumentException>(() => conversion.Compose("{ \"recipes\": [] }"));
        Assert.Throws<ArgumentNullException>(() => conversion.Compose(null!));
    }

    [Fact]
    public void ARecipeTheGameLocksIsLockedInTheDocumentAndANameThatIsNotARecipeIsAMistake()
    {
        var conversion = CatalogToEconomy.Convert(SteelPack(), OneHourPeriods with { Locked = ["t.smith"] });
        var recipes = JsonNode.Parse(conversion.RecipesJson)!.AsArray();
        Assert.Equal([false, true], recipes.Select(r => r!["locked"]!.GetValue<bool>()).ToArray());
        var set = EconomyLoader.Load(conversion.Compose(World(conversion.Goods, [("m", "t.smelt", 1), ("m", "t.smith", 1)])));
        Assert.True(set.RecipeById["t.smith"].Locked);
        Assert.Throws<ArgumentException>(() => CatalogToEconomy.Convert(SteelPack(), OneHourPeriods with { Locked = ["t.nothing"] }));
        Assert.Throws<ArgumentException>(() => CatalogToEconomy.Convert(SteelPack(), OneHourPeriods with { MilliPerPriceUnit = 0 }));
        Assert.Throws<ArgumentException>(() => CatalogToEconomy.Convert(SteelPack(), OneHourPeriods with { SecondsPerPeriod = 0 }));
    }

    // ---- the capability matrix, one feature at a time -----------------------------------------------------------------------------------

    [Fact]
    public void EachFeatureTheEconomyCannotRunIsRefusedWithItsOwnCode()
    {
        var resources = string.Join(",", Priced("t.a", 4), Priced("t.b", 5, "intermediate", "crafted"), Priced("t.c", 6, "intermediate", "crafted"), Res("t.g", grade: """{ "scheme": "quality", "levels": ["lo", "hi"] }""", properties: "\"base-price\": 4"),
            Res("t.gold", cls: "currency", unit: "{coin}", origin: "crafted", currency: """{ "kind": "fictional", "code": "GLD" }"""), Res("t.free", origin: "crafted"), Priced("t.cheap", 0.0001m, "intermediate", "crafted"));
        var recipes = string.Join(",",
            Rec("t.drops", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), drops: """{ "resource": "t.c", "amount": 1, "chance": "1/2" }"""),
            Rec("t.range", inputs: Line("t.a", "1"), outputs: Line("t.b", """{ "min": 1, "max": 3 }""")),
            Rec("t.skill", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), skill: """{ "id": "t.craft", "level": 1 }"""),
            Rec("t.env", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), environment: "\"night\""),
            Rec("t.quality", inputs: Line("t.g", "1"), outputs: Line("t.b", "1"), quality: "\"inherit-input\""),
            Rec("t.dig", kind: "extract", outputs: Line("t.a", "1")),
            Rec("t.eat", kind: "consume", inputs: Line("t.a", "1")),
            Rec("t.pay", inputs: Line("t.a", "1"), outputs: Line("t.gold", "1")),
            Rec("t.noprice", inputs: Line("t.a", "1"), outputs: Line("t.free", "1")),
            Rec("t.cheapo", inputs: Line("t.a", "1"), outputs: Line("t.cheap", "1")),
            Rec("t.precise", inputs: Line("t.a", "0.1234"), outputs: Line("t.b", "1")),
            Rec("t.huge", inputs: Line("t.a", "2000000"), outputs: Line("t.b", "1")),
            Rec("t.slow", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), duration: """{ "value": 70, "unit": "h" }"""),
            Rec("t.fine", inputs: Line("t.a", "1"), outputs: Line("t.c", "1")));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes, "", Craft), OneHourPeriods);
        string Refusal(string recipe) => conversion.Report.Refusals.Single(n => n.Subject == recipe).Code;
        Assert.Equal(EconomyConversionCodes.RandomYield, Refusal("t.drops"));
        Assert.Equal(EconomyConversionCodes.AmountRange, Refusal("t.range"));
        Assert.Equal(EconomyConversionCodes.SkillGate, Refusal("t.skill"));
        Assert.Equal(EconomyConversionCodes.Environment, Refusal("t.env"));
        Assert.Equal(EconomyConversionCodes.Quality, Refusal("t.quality"));
        Assert.Equal(EconomyConversionCodes.NoInputs, Refusal("t.dig"));
        Assert.Equal(EconomyConversionCodes.NoOutputs, Refusal("t.eat"));
        Assert.Equal(EconomyConversionCodes.NotAGood, Refusal("t.pay"));
        Assert.Equal(EconomyConversionCodes.NoPrice, Refusal("t.noprice"));
        Assert.Equal(EconomyConversionCodes.PriceRange, Refusal("t.cheapo"));
        Assert.Equal(EconomyConversionCodes.Precision, Refusal("t.precise"));
        Assert.Equal(EconomyConversionCodes.Precision, Refusal("t.huge"));
        Assert.Equal(EconomyConversionCodes.DurationTooLong, Refusal("t.slow"));
        Assert.Equal(["t.fine"], conversion.Recipes);   // the only recipe the Economy can run
    }

    [Fact]
    public void ADurationOfExactlySixtyFourPeriodsIsKeptAndSixtyFiveIsRefused()
    {
        var resources = Priced("t.a", 4) + "," + Priced("t.b", 5, "intermediate", "crafted") + "," + Priced("t.c", 6, "intermediate", "crafted");
        var recipes = Rec("t.edge", inputs: Line("t.a", "1"), outputs: Line("t.b", "1"), duration: """{ "value": 64, "unit": "h" }""") + "," +
            Rec("t.over", inputs: Line("t.a", "1"), outputs: Line("t.c", "1"), duration: """{ "value": 65, "unit": "h" }""");
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes), OneHourPeriods);
        Assert.Equal(["t.edge"], conversion.Recipes);
        Assert.Equal(64, JsonNode.Parse(conversion.RecipesJson)![0]!["duration"]!.GetValue<int>());
        Assert.Equal(EconomyConversionCodes.DurationTooLong, Assert.Single(conversion.Report.Refusals).Code);
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.edge" && n.Code == EconomyConversionCodes.DurationRounded);
    }

    [Fact]
    public void EnergyFluidsAndGasesAreGoodsALaborInformationPopulationAndUniqueThingAreNot()
    {
        string Resource(string id, string cls, string unit) => Res(id, cls: cls, unit: unit, origin: "crafted", stack: cls == "unique" ? "1" : "10", properties: "\"base-price\": 5");
        var resources = Priced("t.a", 4) + "," + Resource("t.power", "energy", "kW.h") + "," + Resource("t.water", "fluid", "L") + "," + Resource("t.steam", "gas", "m3") + "," +
            Resource("t.hours", "labor", "h") + "," + Resource("t.lore", "information", "{point}") + "," + Resource("t.crowd", "population", "{person}") + "," + Resource("t.relic", "unique", "{each}");
        var recipes = string.Join(",", new[] { "power", "water", "steam", "hours", "lore", "crowd", "relic" }.Select(name =>
            Rec("t.make-" + name, inputs: Line("t.a", "1"), outputs: Line("t." + name, "1"))));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes), OneHourPeriods);
        Assert.Equal(["t.make-power", "t.make-steam", "t.make-water"], conversion.Recipes);
        Assert.Equal(["t.make-crowd", "t.make-hours", "t.make-lore", "t.make-relic"], conversion.Report.Refusals.Where(n => n.Code == EconomyConversionCodes.NotAGood).Select(n => n.Subject).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ARecipeWithMoreThanEightGoodsOnASideIsRefused()
    {
        var resources = string.Join(",", Enumerable.Range(1, 9).Select(i => Priced("t.i" + i, 4))) + "," + Priced("t.big", 4, "intermediate", "crafted");
        var recipe = Rec("t.many", inputs: string.Join(",", Enumerable.Range(1, 9).Select(i => Line("t.i" + i, "1"))), outputs: Line("t.big", "1"));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipe), OneHourPeriods);
        Assert.Equal(EconomyConversionCodes.TooManyGoods, Assert.Single(conversion.Report.Refusals).Code);
        Assert.Empty(conversion.Recipes);
    }

    [Fact]
    public void EachFeatureTheEconomyApproximatesIsReducedWithANote()
    {
        var resources = string.Join(",",
            Priced("t.a", 4, tags: "\"a1\",\"a2\",\"a3\",\"a4\",\"a5\",\"a6\",\"a7\",\"a8\",\"a9\""), Priced("t.pick", 9, "tool", "natural", "{each}"), Priced("t.seal", 9, "equipment", "natural", "{each}"),
            Res("t.b", origin: "crafted", cls: "intermediate", stack: "50", grade: """{ "scheme": "quality", "levels": ["lo", "hi"] }""", properties: "\"base-price\": 5"),
            Priced("t.slag", 1, "intermediate", "crafted"), Priced("t.c", 6, "intermediate", "crafted"));
        var recipes = string.Join(",",
            Rec("t.half", inputs: Line("t.a", "0.5"), outputs: Line("t.b", "1")),
            Rec("t.milli", inputs: Line("t.a", "0.001"), outputs: Line("t.c", "1")),
            Rec("t.rich", kind: "smelt", inputs: Line("t.a", "2"), catalysts: Line("t.seal", "1"), tools: """{ "resource": "t.pick", "uses": 50 }""", outputs: Line("t.b", "1"), byproducts: Line("t.slag", "2"),
                station: """{ "id": "t.bench", "level": 2 }""", duration: """{ "value": 90, "unit": "min" }"""),
            Rec("t.back", kind: "disassemble", inputs: Line("t.c", "1"), outputs: Line("t.b", "1")),
            Rec("t.fwd", inputs: Line("t.b", "1"), outputs: Line("t.c", "1"), reverse: "\"t.back\""));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes, Bench), OneHourPeriods);
        Note(conversion, "t.half", EconomyConversionCodes.BatchScaled);
        Assert.Contains("1000 catalog runs", Note(conversion, "t.milli", EconomyConversionCodes.BatchScaled).Detail, StringComparison.Ordinal);   // the largest batch allowed
        var half = JsonNode.Parse(conversion.RecipesJson)!.AsArray().Single(r => r!["id"]!.GetValue<string>() == "t.half")!;
        Assert.Equal((1L, 2L), (half["inputs"]![0]!["amount"]!.GetValue<long>(), half["outputs"]![0]!["amount"]!.GetValue<long>()));   // a run is two catalog runs: 1 a in, 2 b out
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.rich" && n.Code.Contains("TOOL", StringComparison.Ordinal));   // the pick's 50 uses are worn exactly: supported, no note
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.rich" && n.Code == EconomyConversionCodes.RequirementNotAGood);   // the seal and the pick are priced goods: supported, no note
        Note(conversion, "t.rich", EconomyConversionCodes.StationAsLine);
        Note(conversion, "t.rich", EconomyConversionCodes.ByproductsMerged);
        Note(conversion, "t.rich", EconomyConversionCodes.DurationRounded);   // 90 minutes are 1.5 periods: two
        var rich = JsonNode.Parse(conversion.RecipesJson)!.AsArray().Single(r => r!["id"]!.GetValue<string>() == "t.rich")!;
        Assert.Equal(2, rich["duration"]!.GetValue<int>());
        Assert.Equal([("t.pick", 1L), ("t.seal", 1L)], rich["requires"]!.AsArray().Select(r => (r!["good"]!.GetValue<string>(), r["amount"]!.GetValue<long>())).ToArray());   // catalyst and tool, by good id
        Assert.Equal((1L, 50L), (rich["requires"]![0]!["wear"]!["units"]!.GetValue<long>(), rich["requires"]![0]!["wear"]!["runs"]!.GetValue<long>()));   // the pick: 1 item in its 50 uses
        Assert.Null(rich["requires"]![1]!["wear"]);       // the seal is a catalyst: kept, it never wears
        Assert.Equal(["t.b", "t.slag"], rich["outputs"]!.AsArray().Select(o => o!["good"]!.GetValue<string>()).ToArray());
        Note(conversion, "t.fwd", EconomyConversionCodes.ReverseNotModelled);
        Note(conversion, "t.a", EconomyConversionCodes.TagsTruncated);
        Note(conversion, "t.b", EconomyConversionCodes.ResourceFields);
        var a = JsonNode.Parse(conversion.GoodsJson)!.AsArray().Single(g => g!["id"]!.GetValue<string>() == "t.a")!;
        Assert.Equal(8, a["tags"]!.AsArray().Count);
        Assert.Equal("t.back", Assert.Single(conversion.Report.Refusals).Subject);   // the cycle t.fwd > t.back is broken at the disassembly
        Assert.Equal(EconomyConversionCodes.Cycle, Assert.Single(conversion.Report.Refusals).Code);
    }

    // ---- catalysts and tools: held in stock, never used up ------------------------------------------------------------------------------

    private static CatalogSet ForgePack() => Set(
        Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Priced("t.hammer", 30, "tool", "natural", "{each}"),
        Rec("t.forge", kind: "smelt", inputs: Line("t.ore", "2"), tools: """{ "resource": "t.hammer", "uses": 100 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }"""));

    [Fact]
    public void AToolIsAGoodTheLineMustHoldAndNeverUsesUpAndTheRunWaitsForIt()
    {
        var conversion = CatalogToEconomy.Convert(ForgePack(), OneHourPeriods);
        Assert.Equal(["t.hammer", "t.ore", "t.steel"], conversion.Goods);   // the hammer is a good of the document because the recipe requires it
        Assert.True(conversion.Report.Notes.All(n => n.Code is EconomyConversionCodes.ResourceFields or EconomyConversionCodes.DurationRounded), string.Join("; ", conversion.Report.Notes.Select(n => n.Code)));   // holding the tool and wearing it by its uses are supported: no note says anything was lost
        var forge = JsonNode.Parse(conversion.RecipesJson)!.AsArray()[0]!["requires"]![0]!;
        Assert.Equal((1L, 100L), (forge["wear"]!["units"]!.GetValue<long>(), forge["wear"]!["runs"]!.GetValue<long>()));
        EconomyRuntime Play(long hammers, out EconomyRuntime runtimeOut)
        {
            var text = conversion.Compose(World(conversion.Goods, [("m", "t.forge", 10)], new Dictionary<string, long> { ["t.ore"] = 100, ["t.hammer"] = hammers }));
            var runtime = EconomyRuntime.Create(EconomyLoader.Load(text));
            for (var period = 0; period < 3; period++) runtime.Advance(runtime.Step + 1, new Dictionary<string, ScenarioEngine.Contracts.FactValue>());
            return runtimeOut = runtime;
        }
        long Stock(EconomyRuntime runtime, string good) => runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex(good))].Stock;
        Play(0, out var without);
        Assert.Equal((100L, 0L, LineLimit.Held), (Stock(without, "t.ore"), Stock(without, "t.steel"), without.Markets[0].Lines[0].Limit));
        Play(1, out var with);
        Assert.Equal((40L, 20L, 1L), (Stock(with, "t.ore"), Stock(with, "t.steel"), Stock(with, "t.hammer")));   // three periods of ten runs use 60 ore; two batches (20 steel) are in; the hammer is still there
    }

    [Fact]
    public void AToolWearsByItsUsesInTheRunningEconomyAndTheLineStopsWhenItIsGone()
    {
        var pack = Set(Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Priced("t.hammer", 30, "tool", "natural", "{each}"),
            Rec("t.forge", kind: "smelt", inputs: Line("t.ore", "2"), tools: """{ "resource": "t.hammer", "uses": 25 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }"""));
        var conversion = CatalogToEconomy.Convert(pack, OneHourPeriods);
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(conversion.Compose(World(conversion.Goods, [("m", "t.forge", 10)], new Dictionary<string, long> { ["t.ore"] = 100, ["t.hammer"] = 1 }))));
        long Stock(string good) => runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex(good))].Stock;
        var facts = new Dictionary<string, ScenarioEngine.Contracts.FactValue>();
        for (var period = 0; period < 3; period++) runtime.Advance(runtime.Step + 1, facts);
        // 10 + 10 runs, then the pick can do only 5 more of its 25 uses: 25 runs started, 50 ore used, the hammer worn out.
        Assert.Equal((50L, 0L), (Stock("t.ore"), Stock("t.hammer")));
        Assert.Equal((5L, LineLimit.Worn), (runtime.Markets[0].Lines[0].LastRuns, runtime.Markets[0].Lines[0].Limit));
        runtime.Advance(runtime.Step + 1, facts);
        Assert.Equal((0L, LineLimit.Held), (runtime.Markets[0].Lines[0].LastRuns, runtime.Markets[0].Lines[0].Limit));
    }

    [Fact]
    public void AToolsWearScalesWithTheBatchAndTheLongestLifeTheCatalogAllowsConverts()
    {
        var resources = Priced("t.a", 4) + "," + Priced("t.b", 5, "intermediate", "crafted") + "," + Priced("t.pick", 9, "tool", "natural", "{each}") + "," + Priced("t.tough", 9, "tool", "natural", "{each}");
        var recipes = string.Join(",",
            Rec("t.half", inputs: Line("t.a", "0.5"), tools: """{ "resource": "t.pick", "uses": 50 }""", outputs: Line("t.b", "1")),
            Rec("t.long", inputs: Line("t.a", "1"), tools: """{ "resource": "t.tough", "uses": 2000000000 }""", outputs: Line("t.b", "1")));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes), OneHourPeriods);
        var json = JsonNode.Parse(conversion.RecipesJson)!.AsArray();
        Note(conversion, "t.half", EconomyConversionCodes.BatchScaled);
        var half = json.Single(r => r!["id"]!.GetValue<string>() == "t.half")!["requires"]![0]!["wear"]!;
        Assert.Equal((2L, 50L), (half["units"]!.GetValue<long>(), half["runs"]!.GetValue<long>()));      // a run is two catalog runs: 2 items' worth in the 50 uses of each
        var tough = json.Single(r => r!["id"]!.GetValue<string>() == "t.long")!["requires"]![0]!["wear"]!;
        Assert.Equal((1L, 2_000_000_000L), (tough["units"]!.GetValue<long>(), tough["runs"]!.GetValue<long>()));
        EconomyLoader.Load(conversion.Compose(World(conversion.Goods, [("m", "t.half", 5), ("m", "t.long", 5)])));   // and the document loads
    }

    [Fact]
    public void ACatalystThatIsNotAPricedGoodOrIsTheRecipesOwnGoodIsReducedWithANoteAndTheRecipeStillConverts()
    {
        var resources = Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Res("t.deed", cls: "unique", unit: "{each}", stack: "1", properties: "\"base-price\": 9") + "," + Priced("t.salt", 2, "raw", "natural", "kg") + "," + Priced("t.hammer", 30, "tool", "natural", "{each}");
        var recipes = string.Join(",",
            Rec("t.a", inputs: Line("t.ore", "2"), catalysts: Line("t.deed", "1"), outputs: Line("t.steel", "1")),
            Rec("t.b", kind: "trade", inputs: Line("t.ore", "2"), tools: """{ "resource": "t.hammer", "uses": 10 }""", outputs: Line("t.steel", "1") + "," + Line("t.hammer", "1")),
            Rec("t.c", inputs: Line("t.ore", "2"), catalysts: Line("t.salt", "0.5"), outputs: Line("t.steel", "1")));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes), OneHourPeriods);
        Assert.Equal(["t.a", "t.b", "t.c"], conversion.Recipes);
        Assert.Contains("not a tradable good", Note(conversion, "t.a", EconomyConversionCodes.RequirementNotAGood).Detail, StringComparison.Ordinal);
        Note(conversion, "t.b", EconomyConversionCodes.RequirementSelf);   // a hammer the recipe also makes: its requirement is left out
        Assert.Contains("rounded up to 1", Note(conversion, "t.c", EconomyConversionCodes.RequirementRounded).Detail, StringComparison.Ordinal);
        var json = JsonNode.Parse(conversion.RecipesJson)!.AsArray();
        Assert.Empty(json[0]!["requires"]!.AsArray());
        Assert.Empty(json[1]!["requires"]!.AsArray());
        Assert.Equal(("t.salt", 1L), (json[2]!["requires"]![0]!["good"]!.GetValue<string>(), json[2]!["requires"]![0]!["amount"]!.GetValue<long>()));
        Assert.Empty(conversion.Report.Refusals);
    }

    [Fact]
    public void AToolThatTheChainItselfMakesClosesACycleSoTheRequirementIsDroppedAndBothRecipesAreKept()
    {
        var resources = Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Priced("t.hammer", 30, "tool", "natural", "{each}");
        var recipes = string.Join(",",
            Rec("t.forge", kind: "smelt", inputs: Line("t.ore", "2"), tools: """{ "resource": "t.hammer", "uses": 100 }""", outputs: Line("t.steel", "1")),
            Rec("t.hammer-make", kind: "trade", inputs: Line("t.steel", "2"), outputs: Line("t.hammer", "1")));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipes), OneHourPeriods);
        Assert.Equal(["t.forge", "t.hammer-make"], conversion.Recipes);   // neither is refused
        Assert.Empty(conversion.Report.Refusals);
        Assert.Contains("close a cycle", Note(conversion, "t.forge", EconomyConversionCodes.RequirementCycle).Detail, StringComparison.Ordinal);
        Assert.Empty(JsonNode.Parse(conversion.RecipesJson)!.AsArray()[0]!["requires"]!.AsArray());
        EconomyLoader.Load(conversion.Compose(World(conversion.Goods, [("m", "t.forge", 5), ("m", "t.hammer-make", 5)])));   // and the document loads
    }

    [Fact]
    public void MoreThanEightCatalystsAndToolsTogetherRefuseTheRecipeWithTheTooManyGoodsCode()
    {
        var resources = string.Join(",", Enumerable.Range(1, 9).Select(i => Priced("t.c" + i, 4))) + "," + Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted");
        var recipe = Rec("t.many", inputs: Line("t.ore", "1"), catalysts: string.Join(",", Enumerable.Range(1, 9).Select(i => Line("t.c" + i, "1"))), outputs: Line("t.steel", "1"));
        var conversion = CatalogToEconomy.Convert(Set(resources, recipe), OneHourPeriods);
        Assert.Equal(EconomyConversionCodes.TooManyGoods, Assert.Single(conversion.Report.Refusals).Code);
    }

    [Fact]
    public void AnEraGatedRecipeIsConvertedWithOneNoteThatTheGameUnlocksItWhenItsEraComes()
    {
        var manifest = Manifest("t", world: "fictional");
        var head = """
            { "format": 1, "pack": "t",
              "resources": [ { "id": "t.a", "name": "a", "class": "raw", "unit": "kg", "origin": "natural", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": { "base-price": 4 } },
                             { "id": "t.b", "name": "b", "class": "intermediate", "unit": "kg", "origin": "crafted", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": { "base-price": 5 } } ],
              "eras": [ { "id": "t.old", "name": "Old", "order": 1, "tier": 1, "label": "stone-age", "span": null } ],
              "availability": [ { "id": "t.make", "from": "t.old", "until": null } ],
            """;
        var document = head + "\"recipes\": [ " + Rec("t.make", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")) + " ] }";
        var set = Load(new MemoryCatalogSource(new Dictionary<string, string> { ["t/pack.json"] = manifest, ["t/doc.json"] = document })).Required();
        var conversion = CatalogToEconomy.Convert(set, OneHourPeriods);
        Assert.Equal(["t.make"], conversion.Recipes);
        Assert.Single(conversion.Report.Notes, n => n.Code == EconomyConversionCodes.EraNotModelled && n.Subject == "pack");
    }

    // ---- cycles ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACycleIsBrokenAtTheRecipeMostReadilyGivenUpAndTheRestComposeIntoALoadableDocument()
    {
        var resources = string.Join(",", Priced("t.a", 4), Priced("t.b", 5, "intermediate", "crafted"), Priced("t.c", 5, "intermediate", "crafted"));
        // b > c and c > b are two crafts: the one with the higher id goes.
        var two = CatalogToEconomy.Convert(Set(resources, string.Join(",", Rec("t.up", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")), Rec("t.x", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")), Rec("t.y", inputs: Line("t.c", "1"), outputs: Line("t.b", "1")))), OneHourPeriods);
        Assert.Equal(["t.up", "t.x"], two.Recipes);
        Assert.Equal("t.y", Assert.Single(two.Report.Refusals).Subject);
        Assert.Contains("t.x > t.y", Assert.Single(two.Report.Refusals).Detail, StringComparison.Ordinal);

        // The same ring with a disassembly in it: the disassembly goes, whatever its id.
        string Ring(bool reversed)
        {
            var steps = new[] { Rec("t.ab", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")), Rec("t.bc", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")), Rec("t.aa-cb", kind: "disassemble", inputs: Line("t.c", "1"), outputs: Line("t.b", "1")) };
            return string.Join(",", reversed ? steps.Reverse() : steps);
        }
        var broken = CatalogToEconomy.Convert(Set(resources, Ring(false)), OneHourPeriods);
        Assert.Equal(["t.ab", "t.bc"], broken.Recipes);
        Assert.Equal("t.aa-cb", Assert.Single(broken.Report.Refusals).Subject);
        // What is left loads: no cycle, in the order of the chain.
        var set = EconomyLoader.Load(broken.Compose(World(broken.Goods, [("m", "t.ab", 1), ("m", "t.bc", 1)])));
        Assert.Equal(["t.ab", "t.bc"], set.RecipeOrder.Select(i => set.Recipes[i].Id).ToArray());
        // Same pack, same result, whatever order it lists its recipes in.
        Assert.Equal(broken.RecipesJson, CatalogToEconomy.Convert(Set(resources, Ring(true)), OneHourPeriods).RecipesJson);

        // A recipe that consumes and makes the same good is a cycle of one.
        var loop = CatalogToEconomy.Convert(Set(resources, Rec("t.ab", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")) + "," + Rec("t.ab2", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")) + "," + Rec("t.self", inputs: Line("t.b", "2"), outputs: Line("t.b", "1"))), OneHourPeriods);
        Assert.Equal(["t.ab", "t.ab2"], loop.Recipes);
        Assert.Equal(("t.self", EconomyConversionCodes.Cycle), (Assert.Single(loop.Report.Refusals).Subject, Assert.Single(loop.Report.Refusals).Code));
    }

    // ---- who owns a recipe --------------------------------------------------------------------------------------------------------------

    private static CatalogSet TwoRecipes() => Set(Priced("t.a", 4) + "," + Priced("t.b", 5, "intermediate", "crafted") + "," + Priced("t.c", 6, "intermediate", "crafted"),
        Rec("t.one", inputs: Line("t.a", "1"), outputs: Line("t.b", "1")) + "," + Rec("t.two", inputs: Line("t.b", "1"), outputs: Line("t.c", "1")));

    [Fact]
    public void ARecipeConvertedForBothLayersIsADoubleOwnerAndTheCheckSaysWhich()
    {
        var set = TwoRecipes();
        var sim = CatalogToSim.Convert(set);
        var economy = CatalogToEconomy.Convert(set, OneHourPeriods);
        Assert.Contains("t.one", sim.Transformations);
        Assert.Contains("t.one", economy.Recipes);   // with no declaration both layers take it: this is the mistake the check exists for
        var report = RecipeOwnership.CheckConversions(sim.Transformations, economy.Recipes);
        Assert.False(report.IsValid);
        Assert.Equal([OwnershipCodes.DoubleOwner, OwnershipCodes.DoubleOwner], report.Errors.Select(e => e.Code).ToArray());
        Assert.Equal(["t.one", "t.two"], report.Errors.Select(e => e.Location).ToArray());
    }

    [Fact]
    public void ADeclaredOwnerMakesEachLayerTakeItsOwnRecipesAndLeaveTheOthersWithACode()
    {
        var set = TwoRecipes();
        var ownership = RecipeOwnership.Parse("""{ "format": 1, "owners": { "t.one": "sim", "t.two": "economy" } }""");
        Assert.True(ownership.Check(set).IsValid);
        var sim = CatalogToSim.Convert(set, ownership);
        var economy = CatalogToEconomy.Convert(set, OneHourPeriods with { Ownership = ownership });
        Assert.Equal(["t.one"], sim.Transformations);
        Assert.Equal(["t.two"], economy.Recipes);
        Assert.Equal(SimConversionCodes.OwnedByEconomy, sim.Report.Refusals.Single(n => n.Subject == "t.two").Code);
        Assert.Equal(EconomyConversionCodes.OwnedBySim, economy.Report.Refusals.Single(n => n.Subject == "t.one").Code);
        Assert.True(RecipeOwnership.CheckConversions(sim.Transformations, economy.Recipes).IsValid);
        // A recipe the declaration does not mention is refused by both: there is no default owner.
        var partial = RecipeOwnership.Parse("""{ "format": 1, "owners": { "t.one": "sim" } }""");
        Assert.Equal(SimConversionCodes.NoOwner, CatalogToSim.Convert(set, partial).Report.Refusals.Single(n => n.Subject == "t.two").Code);
        Assert.Equal(EconomyConversionCodes.NoOwner, CatalogToEconomy.Convert(set, OneHourPeriods with { Ownership = partial }).Report.Refusals.Single(n => n.Subject == "t.two").Code);
        // A recipe the Economy cannot run and that is declared the Economy's is refused for what it is, not run anywhere.
        Assert.Equal(RecipeLayer.Sim, ownership.LayerOf("t.one"));
        Assert.Null(ownership.LayerOf("t.other"));
    }

    [Fact]
    public void AnOwnershipDeclarationIsStrictAndNamesOnlyRecipesThatExist()
    {
        foreach (var bad in new[] { "{ not json", "[]", """{ "format": 1 }""", """{ "format": 2, "owners": {} }""", """{ "format": "1", "owners": {} }""", """{ "format": 1, "owners": [] }""",
                     """{ "format": 1, "owners": { "t.one": "both" } }""", """{ "format": 1, "owners": { "t.one": 1 } }""", """{ "format": 1, "owners": {}, "extra": 1 }""" })
            Assert.Equal(OwnershipCodes.Format, Assert.Throws<CatalogRefusedException>(() => RecipeOwnership.Parse(bad)).Code);
        var unknown = RecipeOwnership.Parse("""{ "format": 1, "owners": { "t.nothing": "sim", "t.one": "economy" } }""").Check(TwoRecipes());
        Assert.Equal(OwnershipCodes.UnknownRecipe, Assert.Single(unknown.Errors).Code);
        Assert.Equal("t.nothing", Assert.Single(unknown.Errors).Location);
        Assert.Equal(RecipeLayer.Economy, RecipeOwnership.Of([new("t.one", RecipeLayer.Economy)]).LayerOf("t.one"));
    }

    // ---- the shipped packs --------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> Packs() =>
        Directory.GetDirectories(ShippedPackTests.PacksRoot).Where(d => File.Exists(Path.Combine(d, "pack.json"))).Select(d => new object[] { Path.GetFileName(d)! }).OrderBy(p => (string)p[0], StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Packs))]
    public void EveryShippedPackConvertsAccountsForEveryRecipeAndWhatItKeepsLoadsInTheEconomy(string pack)
    {
        var set = CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), pack).Required();
        var options = new EconomyConversionOptions(10_000, 3600m, GoodTuning.Town, []);
        var conversion = CatalogToEconomy.Convert(set, options);
        // Every recipe of the pack is either kept or refused with a code, none is lost.
        var total = set.Recipes.Count();
        Assert.Equal(total, conversion.Recipes.Count + conversion.Report.Refusals.Count());
        Assert.Equal(conversion.Recipes.Count, conversion.Recipes.Distinct().Count());
        // What is kept loads and has no cycle.
        var world = World(conversion.Goods.Count == 0 ? ["t"] : conversion.Goods, conversion.Recipes.Select(r => ("m", r, 5)));
        if (conversion.Goods.Count == 0) return;
        var set2 = EconomyLoader.Load(conversion.Compose(world));
        Assert.Equal(conversion.Recipes.Count, set2.Recipes.Count);
        Assert.Equal(conversion.Recipes.Count, set2.RecipeOrder.Count);
        // The same pack converts the same way twice.
        Assert.Equal(conversion.RecipesJson, CatalogToEconomy.Convert(set, options).RecipesJson);
    }

    // ---- a resource's own durability: a different count from the recipe's uses -----------------------------------------------------------

    [Fact]
    public void AToolsOwnLifeIsNotReadAndTheRecipesUsesStillWearItAndTheNoteSaysBoth()
    {
        // The hammer lasts 300 uses as an item; the recipe says a run wears 1/100 of one. The two counts are not tied, so the recipe's declaration is the wear and the item's life is reported as not read.
        var pack = Set(Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Res("t.hammer", cls: "tool", unit: "{each}", stack: "1", durability: """{ "uses": 300 }""", properties: "\"base-price\": 30") + "," + Res("t.tongs", cls: "tool", unit: "{each}", stack: "1", durability: """{ "uses": 900 }""", properties: "\"base-price\": 20"),
            Rec("t.forge", kind: "smelt", inputs: Line("t.ore", "2"), tools: """{ "resource": "t.hammer", "uses": 100 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }""") + "," +
            Rec("t.forge2", kind: "smelt", inputs: Line("t.ore", "3"), tools: """{ "resource": "t.hammer", "uses": 50 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }""") + "," +
            Rec("t.forge3", kind: "smelt", inputs: Line("t.ore", "4"), tools: """{ "resource": "t.hammer", "uses": 100 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }""") + "," +
            Rec("t.forge4", kind: "smelt", inputs: Line("t.ore", "5"), tools: """{ "resource": "t.tongs", "uses": 7 }""", outputs: Line("t.steel", "1"), duration: """{ "value": 1, "unit": "h" }"""));
        var conversion = CatalogToEconomy.Convert(pack, OneHourPeriods);
        var note = Note(conversion, "t.hammer", EconomyConversionCodes.DurabilityNotMapped);
        Assert.Contains("durability uses 300", note.Detail, StringComparison.Ordinal);
        Assert.Contains("(50, 100);", note.Detail, StringComparison.Ordinal);   // the recipes' uses, which do wear it: each once, in order
        Assert.DoesNotContain("7", note.Detail.Replace("durability uses 300", ""), StringComparison.Ordinal);   // another good's tool uses (tongs: 7) are not this good's wear
        var tongs = Note(conversion, "t.tongs", EconomyConversionCodes.DurabilityNotMapped).Detail;
        Assert.Contains("(7);", tongs, StringComparison.Ordinal);
        Assert.DoesNotContain("50", tongs, StringComparison.Ordinal);
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.hammer" && n.Code == EconomyConversionCodes.ResourceFields);   // durability has a code of its own; stack 1 is nothing to report
        var wear = JsonNode.Parse(conversion.RecipesJson)!.AsArray()[0]!["requires"]![0]!["wear"]!;
        Assert.Equal((1L, 100L), (wear["units"]!.GetValue<long>(), wear["runs"]!.GetValue<long>()));   // 100, not 300
    }

    [Fact]
    public void AGoodWithALifeNoKeptRecipeWearsSaysSoAndOtherFieldsKeepTheirOwnCode()
    {
        var pack = Set(Priced("t.ore", 4) + "," + Priced("t.steel", 14, "intermediate", "crafted") + "," + Res("t.seal", cls: "equipment", unit: "{each}", stack: "1", durability: """{ "uses": 40 }""", properties: "\"base-price\": 9") + "," +
                       Res("t.bag", cls: "product", origin: "crafted", unit: "{each}", stack: "50", properties: "\"base-price\": 3"),
            Rec("t.forge", kind: "smelt", inputs: Line("t.ore", "2"), catalysts: Line("t.seal", "1"), outputs: Line("t.steel", "1") + "," + Line("t.bag", "1")));
        var conversion = CatalogToEconomy.Convert(pack, OneHourPeriods);
        Assert.Contains("No kept recipe holds this good as a worn tool, so it does not wear in the Economy.", Note(conversion, "t.seal", EconomyConversionCodes.DurabilityNotMapped).Detail, StringComparison.Ordinal);   // a catalyst never wears
        Assert.Null(JsonNode.Parse(conversion.RecipesJson)!.AsArray()[0]!["requires"]![0]!["wear"]);
        Note(conversion, "t.bag", EconomyConversionCodes.ResourceFields);   // stack 50 is the other fields' code
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.bag" && n.Code == EconomyConversionCodes.DurabilityNotMapped);
        Assert.DoesNotContain(conversion.Report.Notes, n => n.Subject == "t.seal" && n.Code == EconomyConversionCodes.ResourceFields);
    }

    [Fact]
    public void TheCommandLineConvertsAPackAndWritesTheTwoArraysAndRefusesWhatItWasNotTold()
    {
        string Cli(out int exit, params string[] args)
        {
            var output = new StringWriter();
            exit = CatalogCli.Run(args, output, new StringWriter());
            return output.ToString();
        }
        var text = Cli(out var exit, "economy", ShippedPackTests.PacksRoot, "mining", "--milli-per-price", "10000", "--period-seconds", "3600", "--notes", "--out", _dir.FullName);
        Assert.Equal(0, exit);
        Assert.Contains("economy goods=", text, StringComparison.Ordinal);
        Assert.Contains(EconomyConversionCodes.NoInputs, text, StringComparison.Ordinal);
        Assert.Contains("refused " + EconomyConversionCodes.NoInputs, text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_dir.FullName, "goods.json")));
        Assert.StartsWith("[", File.ReadAllText(Path.Combine(_dir.FullName, "recipes.json")), StringComparison.Ordinal);
        Cli(out exit, "economy", ShippedPackTests.PacksRoot, "mining");
        Assert.Equal(2, exit);   // the price unit and the period are the game's to state
        Cli(out exit, "economy", ShippedPackTests.PacksRoot, "mining", "--milli-per-price", "0", "--period-seconds", "3600");
        Assert.Equal(2, exit);
        var owners = Path.Combine(_dir.FullName, "owners.json");
        File.WriteAllText(owners, """{ "format": 1, "owners": { "mining.nothing": "economy" } }""");
        Assert.Equal(1, (Cli(out exit, "economy", ShippedPackTests.PacksRoot, "mining", "--milli-per-price", "10000", "--period-seconds", "3600", "--owners", owners).Length, exit).exit);
    }
}
