using System.Numerics;
using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Money: growth and the price level (the quantity theory in growth form), rates that follow purchasing power parity by a bounded step, pegs, and devaluation.</summary>
public sealed class CurrencyTests
{
    private static JsonObject Currency(string id, bool reference, long anchor, long money, long growth, long step) => new()
    {
        ["id"] = id, ["key"] = "k." + id, ["reference"] = reference, ["anchor"] = anchor, ["money"] = money, ["growthPpm"] = growth, ["maxRateStepPpm"] = step,
    };

    private static EconomyRuntime Growing(long growth, long money = 1_000_000)
    {
        return Mini.Runtime(root => root["currencies"]!.AsArray()[0] = Currency("c", true, 1_000_000, money, growth, 0));
    }

    [Fact]
    public void GrowthRaisesThePriceLevelByThatMuchEachPeriodAndMintsThatMuchIntoTheWorldAccount()
    {
        var runtime = Growing(10_000);
        long level = 1_000_000, money = 1_000_000, minted = 0;
        for (var period = 1; period <= 12; period++)
        {
            runtime.Advance(period, Fixture.NoFacts);
            level = (long)((BigInteger)level * 1_010_000 + 500_000) / 1_000_000;
            var mint = (long)(((BigInteger)money * 10_000 + 500_000) / 1_000_000);
            money += mint; minted += mint;
            Assert.Equal(level, runtime.Currencies[0].Level);
            Assert.Equal(money, runtime.Currencies[0].Money);
            Assert.Equal(minted, runtime.Currencies[0].Minted);
            Assert.Equal(minted, runtime.Currencies[0].World);
        }
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void BalancedMarketsFollowTheLevelSoPricesRiseWithTheMoneyAndTheirRatioHolds()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["currencies"]!.AsArray()[0] = Currency("c", true, 1_000_000, 1_000_000, 20_000, 0);
            var pair = Mini.ThePair(root);
            pair["production"] = 100; pair["consumption"] = 100; pair["stock"] = 100; pair["cover"] = 1;
        });
        for (var period = 1; period <= 30; period++)
        {
            runtime.Advance(period, Fixture.NoFacts);
            Assert.InRange(runtime.Ratio(0, 0), 995, 1005);
        }
        Assert.True(runtime.Markets[0].Pairs[0].Price > 17_000, "after 30 periods of 2 percent growth prices are about 1.81 times higher, here " + runtime.Markets[0].Pairs[0].Price);
        Assert.Equal(runtime.ReferencePrice(0, runtime.Set.Goods[0]), (long)((BigInteger)10_000 * runtime.Currencies[0].Level / 1_000_000));
    }

    [Fact]
    public void NegativeGrowthDeflatesAndMoneyNeverFallsBelowZero()
    {
        var runtime = Growing(-500_000, 1000);
        for (var period = 1; period <= 20; period++)
        {
            runtime.Advance(period, Fixture.NoFacts);
            Assert.True(runtime.Currencies[0].Money >= 0);
            Assert.True(runtime.Currencies[0].Level >= 1);
        }
        Assert.True(runtime.Currencies[0].Level < 10);
        Assert.Equal(runtime.Currencies[0].Minted, runtime.Currencies[0].World);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void RunawayInflationStopsAtTheCeilingOfThePriceLevel()
    {
        var runtime = Growing(500_000);
        for (var period = 1; period <= 70; period++) runtime.Advance(period, Fixture.NoFacts);
        Assert.Equal(EconomyFacts.MaxLevel, runtime.Currencies[0].Level);
        Assert.True(runtime.Markets[0].Pairs[0].Price <= EconomyFacts.MaxPrice);
    }

    private static string TwoCurrencies(long growthD, long step, long anchor = 500_000, long growthC = 0) => Mini.Text(root =>
    {
        root["currencies"]!.AsArray()[0] = Currency("c", true, 1_000_000, 1_000_000, growthC, 0);
        root["currencies"]!.AsArray().Add(Currency("d", false, anchor, 1_000_000, growthD, step));
        root["markets"]!.AsArray().Add(Mini.Market("n", "d"));
        root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "devalue", ["key"] = "k.d", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "rate", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = -300 } } });
    });

    [Fact]
    public void ARateFollowsPurchasingPowerParityByAtMostItsStepEachPeriod()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 20_000, step: 5000)));
        BigInteger levelC = 1_000_000, levelD = 1_000_000;
        long rate = 500_000;
        for (var period = 1; period <= 25; period++)
        {
            var changes = runtime.Advance(period, Fixture.NoFacts);
            levelD = (levelD * 1_020_000 + 500_000) / 1_000_000;
            var target = (long)((500_000 * levelC * 2 + levelD) / (2 * levelD));      // round(anchor * levelC / levelD)
            var room = (long)((BigInteger)rate * 5000 / 1_000_000);
            var expected = Math.Clamp(target, rate - room, rate + room);
            Assert.Equal(expected, runtime.Currencies[1].Rate);
            if (expected != rate) Assert.Equal(new RateChanged(1, rate, expected), Assert.Single(changes.Rates));
            else Assert.Empty(changes.Rates);
            Assert.True(runtime.Currencies[1].Rate <= rate);
            rate = expected;
        }
        Assert.True(rate < 500_000);
        Assert.Equal(1_000_000, runtime.Currencies[0].Rate);
    }

    [Fact]
    public void ARateThatIsPeggedNeverMovesAndTheReferenceNeverDoes()
    {
        var pegged = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 50_000, step: 0, growthC: 30_000)));
        for (var period = 1; period <= 20; period++) pegged.Advance(period, Fixture.NoFacts);
        Assert.Equal(500_000, pegged.Currencies[1].Rate);
        Assert.Equal(1_000_000, pegged.Currencies[0].Rate);
        Assert.True(pegged.Currencies[0].Level > 1_000_000);
    }

    [Fact]
    public void ADevaluationCutsTheRateAtOnceAndParityThenPullsItBackAtItsStep()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 0, step: 10_000)));
        var changes = runtime.ApplyShock("devalue", "n", 1000);
        Assert.Equal(350_000, runtime.Currencies[1].Rate);
        Assert.Equal(new RateChanged(1, 500_000, 350_000), Assert.Single(changes.Rates));
        Assert.Equal(new ShockChanged(0, 1, true), Assert.Single(changes.Shocks));
        long rate = 350_000;
        for (var period = 1; period <= 5; period++)
        {
            runtime.Advance(period, Fixture.NoFacts);
            var expected = Math.Min(500_000, rate + rate * 10_000 / 1_000_000);
            Assert.Equal(expected, runtime.Currencies[1].Rate);
            rate = expected;
        }
    }

    [Fact]
    public void ADevaluationOfEveryMarketSkipsTheReferenceAndOneAimedAtTheReferenceIsRefused()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 0, step: 0)));
        runtime.ApplyShock("devalue", "", 1000);
        Assert.Equal(1_000_000, runtime.Currencies[0].Rate);
        Assert.Equal(350_000, runtime.Currencies[1].Rate);
        var digest = runtime.StateDigest();
        Assert.Equal(EconomyCodes.Shock, Fixture.Refusal(() => runtime.ApplyShock("devalue", "m", 1000)));
        Assert.Equal(digest, runtime.StateDigest());
    }

    [Fact]
    public void AScaleOfHalfDevaluesByHalfAsMuch()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 0, step: 0)));
        runtime.ApplyShock("devalue", "n", 500);
        Assert.Equal(425_000, runtime.Currencies[1].Rate);      // -300 * 500 / 1000 = -150 per mille
    }

    [Fact]
    public void ACurrencyWorthLessThanTheReferenceHasHigherPricesInItsOwnUnits()
    {
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies(growthD: 0, step: 0, anchor: 250_000)));
        Assert.Equal(40_000, runtime.Markets[1].Pairs[0].Price);
        Assert.Equal(10_000, runtime.Markets[0].Pairs[0].Price);
        Assert.Equal(40_000, runtime.ReferencePrice(1, runtime.Set.Goods[0]));
    }

    [Fact]
    public void TheCentralBanksGrowthDecisionTakesEffectFromTheNextPeriod()
    {
        var runtime = Growing(0);
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(1_000_000, runtime.Currencies[0].Level);
        runtime.SetGrowth("c", 50_000);
        Assert.Equal(1_000_000, runtime.Currencies[0].Level);
        runtime.Advance(2, Fixture.NoFacts);
        Assert.Equal(1_050_000, runtime.Currencies[0].Level);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.SetGrowth("c", 500_001)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.SetGrowth("c", -500_001)));
        Assert.Equal(EconomyCodes.UnknownCurrency, Fixture.Refusal(() => runtime.SetGrowth("zz", 1)));
    }

    [Fact]
    public void TheQuantityTheoryHoldsInGrowthFormPricesGrowAtTheRateOfMoney()
    {
        // With velocity and output held (a balanced market), the price level grows at the money growth: after n periods both are (1 + g)^n of the start, to rounding.
        var runtime = Growing(7000);
        for (var period = 1; period <= 50; period++) runtime.Advance(period, Fixture.NoFacts);
        var money = runtime.Currencies[0].Money;       // both started at 1,000,000
        var level = runtime.Currencies[0].Level;
        Assert.True(Math.Abs(money - level) * 1000 <= level, "money " + money + " and level " + level + " differ by more than a thousandth");
    }
}
