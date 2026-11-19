using System.Globalization;
using System.Text;
using EngineCommon.TailTool;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// The tail tool's checks of the shape of a sample (<see cref="ModalityCheck"/> and <see cref="BoundCheck"/>): a fit across separate clusters is refused (TAIL_MULTIMODAL), a tail the sample cannot call unbounded with a level beyond
/// everything seen is refused (TAIL_BOUNDED), and valid unimodal unbounded input gets exactly the numbers it got before. The samples are built here: quantile grids of known distributions and seeded pseudo-random draws, so every run sees the same data.
/// </summary>
public sealed class TailShapeTests
{
    private static string Rows(IEnumerable<long> values)
    {
        var text = new StringBuilder();
        var seed = 0;
        foreach (var value in values) text.Append("sample m ").Append(seed++).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }

    private static string Propose(IEnumerable<long> values, long[] levels, int percent = 90) => TailCli.Propose(TailSample.Read(Rows(values), "m"), levels, percent, 30);

    private static string RefusalCode(IEnumerable<long> values, long[] levels, int percent = 90) => Assert.Throws<TailRefusedException>(() => Propose(values, levels, percent)).Code;

    private static long[] Sorted(IEnumerable<long> values) => values.OrderBy(v => v).ToArray();

    /// <summary>The (i - 1/2)/n quantile grid of a distribution given by its inverse, as whole numbers.</summary>
    private static IEnumerable<long> Grid(int n, Func<double, double> inverse) => Enumerable.Range(1, n).Select(i => (long)Math.Round(inverse((i - 0.5) / n)));

