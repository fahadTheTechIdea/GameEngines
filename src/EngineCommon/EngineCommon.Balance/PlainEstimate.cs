using System.Numerics;

namespace EngineCommon.Balance;

/// <summary>The plain Monte Carlo estimate of the same probability, for comparison: hits in independent runs, with a band of twice the binomial standard error (rounded up) and the rule of three when there is no hit.</summary>
public static class PlainEstimate
{
    private const long Billion = 1_000_000_000;

    /// <summary>The estimate and the half-width of its band, both in ppb.</summary>
    public static (long Ppb, long BandPpb) Of(long hits, long runs)
    {
        if (runs < 1 || hits < 0 || hits > runs) throw new ArgumentException("hits are 0 to runs, and there is at least one run");
        var ppb = (long)((BigInteger)hits * Billion / runs);
        if (hits == 0) return (0, (long)((3 * (BigInteger)Billion + runs - 1) / runs));
        // se^2 = hits (runs - hits) / runs^3 in probability, so hits (runs - hits) 10^18 / runs^3 in ppb squared.
        var numerator = (BigInteger)hits * (runs - hits) * Billion * Billion;
        var denominator = (BigInteger)runs * runs * runs;
        var se = IntegerRoot.CeilingSqrt((numerator + denominator - 1) / denominator);
        return (ppb, (long)(2 * se));
    }
}
