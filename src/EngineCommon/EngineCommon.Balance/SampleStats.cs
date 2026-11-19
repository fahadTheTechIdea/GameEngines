using System.Globalization;
using System.Numerics;
using System.Text;

namespace EngineCommon.Balance;

/// <summary>
/// Exact integer statistics of a sample of whole numbers. Percentiles are nearest-rank (the value at 1-based rank ceil(p n / 100) of the sorted sample, so always a sample value); the mean is a rational printed to three decimals, rounded half up
/// (toward plus infinity); the interval is the distribution-free 95% interval for the median from the sign test: [x(k), x(n+1-k)] with k the largest rank whose binomial tail P(Bin(n,1/2) &lt;= k-1) is at most 1/40, so its coverage is
/// at least 95% for any distribution (ties make it conservative), and it exists from six values on.
/// </summary>
public static class SampleStats
{
    public static long[] Sorted(IEnumerable<long> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        return sorted;
    }

    /// <summary>The nearest-rank percentile (1 to 100) of a sorted sample.</summary>
    public static long Percentile(IReadOnlyList<long> sorted, int percent)
    {
        if (sorted.Count == 0) throw new ArgumentException("a percentile needs a value", nameof(sorted));
        if (percent < 1 || percent > 100) throw new ArgumentException("a percentile is 1 to 100", nameof(percent));
        var rank = ((long)percent * sorted.Count + 99) / 100;
        return sorted[(int)rank - 1];
    }

    /// <summary>The mean in thousandths, rounded half up: floor((2000 sum + n) / 2n).</summary>
    public static BigInteger MeanMilli(IReadOnlyList<long> values)
    {
        if (values.Count == 0) throw new ArgumentException("a mean needs a value", nameof(values));
        BigInteger sum = 0;
        foreach (var value in values) sum += value;
        var numerator = 2000 * sum + values.Count;
        var denominator = 2 * (BigInteger)values.Count;
        var quotient = BigInteger.DivRem(numerator, denominator, out var rest);
        return rest.Sign < 0 ? quotient - 1 : quotient;
    }

    public static string Fixed3(BigInteger milli)
    {
        var text = new StringBuilder();
        if (milli.Sign < 0) text.Append('-');
        var magnitude = BigInteger.Abs(milli);
        text.Append((magnitude / 1000).ToString(CultureInfo.InvariantCulture)).Append('.').Append((magnitude % 1000).ToString("D3", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    /// <summary>The 1-based rank k of the 95% median interval for n values, or 0 when n is below six and no such interval exists.</summary>
    public static int MedianRank(int n)
    {
        if (n < 1) return 0;
        var limit = BigInteger.Pow(2, n) / 40;       // the tail may be at most 2^n / 40 of the 2^n outcomes
        var k = 0;
        BigInteger choose = 1;                       // C(n, j)
        BigInteger tail = 0;                         // C(n, 0) + ... + C(n, j)
        for (var j = 0; j < n; j++)
        {
            tail += choose;
            if (tail > limit) break;
            k = j + 1;
            choose = choose * (n - j) / (j + 1);
        }
        return 2 * k > n ? 0 : k;
    }

    /// <summary>The interval's values as (low, high), or null when the sample is too small.</summary>
    public static (long Low, long High)? MedianInterval(IReadOnlyList<long> sorted)
    {
        var k = MedianRank(sorted.Count);
        return k == 0 ? null : (sorted[k - 1], sorted[sorted.Count - k]);
    }
}
