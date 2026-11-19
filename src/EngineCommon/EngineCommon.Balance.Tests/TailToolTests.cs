using System.Globalization;
using System.Text;
using EngineCommon.TailTool;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>The offline tail tool against samples whose true tail is known (a quantile grid of a known distribution, so there is no sampling noise), with the tolerance stated at each check.</summary>
public sealed class TailToolTests
{
    private const int N = 20_000;

    /// <summary>A sweep file with a 'sample' row per value; other lines are there to show they are ignored.</summary>
    private static string Rows(string metric, IEnumerable<long> values)
    {
        var text = new StringBuilder("balance test\nmetric " + metric + " n 1\n");
        var seed = 0;
        foreach (var value in values) text.Append("sample ").Append(metric).Append(' ').Append(seed++).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("sample other 0 999999\nsweep-digest 00\n");
        return text.ToString();
    }

    /// <summary>The (i - 1/2) / N quantiles of an exponential with mean 1000: true P(X >= L) = exp(-(L - 1/2) / 1000) for the whole-number values.</summary>
    private static IEnumerable<long> Exponential() => Enumerable.Range(1, N).Select(i => (long)Math.Round(-1000 * Math.Log(1 - (i - 0.5) / N)));

    /// <summary>The same grid for a generalized Pareto tail with shape 0.25 and scale 1000: true P(X >= L) = (1 + 0.25 (L - 1/2) / 1000)^-4.</summary>
    private static IEnumerable<long> Pareto() => Enumerable.Range(1, N).Select(i => (long)Math.Round(1000 * (Math.Pow(1 - (i - 0.5) / N, -0.25) - 1) / 0.25));

    private static long[] Proposal(string text, string metric, long[] levels, int percent = 90)
    {
        var sample = TailSample.Read(text, metric);
        var result = TailCli.Propose(sample, levels, percent, 30);
        var last = result.TrimEnd().Split('\n').Last();
        Assert.StartsWith("tailPpm ", last, StringComparison.Ordinal);
        return last["tailPpm ".Length..].Split(',').Select(long.Parse).ToArray();
    }

    private static void Near(double expectedPpm, long actual, double relative, double absolute) =>
        Assert.True(Math.Abs(actual - expectedPpm) <= Math.Max(relative * expectedPpm, absolute), $"expected {expectedPpm:F1} ppm within {relative:P0} or {absolute} ppm, got {actual}");

    [Fact]
    public void AnExponentialTailIsRecoveredWithinFivePercentOrFiftyPpm()
    {
        long[] levels = [0, 1000, 3000, 6000, 9000];
        var rungs = Proposal(Rows("m", Exponential()), "m", levels);
        Assert.Equal(1_000_000, rungs[0]);
        for (var i = 1; i < levels.Length; i++) Near(1e6 * Math.Exp(-(levels[i] - 0.5) / 1000), rungs[i], 0.05, 50);
        Assert.True(rungs.Zip(rungs.Skip(1), (a, b) => a > b).All(x => x));
    }

    [Fact]
    public void AHeavyTailIsRecoveredWithinFivePercentOrFiftyPpm()
    {
        long[] levels = [0, 2000, 10_000, 30_000, 60_000];
        var rungs = Proposal(Rows("m", Pareto()), "m", levels);
        for (var i = 1; i < levels.Length; i++) Near(1e6 * Math.Pow(1 + 0.25 * (levels[i] - 0.5) / 1000, -4), rungs[i], 0.05, 50);
    }

    [Fact]
    public void ALevelAtOrUnderTheThresholdIsCountedFromTheSample()
    {
        var text = Rows("m", Enumerable.Range(1, 1000).Select(i => (long)i));           // 1..1000, uniform
        var result = TailCli.Propose(TailSample.Read(text, "m"), [1, 500, 900], 90, 30);
        Assert.Contains("level 500 tail-ppm 501000 source empirical", result, StringComparison.Ordinal);   // 501 values are at least 500
        Assert.Contains("threshold-percent 90 threshold 900 exceedances 100", result, StringComparison.Ordinal);
        Assert.Contains("level 900 tail-ppm 101000 source empirical", result, StringComparison.Ordinal);   // 101 values are at least 900, the threshold itself
    }

