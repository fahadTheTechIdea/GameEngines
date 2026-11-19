namespace SpatialEngine;

/// <summary>Distances in whole millimetres. The Euclidean distance is the exact floor of the square root, found with integers only, so every machine agrees to the last millimetre.</summary>
public static class Metric
{
    public const long CoordinateLimit = 1_000_000_000_000;

    public static long Distance(long dx, long dy)
    {
        var square = (UInt128)((Int128)dx * dx + (Int128)dy * dy);
        return (long)Root(square);
    }

    public static long Distance(Point a, Point b) => Distance(checked(a.X - b.X), checked(a.Y - b.Y));

    /// <summary>The largest r with r times r at most <paramref name="value"/>, by the bit-by-bit method.</summary>
    internal static UInt128 Root(UInt128 value)
    {
        UInt128 result = 0;
        UInt128 bit = UInt128.One << 126;
        while (bit > value) bit >>= 2;
        while (bit != 0)
        {
            if (value >= result + bit)
            {
                value -= result + bit;
                result = (result >> 1) + bit;
            }
            else result >>= 1;
            bit >>= 2;
        }
        return result;
    }
}