    private static double Normal(Random random) => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());

    // ---- the step ratio ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void AGapIsWorkedByHand()
    {
        // 50 values 1..50 and 50 values 1001..1050. At the step between 50 and 1001 the window holds 25 values on each side (indices 25 to 74): 48 steps besides the gap, of total (1025 - 26) - 951 = 48, so the typical spacing is 1 and the ratio is 951.
        var sample = Enumerable.Range(1, 50).Select(v => (long)v).Concat(Enumerable.Range(1001, 50).Select(v => (long)v)).ToArray();
        var gap = Assert.Single(ModalityCheck.Gaps(sample));
        Assert.Equal((50L, 1001L, 50, 50), (gap.Low, gap.High, gap.Below, gap.Above));
        Assert.Equal(951.0, gap.Ratio, 9);
    }

    [Fact]
    public void ASingleClusterAndStepsOfOneAreNeverAGap()
    {
        Assert.Empty(ModalityCheck.Gaps(Enumerable.Range(0, 400).Select(v => (long)v).ToArray()));
        Assert.Empty(ModalityCheck.Gaps(Sorted(Enumerable.Repeat(0L, 500).Concat(Enumerable.Repeat(1L, 500)))));       // whole-number data cannot have a gap narrower than 2
        Assert.Empty(ModalityCheck.Gaps(Sorted(Enumerable.Repeat(0L, 500).Concat(Enumerable.Repeat(25L, 500)))));      // a step of 25 is under the limit of 30 times the floor spacing of 1
    }

    [Fact]
    public void TheThinEndOfAHeavyTailIsNotMistakenForASecondCluster()
    {
        // The last few values of a Pareto sample are far apart by nature; the steps within the last max(5, n/50) values are not looked at.
        var heavy = Sorted(Grid(2000, p => 1000 * (Math.Pow(1 - p, -0.5) - 1) / 0.5));
        Assert.Empty(ModalityCheck.Gaps(heavy));
        var lone = Sorted(Enumerable.Range(1, 400).Select(v => (long)v).Append(1_000_000));
        Assert.Empty(ModalityCheck.Gaps(lone));
    }

    [Fact]
    public void SimulatedUnimodalSamplesAreNeverCalledMultimodalAndSeparatedClustersAlwaysAre()
    {
        var random = new Random(20261030);
        var falseAlarms = new List<string>();
        foreach (var n in new[] { 200, 1000, 5000 })
            for (var rep = 0; rep < 150; rep++)
            {
                (string Name, long[] Values)[] unimodal =
                [
                    ("exponential", Sorted(Enumerable.Range(0, n).Select(_ => (long)(-1000 * Math.Log(1 - random.NextDouble()))))),
                    ("pareto 0.5", Sorted(Enumerable.Range(0, n).Select(_ => (long)(1000 * (Math.Pow(1 - random.NextDouble(), -0.5) - 1) / 0.5)))),
                    ("pareto 1", Sorted(Enumerable.Range(0, n).Select(_ => (long)(1000 * (Math.Pow(1 - random.NextDouble(), -1.0) - 1))))),
                    ("lognormal", Sorted(Enumerable.Range(0, n).Select(_ => (long)Math.Exp(6 + Normal(random))))),
                    ("normal", Sorted(Enumerable.Range(0, n).Select(_ => (long)(5000 + 800 * Normal(random))))),
                    ("uniform", Sorted(Enumerable.Range(0, n).Select(_ => (long)(random.NextDouble() * 10_000)))),
                ];
                foreach (var (name, values) in unimodal)
                    foreach (var gap in ModalityCheck.Gaps(values)) falseAlarms.Add(name + " n " + n + " ratio " + gap.Ratio.ToString("F1", CultureInfo.InvariantCulture));
            }
        Assert.True(falseAlarms.Count == 0, "unimodal samples flagged: " + string.Join("; ", falseAlarms.Take(10)) + " (" + falseAlarms.Count + " of 2700)");

        var missed = 0;
        for (var rep = 0; rep < 200; rep++)
        {
            // Two normal clusters 20 standard deviations apart (the larger's 80 to 20 of the whole), whole numbers.
            var mixture = Sorted(Enumerable.Range(0, 800).Select(_ => (long)(1000 + 50 * Normal(random))).Concat(Enumerable.Range(0, 200).Select(_ => (long)(3000 + 50 * Normal(random)))));
            if (ModalityCheck.Gaps(mixture).Count == 0) missed++;
        }
        Assert.Equal(0, missed);
    }

    // ---- what the tool does with them ----------------------------------------------------------------------------------------------

    private static IEnumerable<long> Clusters(int low, int high) =>
        Enumerable.Range(0, low).Select(i => (long)(100 + i % 50)).Concat(Enumerable.Range(0, high).Select(i => (long)(2000 + i % 100)));

    [Fact]
    public void AFitAcrossTwoClustersIsRefusedAndAFitInsideTheUpperOneIsNot()
    {
        // 600 values at 100..149 and 400 at 2000..2099. Threshold at the 50th percentile (149): the values above it are in the upper cluster only if the threshold is 149 or more - here it is the top of the lower cluster, so it straddles nothing and warns.
        Assert.Equal(TailCodes.Multimodal, RefusalCode(Clusters(600, 400), [0, 150, 3000], 20));                 // threshold 100: values from 101 up lie in both clusters
        var edge = Propose(Clusters(600, 400), [0, 150, 2050], 60);                                              // threshold 149, the lower edge of the gap
        Assert.Contains("warning " + TailCodes.Multimodal + ": the sample has a gap between 149 and 2000", edge, StringComparison.Ordinal);
        var inside = Propose(Clusters(600, 400), [0, 2050], 80);                                                 // threshold in the upper cluster: the lower one lies wholly below
        Assert.DoesNotContain(TailCodes.Multimodal, inside, StringComparison.Ordinal);
    }

    [Fact]
    public void ALevelAtOrUnderTheThresholdIsNotCheckedBecauseItIsCountedFromTheSample()
    {
        var text = Propose(Clusters(600, 400), [0, 120, 149], 60);
        Assert.DoesNotContain("warning TAIL_", text, StringComparison.Ordinal);
        Assert.Contains("level 120 tail-ppm ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABoundedSampleRefusesALevelBeyondItsLargestValueAndWarnsInsideIt()
    {
        var uniform = Enumerable.Range(1, 1000).Select(v => (long)v).ToArray();
        var refused = Assert.Throws<TailRefusedException>(() => Propose(uniform, [1, 5000]));
        Assert.Equal(TailCodes.Bounded, refused.Code);
        Assert.Contains("5000", refused.Message, StringComparison.Ordinal);
        var atMax = Propose(uniform, [1, 1000]);                      // a level equal to the largest value is inside the sample: a warning, not a refusal
        Assert.Contains("warning " + TailCodes.Bounded, atMax, StringComparison.Ordinal);
        var fit = PeaksOverThreshold.Fit(uniform, 90, 30);
        var evidence = BoundCheck.Of(PeaksOverThreshold.ExcessesOver(uniform, fit.Threshold), fit);
        Assert.True(evidence.Bounded);
        Assert.InRange(evidence.Endpoint!.Value, 1000, 1003);        // the end the fit implies, for a uniform sample that ends at 1000
        var inside = Propose(uniform, [1, 950]);
        Assert.Contains("warning " + TailCodes.Bounded + ": the tail looks bounded", inside, StringComparison.Ordinal);
        Assert.StartsWith("tail-tool propose", inside, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeavyTailedSampleIsNeverCalledBoundedAndALevelBeyondItsMaximumIsStillProposed()
    {
        var pareto = Grid(20_000, p => 1000 * (Math.Pow(1 - p, -0.25) - 1) / 0.25).ToArray();
        var text = Propose(pareto, [0, 2000, 60_000, 200_000]);                         // 200,000 is beyond the largest of the grid (about 52,600)
        Assert.DoesNotContain("TAIL_BOUNDED", text, StringComparison.Ordinal);
        Assert.Contains("level 200000 tail-ppm ", text, StringComparison.Ordinal);
        var exponential = Propose(Grid(20_000, p => -1000 * Math.Log(1 - p)), [0, 1000, 12_000]);
        Assert.DoesNotContain("warning", exponential, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulatedHeavyTailsAreNotCalledBoundedAndSimulatedBoundedSamplesAre()
    {
        var random = new Random(77);
        var bounded = 0;
        var falselyBounded = 0;
        for (var rep = 0; rep < 60; rep++)
        {
            var heavy = Sorted(Enumerable.Range(0, 2000).Select(_ => (long)(1000 * (Math.Pow(1 - random.NextDouble(), -0.3) - 1) / 0.3)));
            var fitHeavy = PeaksOverThreshold.Fit(heavy, 90, 30);
            if (BoundCheck.Of(PeaksOverThreshold.ExcessesOver(heavy, fitHeavy.Threshold), fitHeavy).Bounded) falselyBounded++;
            var cap = Sorted(Enumerable.Range(0, 2000).Select(_ => (long)(1000 * random.NextDouble())));       // uniform: a hard end at 1000
            var fitCap = PeaksOverThreshold.Fit(cap, 90, 30);
            if (BoundCheck.Of(PeaksOverThreshold.ExcessesOver(cap, fitCap.Threshold), fitCap).Bounded) bounded++;
        }
        Assert.Equal(0, falselyBounded);
        Assert.Equal(60, bounded);
    }

    [Fact]
    public void TheBoundedVerdictIsExactlyNinetySevenAndAHalfPercentOfTheResamples()
    {
        Assert.True(new BoundEvidence(200, 195, null).Bounded);          // 195 / 200 = 97.5%
        Assert.False(new BoundEvidence(200, 194, null).Bounded);
        Assert.False(new BoundEvidence(200, 0, null).Bounded);
    }

    [Fact]
    public void ThePlagueShapeOfTheTwentyTwentySixPassIsDeclinedAtEveryThreshold()
    {
        // The shape of the pass-50 per-outbreak deaths: a body of small counts, a cluster near 500 and a cluster near 1800 that ends at 1950 (the region's whole population).
        var random = new Random(5);
        var deaths = Enumerable.Range(0, 1100).Select(_ => (long)Math.Min(116, -8 * Math.Log(1 - random.NextDouble())))
            .Concat(Enumerable.Range(0, 300).Select(_ => (long)(390 + 225 * random.NextDouble())))
            .Concat(Enumerable.Range(0, 200).Select(_ => (long)(1616 + 334 * random.NextDouble() * random.NextDouble() * 0 + (1950 - 1616) * Math.Pow(random.NextDouble(), 0.5))))
            .ToArray();
        foreach (var percent in new[] { 50, 70, 80 })
            Assert.Equal(TailCodes.Multimodal, RefusalCode(deaths, [1000, 1500, 2000, 2500], percent));
        foreach (var percent in new[] { 90, 95 })
            Assert.Equal(TailCodes.Bounded, RefusalCode(deaths, [1000, 1500, 2000, 2500], percent));
    }
}
