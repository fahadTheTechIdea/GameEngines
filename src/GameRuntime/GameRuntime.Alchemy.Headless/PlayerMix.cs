namespace GameRuntime.Alchemy.Headless;

/// <summary>A stateless mix of a seed, a tick and a draw number (SplitMix64 finalizer), so a policy needs no random state that a checkpoint would have to hold.</summary>
public static class PlayerMix
{
    public static ulong Mix(ulong seed, long tick, int draw)
    {
        var z = seed + 0x9E3779B97F4A7C15UL * (ulong)(tick * 256 + draw + 1);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
