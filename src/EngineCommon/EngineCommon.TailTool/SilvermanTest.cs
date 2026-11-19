namespace EngineCommon.TailTool;

/// <summary>What <see cref="SilvermanTest"/> found: the critical bandwidth (the smallest Gaussian bandwidth whose density estimate has one mode), the share of smoothed-bootstrap samples whose own critical bandwidth was at least as large, and the number of bootstrap samples.</summary>
public sealed record SilvermanVerdict(double CriticalBandwidth, double PValue, int Resamples)
{
    /// <summary>True when the critical bandwidth is larger than the unimodal bootstrap allows at <paramref name="level"/>.</summary>
    public bool Rejects(double level) => PValue <= level;
}

/// <summary>
/// Silverman's test of unimodality (Silverman 1981, "Using kernel density estimates to investigate multimodality", JRSS B 43). With a Gaussian kernel the number of modes of a kernel density estimate cannot rise as the bandwidth h grows, so there is a
/// smallest h, the critical bandwidth h1, at which the estimate has a single mode; a large h1 means the sample needs heavy smoothing to look unimodal. The significance is found by the smoothed bootstrap: samples are drawn from the unimodal estimate at
/// h1, y = mean + (x* + h1 e - mean) / sqrt(1 + h1^2 / s^2) with x* a draw of the data, e a standard normal number and s^2 the variance of the data (the rescaling keeps the variance), and the p-value is the share of such samples whose own critical
/// bandwidth is at least h1. Hall and York (2001) showed the test is conservative (its true level is below the nominal one) and gave a calibration for it; this tool does not apply their correction and the tests measure the level it has.
/// <para>
/// Computation (the tool's own choices): the estimate is evaluated on a grid of <see cref="GridSize"/> points over the data's range extended by 4 h each side, the data binned to the grid, the kernel truncated at 4 h; the modes are the local maxima of
/// the grid values (a flat stretch counts once); h1 is found by <see cref="Steps"/> bisections of log h between range / 2000 (below that the grid cannot resolve the estimate and h1 is reported as that floor) and the range. The bootstrap uses a
/// fixed pseudo-random sequence (<see cref="TailRandom"/>) that depends on the sample size only, so the same sample gives the same answer. The Gaussian kernel's tail and the grid resolution are approximations; heavy-tailed samples on their raw scale
/// have most of their points in a few cells and the test then sees little structure, which the calibration shows.
/// </para>
/// </summary>
public static class SilvermanTest
{
    public const int GridSize = 256;

    public const int Steps = 12;

    public const int DefaultResamples = 100;

    /// <summary>The smallest bandwidth, as a fraction of the range, the grid is trusted to resolve.</summary>
    private const double FloorFraction = 1.0 / 2000.0;

    public static SilvermanVerdict Test(IReadOnlyList<long> sorted, int resamples = DefaultResamples)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        if (sorted.Count < 4) throw new ArgumentException("a critical bandwidth needs four values", nameof(sorted));
        if (resamples < 1) throw new ArgumentOutOfRangeException(nameof(resamples), "at least one bootstrap sample");
        var data = new double[sorted.Count];
        for (var i = 0; i < data.Length; i++) data[i] = sorted[i];
        var critical = CriticalBandwidth(data);
        var n = data.Length;
        var mean = data.Average();
        var variance = data.Sum(v => (v - mean) * (v - mean)) / n;
        var scale = 1.0 / Math.Sqrt(1.0 + critical * critical / variance);
        var random = new TailRandom(0xA24BAED4963EE407UL ^ (ulong)n);
        var sample = new double[n];
        var atLeast = 0;
        for (var b = 0; b < resamples; b++)
        {
            for (var i = 0; i < n; i++)
                sample[i] = mean + (data[(int)(random.Next() % (ulong)n)] + critical * random.NextNormal() - mean) * scale;
            if (CriticalBandwidth(sample) >= critical) atLeast++;
        }
        return new SilvermanVerdict(critical, (atLeast + 1.0) / (resamples + 1.0), resamples);
    }

    /// <summary>The smallest bandwidth (to the precision of <see cref="Steps"/> bisections) at which the density estimate of <paramref name="data"/> has one mode; the floor when it has one already there.</summary>
    public static double CriticalBandwidth(double[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var min = data.Min();
        var max = data.Max();
        var range = max - min;
        if (!(range > 0)) throw new ArgumentException("the sample has no spread", nameof(data));
        var lo = range * FloorFraction;
        if (Modes(data, min, max, lo) <= 1) return lo;
        var hi = range;
        for (var step = 0; step < Steps; step++)
        {
            var mid = Math.Sqrt(lo * hi);
            if (Modes(data, min, max, mid) <= 1) hi = mid;
            else lo = mid;
        }
        return hi;
    }

    /// <summary>The number of modes of the Gaussian estimate with bandwidth <paramref name="h"/>, on the binned grid.</summary>
    public static int Modes(double[] data, double min, double max, double h)
    {
        var start = min - 4 * h;
        var step = (max - min + 8 * h) / (GridSize - 1);
        var counts = new double[GridSize];
        foreach (var value in data) counts[(int)Math.Round((value - start) / step)]++;
        var reach = (int)Math.Ceiling(4 * h / step);
        var kernel = new double[reach + 1];
        for (var d = 0; d <= reach; d++) kernel[d] = Math.Exp(-0.5 * (d * step / h) * (d * step / h));
        var density = new double[GridSize];
        for (var cell = 0; cell < GridSize; cell++)
        {
            if (counts[cell] == 0) continue;
            var from = Math.Max(0, cell - reach);
            var to = Math.Min(GridSize - 1, cell + reach);
            for (var j = from; j <= to; j++) density[j] += counts[cell] * kernel[Math.Abs(j - cell)];
        }
        var tolerance = 1e-12 * density.Max();
        var modes = 0;
        var rising = true;                                      // the estimate climbs from the empty edge
        for (var j = 1; j < GridSize; j++)
        {
            var change = density[j] - density[j - 1];
            if (change > tolerance) rising = true;
            else if (change < -tolerance)
            {
                if (rising) modes++;
                rising = false;
            }
        }
        if (rising) modes++;
        return modes;
    }
}
