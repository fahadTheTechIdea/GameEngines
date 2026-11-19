using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;
using static EconomyEngine.Tests.Chain;

namespace EconomyEngine.Tests;

/// <summary>
/// Pass 57 (B3): a required good (a tool or a catalyst) is held by one line at a time within a period. Lines run in the engine's order; a line that starts runs holds the amount it requires and a later line
/// sees only the rest. Every expected number is worked by hand beside it; the period is also checked against a run-by-run oracle that shares no code with the engine.
/// </summary>
public sealed class SharedToolTests
{
    private static readonly string[] Goods = ["ore", "coal", "steel", "tools", "hammer"];

    private static RecipeSpec A(long needed = 1, (long Units, long Runs)? wear = null) => new("a", [("ore", 1)], [("steel", 1)], 0, false, [("hammer", needed)], wear is { } w ? new() { ["hammer"] = w } : null);
    private static RecipeSpec B(long needed = 1, (long Units, long Runs)? wear = null) => new("b", [("coal", 1)], [("tools", 1)], 0, false, [("hammer", needed)], wear is { } w ? new() { ["hammer"] = w } : null);

    private static EconomyRuntime Shared(long hammers, RecipeSpec a, RecipeSpec b, long ore = 100_000, long capacityA = 5, long capacityB = 5) =>
        Runtime(Goods, [a, b], [new MarketSpec("m", new() { ["ore"] = ore, ["coal"] = 100_000, ["hammer"] = hammers }, 1_000_000, [new LineSpec("a", capacityA), new LineSpec("b", capacityB)])]);

