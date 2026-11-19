using System.Numerics;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>The integer arithmetic of the splitting summary, the plain-run band and the integer square root, each against a value worked by hand.</summary>
public sealed class SplitSummaryTests
{
    private static SplitReport Report(params long[] ppb) =>
        new(2, 100, ppb.Select((p, i) => new SplitReplication(i + 1, [10, 10], 200, p)).ToList());

    [Fact]
    public void TheMeanErrorBandAndMedianAreWorkedOutByHand()
    {
        // 100..600: mean 350; the sum of squares about it is 2 (250^2 + 150^2 + 50^2) = 175,000, over n - 1 = 5 is 35,000, over n = 6 is 5,833.33, rounded up 5,834, root rounded up 77; band 350 +- 154.
        var summary = SplitSummary.Of(Report(100, 200, 300, 400, 500, 600));
        Assert.Equal(350, summary.MeanPpb);
        Assert.Equal(77, summary.SePpb);
        Assert.Equal(196, summary.LowPpb);
        Assert.Equal(504, summary.HighPpb);
        Assert.Equal(300, summary.MedianPpb);                    // nearest rank: ceil(0.5 x 6) = 3
        Assert.Equal((100L, 600L), summary.Median95);            // six values are the smallest sample with a 95% interval: the first to the sixth
        Assert.Equal(100, summary.MinPpb);
        Assert.Equal(600, summary.MaxPpb);
        Assert.Equal(0, summary.Extinct);
        Assert.Equal(6 * 200, summary.Runs);
    }

    [Fact]
    public void TheMeanRoundsHalfUpAndTheBandIsCutAtZeroAndCertainty()
    {
        Assert.Equal(2, SplitSummary.Of(Report(1, 2)).MeanPpb);                  // 1.5 rounds up
        Assert.Equal(0, SplitSummary.Of(Report(0, 0, 0, 0, 0, 100)).LowPpb);      // mean 17, se 17: the low end would be negative
        Assert.Equal(1_000_000_000, SplitSummary.Of(Report(1_000_000_000, 1_000_000_000, 999_999_000)).HighPpb);
        var one = SplitSummary.Of(Report(5));
        Assert.Equal(0, one.SePpb);                              // a single replication has no spread to measure; the report says nothing more than its value
        Assert.Null(one.Median95);
    }

    [Fact]
    public void ThePlainBandIsTwiceTheBinomialErrorRoundedUpAndZeroHitsGetTheRuleOfThree()
    {
        // 4 hits in 100: 40,000,000 ppb; se^2 = 4 x 96 x 10^18 / 10^6 = 3.84 x 10^14, root 19,595,917.9 rounds up to 19,595,918; band 39,191,836.
        Assert.Equal((40_000_000L, 39_191_836L), PlainEstimate.Of(4, 100));
        // 1 hit in 3: se^2 = 2 x 10^18 / 27 = 74,074,074,074,074,074.07 rounds up to ...075 before the root, 272,165,527 after it; twice is 544,331,054 (rounding down at either step gives 544,331,052).
        Assert.Equal((333_333_333L, 544_331_054L), PlainEstimate.Of(1, 3));
        Assert.Equal((0L, 30_000_000L), PlainEstimate.Of(0, 100));              // 3 / 100
        Assert.Equal((0L, 3_000_000_000L / 7 + 1), PlainEstimate.Of(0, 7));       // rounded up
        Assert.Equal((1_000_000_000L, 0L), PlainEstimate.Of(50, 50));
        Assert.Throws<ArgumentException>(() => PlainEstimate.Of(3, 2));
        Assert.Throws<ArgumentException>(() => PlainEstimate.Of(0, 0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(1_000_000, 1000)]
    [InlineData(1_000_001, 1001)]
    public void TheCeilingSquareRootIsTheSmallestRootWhoseSquareIsEnough(long value, long expected) => Assert.Equal(expected, (long)IntegerRoot.CeilingSqrt(value));

    [Fact]
    public void TheRootHoldsForVeryLargeValuesAndRefusesNegativeOnes()
    {
        var big = BigInteger.Pow(10, 40) + 1;
        var root = IntegerRoot.CeilingSqrt(big);
        Assert.True(root * root >= big && (root - 1) * (root - 1) < big);
        Assert.Throws<ArgumentException>(() => IntegerRoot.CeilingSqrt(-1));
    }
}
