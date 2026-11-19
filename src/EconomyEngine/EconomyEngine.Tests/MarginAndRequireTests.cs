using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;
using static EconomyEngine.Tests.Chain;

namespace EconomyEngine.Tests;

/// <summary>
/// Pass 49: the margin throttle of a line, supply modifiers and shocks acting on what a recipe makes, goods a line must hold but never uses up, workers and energy as ordinary goods, and the grouped batches of a
/// save. Every expected number is worked by hand from the arithmetic written beside it.
/// </summary>
public sealed class MarginAndRequireTests
{
    private static readonly string[] SteelGoods = ["ore", "coal", "steel", "tools"];

    private static RecipeSpec Smelt(int duration = 1) => new("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], duration);

    private static void SetPrice(EconomyRuntime runtime, string market, string good, long price)
    {
        var m = runtime.MarketIndex(market);
        runtime.Markets[m].Pairs[runtime.PairIndex(m, runtime.GoodIndex(good))].Price = price;
    }

    /// <summary>A market with one smelter (2 ore + 1 coal make 1 steel; at the start every good costs 10,000 milli, so a run's inputs cost 30,000) and a throttle; steel is set to <paramref name="steelPrice"/>.</summary>
    private static EconomyRuntime Throttled(long steelPrice, long ore = 1000, long coal = 1000, int stop = 900, int full = 1200, int duration = 1)
    {
        var runtime = Runtime(SteelGoods, [Smelt(duration)], [new MarketSpec("m", new() { ["ore"] = ore, ["coal"] = coal }, 1_000_000, [new LineSpec("smelt", 40, stop, full)])]);
        SetPrice(runtime, "m", "steel", steelPrice);
        return runtime;
    }

    // ---- the throttle, worked by hand --------------------------------------------------------------------------------------------------

    [Theory]
    // price of steel, margin per mille (= price x 1000 / 30,000), runs of 40 (capacity x fraction / 1000, truncated), limit
    [InlineData(10_000, 333, 0, LineLimit.Margin)]         // far below the stop: nothing runs
    [InlineData(26_970, 899, 0, LineLimit.Margin)]         // one under the stop
    [InlineData(27_000, 900, 0, LineLimit.Margin)]         // exactly the stop: still nothing (the stop is exclusive)
    [InlineData(27_300, 910, 1, LineLimit.Margin)]         // fraction (910 - 900) x 1000 / 300 = 33, 40 x 33 / 1000 = 1.32 -> 1
    [InlineData(31_500, 1050, 20, LineLimit.Margin)]       // fraction 150 x 1000 / 300 = 500: half the capacity
    [InlineData(35_970, 1199, 39, LineLimit.Margin)]       // fraction 299 x 1000 / 300 = 996, 39.84 -> 39
    [InlineData(36_000, 1200, 40, LineLimit.Capacity)]     // exactly full: the whole capacity, and not a throttled line
    [InlineData(40_000, 1333, 40, LineLimit.Capacity)]
    public void AThrottledLineRunsTheFractionOfItsCapacityThatItsMarginGives(long steelPrice, long margin, long runs, LineLimit limit)
    {
        var runtime = Throttled(steelPrice);
        Assert.Equal(margin, runtime.MarginOfLine(0, 0).Ratio);
        var plan = Assert.Single(runtime.PlanLines(0));
        Assert.Equal((runs, limit), (plan.Runs, plan.Limit));
        Period(runtime);
        Assert.Equal((runs, limit), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        Assert.Equal(1000 - 2 * runs, Stock(runtime, "m", "ore"));           // the inputs of exactly those runs were used
    }

    [Fact]
    public void TheMarginIsTheValueOfTheOutputsOverTheCostOfTheInputsAtTheMarketsPrices()
    {
        var runtime = Runtime(SteelGoods, [Smelt(), new RecipeSpec("smith", [("steel", 2)], [("tools", 3)])], [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100 }, 1_000_000, [new LineSpec("smelt", 10, 900, 1200), new LineSpec("smith", 10, 500, 600)])]);
        SetPrice(runtime, "m", "ore", 4000);
        SetPrice(runtime, "m", "coal", 2000);
        SetPrice(runtime, "m", "steel", 12_000);
        SetPrice(runtime, "m", "tools", 5000);
        var smelt = runtime.MarginOfLine(0, runtime.Set.RecipeById["smelt"].Index);
        Assert.Equal((2 * 4000 + 1 * 2000L, 12_000L, 1200L, 1000L), (smelt.InputCost, smelt.OutputValue, smelt.Ratio, smelt.Fraction));        // 10,000 in, 12,000 out: 1,200 per mille, full
        var smith = runtime.MarginOfLine(0, runtime.Set.RecipeById["smith"].Index);
        Assert.Equal((2 * 12_000L, 3 * 5000L, 625L, 1000L), (smith.InputCost, smith.OutputValue, smith.Ratio, smith.Fraction));                  // 24,000 in, 15,000 out: 625, between 500 and 600 it would be partial, above it is full
        SetPrice(runtime, "m", "tools", 4000);
        Assert.Equal((500L, 0L), (runtime.MarginOfLine(0, 1).Ratio, runtime.MarginOfLine(0, 1).Fraction));                                            // 12,000 / 24,000 = 500: at the stop
    }

