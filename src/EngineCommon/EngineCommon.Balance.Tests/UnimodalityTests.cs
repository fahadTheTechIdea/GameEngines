using System.Globalization;
using System.Text;
using EngineCommon.TailTool;
using EngineCommon.TestSupport;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// Hartigan's dip test (<see cref="DipTest"/>), Silverman's critical-bandwidth test (<see cref="SilvermanTest"/>) and the tool's <c>modality</c> report. The dip statistic is checked against values from an independent exact linear program (the
/// smallest D for which a unimodal distribution function lies within D of the empirical one, solved in rational arithmetic outside this repository for each position of the mode; the values below are its optimum), then by properties; the p-values and
/// the calibration (false alarms on unimodal samples, power on mixtures, next to the gap test) are measured on seeded simulated samples. The full calibration is in docs/WORLD_EVENTS_ENGINE.md section 16; these are its smaller, faster forms.
/// </summary>
public sealed class UnimodalityTests
{
    private static double Normal(Random random) => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());

    // ---- the dip statistic --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 1, 4, 16, 26 }, 3, 22)]
    [InlineData(new[] { 0, 1, 3, 76, 78 }, 73, 375)]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, 1, 12)]
    [InlineData(new[] { 2, 4, 6, 13, 16, 17, 22, 94, 99 }, 7, 66)]
    [InlineData(new[] { 0, 2, 5, 9, 28, 35, 47, 82, 98 }, 1, 12)]
    [InlineData(new[] { 2, 4, 5, 11, 16, 20 }, 1, 10)]
    [InlineData(new[] { 0, 3, 9, 13, 16, 18, 57, 86 }, 1, 12)]
    [InlineData(new[] { 3, 5, 12, 18, 19, 31, 93, 95 }, 31, 256)]
    [InlineData(new[] { 1, 2, 3, 50, 51, 52, 53 }, 141, 686)]
    [InlineData(new[] { 5, 6, 7, 8, 30, 31, 32, 33, 34 }, 44, 225)]
    public void TheDipEqualsTheOptimumOfTheIndependentLinearProgram(int[] values, int numerator, int denominator)
    {
        var dip = DipTest.Of(values.Select(v => (double)v).ToArray());
        Assert.Equal((double)numerator / denominator, dip.Dip, 12);
        Assert.Equal(values.Length, dip.Samples);
    }

    [Fact]
    public void AnEvenlySpacedSampleHasTheSmallestDipAndTheDipIsUnchangedByShiftAndScale()
    {
        foreach (var n in new[] { 4, 10, 57 })
            Assert.Equal(1.0 / (2 * n), DipTest.Of(Enumerable.Range(0, n).Select(i => 3.0 * i + 5).ToArray()).Dip, 15);
        var sample = new[] { 0.0, 1, 3, 4, 9, 30, 31, 32, 35, 90 };
        var moved = sample.Select(v => 7.5 * v - 40).ToArray();
        Assert.Equal(DipTest.Of(sample).Dip, DipTest.Of(moved).Dip, 12);
        Assert.True(DipTest.Of(new[] { 0.0, 1, 2, 3, 100, 101, 102, 103 }).Dip > DipTest.Of(new[] { 0.0, 1, 2, 3, 4, 5, 6, 7 }).Dip);
    }

    [Fact]
    public void TheDipRefusesWhatItCannotMeasure()
    {
        Assert.Throws<ArgumentException>(() => DipTest.Of(new[] { 1.0, 2, 3 }));
        Assert.Throws<ArgumentException>(() => DipTest.Of(new[] { 1.0, 2, 2, 3, 4 }));
        Assert.Throws<ArgumentException>(() => DipTest.Of(new[] { 1.0, 3, 2, 4 }));
    }

    [Fact]
    public void TiedWholeNumbersAreSpreadEvenlyOverTheirCells()
    {
        var spread = DipTest.Spread([5, 5, 5, 7, 9, 9]);
        Assert.Equal([4.5 + 0.5 / 3, 4.5 + 1.5 / 3, 4.5 + 2.5 / 3, 7.0, 8.75, 9.25], spread);
        for (var i = 1; i < spread.Length; i++) Assert.True(spread[i] > spread[i - 1]);
    }

    // ---- the p-value ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheNullIsFixedSortedAndScalesAsTheDipsOfUniformSamplesDo()
    {
        var first = DipTest.NullDistribution(200);
        Assert.Equal(DipTest.NullSamples, first.Length);
        Assert.Equal(first.OrderBy(d => d).ToArray(), first);
        Assert.Same(first, DipTest.NullDistribution(200));
        // The dip of a uniform sample is about 0.5 / sqrt(n) at its 95th percentile (Hartigan and Hartigan's tables have the same scale): 0.52 at n = 200 here.
        Assert.InRange(Math.Sqrt(200) * first[949], 0.4, 0.65);
        Assert.InRange(Math.Sqrt(1000) * DipTest.NullDistribution(1000)[949], 0.4, 0.65);
        Assert.All(first, d => Assert.True(d >= 1.0 / 400));
    }

    [Fact]
    public void ABimodalSampleIsRejectedAUnimodalOneIsNotAndTheVerdictRepeats()
    {
        var random = new Random(7);
        var normal = Enumerable.Range(0, 400).Select(_ => (long)Math.Round(5000 + 800 * Normal(random))).OrderBy(v => v).ToArray();
        var bimodal = Enumerable.Range(0, 400).Select(i => (long)Math.Round((i % 2 == 0 ? 5000 : 10_000) + 800 * Normal(random))).OrderBy(v => v).ToArray();
        var unimodal = DipTest.Test(normal);
        var two = DipTest.Test(bimodal);
        Assert.True(unimodal.PValue > 0.5, "p " + unimodal.PValue);
        Assert.True(two.PValue <= 0.002, "p " + two.PValue);
        Assert.True(two.Rejects(0.01));
        Assert.False(unimodal.Rejects(0.05));
        Assert.Equal(two, DipTest.Test(bimodal));
        Assert.Equal(DipTest.NullSamples, two.Resamples);
        // The p-value counts the sample itself: a dip larger than all 1,000 simulated ones is 1 / 1001, not 0; a dip no larger than the smallest possible is (1000 + 1) / 1001 = 1.
        Assert.Equal(1.0 / (DipTest.NullSamples + 1), two.PValue, 12);
        Assert.Equal(1.0, DipTest.Test(Enumerable.Range(0, 200).Select(v => (long)v).ToArray()).PValue, 12);
    }

    // ---- Silverman's test ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCriticalBandwidthOfTwoEqualSpikesIsHalfTheirDistance()
    {
        // A mixture of two equal Gaussian kernels at distance R is unimodal exactly when the bandwidth is at least R / 2 (the textbook case), so 100 apart gives 50. The binned grid and the bisection give it to a few percent.
        var data = Enumerable.Repeat(0.0, 50).Concat(Enumerable.Repeat(100.0, 50)).ToArray();
        Assert.InRange(SilvermanTest.CriticalBandwidth(data), 48.0, 52.0);
        // The bandwidth scales with the data.
        Assert.Equal(SilvermanTest.CriticalBandwidth(data), SilvermanTest.CriticalBandwidth(data.Select(v => 10 * v + 3).ToArray()) / 10, 6);
    }

    [Fact]
    public void TheModesOfTwoClustersAreTwoUntilTheSmoothingJoinsThem()
    {
        var data = Enumerable.Range(0, 100).Select(i => (double)(i % 10)).Concat(Enumerable.Range(0, 100).Select(i => 100.0 + i % 10)).ToArray();
        Assert.Equal(2, SilvermanTest.Modes(data, data.Min(), data.Max(), 3));
        Assert.Equal(1, SilvermanTest.Modes(data, data.Min(), data.Max(), 60));
    }

    [Fact]
    public void ABimodalSampleIsRejectedByTheSmoothedBootstrapAndTheVerdictRepeats()
    {
        var random = new Random(11);
        var bimodal = Enumerable.Range(0, 400).Select(i => (long)Math.Round((i % 2 == 0 ? 5000 : 10_000) + 800 * Normal(random))).OrderBy(v => v).ToArray();
        var verdict = SilvermanTest.Test(bimodal);
        Assert.True(verdict.PValue <= 0.02, "p " + verdict.PValue);
        Assert.True(verdict.CriticalBandwidth > 1000);
        Assert.Equal(verdict, SilvermanTest.Test(bimodal));
        var normal = Enumerable.Range(0, 400).Select(_ => (long)Math.Round(5000 + 800 * Normal(random))).OrderBy(v => v).ToArray();
        // Pinned values of the seeded samples (91 of 101 bootstrap samples at least as smooth; the bimodal one 1 of 101 beside itself): a change to the rescaling, the bootstrap or the bandwidth search moves them.
        Assert.Equal(91.0 / 101, SilvermanTest.Test(normal).PValue, 12);
        Assert.Equal(1.0 / 101, verdict.PValue, 12);
        Assert.InRange(verdict.CriticalBandwidth, 2300, 2500);
        Assert.Throws<ArgumentException>(() => SilvermanTest.Test([1, 2, 3]));
        Assert.Throws<ArgumentException>(() => SilvermanTest.CriticalBandwidth([4.0, 4, 4, 4]));
    }

    // ---- the calibration, small forms -------------------------------------------------------------------------------------------------

    private static long[] Draw(Random random, string family, int n)
    {
        IEnumerable<long> values = family switch
        {
            "normal" => Enumerable.Range(0, n).Select(_ => (long)Math.Round(5000 + 800 * Normal(random))),
            "exponential" => Enumerable.Range(0, n).Select(_ => (long)(-1000 * Math.Log(1 - random.NextDouble()))),
            "uniform" => Enumerable.Range(0, n).Select(_ => (long)(random.NextDouble() * 10_000)),
            "mix50-5sd" => Enumerable.Range(0, n).Select(_ => (long)Math.Round((random.NextDouble() < 0.5 ? 5000 : 9000) + 800 * Normal(random))),
            "mix80-6sd" => Enumerable.Range(0, n).Select(_ => (long)Math.Round((random.NextDouble() < 0.8 ? 5000 : 9800) + 800 * Normal(random))),
            "clusters" => Enumerable.Range(0, n).Select(_ => (long)Math.Round((random.NextDouble() < 0.8 ? 1000 : 3000) + 50 * Normal(random))),
            _ => throw new ArgumentException(family),
        };
        return values.OrderBy(v => v).ToArray();
    }

    [Fact]
    public void UnimodalSamplesAreRarelyCalledMultimodalByTheDipAndTheGapTestNeverIs()
    {
        var random = new Random(20261031);
        var dipAlarms = 0;
        var gapAlarms = 0;
        const int reps = 60;
        foreach (var family in new[] { "normal", "exponential", "uniform" })
            for (var rep = 0; rep < reps; rep++)
            {
                var sample = Draw(random, family, 200);
                if (DipTest.Test(sample).PValue <= 0.05) dipAlarms++;
                if (ModalityCheck.Gaps(sample).Count > 0) gapAlarms++;
            }
        Assert.Equal(0, gapAlarms);
        Assert.True(dipAlarms <= 3 * reps / 20 + 3, "the dip called " + dipAlarms + " of " + 3 * reps + " unimodal samples multimodal at 5%");
    }

    [Fact]
    public void SeparatedMixturesAreFoundByTheDipAndTheTopOfASkewedMixtureIsFoundByTheDipNotTheGapTest()
    {
        var random = new Random(31);
        var dipFound = 0;
        var gapFound = 0;
        for (var rep = 0; rep < 40; rep++)
        {
            var mixture = Draw(random, "mix50-5sd", 400);
            if (DipTest.Test(mixture).PValue <= 0.01) dipFound++;
            if (ModalityCheck.Gaps(mixture).Count > 0) gapFound++;
        }
        Assert.Equal(40, dipFound);                    // five standard deviations apart, half and half: the gap test's ratio limit is not reached, the dip finds all
        Assert.True(gapFound <= 10, "the gap test found " + gapFound);

        // Values above the 70th percentile of a mixture of 80% N(5000, 800) and 20% N(9800, 800): the upper cluster and the top of the lower one. A fit across them is wrong and the gap test does not see it.
        var dipRefused = 0;
        var gapRefused = 0;
        for (var rep = 0; rep < 30; rep++)
        {
            var sample = Draw(random, "mix80-6sd", 1000);
            var threshold = sample[(int)(((long)70 * sample.Length + 99) / 100) - 1];
            var upper = sample.Where(v => v > threshold).ToArray();
            if (DipTest.Test(upper).PValue <= TailCli.DipRefusalLevel) dipRefused++;
            if (ModalityCheck.Gaps(sample).Any(g => g.Low > threshold)) gapRefused++;
        }
        Assert.True(dipRefused >= 27, "the dip refused " + dipRefused + " of 30");
        Assert.True(gapRefused <= 6, "the gap test refused " + gapRefused + " of 30");
    }

    // ---- the tool ---------------------------------------------------------------------------------------------------------------------

    private static string Rows(IEnumerable<long> values, string kind = "sample")
    {
        var text = new StringBuilder();
        var seed = 0;
        foreach (var value in values) text.Append(kind).Append(" m ").Append(seed++).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }

    [Fact]
    public void TheProposalRefusesAFitAcrossAMixtureTheGapTestMissesAndLeavesAUnimodalTailAlone()
    {
        var random = new Random(31);
        var mixture = Draw(random, "mix80-6sd", 1000);
        var refusal = Assert.Throws<TailRefusedException>(() => TailCli.Propose(TailSample.Read(Rows(mixture), "m"), [0, 8000, 20_000], 70, 30));
        Assert.Equal(TailCodes.Multimodal, refusal.Code);
        Assert.Contains("Hartigan's dip test finds more than one mode", refusal.Message, StringComparison.Ordinal);
        var threshold = mixture[(int)(((long)70 * mixture.Length + 99) / 100) - 1];
        Assert.DoesNotContain(ModalityCheck.Gaps(mixture), g => g.Low > threshold);
        var exponential = Draw(random, "exponential", 1000);
        Assert.StartsWith("tail-tool propose", TailCli.Propose(TailSample.Read(Rows(exponential), "m"), [0, 2000, 6000], 70, 30), StringComparison.Ordinal);
    }

    [Fact]
    public void TheModalityCommandPrintsTheThreeTestsAndDecidesNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), "modality-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var random = new Random(5);
            File.WriteAllText(path, Rows(Draw(random, "clusters", 400)));
            var output = new StringWriter();
            Assert.Equal(0, TailCli.Run(["modality", path, "m", "--threshold-percent", "50"], output, new StringWriter()));
            var lines = output.ToString().Split('\n');
            Assert.StartsWith("tail-tool modality metric m samples 400 ", lines[0], StringComparison.Ordinal);
            Assert.StartsWith("all-values n 400 gap-test gap ", lines[1], StringComparison.Ordinal);
            Assert.Contains(" dip ", lines[1], StringComparison.Ordinal);
            Assert.Contains(" silverman-bandwidth ", lines[1], StringComparison.Ordinal);
            Assert.StartsWith("above-threshold-50% (threshold ", lines[2], StringComparison.Ordinal);
            Assert.Equal(output.ToString(), RunText(path, "50"));
            var defaulted = new StringWriter();
            Assert.Equal(0, TailCli.Run(["modality", path, "m"], defaulted, new StringWriter()));
            Assert.StartsWith("above-threshold-90% (threshold ", defaulted.ToString().Split('\n')[2], StringComparison.Ordinal);          // the default percentile is 90
            Assert.Equal(1, TailCli.Run(["modality", path, "m", "--levels", "1,2"], new StringWriter(), new StringWriter()));
            Assert.Equal(2, TailCli.Run(["modality", path], new StringWriter(), new StringWriter()));
            var refused = new StringWriter();
            Assert.Equal(1, TailCli.Run(["modality", path, "other"], refused, new StringWriter()));
            Assert.Contains(TailCodes.MetricMissing, refused.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string RunText(string path, string percent)
    {
        var output = new StringWriter();
        TailCli.Run(["modality", path, "m", "--threshold-percent", percent], output, new StringWriter());
        return output.ToString();
    }

    [Fact]
    public void ASmallOrConstantPartIsReportedAsNotTestedNotGuessed()
    {
        var text = ModalityReport.Of(TailSample.Read(Rows(Enumerable.Range(0, 30).Select(v => (long)v)), "m"), 90);
        Assert.Contains("above-threshold-90% (threshold 26) n 3 not tested: fewer than 20 values", text, StringComparison.Ordinal);
        var constant = ModalityReport.Of(TailSample.Read(Rows(Enumerable.Repeat(4L, 30)), "m"), 90);
        Assert.Contains("all-values n 30 not tested: every value is 4", constant, StringComparison.Ordinal);
    }

    // ---- the boundaries ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheToolDrawsItsNumbersOnlyFromItsOneSeededSequenceAndNothingElseReadsTheClock()
    {
        var sources = SyntaxGuards.Sources(["EngineCommon/EngineCommon.TailTool"]);
        Assert.True(sources.Count >= 9, "found only " + sources.Count + " tail tool files");
        var forbidden = new[] { "new Random(", "Random.Shared", "DateTime", "Stopwatch", "Environment.TickCount", "Guid.NewGuid", "TimeProvider" };
        var found = sources.SelectMany(s => forbidden.Where(word => s.Text.Contains(word, StringComparison.Ordinal)).Select(word => Path.GetFileName(s.File) + ": " + word)).ToList();
        Assert.True(found.Count == 0, string.Join("; ", found));
        // The guard can fail: the same words in a copy of a source are found.
        var random = sources.First(s => s.File.EndsWith("TailRandom.cs", StringComparison.Ordinal));
        Assert.Contains(forbidden, word => (random.Text + "\nvar r = new Random(1);").Contains(word, StringComparison.Ordinal));
        Assert.Single(sources, s => s.Text.Contains("0x9E3779B97F4A7C15UL", StringComparison.Ordinal) && s.Text.Contains("0xBF58476D1CE4E5B9UL", StringComparison.Ordinal));        // SplitMix64 lives in one file
    }
}
