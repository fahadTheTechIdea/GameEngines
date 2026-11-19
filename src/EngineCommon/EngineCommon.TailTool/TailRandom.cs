namespace EngineCommon.TailTool;

/// <summary>
/// The tool's one pseudo-random sequence: SplitMix64 (Steele, Lea and Flood 2014) from a seed the caller names, so every resample, bootstrap and simulated null in the tool is a pure function of its inputs (no clock, no environment). Floating
/// point is allowed here as everywhere in this offline tool.
/// </summary>
public struct TailRandom(ulong seed)
{
    private ulong _state = seed;

    /// <summary>The next 64 random bits.</summary>
    public ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A number in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (Next() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>A standard normal number (Box and Muller 1958), from two uniform numbers.</summary>
    public double NextNormal()
    {
        var u = 1.0 - NextDouble();                    // in (0, 1], so the logarithm is finite
        var v = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v);
    }
}
