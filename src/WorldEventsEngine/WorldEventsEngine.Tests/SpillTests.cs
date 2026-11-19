using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Weather that is correlated through the region graph. The oracles: the tilt worked out by hand for one case; the closed form of a region whose neighbours never change; and, for two linked regions, the stationary
/// distribution of the joint Markov chain found by power iteration in floating point (an independent integration of the same model). The reach of a hazard along the graph is checked against a breadth-first search written
/// again in the test.
/// </summary>
public sealed class SpillTests
{
    private static WorldRuntime World(int[] frequency, int persistence, int spill, string[] regions, (string, string)[] links, bool frozen = false, long seed = 11) =>
        WorldRuntime.Create(WorldLoader.Load(Lab.Weather(seed, frequency, persistence, spill, regions, links, frozen).ToJsonString()));

    // ---- the tilt -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTiltOfOneRedrawIsTheWeightsTimesOnePlusSpillTimesTheNeighboursSurplus()
    {
        var runtime = World([2, 1], 500, 600, ["a", "b"], [("a", "b")]);
        // Weights 2000 and 1000, so the usual shares are 666 and 333 per mille. The one neighbour has weather 0 (share 1000 and 0):
        // tilt of 0 = 1000 + 600 x (1000 - 666) / 1000 = 1200; tilt of 1 = 1000 + 600 x (0 - 333) / 1000 = 801 (-199.8 truncates toward zero).
        Assert.Equal(new long[] { 2000L * 1200, 1000L * 801 }, runtime.RedrawWeights(0, 0, [0, 0]));
        // Neighbour weather 1: 1000 + 600 x (0 - 666) / 1000 = 601 and 1000 + 600 x (1000 - 333) / 1000 = 1400.
        Assert.Equal(new long[] { 2000L * 601, 1000L * 1400 }, runtime.RedrawWeights(0, 0, [0, 1]));
    }

    [Fact]
    public void TheUsualSharesAreWorkedOutFromTheWholeOfTheWeights()
    {
        var runtime = World([3, 1], 500, 600, ["a", "b"], [("a", "b")]);
        // Weights 3000 and 1000: usual shares 750 and 250. Neighbour weather 0: tilt 1000 + 600 x (1000 - 750) / 1000 = 1150 and 1000 + 600 x (0 - 250) / 1000 = 850.
        Assert.Equal(new long[] { 3000L * 1150, 1000L * 850 }, runtime.RedrawWeights(0, 0, [0, 0]));
    }

    [Fact]
    public void ANeighbourhoodThatHasTheZonesOwnClimateTiltsNothing()
    {
        var runtime = World([1, 1], 500, 900, ["a", "b", "c"], [("a", "b"), ("a", "c")]);
        Assert.Equal(new long[] { 1000L * 1000, 1000L * 1000 }, runtime.RedrawWeights(0, 0, [0, 0, 1]));
    }

    [Fact]
    public void WithoutSpillOrWithoutALinkTheWeightsAreTheUntiltedOnes()
    {
        var none = World([2, 1], 500, 0, ["a", "b"], [("a", "b")]);
        Assert.Equal(new long[] { 2000L, 1000L }, none.RedrawWeights(0, 0, [0, 1]));
        var alone = World([2, 1], 500, 900, ["a", "b", "c"], [("b", "c")]);
        Assert.Equal(new long[] { 2000L, 1000L }, alone.RedrawWeights(0, 0, [0, 1, 1]));
        Assert.NotEqual(new long[] { 2000L, 1000L }, alone.RedrawWeights(1, 0, [0, 1, 1]));
    }

