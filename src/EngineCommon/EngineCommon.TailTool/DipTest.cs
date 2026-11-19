namespace EngineCommon.TailTool;

/// <summary>The dip of a sample and its modal interval (the indices, in the sorted spread sample, of the interval on which the nearest unimodal distribution is linear).</summary>
public sealed record DipStatistic(double Dip, int ModalLow, int ModalHigh, int Samples);

/// <summary>What <see cref="DipTest"/> found: the dip, the chance that a sample of the same size from the uniform distribution (the least favourable unimodal one) has a dip at least as large, and how many simulated samples that chance rests on.</summary>
public sealed record DipVerdict(DipStatistic Statistic, double PValue, int Resamples)
{
    /// <summary>True when the dip is larger than the uniform null allows at <paramref name="level"/>.</summary>
    public bool Rejects(double level) => PValue <= level;
}

/// <summary>
/// Hartigan's dip test of unimodality (Hartigan and Hartigan 1985). The dip of a sample is the smallest sup-norm distance between its empirical distribution function and any unimodal distribution function (convex up to a mode, concave after);
/// it is at least 1 / (2 n) for any sample and grows when the sample has a second mode. The statistic is computed by the iterative algorithm of P. M. Hartigan (1985, Applied Statistics 34, AS 217): from the whole sample, fit the greatest convex minorant
/// to the lower corners (x(i), i) and the least concave majorant to the upper corners (x(i), i + 1) of the empirical step function inside the current modal interval, take the place where the two are furthest apart as the next, smaller, modal
/// interval, charge the distance of the part cut off to the dip, and repeat until the furthest distance no longer exceeds what has been charged; the dip is half the largest charge. It was checked against an independent exact linear program (see the tests).
/// <para>
/// Ties. The algorithm needs distinct values, and the tool's data are whole numbers with hundreds of ties (an atom at zero). The sample is therefore treated as whole numbers rounded from a continuous quantity: the k copies of the value v are spread
/// evenly over [v - 1/2, v + 1/2] (positions v - 1/2 + (j + 1/2) / k), which is deterministic (no jitter), strictly increasing, and leaves a lone value where it is. This is a modelling choice, not part of Hartigan's test, and the p-value below is
/// calibrated for a continuous sample; docs/WORLD_EVENTS_ENGINE.md section 16 reports how it behaves on rounded data.
/// </para>
/// </summary>
public static class DipTest
{
    /// <summary>The number of uniform samples the null distribution of a size is simulated from. Fixed, so a p-value is a pure function of the dip and the sample size.</summary>
    public const int NullSamples = 1000;

    private static readonly Dictionary<int, double[]> NullCache = [];

