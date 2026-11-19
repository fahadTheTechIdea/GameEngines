using EngineCommon;

namespace WorldEventsEngine;

/// <summary>
/// The integer arithmetic the engine is built on. Everything is a whole number: shares are per mille, probabilities parts per million, and a product is taken in 128 bits and divided once, so the only error is the
/// rounding stated here. Rounding is never to nearest and never silent: a quantity that must be a whole number (people, steps) is rounded <i>stochastically</i>, up with exactly the probability of its fraction, drawn
/// from the counter-based random streams, so the expected value is exact and a replay reproduces every rounding.
/// </summary>
internal static class WorldMath
{
    public const int Permille = 1000;
    public const long Ppm = 1_000_000;
    private const long FractionScale = 1_000_000_000;

    public static long Clamp(long value, long minimum, long maximum) => value < minimum ? minimum : value > maximum ? maximum : value;

    public static long Saturated(long value) => Clamp(value, 0, WorldFacts.Saturate);

    /// <summary>The integer square root, rounded down (Newton's method on integers).</summary>
    public static long Isqrt(long value)
    {
        if (value <= 0) return 0;
        long x = value, y = (x + 1) / 2;
        while (y < x)
        {
            x = y;
            y = (x + value / x) / 2;
        }
        return x;
    }

    /// <summary>A share of a whole number, per mille, rounded down.</summary>
    public static long Permilled(long value, long permille) => (long)((Int128)value * permille / Permille);

    /// <summary>
    /// <c>numerator / denominator</c> as a whole number whose expected value is the exact quotient: the integer part, plus one with the probability of the fraction (to one part in 10^9), drawn from the stream at
    /// the index. The same stream and index always give the same answer.
    /// </summary>
    public static long StochasticDivide(Int128 numerator, Int128 denominator, ulong seed, string stream, long index)
    {
        if (numerator <= 0 || denominator <= 0) return 0;
        var whole = numerator / denominator;
        var remainder = numerator % denominator;
        if (remainder == 0) return (long)whole;
        var fraction = (long)(remainder * FractionScale / denominator);
        var extra = DeterministicRoll.Below(seed, stream, index, FractionScale) < fraction ? 1 : 0;
        return (long)(whole + extra);
    }

    /// <summary>
    /// A bell-shaped integer from -1,998 to 1,998 (standard deviation about 577): the sum of four uniform numbers from 0 to 999, centred, cut from the four 16-bit parts of one roll. Four terms of the central-limit sum are a
    /// close bell within about 2.5 standard deviations and stop at 3.5, which is far shorter than a true normal tail (the engine states this error; the heavy tail of rain comes from its own mixture).
    /// </summary>
    public static int Bell(ulong roll)
    {
        var sum = 0;
        for (var part = 0; part < 4; part++) sum += (int)(((roll >> (16 * part)) & 0xFFFF) * 1000 >> 16);
        return sum - 1998;
    }

    /// <summary>The noise of a given spread (hundredths of a degree, say) from a roll: <c>Bell * sigma / 577</c>.</summary>
    public static long Noise(ulong roll, long sigma) => Bell(roll) * sigma / 577;

    /// <summary>The index a roll below <paramref name="total"/> picks from weights: the first whose running sum passes the roll.</summary>
    public static int Pick(IReadOnlyList<long> weights, long roll)
    {
        long running = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            running += weights[i];
            if (roll < running) return i;
        }
        return weights.Count - 1;
    }
}
