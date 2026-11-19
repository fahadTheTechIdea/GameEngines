using System.Numerics;
using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The inputs of one pair of the mini document; the oracle computes one period from them with big integers and no code of the engine.</summary>
internal sealed record PairCase(long Production, long Consumption, long Stock, int Cover, long Capacity, int DemandElasticity, int SupplyElasticity, int Speed, int MaxStep, int Inertia, int Decay, int Floor, int Ceiling);

/// <summary>An independent computation of one period of a pair (no tax, subsidy, modifier, shock, noise or arrears): the same rule, written again with big integers.</summary>
internal static class PriceOracle
{
    private static BigInteger Clamp(BigInteger v, BigInteger lo, BigInteger hi) => v < lo ? lo : v > hi ? hi : v;

    public sealed record Result(long Price, long Stock, long Supply, long Demand, long Met, long Change);

    public static Result Step(PairCase c, long price, long stock, long lastChange, long reference = 10_000, int rmin = 200, int rmax = 3000, int settle = 4)
    {
        BigInteger ratio = (BigInteger)price * 1000 / reference;
        BigInteger sr = Clamp(1000 + (BigInteger)c.SupplyElasticity * (ratio - 1000) / 1000, rmin, rmax);
        BigInteger dr = Clamp(1000 - (BigInteger)c.DemandElasticity * (ratio - 1000) / 1000, rmin, rmax);
        BigInteger supply = (BigInteger)c.Production * sr / 1000;
        BigInteger demand = (BigInteger)c.Consumption * dr / 1000;
        BigInteger held = stock - (BigInteger)stock * c.Decay / 1000;
        BigInteger available = held + supply;
        BigInteger met = BigInteger.Min(demand, available);
        BigInteger newStock = BigInteger.Min(c.Capacity, available - met);
        BigInteger gap = (BigInteger)c.Cover * c.Consumption - newStock;
        BigInteger excess = demand - supply + gap / settle;
        BigInteger norm = BigInteger.Max(1, (demand + supply + 1) / 2);
        BigInteger imbalance = Clamp(excess * 1000 / norm, -1000, 1000);
        BigInteger raw = imbalance * c.Speed / 1000;
        BigInteger smoothed = (BigInteger)lastChange * c.Inertia / 1000 + raw * (1000 - c.Inertia) / 1000;
        BigInteger change = Clamp(smoothed, -c.MaxStep, c.MaxStep);
        BigInteger low = BigInteger.Max(1, (BigInteger)reference * c.Floor / 1000);
        BigInteger high = (BigInteger)reference * c.Ceiling / 1000;
        BigInteger next = Clamp(price * (1000 + change) / 1000, low, BigInteger.Max(low, high));
        return new Result((long)next, (long)newStock, (long)supply, (long)demand, (long)met, (long)change);
    }

    public static void Apply(JsonObject root, PairCase c)
    {
        var good = Mini.TheGood(root);
        good["demandElasticity"] = c.DemandElasticity; good["supplyElasticity"] = c.SupplyElasticity; good["speed"] = c.Speed; good["maxStep"] = c.MaxStep; good["inertia"] = c.Inertia; good["decay"] = c.Decay;
        good["floor"] = c.Floor; good["ceiling"] = c.Ceiling;
        var pair = Mini.ThePair(root);
        pair["production"] = c.Production; pair["consumption"] = c.Consumption; pair["stock"] = c.Stock; pair["cover"] = c.Cover; pair["capacity"] = c.Capacity;
    }
}

public sealed class PriceTests
{
    private static readonly IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue> None = Fixture.NoFacts;

    private static PairCase Random(System.Random random)
    {
        var capacity = random.Next(50, 5000);
        return new PairCase(random.Next(0, 400), random.Next(0, 400), random.Next(0, capacity + 1), random.Next(0, 6), capacity, random.Next(0, 2500), random.Next(0, 2500), random.Next(0, 1001),
            random.Next(1, 400), random.Next(0, 951), random.Next(0, 300), random.Next(1, 1000), random.Next(1000, 6000));
    }

