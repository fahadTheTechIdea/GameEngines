namespace EngineCommon.TailTool;

/// <summary>What <see cref="BoundCheck"/> found: how many of the resampled fits had a negative shape (a tail with a finite end) and, for the fit itself, the end it implies (null when the fitted shape is not negative).</summary>
public sealed record BoundEvidence(int Resamples, int Negative, double? Endpoint)
{
    /// <summary>True when at least 97.5% of the resampled fits have a negative shape: the sample's own variation does not allow an unbounded tail.</summary>
    public bool Bounded => Negative * 1000L >= Resamples * 975L;
}

/// <summary>
/// The tool's test for a hard upper end. A generalized Pareto with a negative shape has a finite end at threshold - sigma / xi; a fit can show a negative shape only by chance, and the shape of a bounded sample (a count that cannot pass the
/// population) can be read as a tail that goes on. So the excesses are resampled with replacement <see cref="Resamples"/> times (a percentile bootstrap, a fixed pseudo-random sequence so the same sample gives the same answer) and each resample is
/// fitted the same way; when at least 97.5% of the fits have a negative shape the tail is called bounded. The peaks-over-threshold estimator is validated for shapes above -0.5 (Hosking and Wallis 1987), so the bootstrap is used rather than their
/// variance formula, which does not hold below it. A level above the largest value ever seen, in a sample called bounded, is not something the sample can support, and the tool refuses it (<see cref="TailCodes.Bounded"/>).
/// </summary>
public static class BoundCheck
{
    public const int Resamples = 200;

    /// <summary>The evidence for the excesses <paramref name="excess"/> (ascending, all above 0) of a fit with <paramref name="fit"/>.</summary>
    public static BoundEvidence Of(IReadOnlyList<double> excess, PeaksOverThreshold fit)
    {
        ArgumentNullException.ThrowIfNull(excess);
        ArgumentNullException.ThrowIfNull(fit);
        var k = excess.Count;
        var negative = 0;
        var random = new TailRandom(0x9E3779B97F4A7C15UL ^ (ulong)k);
        var draw = new double[k];
        for (var round = 0; round < Resamples; round++)
        {
            for (var i = 0; i < k; i++) draw[i] = excess[(int)(random.Next() % (ulong)k)];
            Array.Sort(draw);
            var xi = PeaksOverThreshold.ShapeOf(draw);
            if (xi is { } shape && shape < 0) negative++;
        }
        return new BoundEvidence(Resamples, negative, fit.Xi < 0 ? fit.Threshold - fit.Sigma / fit.Xi : null);
    }
}
