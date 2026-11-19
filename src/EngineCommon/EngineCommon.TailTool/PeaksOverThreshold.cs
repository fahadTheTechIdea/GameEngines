namespace EngineCommon.TailTool;

/// <summary>
/// A generalized Pareto fit to the excesses over a threshold (peaks over threshold). The shape xi and scale sigma come from probability-weighted moments (Hosking and Wallis 1987): with the excesses y(1) &lt;= ... &lt;= y(k), a0 = mean(y) and
/// a1 = mean((1 - p(i)) y(i)) with plotting positions p(i) = (i - 0.35) / k, xi = 2 - a0 / (a0 - 2 a1) and sigma = 2 a0 a1 / (a0 - 2 a1). FLOATING POINT LIVES HERE AND NOWHERE ELSE: this is an offline authoring aid, and the numbers it prints are
/// inputs a person can reproduce.
/// </summary>
public sealed record PeaksOverThreshold(long Threshold, int Samples, int Exceedances, double Xi, double Sigma)
{
    /// <summary>Fits the excesses of <paramref name="sorted"/> (ascending) over its nearest-rank <paramref name="percent"/> percentile.</summary>
    public static PeaksOverThreshold Fit(IReadOnlyList<long> sorted, int percent, int minExceedances)
    {
        var rank = ((long)percent * sorted.Count + 99) / 100;
        var threshold = sorted[(int)rank - 1];
        var excess = ExcessesOver(sorted, threshold);
        if (excess.Length < minExceedances)
            throw new TailRefusedException(TailCodes.FewExceedances, "only " + excess.Length + " values lie above the " + percent + "th percentile (" + threshold + "); at least " + minExceedances + " are needed. Run more seeds or lower --threshold-percent");
        var k = excess.Length;
        var (a0, a1) = Moments(excess);
        var denominator = a0 - 2 * a1;
        if (!(denominator > 0) || !(a0 > 0)) throw new TailRefusedException(TailCodes.FitFailed, "the excesses give no positive scale (a0 " + a0.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ", a1 " + a1.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ")");
        var xi = 2 - a0 / denominator;
        var sigma = 2 * a0 * a1 / denominator;
        if (!(sigma > 0) || !(xi < 1)) throw new TailRefusedException(TailCodes.FitFailed, "the fit is outside the range the estimator holds for (xi " + xi.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ", sigma " + sigma.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ")");
        return new PeaksOverThreshold(threshold, sorted.Count, k, xi, sigma);
    }

    /// <summary>The excesses of the ascending <paramref name="sorted"/> over <paramref name="threshold"/>: each value above it, less it (so ascending, all above 0).</summary>
    public static double[] ExcessesOver(IReadOnlyList<long> sorted, long threshold) => sorted.Where(x => x > threshold).Select(x => (double)(x - threshold)).ToArray();

    /// <summary>The two probability-weighted moments a0 and a1 of ascending excesses (plotting positions (i - 0.35) / k).</summary>
    private static (double A0, double A1) Moments(IReadOnlyList<double> excess)
    {
        var k = excess.Count;
        double a0 = 0, a1 = 0;
        for (var i = 0; i < k; i++)
        {
            a0 += excess[i];
            a1 += (1.0 - (i + 1 - 0.35) / k) * excess[i];
        }
        return (a0 / k, a1 / k);
    }

    /// <summary>The shape of the fit to ascending excesses, or null when they give no positive scale (the same estimator as <see cref="Fit"/>, for the resampled fits of <see cref="BoundCheck"/>).</summary>
    public static double? ShapeOf(IReadOnlyList<double> excess)
    {
        var (a0, a1) = Moments(excess);
        var denominator = a0 - 2 * a1;
        return !(denominator > 0) || !(a0 > 0) ? null : 2 - a0 / denominator;
    }

    /// <summary>The chance a value is at least <paramref name="level"/>: counted from the sample when the level is at or under the threshold, otherwise read from the fitted tail at level - 1/2 (the values are whole numbers, so "at least L" is "above L - 1/2").</summary>
    public double AtLeast(IReadOnlyList<long> sorted, long level)
    {
        if (level <= Threshold)
        {
            var count = sorted.Count(x => x >= level);
            return (double)count / sorted.Count;
        }
        var t = level - 0.5 - Threshold;
        var share = (double)Exceedances / Samples;
        if (Math.Abs(Xi) < 1e-9) return share * Math.Exp(-t / Sigma);
        var basis = 1 + Xi * t / Sigma;
        return basis <= 0 ? 0 : share * Math.Pow(basis, -1 / Xi);
    }
}