    [Fact]
    public void TheOutputNamesItsInputsAndRepeatsByteForByte()
    {
        var text = Rows("m", Exponential());
        var first = TailCli.Propose(TailSample.Read(text, "m"), [0, 1000, 3000], 90, 30);
        Assert.Equal(first, TailCli.Propose(TailSample.Read(text, "m"), [0, 1000, 3000], 90, 30));
        Assert.Contains("samples 20000", first, StringComparison.Ordinal);
        Assert.Contains("sample-digest ", first, StringComparison.Ordinal);
        Assert.Contains("gpd-pwm xi ", first, StringComparison.Ordinal);
        Assert.Contains("sigma ", first, StringComparison.Ordinal);
        Assert.Contains("floating point, offline only", first, StringComparison.Ordinal);
        Assert.Contains("level 0 tail-ppm 1000000 source rule floor-rung", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFitParametersMatchTheKnownDistributionWithinTolerance()
    {
        var fit = PeaksOverThreshold.Fit(Exponential().OrderBy(v => v).ToArray(), 90, 30);
        Assert.InRange(fit.Xi, -0.05, 0.05);                  // an exponential has shape 0
        Assert.InRange(fit.Sigma, 950, 1050);                 // and the memoryless excess has the same scale
        var heavy = PeaksOverThreshold.Fit(Pareto().OrderBy(v => v).ToArray(), 90, 30);
        Assert.InRange(heavy.Xi, 0.20, 0.30);
    }

    [Fact]
    public void AHandWorkedFitOfThreeExcessesGivesTheStatedShapeScaleAndTail()
    {
        // Sample 1,1,1,2,3,4 at the 50th percentile: threshold 1, excesses 1,2,3. Plotting positions (i - 0.35) / 3 = 0.21667, 0.55, 0.88333,
        // a0 = 2, a1 = (0.78333 * 1 + 0.45 * 2 + 0.11667 * 3) / 3 = 0.67778, a0 - 2 a1 = 0.64444, xi = 2 - 2 / 0.64444 = -1.10345, sigma = 4 * 0.67778 / 0.64444 = 4.20690.
        long[] sample = [1, 1, 1, 2, 3, 4];
        var fit = PeaksOverThreshold.Fit(sample, 50, 3);
        Assert.Equal(1, fit.Threshold);
        Assert.Equal(3, fit.Exceedances);
        Assert.Equal(-1.10345, fit.Xi, 4);
        Assert.Equal(4.20690, fit.Sigma, 4);
        // Level 4: t = 4 - 1/2 - 1 = 2.5, basis 1 + xi t / sigma = 0.34426, power -1/xi = 0.90625, times the share 3/6: 0.5 * 0.34426^0.90625 = 0.19023.
        Assert.Equal(0.19023, fit.AtLeast(sample, 4), 3);
        Assert.Equal(1.0, fit.AtLeast(sample, 1), 10);          // at the threshold the sample is counted: all six values are at least 1
    }

    [Fact]
    public void ALevelBeyondTheFittedEndpointButInsideTheSampleIsRaisedToOnePpmAndTwoSuchRungsCannotFall()
    {
        // A uniform sample with one stray value at 1100: the fitted end lies near 1000, so a level between it and the stray value is inside the sample and beyond the fit.
        var sample = TailSample.Read(Rows("m", Enumerable.Range(1, 1000).Select(i => (long)i).Append(1100L)), "m");
        var fit = PeaksOverThreshold.Fit(sample.Values.OrderBy(v => v).ToArray(), 90, 30);
        var end = fit.Threshold - fit.Sigma / fit.Xi;
        Assert.True(end < 1090, "the fit ends at " + end + ", the largest value is " + sample.Values.Max());
        var low = (long)Math.Ceiling(end) + 1;
        Assert.True(low + 1 <= 1100);
        var result = TailCli.Propose(sample, [1, low], 90, 30);
        Assert.Contains("level " + low + " tail-ppm 1 source gpd raised-to-1", result, StringComparison.Ordinal);
        Assert.Contains("warning xi-outside-minus-0.5-to-0.5", result, StringComparison.Ordinal);
        Assert.DoesNotContain("warning", TailCli.Propose(TailSample.Read(Rows("m", Exponential()), "m"), [0, 1000], 90, 30), StringComparison.Ordinal);
        Assert.Equal(TailCodes.CannotFall, Assert.Throws<TailRefusedException>(() => TailCli.Propose(sample, [1, low, low + 1], 90, 30)).Code);
    }

    [Theory]
    [InlineData(TailCodes.FewExceedances)]
    public void TooFewExceedancesAreRefused(string code)
    {
        var sample = TailSample.Read(Rows("m", Enumerable.Range(1, 100).Select(i => (long)i)), "m");
        Assert.Equal(code, Assert.Throws<TailRefusedException>(() => TailCli.Propose(sample, [0, 50], 99, 30)).Code);
    }

    [Fact]
    public void RowsThatAreMissingDuplicatedOrMalformedAreRefused()
    {
        Assert.Equal(TailCodes.MetricMissing, Assert.Throws<TailRefusedException>(() => TailSample.Read("metric m n 1\n", "m")).Code);
        Assert.Equal(TailCodes.SampleDuplicate, Assert.Throws<TailRefusedException>(() => TailSample.Read("sample m 1 5\nsample m 1 6\n", "m")).Code);
        Assert.Equal(TailCodes.SampleFormat, Assert.Throws<TailRefusedException>(() => TailSample.Read("sample m 1\n", "m")).Code);
        Assert.Equal(TailCodes.SampleFormat, Assert.Throws<TailRefusedException>(() => TailSample.Read("sample m x 5\n", "m")).Code);
    }

    [Fact]
    public void TheCommandLineRefusesBadLevelsAndOptionsWithTheirCodes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "rows.txt");
            File.WriteAllText(file, Rows("m", Exponential()));
            (int Code, string Out, string Err) Run(params string[] args)
            {
                var output = new StringWriter();
                var error = new StringWriter();
                var code = TailCli.Run(args, output, error);
                return (code, output.ToString(), error.ToString());
            }
            var ok = Run("propose", file, "m", "--levels", "0,1000,3000");
            Assert.Equal(0, ok.Code);
            Assert.EndsWith("\n", ok.Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.Levels, Run("propose", file, "m", "--levels", "0,0,5").Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.Levels, Run("propose", file, "m", "--levels", "5").Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.Levels, Run("propose", file, "m").Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.Option, Run("propose", file, "m", "--levels", "0,5", "--bogus", "1").Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.Option, Run("propose", file, "m", "--levels", "0,5", "--threshold-percent", "100").Out, StringComparison.Ordinal);
            Assert.StartsWith("refused " + TailCodes.MetricMissing, Run("propose", file, "nope", "--levels", "0,5").Out, StringComparison.Ordinal);
            Assert.Equal(2, Run("propose", Path.Combine(folder, "absent.txt"), "m", "--levels", "0,5").Code);
            Assert.Equal(2, Run("check").Code);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