    [Fact]
    public void ASpillOfNineHundredNeverTiltsAWeatherThatCanComeToNothingAndNeverRevivesOneThatCannot()
    {
        var random = new Random(4242);
        for (var trial = 0; trial < 300; trial++)
        {
            var frequency = new[] { random.Next(0, 6), random.Next(0, 6) };
            if (frequency.Sum() == 0) frequency[random.Next(2)] = 1;
            var runtime = World(frequency, 500, 900, ["c", "n1", "n2", "n3", "n4"], [("c", "n1"), ("c", "n2"), ("c", "n3"), ("c", "n4")], seed: trial);
            var around = new[] { 0, random.Next(2), random.Next(2), random.Next(2), random.Next(2) };
            var weights = runtime.RedrawWeights(0, 0, around);
            for (var w = 0; w < 2; w++) Assert.Equal(frequency[w] > 0, weights[w] > 0);
            Assert.True(weights.Sum() > 0);
        }
    }

    // ---- what it does to the weather ----------------------------------------------------------------------------------------------------

    private static double ShareOfWeather1AtTheCentre(int spill, int neighbourWeather)
    {
        var runtime = World([2, 1], 500, spill, ["c", "n1", "n2", "n3"], [("c", "n1"), ("c", "n2"), ("c", "n3")], frozen: true);
        foreach (var id in new[] { "n1", "n2", "n3" }) runtime.Regions[runtime.Set.RegionById[id].Index].Weather = neighbourWeather;
        var centre = runtime.Set.RegionById["c"].Index;
        const int steps = 120_000;
        var ones = 0;
        for (var step = 0; step < steps; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (step >= 50 && runtime.Regions[centre].Weather == 1) ones++;
        }
        Assert.All(["n1", "n2", "n3"], id => Assert.Equal(neighbourWeather, runtime.Regions[runtime.Set.RegionById[id].Index].Weather));
        return ones / (double)(steps - 50);
    }

    [Fact]
    public void ARegionWhoseNeighboursNeverChangeSettlesOnTheTiltedShareWhateverItsPersistence()
    {
        // The long-run share of weather 1 equals the chance that a redraw gives it: weights 2 and 1, usual shares 2/3 and 1/3.
        // Neighbours all 1: w0 = 2 x (1 + 0.6 x (0 - 2/3)) = 1.2, w1 = 1 x (1 + 0.6 x (1 - 1/3)) = 1.4, so 1.4 / 2.6.
        Assert.InRange(ShareOfWeather1AtTheCentre(600, 1), 1.4 / 2.6 - 0.012, 1.4 / 2.6 + 0.012);
        // Neighbours all 0: w0 = 2 x (1 + 0.6 x (1 - 2/3)) = 2.4, w1 = 1 x (1 + 0.6 x (0 - 1/3)) = 0.8, so 0.8 / 3.2.
        Assert.InRange(ShareOfWeather1AtTheCentre(600, 0), 0.8 / 3.2 - 0.012, 0.8 / 3.2 + 0.012);
        // No spill: the zone's own 1/3.
        Assert.InRange(ShareOfWeather1AtTheCentre(0, 1), 1.0 / 3 - 0.012, 1.0 / 3 + 0.012);
    }

    /// <summary>The joint chain of two linked regions in floating point: each persists with probability q or draws again with the weights tilted by the other's weather before; the stationary distribution by power iteration.</summary>
    private static (double Agree, double[] MarginalA) Oracle(double[] frequency, double q, double spill)
    {
        var k = frequency.Length;
        var usual = frequency.Select(f => f / frequency.Sum()).ToArray();
        double[] Redraw(int neighbour)
        {
            var w = Enumerable.Range(0, k).Select(i => frequency[i] * (1 + spill * ((i == neighbour ? 1 : 0) - usual[i]))).ToArray();
            return w.Select(x => x / w.Sum()).ToArray();
        }
        var redraw = Enumerable.Range(0, k).Select(Redraw).ToArray();
        var pi = Enumerable.Repeat(1.0 / (k * k), k * k).ToArray();
        for (var iteration = 0; iteration < 3000; iteration++)
        {
            var next = new double[k * k];
            for (var x = 0; x < k; x++)
                for (var y = 0; y < k; y++)
                    for (var x2 = 0; x2 < k; x2++)
                        for (var y2 = 0; y2 < k; y2++)
                        {
                            var px = (x2 == x ? q : 0) + (1 - q) * redraw[y][x2];
                            var py = (y2 == y ? q : 0) + (1 - q) * redraw[x][y2];
                            next[x2 * k + y2] += pi[x * k + y] * px * py;
                        }
            pi = next;
        }
        var agree = Enumerable.Range(0, k).Sum(i => pi[i * k + i]);
        var marginal = Enumerable.Range(0, k).Select(x => Enumerable.Range(0, k).Sum(y => pi[x * k + y])).ToArray();
        return (agree, marginal);
    }

