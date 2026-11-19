using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;
using static EconomyEngine.Tests.Chain;

namespace EconomyEngine.Tests;

/// <summary>
/// Pass 51: the profit margin of a throttled line (inputs plus the line's own wage and overhead), and tools that wear out. Every expected number is worked by hand beside it; the wear arithmetic is also checked
/// against a run-by-run oracle that shares no code with the engine.
/// </summary>
public sealed class WearAndProfitTests
{
    private static readonly string[] Goods = ["ore", "coal", "steel", "tools", "hammer"];

    private static RecipeSpec Smelt => new("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)]);

    private static void SetPrice(EconomyRuntime runtime, string good, long price)
    {
        var m = runtime.MarketIndex("m");
        runtime.Markets[m].Pairs[runtime.PairIndex(m, runtime.GoodIndex(good))].Price = price;
    }

    // ---- the profit margin ---------------------------------------------------------------------------------------------------------------

    /// <summary>One smelter (2 ore + 1 coal make 1 steel; every input costs 10,000 so a run's inputs cost 30,000), steel priced at 36,000, a throttle of 900 to 1,200 with the given costs.</summary>
    private static EconomyRuntime Smelter((long Wage, long Overhead, bool Indexed)? costs, Action<JsonObject>? edit = null)
    {
        var runtime = Runtime(Goods, [Smelt], [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000 }, 1_000_000, [new LineSpec("smelt", 40, 900, 1200, costs)])], edit);
        SetPrice(runtime, "steel", 36_000);
        return runtime;
    }

    [Fact]
    public void WithoutCostsTheMarginIsTheOldOneOutputValueOverInputCost()
    {
        var margin = Smelter(null).MarginOfLine(0, 0);
        Assert.Equal((30_000L, 36_000L, 1200L, 1000L, 0L, 0L), (margin.InputCost, margin.OutputValue, margin.Ratio, margin.Fraction, margin.Wage, margin.Overhead));   // 36,000 x 1000 / 30,000 = 1,200: full speed
        Assert.Equal((40L, LineLimit.Capacity), (Smelter(null).PlanLines(0).Single().Runs, Smelter(null).PlanLines(0).Single().Limit));
    }

    [Fact]
    public void WageAndOverheadJoinTheCostOfARunAndLowerTheMarginAndTheRunsFollow()
    {
        // cost = 30,000 + 3,000 + 3,000 = 36,000; ratio = 36,000 x 1000 / 36,000 = 1,000; fraction = (1,000 - 900) x 1000 / 300 = 333; runs = 40 x 333 / 1000 = 13 (truncated).
        var runtime = Smelter((3000, 3000, false));
        var margin = runtime.MarginOfLine(0, 0);
        Assert.Equal((30_000L, 36_000L, 1000L, 333L, 3000L, 3000L), (margin.InputCost, margin.OutputValue, margin.Ratio, margin.Fraction, margin.Wage, margin.Overhead));
        var plan = runtime.PlanLines(0).Single();
        Assert.Equal((13L, 13L, LineLimit.Margin), (plan.Wanted, plan.Runs, plan.Limit));
        // Wage alone: 33,000 -> 1,090; overhead alone: the same; the two are separate terms of the same cost.
        Assert.Equal(1090L, Smelter((3000, 0, false)).MarginOfLine(0, 0).Ratio);        // 36,000 x 1000 / 33,000 = 1,090.9 truncated
        Assert.Equal(1090L, Smelter((0, 3000, false)).MarginOfLine(0, 0).Ratio);
        Assert.Equal(1200L, Smelter((0, 0, false)).MarginOfLine(0, 0).Ratio);           // a declared cost of nothing is the old margin
    }

    [Fact]
    public void IndexedCostsFollowThePriceLevelAndNominalOnesDoNot()
    {
        var indexed = Smelter((3000, 3000, true));
        indexed.Currencies[0].Level = 2_000_000;      // prices are as they were set; the declared costs double: 6,000 + 6,000
        var margin = indexed.MarginOfLine(0, 0);
        Assert.Equal((6000L, 6000L, 857L), (margin.Wage, margin.Overhead, margin.Ratio));          // 36,000 x 1000 / 42,000 = 857.1
        var halves = Smelter((3, 1, true));
        halves.Currencies[0].Level = 1_500_000;                       // 4.5 and 1.5 round to the nearest, away from zero: 5 and 2, not the 4 and 1 a truncation gives
        Assert.Equal((5L, 2L), (halves.MarginOfLine(0, 0).Wage, halves.MarginOfLine(0, 0).Overhead));
        var nominal = Smelter((3000, 3000, false));
        nominal.Currencies[0].Level = 2_000_000;
        Assert.Equal((3000L, 3000L, 1000L), (nominal.MarginOfLine(0, 0).Wage, nominal.MarginOfLine(0, 0).Overhead, nominal.MarginOfLine(0, 0).Ratio));
    }

    [Fact]
    public void InsideAPeriodIndexedCostsUseTheLevelThePricesAreStillIn()
    {
        // Currency grows 10 percent a period: the level is 1,100,000 when production runs, but prices are rescaled to it only later in the period, so the cost must still be 3,000 + 3,000.
        // cost 36,000, ratio 1,000, fraction 333, 13 runs. Using the new level would give wage and overhead 3,300 each, ratio 983, fraction 277, 11 runs.
        var runtime = Smelter((3000, 3000, true), root => root["currencies"]!.AsArray()[0]!["growthPpm"] = 100_000);
        Period(runtime);
        Assert.Equal(13L, Line(runtime, "m", "smelt").LastRuns);
    }

    [Fact]
    public void TheExplanationShowsEveryTermOfTheProfitMarginAndWhyTaxAndSubsidyAreNotOne()
    {
        var text = EconomyExplain.Production(Smelter((3000, 1500, true)), "m", "smelt").Text();
        Assert.Contains("margin smelt: 1043 (per mille: one run's outputs are worth 36000 against a cost of 34500 (inputs 30000 + wage 3000 + overhead 1500)", text, StringComparison.Ordinal);
        Assert.Contains("wage smelt: 3000 (a run's labour, declared 3000, following the price level of c)", text, StringComparison.Ordinal);
        Assert.Contains("overhead smelt: 1500 (a run's other variable cost, declared 1500, following the price level; costs the line pays whether it runs or not are not counted)", text, StringComparison.Ordinal);
        Assert.Contains("sales-tax steel: 0 (per mille buyers pay on top of the price", text, StringComparison.Ordinal);
        var plain = EconomyExplain.Production(Smelter(null), "m", "smelt").Text();
        Assert.Contains("worth 36000 against inputs that cost 30000 (", plain, StringComparison.Ordinal);       // the old text, unchanged
        Assert.DoesNotContain("wage smelt", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void ASalesTaxOnTheOutputReachesTheLineThroughTheDemandItAnswersNotAsACost()
    {
        var taxed = Smelter((3000, 0, false), root =>
        {
            root["policies"] = new JsonArray(new JsonObject { ["id"] = "vat", ["kind"] = "sales", ["rate"] = 100, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "pay" });
            Edit.Obj(root, "markets", 0)["policies"] = new JsonArray { "vat" };
        });
        var free = Smelter((3000, 0, false));
        Assert.Equal(free.MarginOfLine(0, 0).Ratio, taxed.MarginOfLine(0, 0).Ratio);         // the margin's terms are the same with and without a tax
        Assert.Contains("sales-tax steel: 100", EconomyExplain.Production(taxed, "m", "smelt").Text(), StringComparison.Ordinal);   // and the rate is shown
    }

    [Fact]
    public void EveryWrongCostsBlockIsRefusedByCode()
    {
        JsonObject Throttle(JsonObject r) => Edit.Obj(r, "markets", 0)["lines"]!.AsArray()[0]!["throttle"]!.AsObject();
        string Load((long, long, bool)? costs, Action<JsonObject> edit) => Fixture.Refusal(() => EconomyLoader.Load(Root(Goods, [Smelt], [new MarketSpec("m", new(), 1_000_000, [new LineSpec("smelt", 5, 900, 1200, costs)])], edit).ToJsonString()));
        var set = EconomyLoader.Load(Root(Goods, [Smelt], [new MarketSpec("m", new(), 1_000_000, [new LineSpec("smelt", 5, 900, 1200, (7, 9, true))])]).ToJsonString());
        Assert.Equal(new ThrottleDef(900, 1200, new LineCostsDef(7, 9, true)), set.Markets[0].Lines.Single().Throttle);
        Assert.Equal(EconomyCodes.Range, Load((7, 9, true), r => Throttle(r)["costs"]!["wage"] = -1));
        Assert.Equal(EconomyCodes.Range, Load((7, 9, true), r => Throttle(r)["costs"]!["overhead"] = EconomyFacts.MaxPrice + 1));
        Assert.Equal(EconomyCodes.MissingField, Load((7, 9, true), r => Throttle(r)["costs"]!.AsObject().Remove("indexed")));
        Assert.Equal(EconomyCodes.MissingField, Load((7, 9, true), r => Throttle(r)["costs"]!.AsObject().Remove("wage")));
        Assert.Equal(EconomyCodes.UnknownField, Load((7, 9, true), r => Throttle(r)["costs"]!["tax"] = 1));
        Assert.Equal(EconomyCodes.Format, Load((7, 9, true), r => Throttle(r)["costs"]!["indexed"] = 1));            // a flag is true or false
        Assert.Equal(EconomyCodes.UnknownField, Load(null, r => Throttle(r)["costs"] = JsonNode.Parse("{\"wage\":1,\"overhead\":1,\"indexed\":false,\"x\":1}")));
        EconomyLoader.Load(Root(Goods, [Smelt], [new MarketSpec("m", new(), 1_000_000, [new LineSpec("smelt", 5, 900, 1200, (EconomyFacts.MaxPrice, EconomyFacts.MaxPrice, true))])]).ToJsonString());   // the widest legal
    }

    // ---- tool wear -----------------------------------------------------------------------------------------------------------------------

    /// <summary>The smelter with a hammer it requires, which wears <paramref name="units"/> units in <paramref name="runs"/> runs.</summary>
    private static EconomyRuntime Worn(long hammers, long units, long runs, long capacity = 40, long needed = 1, Action<JsonObject>? edit = null) =>
        Runtime(Goods, [Smelt with { Requires = [("hammer", needed)], Wear = new() { ["hammer"] = (units, runs) } }],
            [new MarketSpec("m", new() { ["ore"] = 100_000, ["coal"] = 100_000, ["hammer"] = hammers }, 1_000_000, [new LineSpec("smelt", capacity)])], edit);

    [Fact]
    public void AToolOfOneInTenRunsLetsALineRunTenTimesThenItIsWornOutAndTheLineIsHeld()
    {
        var runtime = Worn(1, 1, 10);
        var plan = runtime.PlanLines(0).Single();
        Assert.Equal((40L, 10L, LineLimit.Worn, runtime.GoodIndex("hammer")), (plan.Wanted, plan.Runs, plan.Limit, plan.Good));
        var changes = Period(runtime);
        Assert.Equal((10L, LineLimit.Worn), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        Assert.Equal((100_000L - 20, 100_000L - 10, 10L, 0L), (Stock(runtime, "m", "ore"), Stock(runtime, "m", "coal"), Stock(runtime, "m", "steel"), Stock(runtime, "m", "hammer")));
        Assert.Equal(0L, Line(runtime, "m", "smelt").Wear[0]);            // ten runs done towards the next loss, and the loss fell due
        Assert.Contains(changes.Limits, c => c.To == LineLimit.Worn && c.Good == runtime.GoodIndex("hammer"));
        Period(runtime);                                                  // the stock is under the one the recipe requires: held, as for any missing tool
        Assert.Equal((0L, LineLimit.Held), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        Assert.Equal(runtime.GoodIndex("hammer"), Line(runtime, "m", "smelt").LimitGood);
    }

    [Fact]
    public void SpareToolsLastLongerAndTheLineRunsUntilTheLastRequiredOneIsGone()
    {
        // 3 hammers, 1 required, 1 lost in 10 runs: 2 losses leave 1 (enough), the third takes it under: 30 runs.
        var runtime = Worn(3, 1, 10);
        Assert.Equal(30L, runtime.PlanLines(0).Single().Runs);
        Period(runtime);
        Assert.Equal((30L, 0L), (Line(runtime, "m", "smelt").LastRuns, Stock(runtime, "m", "hammer")));
        // Two required: with 4 held and 1 lost in 10 runs, 2 losses leave 2 (enough), the third leaves 1 (under): 30 runs.
        Assert.Equal(30L, Worn(4, 1, 10, needed: 2).PlanLines(0).Single().Runs);
    }

    [Fact]
    public void RunsDoneTowardsALossAreKeptFromPeriodToPeriod()
    {
        // 4 runs a period, 1 hammer lost in 10 runs, 2 held (1 required): progress 4, 8, then 12 -> a loss and 2; the stock 2, 2, 1.
        var runtime = Worn(2, 1, 10, capacity: 4);
        var seen = new List<(long Progress, long Stock, long Runs)>();
        for (var i = 0; i < 3; i++)
        {
            Period(runtime);
            seen.Add((Line(runtime, "m", "smelt").Wear[0], Stock(runtime, "m", "hammer"), Line(runtime, "m", "smelt").LastRuns));
        }
        Assert.Equal([(4L, 2L, 4L), (8L, 2L, 4L), (2L, 1L, 4L)], seen);
        // Period 4: progress 6. Period 5: the stock lasts exactly 4 more runs (10 - 6), as many as the capacity: not a worn limit; it runs, loses the last hammer.
        Period(runtime);
        Assert.Equal(6L, Line(runtime, "m", "smelt").Wear[0]);
        Assert.Equal(4L, runtime.PlanLines(0).Single().Runs);
        Assert.Equal(LineLimit.Capacity, runtime.PlanLines(0).Single().Limit);
        Period(runtime);
        Assert.Equal((0L, 0L, LineLimit.Capacity), (Line(runtime, "m", "smelt").Wear[0], Stock(runtime, "m", "hammer"), Line(runtime, "m", "smelt").Limit));
        Period(runtime);
        Assert.Equal(LineLimit.Held, Line(runtime, "m", "smelt").Limit);
    }

    [Fact]
    public void ALossLargerThanTheStockFloorsTheStockAtNothing()
    {
        // 3 lost for every run, 2 held, 1 required: the first run is allowed (the tool is whole when it starts), then 3 > 2 are lost: the stock is 0, not -1.
        var runtime = Worn(2, 3, 1);
        var plan = runtime.PlanLines(0).Single();
        Assert.Equal((1L, LineLimit.Worn), (plan.Runs, plan.Limit));
        Period(runtime);
        Assert.Equal(0L, Stock(runtime, "m", "hammer"));
        Assert.Equal(0L, runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex("hammer"))].Met);       // nothing went negative on the way to the floor
        EconomySave.Open(runtime.Set, EconomySave.Write(runtime));                                                // so the state is one a save accepts
        // A later line of the same period that uses the tool up as an input sees the floored stock (0), not a negative one: it runs no times and is short of the hammer.
        var shared = Runtime(Goods,
            [new RecipeSpec("a", [("ore", 1)], [("steel", 1)], 0, false, [("hammer", 1)], new() { ["hammer"] = (3, 1) }), new RecipeSpec("b", [("hammer", 1)], [("tools", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["hammer"] = 2 }, 1_000_000, [new LineSpec("a", 5), new LineSpec("b", 5)])]);
        var plans = shared.PlanLines(0);
        Assert.Equal((1L, 0L, LineLimit.Input), (plans[0].Runs, plans[1].Runs, plans[1].Limit));
        Period(shared);
        Assert.Equal((0L, LineLimit.Input, 0L), (Line(shared, "m", "b").LastRuns, Line(shared, "m", "b").Limit, Stock(shared, "m", "hammer")));
    }

    [Fact]
    public void ToolsOfADifferentGoodWearIndependentlyAndALineWithoutWearNeverLosesAny()
    {
        var runtime = Runtime(["ore", "coal", "steel", "tools", "hammer", "anvil"],
            [new RecipeSpec("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], 0, false, [("anvil", 1), ("hammer", 1)], new() { ["hammer"] = (1, 10) })],
            [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000, ["hammer"] = 5, ["anvil"] = 1 }, 1_000_000, [new LineSpec("smelt", 5)])]);
        Period(runtime);
        var state = Line(runtime, "m", "smelt");
        Assert.Equal((1L, 5L, 0L), (Stock(runtime, "m", "anvil"), Stock(runtime, "m", "hammer"), state.Wear[0]));      // requirements are in the order of the recipe's goods as written: anvil, hammer
        Assert.Equal(5L, state.Wear[1]);
        Period(runtime);
        Assert.Equal((1L, 4L, 0L), (Stock(runtime, "m", "anvil"), Stock(runtime, "m", "hammer"), Line(runtime, "m", "smelt").Wear[0]));
    }

    [Fact]
    public void TwoLinesSharingATool_TheLaterOneSeesTheWearOfTheEarlierAndThePlanAndThePeriodAgree()
    {
        // Two hammers, one for each line (pass 57: a line that runs holds the amount it requires, so one hammer would serve only the first line; see SharedToolTests).
        var runtime = Runtime(Goods,
            [new RecipeSpec("a", [("ore", 1)], [("steel", 1)], 0, false, [("hammer", 1)], new() { ["hammer"] = (1, 10) }), new RecipeSpec("b", [("coal", 1)], [("tools", 1)], 0, false, [("hammer", 1)], new() { ["hammer"] = (1, 10) })],
            [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["hammer"] = 2 }, 1_000_000, [new LineSpec("a", 5), new LineSpec("b", 5)])]);
        Period(runtime);                                         // 5 runs each: 5 of 10 done towards a loss on both lines, both hammers are whole
        Assert.Equal((5L, 5L, 2L), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Stock(runtime, "m", "hammer")));
        var plan = runtime.PlanLines(0);
        Assert.Equal((5L, LineLimit.Capacity, 5L, LineLimit.Capacity), (plan[0].Runs, plan[0].Limit, plan[1].Runs, plan[1].Limit));       // a's tenth run wears its hammer out; b still has its own
        Period(runtime);
        Assert.Equal((5L, 5L, 0L), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Stock(runtime, "m", "hammer")));   // both reached ten runs in the period: both hammers are gone
        Period(runtime);
        Assert.Equal((0L, 0L, LineLimit.Held, LineLimit.Held), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Line(runtime, "m", "a").Limit, Line(runtime, "m", "b").Limit));
    }

    [Fact]
    public void WearCountsAsDemandForTheToolSoItsPriceRisesAgainstATwinWithoutWear()
    {
        var wearing = Worn(2, 1, 10, capacity: 10);
        var control = Runtime(Goods, [Smelt with { Requires = [("hammer", 1)] }], [new MarketSpec("m", new() { ["ore"] = 100_000, ["coal"] = 100_000, ["hammer"] = 2 }, 1_000_000, [new LineSpec("smelt", 10)])]);
        Period(wearing);
        Period(control);
        long Price(EconomyRuntime r) => r.Markets[0].Pairs[r.PairIndex(0, r.GoodIndex("hammer"))].Price;
        Assert.Equal(10_000L, Price(control));
        Assert.True(Price(wearing) > 10_000, "the worn-out hammer should be dearer; it is " + Price(wearing));
    }

    [Fact]
    public void WearCannotBeDeclaredOnAnInputAndEveryWrongWearIsRefusedByCode()
    {
        JsonObject Need(JsonObject r) => r["recipes"]!.AsArray()[0]!["requires"]!.AsArray()[0]!.AsObject();
        string Load(Action<JsonObject> edit) => Fixture.Refusal(() => EconomyLoader.Load(Root(Goods, [Smelt with { Requires = [("hammer", 1)], Wear = new() { ["hammer"] = (1, 10) } }], [new MarketSpec("m", new())], edit).ToJsonString()));
        var set = EconomyLoader.Load(Root(Goods, [Smelt with { Requires = [("hammer", 1)], Wear = new() { ["hammer"] = (3, 200) } }], [new MarketSpec("m", new())]).ToJsonString());
        Assert.Equal(new WearDef(3, 200), set.Recipes[0].Requires.Single().Wear);
        Assert.Null(EconomyLoader.Load(Root(Goods, [Smelt with { Requires = [("hammer", 1)] }], [new MarketSpec("m", new())]).ToJsonString()).Recipes[0].Requires.Single().Wear);
        Assert.Equal(EconomyCodes.Range, Load(r => Need(r)["wear"]!["units"] = 0));
        Assert.Equal(EconomyCodes.Range, Load(r => Need(r)["wear"]!["units"] = 1_000_001));
        Assert.Equal(EconomyCodes.Range, Load(r => Need(r)["wear"]!["runs"] = 0));
        Assert.Equal(EconomyCodes.Range, Load(r => Need(r)["wear"]!["runs"] = 2_000_000_001));
        Assert.Equal(EconomyCodes.MissingField, Load(r => Need(r)["wear"]!.AsObject().Remove("runs")));
        Assert.Equal(EconomyCodes.MissingField, Load(r => Need(r)["wear"]!.AsObject().Remove("units")));
        Assert.Equal(EconomyCodes.UnknownField, Load(r => Need(r)["wear"]!["chance"] = 5));
        Assert.Equal(EconomyCodes.UnknownField, Load(r => r["recipes"]!.AsArray()[0]!["inputs"]!.AsArray()[0]!["wear"] = new JsonObject { ["units"] = 1, ["runs"] = 1 }));      // an input is used up, it does not wear
        EconomyLoader.Load(Root(Goods, [Smelt with { Requires = [("hammer", 1)], Wear = new() { ["hammer"] = (1_000_000, 2_000_000_000) } }], [new MarketSpec("m", new())]).ToJsonString());   // the widest legal
    }

    [Fact]
    public void TheArithmeticOfWearMatchesARunByRunOracleOverManyToolsAndProgresses()
    {
        var random = new Random(51);
        for (var trial = 0; trial < 600; trial++)
        {
            var units = random.Next(1, 6);
            var every = random.Next(1, 13);
            var needed = random.Next(1, 4);
            var stock = needed + random.Next(0, 12);
            var progress = random.Next(0, every);
            var need = new RecipeRequirement(0, needed, new WearDef(units, every));
            // The oracle starts one run at a time: a run starts only if the tool is whole; after the run the count towards a loss grows, and on reaching `every` it is lost.
            long runs = 0, s = stock, p = progress;
            while (s >= needed && runs < 10_000)
            {
                runs++;
                p++;
                if (p == every) { p = 0; s = Math.Max(0, s - units); }
            }
            Assert.Equal(runs, EconomyRuntime.RunsBeforeWornOut(need, stock, progress));
            Assert.Equal((stock - s, p), (stock - Math.Max(0, stock - EconomyRuntime.Lost(need.Wear!, progress, runs)), EconomyRuntime.Progress(need.Wear!, progress, runs)));
            // Wearing in two steps loses what one step loses.
            var first = random.Next(0, 40);
            var second = random.Next(0, 40);
            Assert.Equal(EconomyRuntime.Lost(need.Wear!, progress, first + second),
                EconomyRuntime.Lost(need.Wear!, progress, first) + EconomyRuntime.Lost(need.Wear!, EconomyRuntime.Progress(need.Wear!, progress, first), second));
        }
    }

    [Fact]
    public void TheExplanationShowsTheWearTheRunsLeftAndWhyALineWasStopped()
    {
        var text = EconomyExplain.Production(Worn(3, 1, 10), "m", "smelt").Text();
        Assert.Contains("wear hammer: 0 (runs done towards the next loss: 1 lost for every 10 runs; the stock lasts 30 more runs before it falls under 1)", text, StringComparison.Ordinal);
        Assert.Contains("runs smelt: 30 (of 40 wanted: the tool hammer lasts no more runs before it wears out)", text, StringComparison.Ordinal);
        var gone = Worn(0, 1, 10);
        Assert.Contains("the tool is worn out until the market holds 1 again", EconomyExplain.Production(gone, "m", "smelt").Text(), StringComparison.Ordinal);
        Assert.Contains("missing hammer", EconomyExplain.Production(gone, "m", "smelt").Text(), StringComparison.Ordinal);
    }

    // ---- saves ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRunsTowardsALossAreSavedAndCoveredByTheDigestAndAnOutOfRangeOneIsRefusedAsState()
    {
        var runtime = Worn(2, 1, 10, capacity: 4);
        Period(runtime);
        Period(runtime);
        var save = EconomySave.Write(runtime);
        var reopened = EconomySave.Open(runtime.Set, save);
        Assert.Equal(8L, Line(reopened, "m", "smelt").Wear[0]);
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Period(runtime);
        Period(reopened);                                         // the reopened economy goes on exactly as the one that never stopped
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(2L, Line(reopened, "m", "smelt").Wear[0]);
        Assert.Equal(EconomyCodes.SaveDigest, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Stale(save, i => i.Markets[0].Lines[0].Wear[0] += 1))));       // a count the digest does not cover would pass
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Wear[0] = 10))));       // under the wear's runs
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Wear[0] = -1))));
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Wear.Add(0)))));       // one count per required good
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Wear.Clear()))));
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => { i.Markets[0].Lines[0].Limit = (int)LineLimit.Worn; i.Markets[0].Lines[0].LimitGood = 0; }))));   // names a good that is not the tool
    }

    [Fact]
    public void AWornLimitIsSavedAndReopenedAndMayOnlyNameAToolThatWearsOut()
    {
        var runtime = Runtime(["ore", "coal", "steel", "tools", "hammer", "anvil"],
            [new RecipeSpec("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], 0, false, [("anvil", 1), ("hammer", 1)], new() { ["hammer"] = (1, 10) })],
            [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000, ["hammer"] = 1, ["anvil"] = 1 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        Period(runtime);
        Assert.Equal((LineLimit.Worn, 10L), (Line(runtime, "m", "smelt").Limit, Line(runtime, "m", "smelt").LastRuns));
        var save = EconomySave.Write(runtime);
        var reopened = EconomySave.Open(runtime.Set, save);                                  // the new limit is inside the range a save accepts
        Assert.Equal((LineLimit.Worn, runtime.GoodIndex("hammer")), (Line(reopened, "m", "smelt").Limit, Line(reopened, "m", "smelt").LimitGood));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].LimitGood = runtime.GoodIndex("anvil")))));   // the anvil never wears out
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(runtime.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].LimitGood = runtime.GoodIndex("ore")))));
    }

    [Fact]
    public void AWornLimitIsRefusedWhereTheRecipeHasNoToolThatWearsAndAWearCountWhereNoneCanBe()
    {
        var plain = Runtime(Goods, [Smelt with { Requires = [("hammer", 1)] }], [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["hammer"] = 1 }, 1_000_000, [new LineSpec("smelt", 5)])]);
        Period(plain);
        var save = EconomySave.Write(plain);
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(plain.Set, Tamper.Forge(save, i => { i.Markets[0].Lines[0].Limit = (int)LineLimit.Worn; i.Markets[0].Lines[0].LimitGood = plain.GoodIndex("hammer"); }))));
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(plain.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Wear[0] = 1))));      // a requirement that does not wear has no count
        Assert.Equal(EconomyCodes.SaveState, Fixture.Refusal(() => EconomySave.Open(plain.Set, Tamper.Forge(save, i => i.Markets[0].Lines[0].Limit = 7))));         // a limit the engine does not have
        Assert.Contains("\"Wear\":[0]", System.Text.Encoding.UTF8.GetString(save), StringComparison.Ordinal);
    }

    // ---- the formats ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADocumentOfFormat4IsRefusedByItsOwnCodeAndSaysWhatFormat5Adds()
    {
        var refusal = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Mini.Text(root => root["format"] = 4)));
        Assert.Equal(EconomyCodes.Format4, refusal.Code);
        Assert.NotEqual(EconomyCodes.Format, EconomyCodes.Format4);
        Assert.NotEqual(EconomyCodes.Format3, EconomyCodes.Format4);
        Assert.Contains("format 4 is no longer read", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'wear'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'costs'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(EconomyCodes.Format3, Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Mini.Text(root => root["format"] = 3))).Code);       // the older refusals keep their own codes
    }

    [Fact]
    public void ASaveOfFormat4IsRefusedByItsOwnCodeWhateverItsShape()
    {
        var node = JsonNode.Parse(EconomySave.Write(Worn(1, 1, 10)))!.AsObject();
        node["Format"] = 4;
        var refusal = Assert.Throws<EngineRefusedException>(() => EconomySave.Open(Worn(1, 1, 10).Set, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        Assert.Equal(EconomyCodes.SaveFormat4, refusal.Code);
        Assert.Contains("format 4 is no longer read", refusal.Message, StringComparison.Ordinal);
        node["Lines"] = 1;       // another shape altogether: still refused by name, before the shape is read
        Assert.Equal(EconomyCodes.SaveFormat4, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(Worn(1, 1, 10).Set, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))).Code);
    }
}