    [Fact]
    public void ThreePeriodsMatchTheIndependentComputationOverTwoHundredRandomPairs()
    {
        var random = new System.Random(20261029);
        for (var n = 0; n < 200; n++)
        {
            var c = Random(random);
            var runtime = Mini.Runtime(root => PriceOracle.Apply(root, c));
            long price = 10_000, stock = c.Stock, last = 0;
            for (var period = 1; period <= 3; period++)
            {
                var expected = PriceOracle.Step(c, price, stock, last);
                runtime.Advance(period, None);
                var state = runtime.Markets[0].Pairs[0];
                var what = "case " + n + " period " + period + " " + c;
                Assert.True(expected.Price == state.Price, what + ": price " + expected.Price + " vs " + state.Price);
                Assert.True(expected.Stock == state.Stock, what + ": stock " + expected.Stock + " vs " + state.Stock);
                Assert.True(expected.Supply == state.Supply && expected.Demand == state.Demand && expected.Met == state.Met, what + ": flows");
                Assert.True(expected.Change == state.LastChange, what + ": change");
                price = expected.Price; stock = expected.Stock; last = expected.Change;
            }
        }
    }

    [Fact]
    public void WhenSupplyMeetsDemandAndStockIsWhatTheMarketKeepsThePriceStays()
    {
        var runtime = Mini.Runtime(root => { Mini.ThePair(root)["production"] = 100; Mini.ThePair(root)["consumption"] = 100; Mini.ThePair(root)["stock"] = 200; Mini.ThePair(root)["cover"] = 2; });
        for (var i = 1; i <= 50; i++)
        {
            runtime.Advance(i, None);
            Assert.Equal(10_000, runtime.Markets[0].Pairs[0].Price);
            Assert.Equal(200, runtime.Markets[0].Pairs[0].Stock);
        }
    }

    [Fact]
    public void ExcessDemandRaisesThePriceAndExcessSupplyLowersIt()
    {
        var scarce = Mini.Runtime(root => { Mini.ThePair(root)["production"] = 10; Mini.ThePair(root)["consumption"] = 100; Mini.ThePair(root)["stock"] = 0; });
        var plenty = Mini.Runtime(root => { Mini.ThePair(root)["production"] = 300; Mini.ThePair(root)["consumption"] = 100; Mini.ThePair(root)["stock"] = 400; });
        long before1 = 10_000, before2 = 10_000;
        for (var i = 1; i <= 4; i++)
        {
            scarce.Advance(i, None); plenty.Advance(i, None);
            Assert.True(scarce.Markets[0].Pairs[0].Price > before1, "scarce " + i + ": " + before1 + " -> " + scarce.Markets[0].Pairs[0].Price);
            Assert.True(plenty.Markets[0].Pairs[0].Price < before2, "plenty " + i + ": " + before2 + " -> " + plenty.Markets[0].Pairs[0].Price);
            before1 = scarce.Markets[0].Pairs[0].Price; before2 = plenty.Markets[0].Pairs[0].Price;
        }
    }

    [Fact]
    public void ThePriceNeverLeavesItsFloorAndCeilingAndNeverMovesMoreThanTheLargestStep()
    {
        var random = new System.Random(77);
        for (var n = 0; n < 60; n++)
        {
            var c = Random(random);
            var runtime = Mini.Runtime(root => PriceOracle.Apply(root, c));
            var previous = 10_000L;
            for (var i = 1; i <= 40; i++)
            {
                runtime.Advance(i, None);
                var price = runtime.Markets[0].Pairs[0].Price;
                Assert.InRange(price, Math.Max(1, 10_000L * c.Floor / 1000), Math.Max(1, 10_000L * c.Floor / 1000) > 10_000L * c.Ceiling / 1000 ? Math.Max(1, 10_000L * c.Floor / 1000) : 10_000L * c.Ceiling / 1000);
                Assert.True(Math.Abs(price - previous) * 1000 <= previous * c.MaxStep + 999, "case " + n + " period " + i + " moved " + previous + " to " + price + " with largest step " + c.MaxStep);
                previous = price;
            }
        }
    }