    /// <summary>The sorted values spread over their whole-number cells (see the type remarks), strictly increasing.</summary>
    public static double[] Spread(IReadOnlyList<long> sorted)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        var spread = new double[sorted.Count];
        var i = 0;
        while (i < sorted.Count)
        {
            var j = i;
            while (j < sorted.Count && sorted[j] == sorted[i]) j++;
            var k = j - i;
            for (var t = 0; t < k; t++) spread[i + t] = sorted[i] - 0.5 + (t + 0.5) / k;
            i = j;
        }
        return spread;
    }

    /// <summary>The dip of strictly increasing <paramref name="x"/> (at least four values).</summary>
    public static DipStatistic Of(IReadOnlyList<double> x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var n = x.Count;
        if (n < 4) throw new ArgumentException("a dip needs four values", nameof(x));
        for (var i = 1; i < n; i++)
            if (!(x[i] > x[i - 1])) throw new ArgumentException("the values must be strictly increasing (spread ties first)", nameof(x));
        var low = 0;
        var high = n - 1;
        double charged = 0;                                                    // the largest distance charged so far, in units of 1 / n
        var gcm = new int[n];
        var lcm = new int[n];
        while (high > low)
        {
            var gc = Hull(x, low, high, 0, gcm);
            var lc = Hull(x, low, high, 1, lcm);
            var gcmGap = Furthest(x, gcm, gc, 0, lcm, lc, 1, first: true);        // at the vertices of the convex minorant: the concave majorant above it
            var lcmGap = Furthest(x, lcm, lc, 1, gcm, gc, 0, first: false);      // at the vertices of the concave majorant: the convex minorant below it
            double d;
            int nextLow, nextHigh;
            if (lcmGap.Distance > gcmGap.Distance)
            {
                d = lcmGap.Distance;
                nextHigh = lcmGap.Index;
                nextLow = low;
                for (var v = 0; v < gc; v++)
                    if (gcm[v] <= nextHigh) nextLow = gcm[v];
            }
            else
            {
                d = gcmGap.Distance;
                nextLow = gcmGap.Index;
                nextHigh = high;
                for (var v = lc - 1; v >= 0; v--)
                    if (lcm[v] >= nextLow) nextHigh = lcm[v];
            }
            if (d <= charged || nextHigh == low || nextLow == high) break;
            // The part cut off on the left is charged against the convex minorant, the part cut off on the right against the concave majorant.
            var left = Deviation(x, gcm, gc, 0, low, nextLow, upper: false);
            var right = Deviation(x, lcm, lc, 1, nextHigh, high, upper: true);
            charged = Math.Max(charged, Math.Max(left, right));
            low = nextLow;
            high = nextHigh;
        }
        return new DipStatistic(Math.Max(charged, 1.0) / (2.0 * n), low, high, n);
    }

    /// <summary>The p-value of a sample's dip against the simulated uniform null of its size.</summary>
    public static DipVerdict Test(IReadOnlyList<long> sorted)
    {
        var statistic = Of(Spread(sorted));
        var nullDips = NullDistribution(sorted.Count);
        // The first index whose null dip is at least the sample's: those from there on are as large or larger.
        var lo = 0;
        var hi = nullDips.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (nullDips[mid] < statistic.Dip) lo = mid + 1;
            else hi = mid;
        }
        return new DipVerdict(statistic, (nullDips.Length - lo + 1.0) / (nullDips.Length + 1.0), nullDips.Length);
    }

    /// <summary>The sorted dips of <see cref="NullSamples"/> uniform samples of size <paramref name="n"/>, drawn from a fixed sequence that depends on n alone.</summary>
    public static double[] NullDistribution(int n)
    {
        lock (NullCache)
        {
            if (NullCache.TryGetValue(n, out var cached)) return cached;
            var random = new TailRandom(0xD1B54A32D192ED03UL ^ (ulong)n);
            var dips = new double[NullSamples];
            var sample = new double[n];
            for (var b = 0; b < NullSamples; b++)
            {
                for (var i = 0; i < n; i++) sample[i] = random.NextDouble();
                Array.Sort(sample);
                dips[b] = Of(sample).Dip;                  // a uniform draw is distinct with overwhelming probability; a repeat would make Of refuse and fail the run loudly
            }
            Array.Sort(dips);
            NullCache[n] = dips;
            return dips;
        }
    }

    /// <summary>The vertices (indices) of the lower convex hull of (x(i), i) when <paramref name="offset"/> is 0, or the upper concave hull of (x(i), i + 1) when it is 1, over indices low to high; collinear points stay vertices. Returns the count.</summary>
    private static int Hull(IReadOnlyList<double> x, int low, int high, int offset, int[] vertices)
    {
        var count = 0;
        for (var i = low; i <= high; i++)
        {
            while (count >= 2)
            {
                var a = vertices[count - 2];
                var b = vertices[count - 1];
                var cross = (x[b] - x[a]) * (i - a) - (b - a) * (x[i] - x[a]);       // the offset is the same for every point, so it cancels in the differences
                if (offset == 0 ? cross < 0 : cross > 0) count--;
                else break;
            }
            vertices[count++] = i;
        }
        return count;
    }

    /// <summary>The value at index <paramref name="i"/> of the hull (vertices <paramref name="h"/>, y = index + offset), moving the segment cursor <paramref name="seg"/> forward (calls come in increasing i).</summary>
    private static double Value(IReadOnlyList<double> x, int[] h, int count, int offset, ref int seg, int i)
    {
        while (seg < count - 2 && h[seg + 1] <= i) seg++;
        var a = h[seg];
        var b = h[seg + 1];
        return (a + offset) + (double)(b - a) * (x[i] - x[a]) / (x[b] - x[a]);
    }

    /// <summary>
    /// The largest vertical distance between the two hulls measured at the vertices of the hull <paramref name="own"/> (offset <paramref name="ownOffset"/>): for the convex minorant, the other (concave majorant) minus its own y; for the concave majorant,
    /// its own y minus the other. With ties between vertices the first is taken when <paramref name="first"/> is true and the last otherwise.
    /// </summary>
    private static (double Distance, int Index) Furthest(IReadOnlyList<double> x, int[] own, int ownCount, int ownOffset, int[] other, int otherCount, int otherOffset, bool first)
    {
        var best = double.NegativeInfinity;
        var index = own[0];
        var seg = 0;
        for (var v = 0; v < ownCount; v++)
        {
            var i = own[v];
            var gap = ownOffset == 0 ? Value(x, other, otherCount, otherOffset, ref seg, i) - i : (i + 1) - Value(x, other, otherCount, otherOffset, ref seg, i);
            if (first ? gap > best : gap >= best)
            {
                best = gap;
                index = i;
            }
        }
        return (best, index);
    }

    /// <summary>The largest distance, over indices from to to, between the hull and the step function's far corner: (i + 1) minus the convex minorant, or the concave majorant minus i.</summary>
    private static double Deviation(IReadOnlyList<double> x, int[] h, int count, int offset, int from, int to, bool upper)
    {
        double worst = 0;
        var seg = 0;
        for (var i = from; i <= to; i++)
        {
            var hull = Value(x, h, count, offset, ref seg, i);
            worst = Math.Max(worst, upper ? hull - i : (i + 1) - hull);
        }
        return worst;
    }
}