    [Fact]
    public void ALineWithoutAThrottleNeverLooksAtPricesAndSaysNoMarginInItsExplanation()
    {
        var runtime = Runtime(SteelGoods, [Smelt()], [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        SetPrice(runtime, "m", "steel", 1000);       // steel is worth a tenth of its inputs
        Period(runtime);
        Assert.Equal((40L, LineLimit.Capacity), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        Assert.DoesNotContain("margin smelt", EconomyExplain.Production(runtime, "m", "smelt").Text(), StringComparison.Ordinal);
        Assert.Equal(1000L, runtime.MarginOfLine(0, 0).Fraction);
    }

    [Fact]
    public void TheThrottleOnlyShrinksWhatTheLineWantsAndAnInputOrRoomLimitStillNamesItself()
    {
        var shortOfCoal = Throttled(31_500, coal: 5);        // wants 20, coal allows 5
        var plan = Assert.Single(shortOfCoal.PlanLines(0));
        Assert.Equal((20L, 5L, LineLimit.Input, shortOfCoal.GoodIndex("coal")), (plan.Wanted, plan.Runs, plan.Limit, plan.Good));
        var plenty = Throttled(31_500, coal: 30);            // coal would allow 30 runs, more than the 20 the margin leaves
        plan = Assert.Single(plenty.PlanLines(0));
        Assert.Equal((20L, 20L, LineLimit.Margin, -1), (plan.Wanted, plan.Runs, plan.Limit, plan.Good));
        var tie = Throttled(31_500, coal: 20);               // coal allows exactly the 20 the margin leaves: the margin names the limit, as the capacity does on a tie
        plan = Assert.Single(tie.PlanLines(0));
        Assert.Equal((20L, LineLimit.Margin), (plan.Runs, plan.Limit));
    }

    [Fact]
    public void TheLimitChangesOfAThrottledLineAreReportedOncePerChange()
    {
        var runtime = Throttled(31_500);
        var first = Period(runtime);
        var change = Assert.Single(first.Limits);
        Assert.Equal((LineLimit.Capacity, LineLimit.Margin, -1), (change.From, change.To, change.Good));
        // The prices move as the line buys its inputs (a large stock of ore and coal sinks their prices, which lifts the margin), so the line moves between the throttle and the full capacity. Over ten more periods
        // an event is raised exactly when the line's limit or limiting good changed since the last period, with the right From and To, and never otherwise.
        var (limit, good) = (Line(runtime, "m", "smelt").Limit, Line(runtime, "m", "smelt").LimitGood);
        var changes = 0;
        for (var i = 0; i < 10; i++)
        {
            var events = Period(runtime).Limits;
            var now = (Line(runtime, "m", "smelt").Limit, Line(runtime, "m", "smelt").LimitGood);
            if (now == (limit, good)) Assert.Empty(events);
            else
            {
                var e = Assert.Single(events);
                Assert.Equal((limit, now.Limit, now.LimitGood), (e.From, e.To, e.Good));
                changes++;
            }
            (limit, good) = now;
        }
        Assert.True(changes >= 1, "the line never moved between the throttle and the capacity");
    }

    [Fact]
    public void AThrottledChainStopsPilingUpAGoodWorthLessThanItsInputsWhileTheSameChainWithoutAThrottleKeepsMakingIt()
    {
        EconomyRuntime Chain(bool throttled) => Runtime(SteelGoods, [Smelt(0)], [new MarketSpec("m", new() { ["ore"] = 5000, ["coal"] = 5000 }, 100_000, [throttled ? new LineSpec("smelt", 40, 900, 1200) : new LineSpec("smelt", 40)])],
            root =>
            {
                var pairs = Edit.Obj(root, "markets", 0)["pairs"]!.AsArray();
                pairs[0]!["production"] = 100;     // ore and coal arrive every period
                pairs[1]!["production"] = 100;
            });
        var free = Chain(false);
        var throttled = Chain(true);
        SetPrice(free, "m", "steel", 33_000);          // steel starts worth 10 percent more than its inputs: the throttle lets two thirds of the capacity run
        SetPrice(throttled, "m", "steel", 33_000);
        for (var i = 0; i < 60; i++) { Period(free); Period(throttled); }
        // The glut of steel (nobody uses any) pushes its price to the floor, 1,000 against inputs that cost 30,000: the throttle stops the line, the control keeps running to the warehouse.
        Assert.True(Stock(free, "m", "steel") > 2000, "the control made " + Stock(free, "m", "steel"));
        Assert.True(Stock(throttled, "m", "steel") < Stock(free, "m", "steel") / 4, "the throttled line made " + Stock(throttled, "m", "steel") + " against " + Stock(free, "m", "steel"));
        Assert.True(Stock(throttled, "m", "steel") > 0, "the throttled line never ran");
        Assert.Equal(LineLimit.Margin, Line(throttled, "m", "smelt").Limit);
        BoundsTests.Invariants(throttled);
    }

    [Fact]
    public void TheExplanationOfAThrottledLineShowsTheMarginTheStopsAndTheRunsItLeaves()
    {
        var runtime = Throttled(31_500);
        var explanation = EconomyExplain.Production(runtime, "m", "smelt");
        var text = explanation.Text();
        Assert.Equal((20L, "margin"), (explanation.Value, explanation.State));
        Assert.Contains("margin smelt: 1050 (per mille: one run's outputs are worth 31500 against inputs that cost 30000", text, StringComparison.Ordinal);
        Assert.Contains("stops at 900, runs in full from 1200, so it runs 500 per mille of its capacity", text, StringComparison.Ordinal);
        Assert.Contains("runs smelt: 20 (of 20 wanted: the margin throttle", text, StringComparison.Ordinal);
        Period(runtime);
        Assert.Contains("last-period smelt: 20 (the margin throttle", EconomyExplain.Production(runtime, "m", "smelt").Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void AShockOrAModifierOnWhatALineMakesLowersItsMarginAndTheThrottleFollows()
    {
        EconomyRuntime World() => Runtime(SteelGoods, [Smelt()], [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000 }, 1_000_000, [new LineSpec("smelt", 40, 900, 1200)])], root =>
        {
            root["shocks"] = new JsonArray(new JsonObject { ["id"] = "s", ["key"] = "k.s", ["duration"] = 5, ["effects"] = new JsonArray(new JsonObject { ["kind"] = "supply", ["good"] = "steel", ["tag"] = "", ["route"] = "", ["amount"] = -500 }) });
            root["facts"] = new JsonArray(new JsonObject { ["id"] = "season", ["type"] = "string" });
            root["modifiers"] = new JsonArray(new JsonObject { ["id"] = "dry", ["kind"] = "supply", ["good"] = "steel", ["tag"] = "", ["permille"] = -400, ["when"] = new JsonObject { ["fact"] = "season", ["op"] = "eq", ["value"] = "dry" } });
        });
        var runtime = World();
        SetPrice(runtime, "m", "steel", 36_000);                                   // steel is worth 36,000 against inputs of 30,000: a margin of 1,200, in full
        Assert.Equal((1200L, 1000L), (runtime.MarginOfLine(0, 0).Ratio, runtime.MarginOfLine(0, 0).Fraction));
        Assert.Equal(720L, runtime.MarginOfLine(0, 0, Dry).Ratio);                  // a supply modifier of -400: 36,000 x 600 / 1,000 = 21,600 over 30,000
        Assert.Equal((0L, LineLimit.Margin), (runtime.PlanLines(0, Dry).Single().Runs, runtime.PlanLines(0, Dry).Single().Limit));
        Assert.Equal((40L, LineLimit.Capacity), (runtime.PlanLines(0, Wet).Single().Runs, runtime.PlanLines(0, Wet).Single().Limit));
        runtime.ApplyShock("s", "m", 1000);                                         // the shock halves what the line makes: 18,000 over 30,000
        Assert.Equal((600L, 0L), (runtime.MarginOfLine(0, 0).Ratio, runtime.MarginOfLine(0, 0).Fraction));
        Assert.Equal(0L, runtime.PlanLines(0).Single().Runs);
        Advance(runtime, Wet);                                                      // and it stops: the margin decided, the shock only cut the price it is judged by
        Assert.Equal((0L, LineLimit.Margin), (Line(runtime, "m", "smelt").LastRuns, Line(runtime, "m", "smelt").Limit));
        var mild = World();
        SetPrice(mild, "m", "steel", 36_000);
        mild.ApplyShock("s", "m", 400);                                             // a factor of 800: 28,800 over 30,000 = 960, fraction 60 x 1000 / 300 = 200, 8 of 40 runs
        Assert.Equal((960L, 200L), (mild.MarginOfLine(0, 0).Ratio, mild.MarginOfLine(0, 0).Fraction));
        Assert.Equal(8L, mild.PlanLines(0).Single().Runs);
        // The shock and the modifier together: 36,000 x 600 / 1,000 x 500 / 1,000 = 10,800 over 30,000 = 360.
        var both = World();
        SetPrice(both, "m", "steel", 36_000);
        both.ApplyShock("s", "m", 1000);
        Assert.Equal(360L, both.MarginOfLine(0, 0, Dry).Ratio);
        Assert.Throws<EngineRefusedException>(() => World().MarginOfLine(0, 0, new Dictionary<string, FactValue>()));           // a fact a modifier reads must be given
    }

    // ---- the document: throttle and requires -------------------------------------------------------------------------------------------

    private static string Doc(Action<JsonObject> edit) => Root(SteelGoods, [Smelt(), new RecipeSpec("smith", [("steel", 2)], [("tools", 3)])], [new MarketSpec("m", new(), 1_000_000, [new LineSpec("smelt", 5, 900, 1200), new LineSpec("smith", 5)])], edit).ToJsonString();

    private static JsonObject Line0(JsonObject root) => Edit.Obj(root, "markets", 0)["lines"]!.AsArray()[0]!.AsObject();

    private static JsonObject Recipe(JsonObject root, int index) => root["recipes"]!.AsArray()[index]!.AsObject();

    [Fact]
    public void TheThrottleIsReadAndEveryWrongThrottleIsRefusedByCode()
    {
        var set = EconomyLoader.Load(Doc(_ => { }));
        Assert.Equal(new ThrottleDef(900, 1200, null), set.Markets[0].Lines.First(l => l.Recipe == set.RecipeById["smelt"].Index).Throttle);
        Assert.Null(set.Markets[0].Lines.First(l => l.Recipe == set.RecipeById["smith"].Index).Throttle);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["stop"] = 1200; }))));          // stop equals full
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["stop"] = 1300; }))));          // stop above full
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["full"] = 100_001; }))));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["stop"] = -1; }))));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["full"] = 0; Line0(r)["throttle"]!["stop"] = 0; }))));
        Assert.Equal(EconomyCodes.MissingField, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => ((JsonObject)Line0(r)["throttle"]!).Remove("full")))));
        Assert.Equal(EconomyCodes.UnknownField, Fixture.Refusal(() => EconomyLoader.Load(Doc(r => Line0(r)["throttle"]!["extra"] = 1))));
        EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["stop"] = 0; Line0(r)["throttle"]!["full"] = 1; }));                                         // the widest legal: 0 to 1
        EconomyLoader.Load(Doc(r => { Line0(r)["throttle"]!["stop"] = 99_999; Line0(r)["throttle"]!["full"] = 100_000; }));
    }

    [Fact]
    public void ARecipeRequirementIsReadWithItsGoodAndAmountAndTheWrongOnesAreRefusedByCode()
    {
        string[] goods = ["ore", "coal", "steel", "tools", "hammer"];
        string Text(RecipeSpec[] recipes, Action<JsonObject>? edit = null) => Root(goods, recipes, [new MarketSpec("m", new(), 1_000_000, [new LineSpec(recipes[0].Id, 5)])], edit).ToJsonString();
        var ok = EconomyLoader.Load(Text([Smelt() with { Requires = [("hammer", 2)] }]));
        Assert.Equal([(ok.GoodById["hammer"].Index, 2L)], ok.Recipes[0].Requires.Select(r => (r.Good, r.Amount)).ToArray());
        Assert.Equal(ok.Markets[0].Pairs.ToList().FindIndex(p => p.Good == ok.GoodById["hammer"].Index), Assert.Single(ok.Markets[0].Lines[0].RequirePairs));
        var both = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("ore", 1)] }])));                          // consumed and held: ambiguous
        Assert.Equal(EconomyCodes.Recipe, both.Code);
        Assert.Contains("also consumes it", both.Message, StringComparison.Ordinal);
        var own = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("steel", 1)] }])));                         // depends on its own output
        Assert.Equal(EconomyCodes.RecipeCycle, own.Code);
        Assert.Contains("also makes it", own.Message, StringComparison.Ordinal);                                                                                    // said at the recipe, not only found later as a ring
        Assert.Equal(EconomyCodes.Recipe, Fixture.Refusal(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("hammer", 1), ("hammer", 2)] }]))));     // a good named twice
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("hammer", 0)] }]))));                    // amounts are whole and positive
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("hammer", 1_000_001)] }]))));
        Assert.Equal(EconomyCodes.Reference, Fixture.Refusal(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("anvil", 1)] }]))));
        var nine = Enumerable.Range(0, 9).Select(i => ("h" + i, 1L)).ToArray();
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => EconomyLoader.Load(Root([.. goods, .. nine.Select(n => n.Item1)], [Smelt() with { Requires = nine }], [new MarketSpec("m", new())]).ToJsonString())));
        // The market that runs the line must trade the required good.
        Assert.Equal(EconomyCodes.Line, Fixture.Refusal(() => EconomyLoader.Load(Text([Smelt() with { Requires = [("hammer", 1)] }], root => { var pairs = Edit.Obj(root, "markets", 0)["pairs"]!.AsArray(); pairs.RemoveAt(4); }))));
        // A requirement is a dependency like an input: a recipe that makes what another needs is ordered before it, and a ring through requirements is a cycle.
        var order = EconomyLoader.Load(Root(goods, [new RecipeSpec("a-use", [("ore", 1)], [("tools", 1)], 0, false, [("hammer", 1)]), new RecipeSpec("z-make", [("coal", 1)], [("hammer", 1)])], [new MarketSpec("m", new())]).ToJsonString());
        Assert.Equal(["z-make", "a-use"], order.RecipeOrder.Select(i => order.Recipes[i].Id).ToArray());
        var ring = Fixture.Refusal(() => EconomyLoader.Load(Root(goods, [new RecipeSpec("a", [("ore", 1)], [("hammer", 1)], 0, false, [("tools", 1)]), new RecipeSpec("b", [("coal", 1)], [("tools", 1)], 0, false, [("hammer", 1)])], [new MarketSpec("m", new())]).ToJsonString()));
        Assert.Equal(EconomyCodes.RecipeCycle, ring);
        var message = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Root(goods, [new RecipeSpec("a", [("ore", 1)], [("hammer", 1)], 0, false, [("tools", 1)]), new RecipeSpec("b", [("coal", 1)], [("tools", 1)], 0, false, [("hammer", 1)])], [new MarketSpec("m", new())]).ToJsonString())).Message;
        Assert.Contains("a makes hammer for b", message, StringComparison.Ordinal);
        Assert.Contains("b makes tools for a", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentOfFormat3IsRefusedByItsOwnCodeAndSaysWhatChanged()
    {
        var refusal = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Mini.Text(root => root["format"] = 3)));
        Assert.Equal(EconomyCodes.Format3, refusal.Code);
        Assert.NotEqual(EconomyCodes.Format, EconomyCodes.Format3);
        Assert.Contains("format 3 is no longer read", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("shocks", refusal.Message, StringComparison.Ordinal);
    }

    // ---- required stock: held, never used up ----------------------------------------------------------------------------------------------

    private static EconomyRuntime WithHammer(long hammers, long amount = 1, Action<JsonObject>? edit = null) =>
        Runtime(["ore", "coal", "steel", "hammer"], [new RecipeSpec("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], 0, false, [("hammer", amount)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["hammer"] = hammers }, 1_000_000, [new LineSpec("smelt", 10)])], edit);

    [Fact]
    public void ALineRunsOnlyWhileItHoldsWhatItRequiresAndNeverUsesItUp()
    {
        var without = WithHammer(0);
        var plan = Assert.Single(without.PlanLines(0));
        Assert.Equal((0L, LineLimit.Held, without.GoodIndex("hammer")), (plan.Runs, plan.Limit, plan.Good));
        Period(without);
        Assert.Equal((0L, LineLimit.Held, without.GoodIndex("hammer")), (Line(without, "m", "smelt").LastRuns, Line(without, "m", "smelt").Limit, Line(without, "m", "smelt").LimitGood));
        Assert.Equal(100, Stock(without, "m", "ore"));                                 // nothing was used
        Assert.Contains("missing hammer, which the recipe requires free (in stock and not held by a line earlier in the order this period)", EconomyExplain.Production(without, "m", "smelt").Text(), StringComparison.Ordinal);

        var with = WithHammer(1);
        for (var i = 0; i < 3; i++) Period(with);
        Assert.Equal((40L, 70L, 30L, 1L), (Stock(with, "m", "ore"), Stock(with, "m", "coal"), Stock(with, "m", "steel"), Stock(with, "m", "hammer")));   // 3 periods of 10 runs use 60 ore and 30 coal; the hammer is still there
    }

    [Fact]
    public void TheRequiredAmountIsAGateNotAPerRunCostAndTheFirstMissingGoodIsNamed()
    {
        var two = WithHammer(2, amount: 3);
        Assert.Equal(LineLimit.Held, Assert.Single(two.PlanLines(0)).Limit);          // 2 held, 3 required
        var three = WithHammer(3, amount: 3);
        var plan = Assert.Single(three.PlanLines(0));
        Assert.Equal((10L, LineLimit.Capacity), (plan.Runs, plan.Limit));              // 3 held is enough for all 10 runs
        Period(three);
        Assert.Equal(3, Stock(three, "m", "hammer"));
        Assert.Equal(100 - 20, Stock(three, "m", "ore"));
        var two2 = Runtime(["ore", "coal", "steel", "hammer", "saw"], [new RecipeSpec("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], 0, false, [("hammer", 1), ("saw", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["hammer"] = 1, ["saw"] = 0 }, 1_000_000, [new LineSpec("smelt", 10)])]);
        Assert.Equal(two2.GoodIndex("saw"), Assert.Single(two2.PlanLines(0)).Good);
    }

    [Fact]
    public void WhatAnEarlierLineMakesAtOnceCountsAsHeldForTheLaterLineOfTheSamePeriodButWhatIsOnItsWayDoesNot()
    {
        EconomyRuntime Play(int makeDuration) => Runtime(["ore", "coal", "steel", "hammer"],
            [new RecipeSpec("make", [("coal", 1)], [("hammer", 1)], makeDuration), new RecipeSpec("smelt", [("ore", 2)], [("steel", 1)], 0, false, [("hammer", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100 }, 1_000_000, [new LineSpec("make", 1), new LineSpec("smelt", 10)])]);
        var instant = Play(0);
        Period(instant);
        Assert.Equal((1L, 10L), (Line(instant, "m", "make").LastRuns, Line(instant, "m", "smelt").LastRuns));          // the hammer made at once is there for smelt's turn
        var slow = Play(1);
        Period(slow);
        Assert.Equal((1L, 0L, LineLimit.Held), (Line(slow, "m", "make").LastRuns, Line(slow, "m", "smelt").LastRuns, Line(slow, "m", "smelt").Limit));   // on its way is not yet held
        Period(slow);
        Assert.Equal(10L, Line(slow, "m", "smelt").LastRuns);
    }

    // ---- shocks and supply modifiers act on what a recipe makes ----------------------------------------------------------------------------

    private static string[] ShockGoods => ["ore", "coal", "steel", "tools", "x"];

    /// <summary>Smith (2 steel make 3 tools, 10 runs a period) and a market whose good x has a production of 100 of its own; tools and x carry the tag 't'; shock 's' cuts the supply of tag 't' by half.</summary>
    private static EconomyRuntime ShockWorld(int duration, Action<JsonObject>? edit = null) =>
        Runtime(ShockGoods, [new RecipeSpec("smith", [("steel", 2)], [("tools", 3)], duration)], [new MarketSpec("m", new() { ["steel"] = 10_000 }, 20_000, [new LineSpec("smith", 10)])], root =>
        {
            var goods = root["goods"]!.AsArray();
            goods[3]!["tags"] = new JsonArray("t");
            goods[4]!["tags"] = new JsonArray("t");
            Edit.Obj(root, "markets", 0)["pairs"]!.AsArray()[4]!["production"] = 100;
            root["shocks"] = new JsonArray(new JsonObject { ["id"] = "s", ["key"] = "k.s", ["duration"] = 5, ["effects"] = new JsonArray(new JsonObject { ["kind"] = "supply", ["good"] = "", ["tag"] = "t", ["route"] = "", ["amount"] = -500 }) });
            root["facts"] = new JsonArray(new JsonObject { ["id"] = "season", ["type"] = "string" });
            root["modifiers"] = new JsonArray(new JsonObject { ["id"] = "dry", ["kind"] = "supply", ["good"] = "", ["tag"] = "t", ["permille"] = -400, ["when"] = new JsonObject { ["fact"] = "season", ["op"] = "eq", ["value"] = "dry" } });
            edit?.Invoke(root);
        });

    private static readonly IReadOnlyDictionary<string, FactValue> Dry = new Dictionary<string, FactValue> { ["season"] = FactValue.From("dry") };
    private static readonly IReadOnlyDictionary<string, FactValue> Wet = new Dictionary<string, FactValue> { ["season"] = FactValue.From("wet") };

    private static void Advance(EconomyRuntime runtime, IReadOnlyDictionary<string, FactValue> facts) => runtime.Advance(runtime.Step + runtime.Set.PeriodSteps, facts);

    [Fact]
    public void AShockActsOnWhatARecipeMakesAsItDoesOnAPairsOwnProduction()
    {
        var calm = ShockWorld(0);
        Advance(calm, Wet);
        var calmTools = Stock(calm, "m", "tools");
        var calmX = Stock(calm, "m", "x");
        Assert.Equal((30L, 100L), (calmTools, calmX));         // 10 runs of 3, and a production of 100 at a price ratio of 1: the baseline
        var shocked = ShockWorld(0);
        shocked.ApplyShock("s", "m", 1000);
        Advance(shocked, Wet);
        Assert.Equal((15L, 50L), (Stock(shocked, "m", "tools"), Stock(shocked, "m", "x")));          // the same half: what the recipe made, what the pair made
        // A scale of the shock scales its effect: 400 per mille of -500 is -200, so 80 percent comes through.
        var mild = ShockWorld(0);
        mild.ApplyShock("s", "m", 400);
        Advance(mild, Wet);
        Assert.Equal((24L, 80L), (Stock(mild, "m", "tools"), Stock(mild, "m", "x")));
        // The shock ends after its duration of five periods, and the recipe's output is whole again.
        for (var i = 0; i < 6; i++) Advance(shocked, Wet);
        Assert.Empty(shocked.ActiveShocks);
        var before = Stock(shocked, "m", "tools");
        Advance(shocked, Wet);
        Assert.Equal(30L, Stock(shocked, "m", "tools") - before);
    }

    [Fact]
    public void ASupplyModifierAndAShockComposeInTheOrderOfAPairsSupplyAndEachTruncates()
    {
        var dry = ShockWorld(0);
        Advance(dry, Dry);
        Assert.Equal((18L, 60L), (Stock(dry, "m", "tools"), Stock(dry, "m", "x")));              // 30 x 600 / 1000 and 100 x 600 / 1000
        var both = ShockWorld(0);
        both.ApplyShock("s", "m", 1000);
        Advance(both, Dry);
        Assert.Equal((9L, 30L), (Stock(both, "m", "tools"), Stock(both, "m", "x")));             // then half again
        // Each factor truncates on its own, as a pair's supply does: a shock of scale 400 is a factor of 800. 7 runs make 21 tools; 21 x 600 / 1000 = 12.6 -> 12, x 800 / 1000 = 9.6 -> 9 (one multiplication at once would give 10).
        var small = ShockWorld(0, root => Edit.Obj(root, "markets", 0)["lines"]!.AsArray()[0]!["capacity"] = 7);
        small.ApplyShock("s", "m", 400);
        Advance(small, Dry);
        Assert.Equal(9L, Stock(small, "m", "tools"));
        Assert.Equal(48L, Stock(small, "m", "x"));                  // the pair's own 100 x 600 / 1000 = 60 x 800 / 1000 = 48
    }

    [Fact]
    public void OutputsThatTakeTimeArriveUnderTheShocksOfTheirArrivalPeriodNotTheirStart()
    {
        var late = ShockWorld(1);
        Advance(late, Wet);                                          // period 1: 10 runs start, 30 tools are due in period 2
        Assert.Equal(0, Stock(late, "m", "tools"));
        late.ApplyShock("s", "m", 1000);                             // the shock is on for period 2, when they arrive
        Advance(late, Wet);
        Assert.Equal(15L, Stock(late, "m", "tools"));
        var early = ShockWorld(1, root => root["shocks"]!.AsArray()[0]!["duration"] = 1);
        early.ApplyShock("s", "m", 1000);                            // on in period 1 only: the runs start under it, but arrive in a quiet period
        Advance(early, Wet);
        Assert.Equal(0, Stock(early, "m", "tools"));
        Assert.Empty(early.ActiveShocks);
        Advance(early, Wet);
        Assert.Equal(30L, Stock(early, "m", "tools"));
    }

    [Fact]
    public void ASupplyBoostCannotOverfillTheWarehouseAndThePlanSeesTheSameCap()
    {
        var boosted = ShockWorld(0, root =>
        {
            root["modifiers"]!.AsArray()[0]!["permille"] = 500;
            Edit.Obj(root, "markets", 0)["pairs"]!.AsArray()[3]!["capacity"] = 20;
        });
        Advance(boosted, Dry);                                       // 6 runs fit the warehouse of 20 (room 20 / 3), 18 tools x 1.5 = 27, cut to 20
        Assert.Equal(20, Stock(boosted, "m", "tools"));
        Assert.Equal((6L, LineLimit.Room), (Line(boosted, "m", "smith").LastRuns, Line(boosted, "m", "smith").Limit));
        BoundsTests.Invariants(boosted);
    }

    [Fact]
    public void TheExplanationShowsTheYieldWithItsModifierAndItsShockAndSaysWhenNoFactsWereGiven()
    {
        var runtime = ShockWorld(0);
        runtime.ApplyShock("s", "m", 1000);
        var bare = EconomyExplain.Production(runtime, "m", "smith").Text();
        Assert.Contains("yield tools: 1500 (thousandths of a unit a run actually joins the stock: supply modifiers 1000 per mille, shocks 500 per mille, applied when the outputs arrive (modifiers not applied: no host facts were given))", bare, StringComparison.Ordinal);
        var withFacts = EconomyExplain.Production(runtime, "m", "smith", Dry).Text();
        Assert.Contains("yield tools: 900 (thousandths of a unit a run actually joins the stock: supply modifiers 600 per mille, shocks 500 per mille, applied when the outputs arrive)", withFacts, StringComparison.Ordinal);
        Assert.Equal(10L, runtime.PlanLines(0).Single().Runs);               // the runs do not depend on the yield: only what they make does
        Assert.Equal(10L, runtime.PlanLines(0, Dry).Single().Runs);
    }

    [Fact]
    public void ALaterLineOfThePeriodSeesTheStockAnEarlierBoostedLineLeftAfterTheWarehouseCutAndNeverMoreThanIsThere()
    {
        // 'smith' (2 steel make 3 tools, at once) is boosted by 50 percent; the warehouse for tools holds 20, so 6 runs fit (room 20 / 3) and make 27 tools, of which 20 stay. 'melt' (1 tool makes 1 coal, at once)
        // comes after it in the order and can only use what is really there: 20 runs, not 27.
        var runtime = Runtime(["ore", "coal", "steel", "tools"], [new RecipeSpec("smith", [("steel", 2)], [("tools", 3)]), new RecipeSpec("melt", [("tools", 1)], [("coal", 1)])],
            [new MarketSpec("m", new() { ["steel"] = 100 }, 20_000, [new LineSpec("smith", 10), new LineSpec("melt", 100)])], root =>
            {
                Edit.Obj(root, "markets", 0)["pairs"]!.AsArray()[3]!["capacity"] = 20;
                root["facts"] = new JsonArray(new JsonObject { ["id"] = "season", ["type"] = "string" });
                root["modifiers"] = new JsonArray(new JsonObject { ["id"] = "boom", ["kind"] = "supply", ["good"] = "tools", ["tag"] = "", ["permille"] = 500, ["when"] = new JsonObject { ["fact"] = "season", ["op"] = "eq", ["value"] = "dry" } });
            });
        Assert.Equal((6L, 20L), (runtime.PlanLines(0, Dry)[0].Runs, runtime.PlanLines(0, Dry)[1].Runs));      // the plan the explanation shows
        Advance(runtime, Dry);
        Assert.Equal((6L, LineLimit.Room, 20L, LineLimit.Input), (Line(runtime, "m", "smith").LastRuns, Line(runtime, "m", "smith").Limit, Line(runtime, "m", "melt").LastRuns, Line(runtime, "m", "melt").Limit));
        Assert.Equal((0L, 20L), (Stock(runtime, "m", "tools"), Stock(runtime, "m", "coal")));
        BoundsTests.Invariants(runtime);
    }

    // ---- workers and energy are ordinary goods ----------------------------------------------------------------------------------------------

    [Fact]
    public void WorkersAndEnergyAreGoodsALineConsumesAndALackOfThemStopsItAndAStrikeStarvesIt()
    {
        var set = EconomyLoader.Load(Fixture.Text("workshop.economy.json"));
        var steel = set.RecipeById["forge-steel"];
        Assert.Equal(["fuel", "labour", "ore"], steel.Inputs.Select(i => set.Goods[i.Good].Id).Order(StringComparer.Ordinal).ToArray());     // energy and labour are inputs like ore
        Assert.Equal(["energy"], set.GoodById["fuel"].Tags);
        Assert.Equal(["labour"], set.GoodById["labour"].Tags);
        Assert.Equal(["hammer"], steel.Requires.Select(r => set.Goods[r.Good].Id).ToArray());                // the tool is required, not used up
        var runtime = EconomyRuntime.Create(set);
        long Stk(string good) => Stock(runtime, "forge", good);
        var first = Period(runtime);
        Assert.Equal((60L, LineLimit.Capacity), (Line(runtime, "forge", "forge-steel").LastRuns, Line(runtime, "forge", "forge-steel").Limit));        // margin 12,000 / 10,000 = 1,200: the throttle lets it run in full
        Assert.Equal(30L, Line(runtime, "forge", "make-nails").LastRuns);          // 50 steel and 140 labour (200 less the 60 the forge took) allow 30 runs at capacity
        Assert.Equal(1, Stk("hammer"));                                               // never used up
        Assert.Empty(first.Limits);
        Assert.Equal(60, Assert.Single(runtime.Batches).Runs);
        // A strike cuts the supply of labour by 60 percent for ten periods: the lines that consume it fall short of it.
        runtime.ApplyShock("strike", "forge", 1000);
        var labour = runtime.GoodIndex("labour");
        var short_ = false;
        for (var i = 0; i < 25; i++)
        {
            Period(runtime);
            short_ |= new[] { "forge-steel", "make-nails" }.Any(r => Line(runtime, "forge", r) is { Limit: LineLimit.Input } l && l.LimitGood == labour);
        }
        Assert.True(short_, "no line fell short of labour during or after the strike");
        // Without the tool the forge stops, and says which good is missing.
        runtime.Markets[0].Pairs[runtime.PairIndex(0, runtime.GoodIndex("hammer"))].Stock = 0;
        Period(runtime);
        Assert.Equal((0L, LineLimit.Held, runtime.GoodIndex("hammer")), (Line(runtime, "forge", "forge-steel").LastRuns, Line(runtime, "forge", "forge-steel").Limit, Line(runtime, "forge", "forge-steel").LimitGood));
        Assert.Contains("requires hammer: 0 (needs 1 free at this line's turn, never used up: stock 0, held by earlier lines this period 0; missing)", EconomyExplain.Production(runtime, "forge", "forge-steel").Text(), StringComparison.Ordinal);
    }

    // ---- the save: batches grouped by line, the new limits --------------------------------------------------------------------------------

    /// <summary>Two lines with batches in flight: smelt takes 3 periods (so it has up to three batches on its way) and smith 2.</summary>
    private static EconomyRuntime Busy()
    {
        var runtime = Runtime(SteelGoods, [new RecipeSpec("smelt", [("ore", 2), ("coal", 1)], [("steel", 1)], 3), new RecipeSpec("smith", [("steel", 2)], [("tools", 3)], 2)],
            [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000, ["steel"] = 100 }, 1_000_000, [new LineSpec("smelt", 7), new LineSpec("smith", 5)])]);
        for (var i = 0; i < 4; i++) Period(runtime);
        return runtime;
    }

    [Fact]
    public void ASaveGroupsTheBatchesOfALineByCompletionPeriodAndTheDigestDoesNotDependOnTheOrderTheyAreHeldIn()
    {
        var runtime = Busy();
        Assert.True(runtime.Batches.Count >= 5, "expected batches of both lines in flight, found " + runtime.Batches.Count);
        var image = Tamper.Image(EconomySave.Write(runtime));
        Assert.Equal(2, image.Batches.Count);                                                                    // one group per line, not one entry per batch
        Assert.Equal([(0, 0), (0, 1)], image.Batches.Select(g => (g.Market, g.Recipe)).ToArray());
        Assert.All(image.Batches, g => Assert.Equal(g.Completes.OrderBy(c => c).ToArray(), g.Completes.ToArray()));
        Assert.All(image.Batches, g => Assert.Equal(g.Completes.Count, g.Runs.Count));
        Assert.Equal(runtime.Batches.Count, image.Batches.Sum(g => g.Completes.Count));
        var canonical = EconomyImage.Canonical(image);
        Assert.Matches(@"\nbatches 0\|0\|(\d+:\d+,){2}\d+:\d+\nbatches 0\|1\|\d+:\d+,\d+:\d+(\n|$)", canonical);
        // Delivery adds the same units to the same stock whatever order it visits the batches in, so a runtime that holds them in another order (an opened save holds them grouped) has the same digest,
        // and goes on to the same states.
        var reopened = EconomySave.Open(runtime.Set, EconomySave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        for (var i = 0; i < 8; i++)
        {
            Period(runtime);
            Period(reopened);
            Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        }
    }

    [Fact]
    public void AGroupedSaveIsSmallerThanTheSameBatchesOneToAnEntryAndTheNumbersAreWhatThePassMeasured()
    {
        var runtime = Busy();
        var image = Tamper.Image(EconomySave.Write(runtime));
        var grouped = System.Text.Json.JsonSerializer.Serialize(image.Batches).Length;
        // The pass-48 form: one object per batch with its market and recipe each time.
        var perBatch = System.Text.Json.JsonSerializer.Serialize(runtime.Batches.Select(b => new { Market = b.Market, Recipe = b.Recipe, Runs = b.Runs, Completes = b.Completes })).Length;
        Assert.True(grouped < perBatch, "grouped " + grouped + " bytes, one to an entry " + perBatch);
    }

    private static EconomySet BusySet() => Busy().Set;

    public static IEnumerable<object[]> BatchForgeries() => BatchForgeryCases.Select((c, i) => new object[] { i, c.Name });

    private static readonly (string Name, Action<EconomyImage> Edit)[] BatchForgeryCases =
    [
        ("groups in the wrong order", i => (i.Batches[0], i.Batches[1]) = (i.Batches[1], i.Batches[0])),
        ("one line in two groups", i => i.Batches.Add(new ImageBatchGroup { Market = 0, Recipe = 1, Completes = [i.Step / 1 + 1], Runs = [1] })),
        ("a group with fewer runs than completion periods", i => i.Batches[0].Runs.RemoveAt(0)),
        ("a group with more runs than completion periods", i => i.Batches[0].Runs.Add(1)),
        ("an empty group", i => { i.Batches[1].Completes.Clear(); i.Batches[1].Runs.Clear(); }),
        ("completion periods that do not ascend", i => (i.Batches[0].Completes[0], i.Batches[0].Completes[1]) = (i.Batches[0].Completes[1], i.Batches[0].Completes[0])),
        ("a repeated completion period", i => i.Batches[0].Completes[1] = i.Batches[0].Completes[0]),
        ("runs beyond the line's capacity in a later entry", i => i.Batches[0].Runs[^1] = 8),
        ("no runs in a later entry", i => i.Batches[0].Runs[^1] = 0),
    ];

    [Theory]
    [MemberData(nameof(BatchForgeries))]
    public void AForgedBatchGroupWithARecomputedDigestIsRefusedAsState(int index, string name)
    {
        var runtime = Busy();
        var good = EconomySave.Write(runtime);
        EconomySave.Open(runtime.Set, good);                                    // the unforged save opens
        Assert.True(Tamper.Image(good).Batches[0].Completes.Count >= 2 && Tamper.Image(good).Batches.Count == 2, "the world must have a line with two batches in flight");
        var forged = Tamper.Forge(good, BatchForgeryCases[index].Edit);
        var code = Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, forged)).Code;
        Assert.True(code == EconomyCodes.SaveState, name + " was refused as " + code);
    }

    [Fact]
    public void TheMarginAndHeldLimitsSurviveASaveAndAForgedOneIsRefused()
    {
        var throttled = Throttled(31_500);
        Period(throttled);
        Assert.Equal(LineLimit.Margin, Line(throttled, "m", "smelt").Limit);
        var good = EconomySave.Write(throttled);
        var reopened = EconomySave.Open(throttled.Set, good);
        Assert.Equal((20L, LineLimit.Margin, -1), (Line(reopened, "m", "smelt").LastRuns, Line(reopened, "m", "smelt").Limit, Line(reopened, "m", "smelt").LimitGood));
        Assert.Equal(throttled.StateDigest(), reopened.StateDigest());
        // A margin limit needs a throttle; a margin limit names no good.
        var plain = Runtime(SteelGoods, [Smelt()], [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100 }, 1_000_000, [new LineSpec("smelt", 40)])]);
        Period(plain);
        var plainSave = EconomySave.Write(plain);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(plain.Set, Tamper.Forge(plainSave, i => i.Markets[0].Lines[0].Limit = (int)LineLimit.Margin))).Code);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(throttled.Set, Tamper.Forge(good, i => i.Markets[0].Lines[0].LimitGood = 0))).Code);
        // A held limit needs a requirement and names one of the required goods.
        var held = WithHammer(0);
        Period(held);
        var heldSave = EconomySave.Write(held);
        Assert.Equal(LineLimit.Held, Line(EconomySave.Open(held.Set, heldSave), "m", "smelt").Limit);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(held.Set, Tamper.Forge(heldSave, i => i.Markets[0].Lines[0].LimitGood = 0))).Code);       // ore is not required
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(plain.Set, Tamper.Forge(plainSave, i => { i.Markets[0].Lines[0].Limit = (int)LineLimit.Held; i.Markets[0].Lines[0].LimitGood = 0; }))).Code);
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(held.Set, Tamper.Forge(heldSave, i => i.Markets[0].Lines[0].Limit = 6))).Code);                       // there are six limits, 0 to 5
    }

    [Fact]
    public void APairIsTenWholeNumbersInTheFileAndAnythingElseIsNotASave()
    {
        var runtime = Busy();
        var good = EconomySave.Write(runtime);
        var pair = JsonNode.Parse(good)!["Markets"]![0]!["Pairs"]![0]!.AsArray();
        Assert.Equal(10, pair.Count);                                                                // an array, not an object: [price, stock, last change, supply, demand, met, band, short, spot demand, spot supply]
        Assert.Equal(runtime.Markets[0].Pairs[0].Price, pair[0]!.GetValue<long>());
        Assert.Equal(runtime.Markets[0].Pairs[0].Stock, pair[1]!.GetValue<long>());
        Assert.Equal(runtime.Markets[0].Pairs[0].Short ? 1 : 0, pair[7]!.GetValue<int>());
        void Refused(string what, Action<JsonArray> edit, string? whole = null)
        {
            var node = JsonNode.Parse(good)!.AsObject();
            var target = node["Markets"]![0]!["Pairs"]!.AsArray();
            if (whole is not null) target[0] = JsonNode.Parse(whole);
            else edit(target[0]!.AsArray());
            var code = Fixture.Refusal(() => EconomySave.Open(runtime.Set, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())), what);
            Assert.True(code == EconomyCodes.SaveFormat, what + " was refused as " + code);
        }
        Refused("nine numbers", a => a.RemoveAt(9));
        Refused("eleven numbers", a => a.Add(1));
        Refused("a flag of 2", a => a[7] = 2);
        Refused("a negative flag", a => a[7] = -1);
        Refused("a fraction", a => a[1] = 1.5);
        Refused("a text", a => a[0] = "5");
        Refused("a band beyond a whole number of 32 bits", a => a[6] = 3_000_000_000L);
        Refused("an object where the array belongs", _ => { }, "{\"Price\":1,\"Stock\":1,\"LastChange\":0,\"Supply\":0,\"Demand\":0,\"Met\":0,\"Band\":0,\"Short\":false,\"SpotDemand\":0,\"SpotSupply\":0}");
        Refused("null", _ => { }, "null");
    }

    [Fact]
    public void ASaveOfFormat3IsRefusedByItsOwnCodeEvenThoughItsShapeIsDifferent()
    {
        var good = EconomySave.Write(Busy());
        var node = JsonNode.Parse(good)!.AsObject();
        node["Format"] = 3;
        var batches = node["Batches"]!.AsArray();
        node["Batches"] = new JsonArray(new JsonObject { ["Market"] = 0, ["Recipe"] = 0, ["Runs"] = 1, ["Completes"] = 2 });    // the old per-batch shape
        Assert.Equal(EconomyCodes.SaveFormat3, Assert.Throws<EngineRefusedException>(() => EconomySave.Open(BusySet(), System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))).Code);
        Assert.NotEqual(EconomyCodes.SaveFormat, EconomyCodes.SaveFormat3);
        Assert.NotEmpty(batches);
    }
}