    [Fact]
    public void OneHammerServesTheFirstLineInTheOrderAndTheNextLineIsHeldNamingIt()
    {
        var runtime = Shared(1, A(), B());
        var plans = runtime.PlanLines(0);
        Assert.Equal((5L, LineLimit.Capacity, -1), (plans[0].Runs, plans[0].Limit, plans[0].Good));
        Assert.Equal((0L, LineLimit.Held, runtime.GoodIndex("hammer")), (plans[1].Runs, plans[1].Limit, plans[1].Good));
        Period(runtime);
        Assert.Equal((5L, 0L, 1L), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Stock(runtime, "m", "hammer")));
        Assert.Equal(LineLimit.Held, Line(runtime, "m", "b").Limit);
        Assert.Equal(99_995L, Stock(runtime, "m", "ore"));
        Assert.Equal(100_000L, Stock(runtime, "m", "coal"));      // the held line used no coal
    }

    [Fact]
    public void TheHoldingIsTheSumOfTheAmountsAndTheLaterLineSeesTheRemainder()
    {
        // Stock 3: a needs 2, b needs 2 -> b sees 1: held. b needs 1 -> both run. Stock 4: both need 2 -> both run.
        Assert.Equal(LineLimit.Held, Shared(3, A(2), B(2)).PlanLines(0)[1].Limit);
        Assert.Equal(5L, Shared(3, A(2), B(1)).PlanLines(0)[1].Runs);
        Assert.Equal(5L, Shared(4, A(2), B(2)).PlanLines(0)[1].Runs);
        Assert.Equal(0L, Shared(0, A(), B()).PlanLines(0)[0].Runs);
    }

    [Fact]
    public void ALineThatStartsNoRunsHoldsNothing()
    {
        // a has no ore, so it starts no run and does not hold the hammer: b may have it.
        var runtime = Shared(1, A(), B(), ore: 0);
        var plans = runtime.PlanLines(0);
        Assert.Equal((0L, LineLimit.Input, 5L, LineLimit.Capacity), (plans[0].Runs, plans[0].Limit, plans[1].Runs, plans[1].Limit));
        Period(runtime);
        Assert.Equal(5L, Line(runtime, "m", "b").LastRuns);
    }

    [Fact]
    public void WhenAToolWearsOutMidPeriodItsHolderLosesItAndNoLaterLineCountsItAgain()
    {
        // 2 hammers, a needs 1 and loses 1 in every run: a may run twice (the tool is whole at the start of each), 2 are lost, the stock is 0 (not negative) and a holds nothing; b finds none.
        var runtime = Shared(2, A(1, (1, 1)), B());
        var plans = runtime.PlanLines(0);
        Assert.Equal((2L, LineLimit.Worn, 0L, LineLimit.Held), (plans[0].Runs, plans[0].Limit, plans[1].Runs, plans[1].Limit));
        Period(runtime);
        Assert.Equal((0L, 2L, LineLimit.Worn), (Stock(runtime, "m", "hammer"), Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "a").Limit));
        Period(runtime);
        Assert.Equal((0L, LineLimit.Held), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "a").Limit));
    }

    [Fact]
    public void TheWearFallsOnTheLineThatUsedItAndTheOtherLineKeepsItsOwnHammer()
    {
        // 2 hammers, a wears 1 in 10 runs, b wears 1 in 5 runs, 5 runs each: a has done 5 of 10 (no loss), b 5 of 5 (one lost). The stock is 1; a holds 1 and lost none, b lost its own.
        var runtime = Shared(2, A(1, (1, 10)), B(1, (1, 5)));
        Period(runtime);
        Assert.Equal((5L, 5L, 1L), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Stock(runtime, "m", "hammer")));
        Assert.Equal((5L, 0L), (Line(runtime, "m", "a").Wear[0], Line(runtime, "m", "b").Wear[0]));
        // Next period: a (first) sees 1 free hammer and runs; b then sees 0: held. a ends at 10 of 10: its hammer is lost, the stock is 0.
        Period(runtime);
        Assert.Equal((5L, 0L, 0L, LineLimit.Held), (Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns, Stock(runtime, "m", "hammer"), Line(runtime, "m", "b").Limit));
        Assert.Equal((0L, 0L), (Line(runtime, "m", "a").Wear[0], Line(runtime, "m", "b").Wear[0]));
    }

    [Fact]
    public void AnInputCannotUseUpWhatAnEarlierLineHolds()
    {
        // b uses the hammer up as an input; a holds 1 of the 3: b can use 2.
        var runtime = Runtime(Goods,
            [A(), new RecipeSpec("b", [("hammer", 1)], [("tools", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["hammer"] = 3 }, 1_000_000, [new LineSpec("a", 5), new LineSpec("b", 10)])]);
        var plans = runtime.PlanLines(0);
        Assert.Equal((5L, 2L, LineLimit.Input), (plans[0].Runs, plans[1].Runs, plans[1].Limit));
        Assert.Contains("input hammer: 2 (needs 1 a run; stock 2 at this line's turn)", EconomyExplain.Production(runtime, "m", "b").Text(), StringComparison.Ordinal);   // 3 in stock, 1 held by a
        Period(runtime);
        Assert.Equal(1L, Stock(runtime, "m", "hammer"));
    }

    [Fact]
    public void AnotherLinesWearCanEatIntoTheFreeStockBelowAnEarlierHoldingAndALaterInputThenSeesNothingNotANegativeAmount()
    {
        // 2 hammers. a holds 1. b holds 1 and loses 3 in its one run: the stock falls to 0 (not -1) while a still holds 1, so the stock is under the holding. c uses a hammer up as an input: nothing is free, so it runs 0 times.
        var runtime = Runtime([.. Goods, "gadget", "widget"],
            [A(), B(1, (3, 1)), new RecipeSpec("c", [("hammer", 1)], [("gadget", 1)]), new RecipeSpec("d", [("ore", 1)], [("widget", 1)], 0, false, [("hammer", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 100, ["coal"] = 100, ["hammer"] = 2 }, 1_000_000, [new LineSpec("a", 5), new LineSpec("b", 1), new LineSpec("c", 5), new LineSpec("d", 5)])]);
        var plans = runtime.PlanLines(0);
        Assert.Equal((5L, 1L, 0L, 0L), (plans[0].Runs, plans[1].Runs, plans[2].Runs, plans[3].Runs));
        Assert.Equal(LineLimit.Held, plans[3].Limit);
        Assert.Contains("requires hammer: 0 (needs 1 free at this line's turn, never used up: stock 0, held by earlier lines this period 1; missing)", EconomyExplain.Production(runtime, "m", "d").Text(), StringComparison.Ordinal);   // the stock is 0, never -1
        Assert.Equal((LineLimit.Capacity, LineLimit.Input), (plans[1].Limit, plans[2].Limit));
        Period(runtime);
        Assert.Equal((0L, 0L), (Stock(runtime, "m", "hammer"), Line(runtime, "m", "c").LastRuns));
    }

    [Fact]
    public void ALineThatIsTheOnlyUserOfAToolBehavesAsWithoutSharing()
    {
        // b requires a different tool (or none): nothing is shared, so a's runs, wear and the hammer's stock equal those of a twin where b requires something else.
        var alone = Runtime(Goods, [A(1, (1, 4)), new RecipeSpec("b", [("coal", 1)], [("tools", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000, ["hammer"] = 3 }, 1_000_000, [new LineSpec("a", 7), new LineSpec("b", 7)])]);
        var other = Runtime(["ore", "coal", "steel", "tools", "hammer", "anvil"], [A(1, (1, 4)), new RecipeSpec("b", [("coal", 1)], [("tools", 1)], 0, false, [("anvil", 1)])],
            [new MarketSpec("m", new() { ["ore"] = 1000, ["coal"] = 1000, ["hammer"] = 3, ["anvil"] = 5 }, 1_000_000, [new LineSpec("a", 7), new LineSpec("b", 7)])]);
        for (var i = 0; i < 4; i++)
        {
            Period(alone); Period(other);
            Assert.Equal(Stock(alone, "m", "hammer"), Stock(other, "m", "hammer"));
            Assert.Equal(Line(alone, "m", "a").LastRuns, Line(other, "m", "a").LastRuns);
            Assert.Equal(Line(alone, "m", "a").Wear[0], Line(other, "m", "a").Wear[0]);
            Assert.Equal(Line(alone, "m", "b").LastRuns, Line(other, "m", "b").LastRuns);
        }
    }

    [Fact]
    public void ThePlanEqualsThePeriodAndTheExplanationNamesWhoHoldsTheTool()
    {
        var runtime = Shared(1, A(), B());
        var text = EconomyExplain.Production(runtime, "m", "b").Text();
        Assert.Contains("requires hammer: 0 (needs 1 free at this line's turn, never used up: stock 1, held by earlier lines this period 1; missing: the earlier lines hold it)", text, StringComparison.Ordinal);
        Assert.Contains("runs b: 0 (of 0 wanted: missing hammer, which the recipe requires free (in stock and not held by a line earlier in the order this period))", text, StringComparison.Ordinal);
        var first = EconomyExplain.Production(runtime, "m", "a").Text();
        Assert.Contains("requires hammer: 1 (needs 1 free at this line's turn, never used up: stock 1, held by earlier lines this period 0; held)", first, StringComparison.Ordinal);
        // The explanation does not change the state.
        var before = runtime.StateDigest();
        _ = EconomyExplain.Production(runtime, "m", "b");
        Assert.Equal(before, runtime.StateDigest());
        // Plan and period agree on every line.
        var plans = runtime.PlanLines(0);
        Period(runtime);
        Assert.Equal(plans.Select(p => p.Runs), new[] { Line(runtime, "m", "a").LastRuns, Line(runtime, "m", "b").LastRuns });
    }

    [Fact]
    public void ACheckListsTheRequirementsThatMoreThanOneLineNeeds()
    {
        var path = Path.Combine(Path.GetTempPath(), "shared-tool-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, Root(Goods, [A(2), B(1)], [new MarketSpec("m", new() { ["hammer"] = 3 }, 1_000_000, [new LineSpec("a", 5), new LineSpec("b", 5)])]).ToJsonString());
        try
        {
            var output = new StringWriter();
            Assert.Equal(0, EconomyCli.Run(["check", path], output, new StringWriter()));
            Assert.Contains("shared requirement m hammer: held by one line at a time, in line order a 2, b 1; the market needs 3 for all of them to run in one period", output.ToString(), StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AFormat5DocumentAndSaveAreRefusedByNameAndSayHowSharedRequirementsChanged()
    {
        var refusal = Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Mini.Text(root => root["format"] = 5)));
        Assert.Equal(EconomyCodes.Format5, refusal.Code);
        Assert.Contains("format 5 is no longer read", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("one line at a time", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(EconomyCodes.Format4, Assert.Throws<EngineRefusedException>(() => EconomyLoader.Load(Mini.Text(root => root["format"] = 4))).Code);
        var runtime = Shared(1, A(), B());
        var node = JsonNode.Parse(EconomySave.Write(runtime))!.AsObject();
        node["Format"] = 5;
        var save = Assert.Throws<EngineRefusedException>(() => EconomySave.Open(runtime.Set, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        Assert.Equal(EconomyCodes.SaveFormat5, save.Code);
        Assert.Contains("format 5 is no longer read", save.Message, StringComparison.Ordinal);
        // A save of the new format round-trips with the same digest and goes on identically.
        Period(runtime);
        var reopened = EconomySave.Open(runtime.Set, EconomySave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Period(runtime); Period(reopened);
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
    }

    /// <summary>
    /// A run-by-run oracle of one period. It keeps the stock of the tool, a holding total, and for each line in order the runs done towards a loss; a run starts if the stock less the holding of earlier lines
    /// covers the line's amount; after it the count grows and on reaching the wear's runs the stock falls by its units (never below nothing). The line then holds its amount less what it lost. Nothing is taken from the engine.
    /// </summary>
    private static (long[] Runs, long Stock, long[] Progress) Oracle(long stock, (long Amount, long Units, long Every, long Progress, long Capacity)[] lines)
    {
        var runs = new long[lines.Length];
        var progress = lines.Select(l => l.Progress).ToArray();
        long held = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var (amount, units, every, _, capacity) = lines[i];
            var before = stock;
            while (runs[i] < capacity && stock - held >= amount)
            {
                runs[i]++;
                if (every > 0 && ++progress[i] == every) { progress[i] = 0; stock = Math.Max(0, stock - units); }
            }
            if (runs[i] > 0) held += Math.Max(0, amount - (before - stock));
        }
        return (runs, stock, progress);
    }

    [Fact]
    public void ThePeriodMatchesARunByRunOracleOnSixHundredRandomMarketsOverTwoPeriods()
    {
        var random = new Random(57);
        for (var trial = 0; trial < 600; trial++)
        {
            var count = random.Next(1, 5);
            var stock = (long)random.Next(0, 9);
            var lines = Enumerable.Range(0, count).Select(_ =>
            {
                var every = random.Next(0, 3) == 0 ? 0 : random.Next(1, 8);
                return (Amount: (long)random.Next(1, 4), Units: (long)random.Next(1, 4), Every: (long)every, Progress: 0L, Capacity: (long)random.Next(1, 13));
            }).ToArray();
            var specs = lines.Select((l, i) => new RecipeSpec("r" + i, [("ore", 1)], [("o" + i, 1)], 0, false, [("hammer", l.Amount)], l.Every > 0 ? new() { ["hammer"] = (l.Units, l.Every) } : null)).ToArray();
            var goods = new[] { "ore", "hammer" }.Concat(lines.Select((_, i) => "o" + i)).ToArray();
            var runtime = Runtime(goods, specs, [new MarketSpec("m", new() { ["ore"] = 1_000_000, ["hammer"] = stock }, 1_000_000, lines.Select((l, i) => new LineSpec("r" + i, l.Capacity)).ToArray())]);
            for (var period = 0; period < 2; period++)
            {
                var expected = Oracle(stock, lines);
                Period(runtime);
                var label = "trial " + trial + " period " + period;
                for (var i = 0; i < count; i++)
                {
                    Assert.True(expected.Runs[i] == Line(runtime, "m", "r" + i).LastRuns, label + " line " + i + " runs " + expected.Runs[i] + " vs " + Line(runtime, "m", "r" + i).LastRuns);
                    if (lines[i].Every > 0) Assert.True(expected.Progress[i] == Line(runtime, "m", "r" + i).Wear[0], label + " line " + i + " progress");
                }
                Assert.True(expected.Stock == Stock(runtime, "m", "hammer"), label + " stock " + expected.Stock + " vs " + Stock(runtime, "m", "hammer"));
                stock = expected.Stock;
                for (var i = 0; i < count; i++) lines[i] = lines[i] with { Progress = expected.Progress[i] };
            }
        }
    }
}
