namespace DiplomacyEngine;

/// <summary>The integer arithmetic the engine is built on: shares are per mille, standing is in millionths, and a product is taken in 64 bits (checked: an overflow is an exception) and divided once.</summary>
internal static class DipMath
{
    public const int Permille = 1000;
    public const long Million = 1_000_000;

    public static long Clamp(long value, long minimum, long maximum) => value < minimum ? minimum : value > maximum ? maximum : value;

    public static int ClampInt(long value, int minimum, int maximum) => (int)Clamp(value, minimum, maximum);

    /// <summary>
    /// Moves <paramref name="value"/> towards <paramref name="target"/> by <paramref name="ratePpm"/> millionths of the distance, rounded towards the value, but by at least one when the rate is above zero and the two differ,
    /// so a decay always arrives instead of stalling a few units away.
    /// </summary>
    public static long Approach(long value, long target, long ratePpm)
    {
        if (value == target || ratePpm <= 0) return value;
        var step = (target - value) * ratePpm / Million;
        if (step == 0) step = target > value ? 1 : -1;
        return value + step;
    }
}
