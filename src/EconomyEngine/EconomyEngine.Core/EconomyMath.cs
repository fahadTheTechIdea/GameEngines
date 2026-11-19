using System.Numerics;

namespace EconomyEngine;

/// <summary>
/// The only arithmetic of money and price. Everything is a whole number: money is minor units of a currency, a price is thousandths of a minor unit ("milli"), a rate is parts per million, a ratio parts per
/// thousand. Products are formed in 128 bits so no valid state overflows, and every division says how it rounds: <see cref="Trunc"/> toward zero, <see cref="Nearest"/> to the nearest with ties away from zero.
/// </summary>
internal static class EconomyMath
{
    public const long Permille = 1000;
    public const long Ppm = 1_000_000;

    /// <summary>a * b / d, truncating toward zero. The result must fit a long (checked).</summary>
    public static long Trunc(long a, long b, long d) => checked((long)((Int128)a * b / d));

    /// <summary>a * b / d rounded to the nearest whole number, ties away from zero.</summary>
    public static long Nearest(long a, long b, long d)
    {
        var product = (Int128)a * b;
        Int128 divisor = d;
        var negative = (product < 0) != (divisor < 0);
        var magnitude = product < 0 ? -product : product;
        var by = divisor < 0 ? -divisor : divisor;
        var rounded = (magnitude + by / 2) / by;
        return checked((long)(negative ? -rounded : rounded));
    }

    public static long Clamp(long value, long low, long high) => value < low ? low : value > high ? high : value;

    /// <summary>
    /// The fixed payment of a loan of <paramref name="principal"/> repaid in <paramref name="periods"/> equal payments at <paramref name="ratePpm"/> a period (the annuity formula A = P i (1+i)^n / ((1+i)^n - 1), exact in big
    /// integers, rounded to the nearest minor unit); with no interest it is the principal over the periods.
    /// </summary>
    public static long Annuity(long principal, long ratePpm, int periods)
    {
        if (ratePpm == 0) return Nearest(principal, 1, periods);
        var s = BigInteger.Pow(Ppm + ratePpm, periods);
        var t = BigInteger.Pow(Ppm, periods);
        var numerator = (BigInteger)principal * ratePpm * s;
        var denominator = (BigInteger)Ppm * (s - t);
        var quotient = BigInteger.Divide(numerator * 2 + denominator, denominator * 2);
        return checked((long)quotient);
    }
}