    private static (double Agree, double[] MarginalA) Measure(int spill)
    {
        var runtime = World([2, 1], 500, spill, ["a", "b"], [("a", "b")], seed: 77);
        const int steps = 400_000;
        long agree = 0;
        var inA = new long[2];
        for (var step = 0; step < steps; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (step < 100) continue;
            var a = runtime.Regions[0].Weather;
            agree += a == runtime.Regions[1].Weather ? 1 : 0;
            inA[a]++;
        }
        var n = steps - 100.0;
        return (agree / n, inA.Select(c => c / n).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    [InlineData(900)]
    public void TwoLinkedRegionsAgreeAsOftenAsTheStationaryDistributionOfTheirJointChainSays(int spill)
    {
        var expected = Oracle([2, 1], 0.5, spill / 1000.0);
        var measured = Measure(spill);
        Assert.InRange(measured.Agree, expected.Agree - 0.01, expected.Agree + 0.01);
        Assert.InRange(measured.MarginalA[1], expected.MarginalA[1] - 0.01, expected.MarginalA[1] + 0.01);
    }

    [Fact]
    public void TheCorrelationRisesWithTheSpillAndTheClimateShiftsOnlyAsTheOracleSays()
    {
        var alone = Measure(0);
        var linked = Measure(600);
        Assert.InRange(alone.Agree, 5.0 / 9 - 0.01, 5.0 / 9 + 0.01);              // independent: (2/3)^2 + (1/3)^2
        Assert.True(linked.Agree > alone.Agree + 0.05, "agreement " + alone.Agree + " -> " + linked.Agree);
        Assert.InRange(alone.MarginalA[1], 1.0 / 3 - 0.01, 1.0 / 3 + 0.01);        // no spill: the zone's own climate exactly
        Assert.True(Math.Abs(linked.MarginalA[1] - 1.0 / 3) < 0.04, "the climate moved to " + linked.MarginalA[1]);    // the stated error of the tilt: small, and not zero in general
    }

    [Fact]
    public void TheOrderOfTheRegionsInTheDocumentCannotShow()
    {
        string[] ids = ["a", "b", "c", "d"];
        (string, string)[] links = [("a", "b"), ("b", "c"), ("c", "d"), ("d", "a")];
        var forward = World([1, 1], 300, 700, ids, links, seed: 5);
        var backward = World([1, 1], 300, 700, ids.Reverse().ToArray(), links.Reverse().ToArray(), seed: 5);
        for (var step = 0; step < 400; step++)
        {
            forward.Advance(step, forward.CycleSeasonAt, Fixture.Host());
            backward.Advance(step, backward.CycleSeasonAt, Fixture.Host());
            foreach (var id in ids)
                Assert.Equal(forward.Regions[forward.Set.RegionById[id].Index].Weather, backward.Regions[backward.Set.RegionById[id].Index].Weather);
        }
        Assert.NotEqual(forward.Set.RegionById["a"].Index, backward.Set.RegionById["a"].Index);
    }

    // ---- the reach of a hazard along the graph ------------------------------------------------------------------------------------------

    private static Dictionary<string, int> Hops(string source, (string A, string B)[] links)
    {
        var distance = new Dictionary<string, int> { [source] = 0 };
        var queue = new Queue<string>([source]);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var (a, b) in links)
            {
                var other = a == at ? b : b == at ? a : null;
                if (other is null || distance.ContainsKey(other)) continue;
                distance[other] = distance[at] + 1;
                queue.Enqueue(other);
            }
        }
        return distance;
    }

