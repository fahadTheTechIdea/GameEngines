using System.Numerics;

namespace EngineCommon.Balance;

/// <summary>Integer square roots for the integer-bounded uncertainty statements.</summary>
public static class IntegerRoot
{
    /// <summary>The smallest whole number whose square is at least <paramref name="value"/> (0 for 0). Newton's method on integers.</summary>
    public static BigInteger CeilingSqrt(BigInteger value)
    {
        if (value.Sign < 0) throw new ArgumentException("a square root needs a value of 0 or more", nameof(value));
        if (value.IsZero) return BigInteger.Zero;
        var x = BigInteger.One << (int)((value.GetBitLength() + 1) / 2);      // at or above the root
        while (true)
        {
            var y = (x + value / x) >> 1;
            if (y >= x) break;
            x = y;
        }
        return x * x >= value ? x : x + 1;
    }
}
