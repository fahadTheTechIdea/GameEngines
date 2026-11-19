using System.Numerics;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>The shared reducer on hand-worked samples: every expected number below was worked out on paper, not by running the code.</summary>
public sealed class ReducerTests
{
    private static readonly long[] Ten = [5, 1, 4, 2, 3, 10, 7, 9, 8, 6];

    [Fact]
    public void NearestRankPercentilesOfOneToTenAreTheRankedValues()
    {
        var sorted = SampleStats.Sorted(Ten);
        Assert.Equal(1, SampleStats.Percentile(sorted, 10));    // ceil(10 * 10 / 100) = rank 1
        Assert.Equal(5, SampleStats.Percentile(sorted, 50));    // rank 5
        Assert.Equal(9, SampleStats.Percentile(sorted, 90));    // rank 9
        Assert.Equal(10, SampleStats.Percentile(sorted, 99));   // ceil(9.9) = rank 10
        Assert.Equal(10, SampleStats.Percentile(sorted, 100));
        Assert.Equal(30, SampleStats.Percentile(SampleStats.Sorted([30, 10, 20, 40, 50]), 50));  // ceil(2.5) = rank 3 is 30
    }

    [Theory]
    [InlineData(5, 0)]      // P(Bin(5,1/2) = 0) = 1/32 is above 1/40: no 95% interval from five values (the first to the fifth covers 93.75%)
    [InlineData(6, 1)]      // 1/64 <= 1/40; adding C(6,1) = 6 gives 7/64 > 1/40
    [InlineData(10, 2)]     // 1 + 10 = 11 of 1024 <= 25.6, 1 + 10 + 45 = 56 > 25.6
    [InlineData(100, 40)]   // P(Bin(100,1/2) <= 39) = 0.0176 <= 0.025 < P(<= 40) = 0.0284
    public void TheMedianIntervalRankIsTheLargestWhoseTailIsAtMostOneFortieth(int n, int rank) => Assert.Equal(rank, SampleStats.MedianRank(n));

    [Fact]
    public void TheMedianIntervalOfOneToTenIsTwoToNine()
    {
        var interval = SampleStats.MedianInterval(SampleStats.Sorted(Ten));
        Assert.Equal((2L, 9L), interval);
        Assert.Null(SampleStats.MedianInterval(SampleStats.Sorted([1, 2, 3, 4, 5])));
    }

    [Theory]
    [InlineData("5.500", "1,2,3,4,5,6,7,8,9,10")]
    [InlineData("1.333", "1,1,2")]               // 4/3
    [InlineData("1.667", "1,2,2")]               // 5/3 rounds up
    [InlineData("-1.333", "-1,-1,-2")]           // -4/3
    [InlineData("0.500", "0,1")]
    public void TheMeanIsARationalToThreeDecimalsRoundedHalfUp(string expected, string values) => Assert.Equal(expected, SampleStats.Fixed3(SampleStats.MeanMilli(values.Split(',').Select(long.Parse).ToArray())));

    [Fact]
    public void TheMeanRoundsAHalfThousandthUpTowardPlusInfinity()
    {
        var up = new long[2000];
        up[0] = 1;                                   // 1/2000 = 0.0005 -> 0.001
        Assert.Equal("0.001", SampleStats.Fixed3(SampleStats.MeanMilli(up)));
        var down = new long[2000];
        down[0] = -1;                                // -0.0005 -> 0.000 (toward plus infinity)
        Assert.Equal("0.000", SampleStats.Fixed3(SampleStats.MeanMilli(down)));
    }

    [Fact]
    public void TheMeanOfHugeValuesDoesNotOverflow()
    {
        var values = new long[] { long.MaxValue, long.MaxValue, long.MaxValue };
        Assert.Equal(new BigInteger(long.MaxValue) * 1000, SampleStats.MeanMilli(values));
    }

    private static SweepResult Sweep(SweepOptions options, Func<long, IReadOnlyDictionary<string, long>> perSeed) => SeedSweep.Run(options, perSeed);

    private static IReadOnlyDictionary<string, long> Reversed(long seed) =>
        new Dictionary<string, long> { ["b"] = 0, ["a"] = Ten[(int)(seed - 100)] };       // inserted in the wrong order on purpose

    [Fact]
    public void TheReportIsTheHandWorkedLinesInOrdinalNameOrderWithADigestOfThem()
    {
        var options = SweepOptions.Parse(["--seeds", "10", "--from-seed", "100", "12"]);
        var text = SeedSweep.Render("balance test", options, Sweep(options, Reversed));
        var expected =
            "balance test\n" +
            "metric a n 10 min 1 p10 1 p50 5 p90 9 p99 10 max 10 mean 5.500 positive 10 median95 2..9\n" +
            "metric b n 10 min 0 p10 0 p50 0 p90 0 p99 0 max 0 mean 0.000 positive 0 median95 0..0 zero-upper95-ppm 300000\n";
        Assert.Equal(expected + "sweep-digest " + Digest.Of(expected) + "\n", text);
    }

