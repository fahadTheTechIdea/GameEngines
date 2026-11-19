using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// Cost: 200 markets that each trade 100 goods (20,000 pairs), 400 routes, three currencies, loans, policies and budgets. The budgets are set from the measurements recorded in docs/ECONOMY_ENGINE.md section 10.4 and
/// docs/measurements (median of 15 repeats on this machine in Release, with a margin for a loaded machine); a budget is a test only for the things that must stay cheap in a game: one period, the first period (the
/// JIT included), a trade, and the allocation of a period. A run writes its numbers to <c>scale-results.csv</c> beside the test assembly.
/// </summary>
public sealed class ScaleTests
{
    public const int Markets = 200;
    public const int Goods = 100;

    internal static string BigText(string mode = "direct")
    {
        var root = Mini.Root();
        root["id"] = "big";
        root["routing"]!["mode"] = mode;
        root["limits"] = new JsonObject { ["maxShipments"] = 100_000, ["maxLoans"] = 64, ["maxShocks"] = 16, ["history"] = 16 };
        root["currencies"] = new JsonArray
        {
            new JsonObject { ["id"] = "crown", ["key"] = "k.crown", ["reference"] = true, ["anchor"] = 1_000_000, ["money"] = 50_000_000, ["growthPpm"] = 500, ["maxRateStepPpm"] = 0 },
            new JsonObject { ["id"] = "florin", ["key"] = "k.florin", ["reference"] = false, ["anchor"] = 500_000, ["money"] = 20_000_000, ["growthPpm"] = 900, ["maxRateStepPpm"] = 5000 },
            new JsonObject { ["id"] = "mark", ["key"] = "k.mark", ["reference"] = false, ["anchor"] = 2_000_000, ["money"] = 20_000_000, ["growthPpm"] = 100, ["maxRateStepPpm"] = 5000 },
        };
        var tags = new[] { "food", "luxury", "material", "fuel", "tool" };
        var goods = new JsonArray();
        for (var g = 0; g < Goods; g++)
        {
            var good = Mini.Good("g" + g.ToString("000", CultureInfo.InvariantCulture));
            good["base"] = 2000 + g * 500;
            good["demandElasticity"] = 400 + g % 7 * 150;
            good["supplyElasticity"] = 500 + g % 5 * 150;
            good["speed"] = 200 + g % 4 * 100;
            good["maxStep"] = 60 + g % 3 * 20;
            good["inertia"] = g % 4 * 100;
            good["decay"] = g % 6;
            good["weight"] = 1 + g % 5;
            good["tags"] = new JsonArray(tags[g % 5]);
            goods.Add(good);
        }
        root["goods"] = goods;
        root["schedules"] = new JsonArray { Mini.Schedule("payday", 7, 0, false), Mini.Schedule("taxday", 30, 0, false), Mini.Schedule("hand", 1, 0, true) };
        root["policies"] = new JsonArray
        {
            new JsonObject { ["id"] = "sales", ["kind"] = "sales", ["rate"] = 80, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "taxday" },
            new JsonObject { ["id"] = "import", ["kind"] = "import", ["rate"] = 100, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "taxday" },
            new JsonObject { ["id"] = "export", ["kind"] = "export", ["rate"] = 40, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "taxday" },
            new JsonObject { ["id"] = "aid", ["kind"] = "subsidy", ["rate"] = 0, ["perUnit"] = 200, ["tags"] = new JsonArray("food"), ["schedule"] = "" },
        };
        root["facts"] = new JsonArray { new JsonObject { ["id"] = "cal.season", ["type"] = "string" }, new JsonObject { ["id"] = "story.plague", ["type"] = "int", ["min"] = 0, ["max"] = 3 } };
        root["modifiers"] = new JsonArray
        {
            new JsonObject { ["id"] = "winter", ["kind"] = "demand", ["good"] = "", ["tag"] = "food", ["permille"] = 200, ["when"] = new JsonObject { ["fact"] = "cal.season", ["op"] = "eq", ["value"] = "winter" } },
            new JsonObject { ["id"] = "sick", ["kind"] = "supply", ["good"] = "", ["tag"] = "", ["permille"] = -200, ["when"] = new JsonObject { ["fact"] = "story.plague", ["op"] = "gte", ["value"] = 2 } },
        };
        root["shocks"] = new JsonArray
        {
            new JsonObject { ["id"] = "drought", ["key"] = "k.drought", ["duration"] = 20, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "supply", ["good"] = "", ["tag"] = "food", ["route"] = "", ["amount"] = -400 } } },
        };
        var markets = new JsonArray();
        for (var m = 0; m < Markets; m++)
        {
            var pairs = new JsonArray();
            for (var g = 0; g < Goods; g++)
            {
                var makes = (m * 7 + g * 13) % 5 == 0;
                var uses = 40 + (m + g) % 9 * 15;
                pairs.Add(Mini.Pair("g" + g.ToString("000", CultureInfo.InvariantCulture), makes ? uses * 3 : uses / 4, uses, uses * 3, 3, 100_000));
            }
            var market = Mini.Market("m" + m.ToString("000", CultureInfo.InvariantCulture), new[] { "crown", "florin", "mark" }[m % 3]);
            market["pairs"] = pairs;
            market["treasury"] = 5_000_000;
            market["staff"] = 100 + m % 50;
            market["policies"] = new JsonArray("sales", "import", "export", "aid");
            market["budget"] = new JsonArray
            {
                new JsonObject { ["id"] = "wages", ["kind"] = "wage", ["schedule"] = "payday", ["amount"] = 0, ["perHead"] = 100, ["indexed"] = true },
                new JsonObject { ["id"] = "roads", ["kind"] = "upkeep", ["schedule"] = "taxday", ["amount"] = 5000, ["perHead"] = 0, ["indexed"] = false },
            };
            markets.Add(market);
        }
        root["markets"] = markets;
        var routes = new JsonArray();
        for (var m = 0; m < Markets; m++)
            foreach (var step in new[] { 1, 17 })
                routes.Add(new JsonObject
                {
                    ["id"] = "r" + m.ToString("000", CultureInfo.InvariantCulture) + "-" + step, ["from"] = "m" + m.ToString("000", CultureInfo.InvariantCulture), ["to"] = "m" + ((m + step) % Markets).ToString("000", CultureInfo.InvariantCulture),
                    ["delay"] = 1 + m % 4, ["capacity"] = 5000, ["costPerWeight"] = 50, ["margin"] = 50, ["share"] = 300,
                });
        root["routes"] = routes;
        var loans = new JsonArray();
        for (var l = 0; l < 20; l++)
            loans.Add(new JsonObject
            {
                ["id"] = "loan" + l, ["lender"] = "world", ["borrower"] = "m" + (l * 3).ToString("000", CultureInfo.InvariantCulture), ["principal"] = 100_000, ["ratePpm"] = 5000, ["term"] = 24, ["kind"] = "annuity", ["schedule"] = "taxday", ["defaultAfter"] = 3,
            });
        root["loans"] = loans;
        return root.ToJsonString();
    }

    private static (double Median, double Min, double Max) Time(int repeats, Action action)
    {
        var times = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        return (times[times.Count / 2], times[0], times[^1]);
    }

    private static string Ms(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("direct")]
    [InlineData("landed")]
    public void TwoHundredMarketsOfAHundredGoodsLivePeriodsSaveAndOpenWithinTheBudgetAndKeepTheirInvariants(string mode)
    {
        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        var watch = Stopwatch.StartNew();
        var text = BigText(mode);
        var set = EconomyLoader.Load(text);
        var load = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(Markets * Goods, set.PairCount);
        Assert.Equal(400, set.Routes.Count);
        var runtime = EconomyRuntime.Create(set);
        var facts = Fixture.Host("autumn", 2);
        watch.Restart();
        runtime.Advance(runtime.Step + set.PeriodSteps, facts);
        var first = watch.Elapsed.TotalMilliseconds;
        for (var i = 0; i < 20; i++) runtime.Advance(runtime.Step + set.PeriodSteps, facts);    // warm up and fill the routes
        var ordinary = Time(15, () => runtime.Advance(runtime.Step + set.PeriodSteps, facts));
        long allocated = 0;
        for (var i = 0; i < 5; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            runtime.Advance(runtime.Step + set.PeriodSteps, facts);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        allocated /= 5;
        var field = Time(15, runtime.ComputeLandedField);
        var trade = Time(15, () => runtime.Trade("m010", "g005", 1));
        byte[] bytes = [];
        var write = Time(5, () => bytes = EconomySave.Write(runtime));
        var open = Time(5, () => EconomySave.Open(set, bytes));
        var digest = Time(5, () => runtime.StateDigest());
        csv.Append("load document,").Append(Ms(load)).Append(',').Append(Ms(load)).Append(',').Append(Ms(load)).Append(",ms,1\n");
        csv.Append("first period,").Append(Ms(first)).Append(',').Append(Ms(first)).Append(',').Append(Ms(first)).Append(",ms,1\n");
        csv.Append("ordinary period,").Append(Ms(ordinary.Median)).Append(',').Append(Ms(ordinary.Min)).Append(',').Append(Ms(ordinary.Max)).Append(",ms,15\n");
        csv.Append("allocation of a period,").Append(allocated / 1024).Append(',').Append(allocated / 1024).Append(',').Append(allocated / 1024).Append(",KB,5\n");
        csv.Append("landed field alone,").Append(Ms(field.Median)).Append(',').Append(Ms(field.Min)).Append(',').Append(Ms(field.Max)).Append(",ms,15\n");
        csv.Append("one trade,").Append(Ms(trade.Median)).Append(',').Append(Ms(trade.Min)).Append(',').Append(Ms(trade.Max)).Append(",ms,15\n");
        csv.Append("save write,").Append(Ms(write.Median)).Append(',').Append(Ms(write.Min)).Append(',').Append(Ms(write.Max)).Append(",ms,5\n");
        csv.Append("save open,").Append(Ms(open.Median)).Append(',').Append(Ms(open.Min)).Append(',').Append(Ms(open.Max)).Append(",ms,5\n");
        csv.Append("state digest,").Append(Ms(digest.Median)).Append(',').Append(Ms(digest.Min)).Append(',').Append(Ms(digest.Max)).Append(",ms,5\n");
        csv.Append("save size,").Append(bytes.Length / 1024).Append(',').Append(bytes.Length / 1024).Append(',').Append(bytes.Length / 1024).Append(",KB,1\n");
        csv.Append("shipments on the way,").Append(runtime.Shipments.Count).Append(',').Append(runtime.Shipments.Count).Append(',').Append(runtime.Shipments.Count).Append(",count,1\n");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "scale-results-" + mode + ".csv"), csv.ToString());

        BoundsTests.Invariants(runtime);
        Assert.NotEmpty(runtime.Shipments);
        Assert.True(ordinary.Median < (mode == "landed" ? Budgets.LandedPeriodMs : Budgets.PeriodMs), mode + " routing: a period took " + Ms(ordinary.Median) + " ms (median of 15)");
        Assert.True(first < Budgets.FirstPeriodMs, "the first period took " + Ms(first) + " ms");
        Assert.True(allocated < Budgets.PeriodKilobytes * 1024, "a period allocated " + allocated / 1024 + " KB");
        Assert.True(trade.Median < Budgets.TradeMs, "a trade took " + Ms(trade.Median) + " ms");
        Assert.True(write.Median < Budgets.WriteMs, "a save took " + Ms(write.Median) + " ms");
        Assert.True(open.Median < Budgets.OpenMs, "opening a save took " + Ms(open.Median) + " ms");
        Assert.Equal(runtime.StateDigest(), EconomySave.Open(set, bytes).StateDigest());
    }
}

/// <summary>The budgets of <see cref="ScaleTests"/>, in one place so the document and the test cannot disagree.</summary>
internal static class Budgets
{
    public const double PeriodMs = 300;

    /// <summary>Landed routing builds a graph per good each period (docs/ECONOMY_ENGINE.md section 11.6): about 1.8 times the direct period in Release, so about twice the budget with the same margin for a loaded machine.</summary>
    public const double LandedPeriodMs = 600;
    public const double FirstPeriodMs = 3000;
    public const long PeriodKilobytes = 20_000;
    public const double TradeMs = 5;
    public const double WriteMs = 500;
    public const double OpenMs = 1000;
}
