namespace GameRuntime.Headless;

/// <summary>SplitMix64: the only source of variation a player policy has. The same seed gives the same sequence on every machine.</summary>
public sealed class PlayerRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A value in [minimum, maximum].</summary>
    public int Next(int minimum, int maximum) => minimum + (int)(NextUInt64() % (ulong)(maximum - minimum + 1));
}
