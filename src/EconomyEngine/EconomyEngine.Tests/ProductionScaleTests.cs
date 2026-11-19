using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// What production adds to a period at the scale of <see cref="ScaleTests"/>: 200 markets of 100 goods, plus 30 recipes in three layers of ten (two inputs each, outputs that later layers use; durations 1, 1 and 2)
/// run by 50 of the markets (1,500 lines). The economy with the lines and the same economy without them are lived in turn inside one process, so a loaded machine slows both; the difference is what production costs.
/// A run writes <c>scale-production-*.csv</c> beside the test assembly.
/// </summary>
public sealed class ProductionScaleTests
{
    public const int Recipes = 30;
    public const int ProducingMarkets = 50;

    private static string G(int index) => "g" + index.ToString("000", CultureInfo.InvariantCulture);

    internal static string WithProduction(string mode, bool lines)
    {
        var root = JsonNode.Parse(ScaleTests.BigText(mode))!.AsObject();
        var recipes = new JsonArray();
        JsonNode Amount(int good, int amount) => new JsonObject { ["good"] = G(good), ["amount"] = amount };
        for (var i = 0; i < 10; i++)
        {
            recipes.Add(new JsonObject { ["id"] = "a" + i, ["key"] = "k.a" + i, ["inputs"] = new JsonArray(Amount(2 * i, 2), Amount(2 * i + 1, 1)), ["outputs"] = new JsonArray(Amount(20 + i, 2)), ["duration"] = 1, ["locked"] = false });
            recipes.Add(new JsonObject { ["id"] = "b" + i, ["key"] = "k.b" + i, ["inputs"] = new JsonArray(Amount(20 + i, 2), Amount(40 + i, 1)), ["outputs"] = new JsonArray(Amount(60 + i, 1)), ["duration"] = 1, ["locked"] = false });
            recipes.Add(new JsonObject { ["id"] = "c" + i, ["key"] = "k.c" + i, ["inputs"] = new JsonArray(Amount(60 + i, 1), Amount(80 + i, 1)), ["outputs"] = new JsonArray(Amount(90 + i, 1)), ["duration"] = 2, ["locked"] = false });
        }
        root["recipes"] = recipes;
        var markets = root["markets"]!.AsArray();
        for (var m = 0; m < markets.Count; m++)
        {
            var list = new JsonArray();
            if (lines && m % (ScaleTests.Markets / ProducingMarkets) == 0)
                foreach (var recipe in recipes) list.Add(new JsonObject { ["recipe"] = recipe!["id"]!.GetValue<string>(), ["capacity"] = 40 });
            markets[m]!["lines"] = list;
        }
        return root.ToJsonString();
    }

    private static string Ms(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("direct")]
    [InlineData("landed")]
    public void ThirtyRecipesInFiftyMarketsAddToAPeriodWhatTheCsvSaysAndRunEveryPeriod(string mode)
    {
        var withSet = EconomyLoader.Load(WithProduction(mode, lines: true));
        var withoutSet = EconomyLoader.Load(WithProduction(mode, lines: false));
        Assert.Equal(Recipes * ProducingMarkets, withSet.LineCount);
        Assert.Equal(Recipes, withSet.RecipeOrder.Count);
        var with = EconomyRuntime.Create(withSet);
        var without = EconomyRuntime.Create(withoutSet);
        var facts = Fixture.Host("autumn", 2);
        for (var i = 0; i < 21; i++)
        {
            with.Advance(with.Step + withSet.PeriodSteps, facts);
            without.Advance(without.Step + withoutSet.PeriodSteps, facts);
        }
        var repeats = 15;
        var withTimes = new List<double>();
        var withoutTimes = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            without.Advance(without.Step + withoutSet.PeriodSteps, facts);
            withoutTimes.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            with.Advance(with.Step + withSet.PeriodSteps, facts);
            withTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        withTimes.Sort();
        withoutTimes.Sort();
        long Allocated(EconomyRuntime runtime, EconomySet set)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            runtime.Advance(runtime.Step + set.PeriodSteps, facts);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long allocWith = 0, allocWithout = 0;
        for (var i = 0; i < 5; i++) { allocWithout += Allocated(without, withoutSet); allocWith += Allocated(with, withSet); }
        allocWith /= 5;
        allocWithout /= 5;
        var plans = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var watch = Stopwatch.StartNew();
            for (var m = 0; m < withSet.Markets.Count; m++) with.PlanLines(m);
            plans.Add(watch.Elapsed.TotalMilliseconds);
        }
        plans.Sort();
        byte[] bytes = [];
        var watchSave = Stopwatch.StartNew();
        bytes = EconomySave.Write(with);
        var write = watchSave.Elapsed.TotalMilliseconds;
        watchSave.Restart();
        var reopened = EconomySave.Open(withSet, bytes);
        var open = watchSave.Elapsed.TotalMilliseconds;

        long ran = 0;
        var limited = 0;
        foreach (var market in withSet.Markets)
            foreach (var line in market.Lines.Select((l, i) => i))
            {
                ran += with.Markets[market.Index].Lines[line].LastRuns;
                if (with.Markets[market.Index].Lines[line].Limit != LineLimit.Capacity) limited++;
            }
        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        csv.Append("period without recipes,").Append(Ms(withoutTimes[repeats / 2])).Append(',').Append(Ms(withoutTimes[0])).Append(',').Append(Ms(withoutTimes[^1])).Append(",ms,15\n");
        csv.Append("period with 30 recipes x 50 markets,").Append(Ms(withTimes[repeats / 2])).Append(',').Append(Ms(withTimes[0])).Append(',').Append(Ms(withTimes[^1])).Append(",ms,15\n");
        csv.Append("difference of medians,").Append(Ms(withTimes[repeats / 2] - withoutTimes[repeats / 2])).Append(",,,ms,15\n");
        csv.Append("allocation of a period without,").Append(allocWithout / 1024).Append(",,,KB,5\n");
        csv.Append("allocation of a period with,").Append(allocWith / 1024).Append(",,,KB,5\n");
        csv.Append("plan every line of every market (explain path),").Append(Ms(plans[plans.Count / 2])).Append(',').Append(Ms(plans[0])).Append(',').Append(Ms(plans[^1])).Append(",ms,15\n");
        csv.Append("save write,").Append(Ms(write)).Append(",,,ms,1\n");
        csv.Append("save open,").Append(Ms(open)).Append(",,,ms,1\n");
        csv.Append("save size,").Append(bytes.Length / 1024).Append(",,,KB,1\n");
        csv.Append("batches on their way,").Append(with.Batches.Count).Append(",,,count,1\n");
        csv.Append("runs started last period (all lines),").Append(ran).Append(",,,runs,1\n");
        csv.Append("lines held back by an input or room,").Append(limited).Append(",,,lines,1\n");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "scale-production-" + mode + ".csv"), csv.ToString());

        BoundsTests.Invariants(with);
        Assert.Equal(with.StateDigest(), reopened.StateDigest());
        Assert.NotEmpty(with.Batches);
        Assert.True(ran > 0, "the lines must run, or the measurement measures nothing");
        Assert.True(withTimes[repeats / 2] < (mode == "landed" ? Budgets.LandedPeriodMs : Budgets.PeriodMs), mode + ": a period with production took " + Ms(withTimes[repeats / 2]) + " ms");
        Assert.True(allocWith - allocWithout < 4_000 * 1024, "production allocated " + (allocWith - allocWithout) / 1024 + " KB a period");
    }
}