    [Fact]
    public void AScarceGoodRisesByTheLargestStepEveryPeriodUntilItsCeiling()
    {
        var runtime = Mini.Runtime(root =>
        {
            var good = Mini.TheGood(root); good["speed"] = 1000; good["maxStep"] = 100; good["inertia"] = 0; good["ceiling"] = 1500; good["demandElasticity"] = 0;
            var pair = Mini.ThePair(root); pair["production"] = 0; pair["consumption"] = 100; pair["stock"] = 0;
        });
        long price = 10_000;
        for (var i = 1; i <= 8; i++)
        {
            runtime.Advance(i, None);
            price = Math.Min(15_000, price * 1100 / 1000);
            Assert.Equal(price, runtime.Markets[0].Pairs[0].Price);
        }
        Assert.Equal(15_000, runtime.Markets[0].Pairs[0].Price);
    }

    [Fact]
    public void InertiaCarriesTheLastChangeIntoTheNext()
    {
        PairCase Fast(int inertia) => new(0, 100, 0, 0, 1000, 0, 0, 300, 1000, inertia, 0, 100, 4000);
        var smooth = Mini.Runtime(root => PriceOracle.Apply(root, Fast(600)));
        var quick = Mini.Runtime(root => PriceOracle.Apply(root, Fast(0)));
        smooth.Advance(1, None); quick.Advance(1, None);
        Assert.True(smooth.Markets[0].Pairs[0].LastChange < quick.Markets[0].Pairs[0].LastChange, smooth.Markets[0].Pairs[0].LastChange + " vs " + quick.Markets[0].Pairs[0].LastChange);
        var first = smooth.Markets[0].Pairs[0].LastChange;
        smooth.Advance(2, None);
        Assert.True(smooth.Markets[0].Pairs[0].LastChange > first, "the second change carries the first and grows");
    }

