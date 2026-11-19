using System.Text.Json.Nodes;
using EconomyEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>
/// Pass 57 (B2): the isles economy was rebalanced with authored numbers only (docs/ECONOMY_ENGINE.md section 17). These tests keep the rebalance from silently regrowing the faults it removed: goods with no producer sitting at
/// their ceiling all year, markets in arrears, and a fair band that holds only a tenth of the pair-periods.
/// </summary>
public sealed class IslesEconomyBalanceTests
{
    private static readonly string[] Seasons = ["spring", "summer", "autumn", "winter"];

    private static (long Fair, long Total, long AtCeiling, long Arrears, long MaxStock) Year(int seed, bool seasons)
    {
        var root = JsonNode.Parse(Samples.Read("isles.economy.json"))!.AsObject();
        root["seed"] = seed;
        var set = EconomyLoader.Load(root.ToJsonString());
        var runtime = EconomyRuntime.Create(set);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["cal.season"] = FactValue.From("spring"), ["story.plague"] = FactValue.From(0L) };
        long fair = 0, total = 0, ceiling = 0, arrears = 0, maxStock = 0;
        for (var period = 0; period < 365; period++)
        {
            if (seasons) facts["cal.season"] = FactValue.From(Seasons[period / 91 % 4]);
            runtime.Advance((period + 1) * set.PeriodSteps, facts);
            foreach (var market in set.Markets)
            {
                if (runtime.InArrears(market.Index)) arrears++;
                foreach (var pair in market.Pairs)
                {
                    total++;
                    if (set.Bands[runtime.Markets[market.Index].Pairs[pair.Index].Band].Id == "fair") fair++;
                    if (runtime.Ratio(market.Index, pair.Index) >= set.Goods[pair.Good].Ceiling) ceiling++;
                    maxStock = Math.Max(maxStock, runtime.Markets[market.Index].Pairs[pair.Index].Stock);
                }
            }
        }
        return (fair, total, ceiling, arrears, maxStock);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void AYearOfTheIslesHasAFairBandWellAboveATenthNoCeilingNoArrearsAndNoRunawayPile(int seed, bool seasons)
    {
        var (fair, total, ceiling, arrears, maxStock) = Year(seed, seasons);
        Assert.True(fair * 100 >= total * 55, "fair band " + fair + " of " + total);   // measured 59 to 77 percent over 30 seeds; before the rebalance 10 to 11 percent
        Assert.Equal(0L, ceiling);                                                      // before: cloth and iron (no producer anywhere) sat at the ceiling in 95 percent of periods
        Assert.Equal(0L, arrears);
        Assert.True(maxStock < 10_000, "largest stock " + maxStock);                    // a warehouse holds 100,000
    }
}