    [Fact]
    public void RowsAreListedByMetricThenSeedAndAreCoveredByTheDigest()
    {
        var options = SweepOptions.Parse(["--seeds", "10", "--from-seed", "100", "--rows", "12"]);
        var text = SeedSweep.Render("h", options, Sweep(options, Reversed));
        var lines = text.Split('\n');
        Assert.Contains("sample a 100 5", lines);
        Assert.Contains("sample a 109 6", lines);
        Assert.True(Array.IndexOf(lines, "sample a 100 5") < Array.IndexOf(lines, "sample a 101 1"));
        Assert.True(Array.IndexOf(lines, "sample a 109 6") < Array.IndexOf(lines, "sample b 100 0"));
        var body = text[..text.LastIndexOf("sweep-digest", StringComparison.Ordinal)];
        Assert.EndsWith("sweep-digest " + Digest.Of(body) + "\n", text);
        var plain = SeedSweep.Render("h", options with { Rows = false }, Sweep(options, Reversed));
        Assert.NotEqual(text, plain);
    }

    [Fact]
    public void TheSameArgumentsGiveTheSameBytesTwice()
    {
        var options = SweepOptions.Parse(["--seeds", "25", "--from-seed", "7", "30"]);
        IReadOnlyDictionary<string, long> Run(long seed) => new Dictionary<string, long> { ["x"] = DeterministicRoll.Below((ulong)seed, "t", 3, 1000), ["y"] = seed % 3 };
        Assert.Equal(SeedSweep.Render("h", options, Sweep(options, Run)), SeedSweep.Render("h", options, Sweep(options, Run)));
    }

    [Fact]
    public void OnlyTheNamedMetricsAreReportedAndAnUnknownOneIsRefusedWithTheList()
    {
        var options = SweepOptions.Parse(["--seeds", "10", "--from-seed", "100", "5", "--metrics", "b"]);
        var text = SeedSweep.Render("h", options, Sweep(options, Reversed));
        Assert.DoesNotContain("metric a ", text, StringComparison.Ordinal);
        Assert.Contains("metric b ", text, StringComparison.Ordinal);
        var bad = SweepOptions.Parse(["--seeds", "2", "--from-seed", "100", "5", "--metrics", "zzz"]);
        var refusal = Assert.Throws<EngineRefusedException>(() => Sweep(bad, Reversed));
        Assert.Equal(BalanceCodes.MetricUnknown, refusal.Code);
        Assert.Contains("a, b", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASeedThatReportsOtherMetricNamesIsRefused()
    {
        var options = SweepOptions.Parse(["--seeds", "3", "5"]);
        var refusal = Assert.Throws<EngineRefusedException>(() => Sweep(options, seed => seed == 1 ? new Dictionary<string, long> { ["a"] = 1, ["c"] = 1 } : new Dictionary<string, long> { ["a"] = 1, ["b"] = 1 }));
        Assert.Equal(BalanceCodes.MetricsDiffer, refusal.Code);
    }

    [Fact]
    public void TheSeedsRunInSeedOrder()
    {
        var order = new List<long>();
        var options = SweepOptions.Parse(["--seeds", "5", "--from-seed", "40", "5"]);
        Sweep(options, seed =>
        {
            order.Add(seed);
            return new Dictionary<string, long> { ["a"] = seed };
        });
        Assert.Equal([40L, 41, 42, 43, 44], order);
    }

    [Fact]
    public void WithSeedChangesOnlyTheSeed()
    {
        var changed = SeedDocument.WithSeed("{\"format\": 2, \"seed\": 5, \"nested\": {\"seed\": 9}}", 77);
        Assert.Equal("{\"format\":2,\"seed\":77,\"nested\":{\"seed\":9}}", changed);
        Assert.Equal(BalanceCodes.DocumentSeed, Assert.Throws<EngineRefusedException>(() => SeedDocument.WithSeed("{\"format\": 2}", 1)).Code);
        Assert.Equal(BalanceCodes.DocumentSeed, Assert.Throws<EngineRefusedException>(() => SeedDocument.WithSeed("[1]", 1)).Code);
        Assert.Equal(BalanceCodes.DocumentSeed, Assert.Throws<EngineRefusedException>(() => SeedDocument.WithSeed("{nope", 1)).Code);
    }
}