    [Fact]
    public void TheResponseToPriceIsBoundedByTheDocument()
    {
        // A price jump to the ceiling: demand responds at most down to responseMin (200 per mille), supply up to responseMax (3000).
        var runtime = Mini.Runtime(root =>
        {
            root["shocks"] = new JsonArray { new JsonObject { ["id"] = "spike", ["key"] = "k.s", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = 9000 } } } };
            var good = Mini.TheGood(root); good["demandElasticity"] = 5000; good["supplyElasticity"] = 5000; good["ceiling"] = 4000; good["speed"] = 0;
            var pair = Mini.ThePair(root); pair["production"] = 100; pair["consumption"] = 100; pair["stock"] = 0;
        });
        runtime.ApplyShock("spike", "", 1000);
        Assert.Equal(40_000, runtime.Markets[0].Pairs[0].Price);
        runtime.Advance(1, None);
        Assert.Equal(300, runtime.Markets[0].Pairs[0].Supply);
        Assert.Equal(20, runtime.Markets[0].Pairs[0].Demand);
    }

    [Fact]
    public void StockSpoilsAndTheWarehouseHoldsNoMoreThanItsCapacity()
    {
        var spoil = Mini.Runtime(root => { Mini.TheGood(root)["decay"] = 100; var p = Mini.ThePair(root); p["production"] = 0; p["consumption"] = 0; p["stock"] = 1000; });
        spoil.Advance(1, None);
        Assert.Equal(900, spoil.Markets[0].Pairs[0].Stock);
        spoil.Advance(2, None);
        Assert.Equal(810, spoil.Markets[0].Pairs[0].Stock);
        var full = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 500; p["consumption"] = 0; p["stock"] = 90; p["capacity"] = 100; });
        full.Advance(1, None);
        Assert.Equal(100, full.Markets[0].Pairs[0].Stock);
    }

    [Fact]
    public void DemandAboveWhatIsAvailableIsShortAndThePairSaysSoUntilItIsMetAgain()
    {
        var runtime = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 0; p["consumption"] = 100; p["stock"] = 150; });
        var changes = runtime.Advance(1, None);
        Assert.False(runtime.Markets[0].Pairs[0].Short);
        Assert.Empty(changes.Shortages);
        changes = runtime.Advance(2, None);
        Assert.True(runtime.Markets[0].Pairs[0].Short);
        Assert.Equal(new ShortageChanged(0, 0, true), Assert.Single(changes.Shortages));
        Assert.True(runtime.Markets[0].Pairs[0].Met < runtime.Markets[0].Pairs[0].Demand);
    }

    [Fact]
    public void TheSeededNoiseMovesDemandInsideItsBoundAndOnlyWhenAsked()
    {
        var quiet = Mini.Runtime(root => { Mini.ThePair(root)["production"] = 100; });
        var loud = Mini.Runtime(root => { Mini.ThePrice(root)["noise"] = 200; Mini.ThePair(root)["production"] = 100; });
        var other = Mini.Runtime(root => { root["seed"] = 8; Mini.ThePrice(root)["noise"] = 200; Mini.ThePair(root)["production"] = 100; });
        var differs = false;
        var seedMatters = false;
        for (var i = 1; i <= 20; i++)
        {
            quiet.Advance(i, None); loud.Advance(i, None); other.Advance(i, None);
            var q = quiet.Markets[0].Pairs[0]; var l = loud.Markets[0].Pairs[0]; var o = other.Markets[0].Pairs[0];
            Assert.InRange(l.Demand * 1000 / Math.Max(1, q.Demand), 0, 100_000);
            differs |= l.Demand != q.Demand;
            seedMatters |= l.Demand != o.Demand;
        }
        Assert.True(differs, "noise changes demand");
        Assert.True(seedMatters, "the seed changes the noise");
        var again = Mini.Runtime(root => { Mini.ThePrice(root)["noise"] = 200; Mini.ThePair(root)["production"] = 100; });
        for (var i = 1; i <= 20; i++) again.Advance(i, None);
        Assert.Equal(loud.StateDigest(), again.StateDigest());
    }

    [Fact]
    public void TheNoiseIsExactlyTheRollOfTheSeedForThePeriodAndThePair()
    {
        // Price stays at its reference in period 1 (speed 0), so demand = consumption * response(1000) * (1000 + noise) / 1000 with noise = roll - 200.
        var runtime = Mini.Runtime(root => { Mini.ThePrice(root)["noise"] = 200; Mini.TheGood(root)["speed"] = 0; Mini.ThePair(root)["consumption"] = 1000; });
        runtime.Advance(1, None);
        var roll = EngineCommon.DeterministicRoll.Below(7, "demand", 1L * 1 + 0 + 0, 401) - 200;
        Assert.Equal(1000 * (1000 + roll) / 1000, runtime.Markets[0].Pairs[0].Demand);
    }

    [Fact]
    public void ThePriceHistoryKeepsTheLastPeriodsOldestFirst()
    {
        var runtime = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 10; p["consumption"] = 100; p["stock"] = 0; });
        var seen = new List<long>();
        for (var i = 1; i <= 12; i++)
        {
            runtime.Advance(i, None);
            seen.Add(runtime.Markets[0].Pairs[0].Price);
            var history = runtime.PriceHistory(0, 0);
            Assert.Equal(seen.TakeLast(8).ToList(), history);
        }
    }

    [Fact]
    public void BandsFollowThePriceWithAMarginBeforeTheyGoDown()
    {
        var runtime = Mini.Runtime(root => { Mini.ThePrice(root)["bandMargin"] = 50; });
        var set = runtime.Set;
        Assert.Equal(1, runtime.BandOf(900, 0));
        Assert.Equal(1, runtime.BandOf(1099, 1));
        Assert.Equal(2, runtime.BandOf(1100, 1));
        Assert.Equal(2, runtime.BandOf(1050, 2));    // inside the margin below its start: stays
        Assert.Equal(1, runtime.BandOf(1049, 2));    // under it by more than the margin: down
        Assert.Equal(0, runtime.BandOf(10, 2));
        Assert.Equal(1, runtime.BandOf(850, 1));
        Assert.Equal(0, runtime.BandOf(849, 1));
        Assert.Equal(3, set.Bands.Count);
    }

    [Fact]
    public void ABandNeverSitsBelowThePlainBandNorMoreThanTheMarginAboveIt()
    {
        var runtime = Mini.Runtime(root => { Mini.ThePrice(root)["bandMargin"] = 40; });
        var random = new System.Random(5);
        var band = 0;
        for (var i = 0; i < 9000; i++)
        {
            var ratio = Math.Clamp(random.Next(0, 1400), 0, 1400);
            band = runtime.BandOf(ratio, band);
            var plain = ratio >= 1100 ? 2 : ratio >= 900 ? 1 : 0;
            Assert.True(band >= plain, ratio + " is in band " + band + ", below its plain band " + plain);
            if (band > plain) Assert.True(ratio >= runtime.Set.Bands[band].From - 40, ratio + " is held in band " + band + " by more than the margin");
        }
    }

    [Fact]
    public void ABandChangeIsReportedWithTheBandsItWentBetween()
    {
        var runtime = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 0; p["consumption"] = 100; p["stock"] = 0; Mini.TheGood(root)["speed"] = 1000; Mini.TheGood(root)["maxStep"] = 100; });
        var changes = runtime.Advance(1, None);
        Assert.Equal(new BandChanged(0, 0, 1, 2), Assert.Single(changes.Bands));
    }

    [Fact]
    public void SplittingAnAdvanceIntoSeveralCallsGivesTheSameState()
    {
        var whole = Fixture.Runtime();
        var split = Fixture.Runtime();
        var step = Fixture.Town.PeriodSteps;
        whole.Advance(40 * step, Fixture.Spring);
        foreach (var n in new[] { 3, 1, 10, 7, 19 }) split.Advance(split.Step + n * step, Fixture.Spring);
        Assert.Equal(whole.StateDigest(), split.StateDigest());
        // A call inside a period moves the step and nothing else.
        var inside = Fixture.Runtime();
        var digest = inside.StateDigest();
        inside.Advance(5, Fixture.NoFacts);
        Assert.Equal(5, inside.Step);
        Assert.Equal(0, inside.Period);
        Assert.NotEqual(digest, inside.StateDigest());
        var a = Fixture.Runtime(); var b = Fixture.Runtime();
        a.Advance(step * 3, Fixture.Spring);
        b.Advance(5, Fixture.Spring); b.Advance(step * 3, Fixture.Spring);
        Assert.Equal(a.StateDigest(), b.StateDigest());
    }

    [Fact]
    public void AtMostSixtyFourPeriodsAreLivedInOneCallAndTheRestIsOwed()
    {
        var runtime = Fixture.Runtime();
        var step = Fixture.Town.PeriodSteps;
        runtime.Advance(100 * step + 5, Fixture.Spring);
        Assert.Equal(64, runtime.Period);
        Assert.Equal(64 * step, runtime.Step);
        Assert.Equal(36, runtime.Owed(100 * step + 5));
        runtime.Advance(100 * step + 5, Fixture.Spring);
        Assert.Equal(100, runtime.Period);
        Assert.Equal(100 * step + 5, runtime.Step);
        Assert.Equal(0, runtime.Owed(100 * step + 5));
    }

    [Fact]
    public void ABuyRemovesStockAndCountsAsDemandSoThePriceEndsHigherThanWithoutIt()
    {
        var with = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 100; p["consumption"] = 100; p["stock"] = 200; p["cover"] = 2; });
        var without = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 100; p["consumption"] = 100; p["stock"] = 200; p["cover"] = 2; });
        var trade = with.Trade("m", "g", 50);
        Assert.Equal(150, with.Markets[0].Pairs[0].Stock);
        Assert.Equal(50, with.Markets[0].Pairs[0].SpotDemand);
        with.Advance(1, None); without.Advance(1, None);
        Assert.True(with.Markets[0].Pairs[0].Price > without.Markets[0].Pairs[0].Price);
        Assert.Equal(0, with.Markets[0].Pairs[0].SpotDemand);
        Assert.Equal(500, trade.Value);
    }
}