    [Theory]
    [InlineData(3, 600)]
    [InlineData(2, 600)]
    [InlineData(1, 900)]
    [InlineData(5, 500)]
    [InlineData(5, 1000)]
    [InlineData(5, 100)]           // powers 100, 10, 1, 0, 0: the regions four and five hops away would get a power of nothing and are left alone
    public void AHazardReachesTheRegionsTheShortestWaysSayAtThePowerOfTheirDistance(int hops, int falloff)
    {
        string[] ids = ["r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7", "r8"];
        (string, string)[] links = [("r0", "r1"), ("r1", "r2"), ("r2", "r3"), ("r3", "r4"), ("r4", "r5"), ("r5", "r6"), ("r6", "r7"), ("r0", "r3")];     // r8 is alone
        var root = Lab.Weather(3, [1, 1], 500, 0, ids, links);
        Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = hops, ["falloff"] = falloff };
        Tiny.Hazard(root)["cooldown"] = 0;
        var runtime = WorldRuntime.Create(WorldLoader.Load(root.ToJsonString()));
        Fixture.Live(runtime, 0);
        runtime.BeginHazard("r0", "h", "low");
        var distance = Hops("r0", links);
        var expected = new Dictionary<string, int>();
        foreach (var (id, d) in distance)
        {
            if (d > hops) continue;
            var power = 1000;
            for (var i = 0; i < d; i++) power = power * falloff / 1000;
            if (power > 0) expected[id] = power;
        }
        var actual = runtime.ActiveHazards.ToDictionary(h => runtime.Set.Regions[h.Region].Id, h => h.Power);
        Assert.Equal(expected.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
        Assert.DoesNotContain("r8", actual.Keys);
    }

    // ---- the explanation ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheExplanationOfTheWeatherShowsTheUsualAndTheTiltedShares()
    {
        var runtime = World([2, 1], 500, 600, ["a", "b"], [("a", "b")]);
        runtime.Regions[1].Weather = 0;
        Fixture.Live(runtime, 0);
        runtime.Regions[1].Weather = 0;
        var text = WorldExplain.Weather(runtime, "a");
        var weights = runtime.RedrawWeights(0, 0, runtime.Regions.Select(r => r.Weather).ToArray());
        var total = weights.Sum();
        Assert.Contains("linked to b dry", text, StringComparison.Ordinal);
        Assert.Contains("spill 600 per mille", text, StringComparison.Ordinal);
        Assert.Contains("dry 666 -> " + weights[0] * 1000 / total + ", wet 333 -> " + weights[1] * 1000 / total, text, StringComparison.Ordinal);
        Assert.Equal(2400000L * 1000 / 3201000, weights[0] * 1000 / total);                        // 749, worked by hand
        var none = WorldExplain.Weather(World([2, 1], 500, 0, ["a", "b"], [("a", "b")]), "a");
        Assert.Contains("dry 666, wet 333", none, StringComparison.Ordinal);
        Assert.Contains("the zone lets its neighbours tilt nothing", none, StringComparison.Ordinal);
        var lonely = WorldExplain.Weather(World([2, 1], 500, 600, ["a", "b", "c"], [("b", "c")]), "a");
        Assert.Contains("linked to no region", lonely, StringComparison.Ordinal);
        Assert.Contains("the region has no link to tilt it", lonely, StringComparison.Ordinal);
        Assert.Equal(WorldCodes.UnknownRegion, Fixture.Refusal(() => WorldExplain.Weather(runtime, "nowhere")));
    }
}
