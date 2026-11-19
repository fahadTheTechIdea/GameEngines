using System.Numerics;

namespace EngineCommon.Balance;

/// <summary>
/// The summary of the replications of a splitting estimate, all in whole parts per billion. Each replication's estimate is rounded down to a whole ppb (an error under 1 ppb each); the mean is their exact mean rounded half up; the standard
/// error is the square root of the sample variance of the replications over their number, rounded up; the band is the mean plus and minus twice it, cut to 0 and 1,000,000,000 (a normal approximation, about 95%: the estimates are
/// unbiased but skewed, so the band is approximate and the median interval beside it is the assumption-free statement); the median interval is the distribution-free 95% interval of <see cref="SampleStats"/>, which needs six replications.
/// </summary>
public sealed record SplitSummary(long MeanPpb, long SePpb, long LowPpb, long HighPpb, long MedianPpb, (long Low, long High)? Median95, long MinPpb, long MaxPpb, int Extinct, long Runs)
{
    private const long Billion = 1_000_000_000;

    public static SplitSummary Of(SplitReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var values = report.Replications.Select(r => r.Ppb).ToArray();
        var n = values.Length;
        if (n == 0) throw new ArgumentException("a summary needs a replication", nameof(report));
        BigInteger sum = 0;
        BigInteger squares = 0;
        foreach (var value in values)
        {
            sum += value;
            squares += (BigInteger)value * value;
        }
        var mean = (long)((2 * sum + n) / (2 * (BigInteger)n));
        BigInteger se = 0;
        if (n > 1)
        {
            // se^2 = (n sum(x^2) - (sum x)^2) / (n^2 (n - 1)); the quotient is rounded up, then the root.
            var numerator = n * squares - sum * sum;
            var denominator = (BigInteger)n * n * (n - 1);
            se = IntegerRoot.CeilingSqrt((numerator + denominator - 1) / denominator);
        }
        var sorted = SampleStats.Sorted(values);
        var band = 2 * se;
        return new SplitSummary(mean, (long)se, (long)BigInteger.Max(0, mean - band), (long)BigInteger.Min(Billion, mean + band), SampleStats.Percentile(sorted, 50), SampleStats.MedianInterval(sorted), sorted[0], sorted[^1],
            report.Replications.Count(r => r.Extinct), report.Replications.Sum(r => (long)r.Runs) + (report.Pilot?.TotalRuns ?? 0));
    }
}
