using System.Text;

namespace EngineCommon;

/// <summary>
/// A random number that is a pure function of a seed, a stream name and an index: no state, no clock, nothing shared between calls, so
/// rolls can be asked for in any order and replayed identically. SplitMix64 over an FNV-1a hash of the stream name.
/// </summary>
public static class DeterministicRoll
{
    public static ulong Next(ulong seed, string stream, long index)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var b in Encoding.UTF8.GetBytes(stream)) { hash ^= b; hash *= 1099511628211UL; }
            var z = seed + 0x9E3779B97F4A7C15UL * (hash + 1) + 0xD6E8FEB86659FD93UL * (ulong)(index + 1);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>A whole number from 0 up to but not including <paramref name="bound"/>.</summary>
    public static long Below(ulong seed, string stream, long index, long bound)
    {
        if (bound <= 0) throw new ArgumentException("a roll bound is above zero", nameof(bound));
        return (long)(Next(seed, stream, index) % (ulong)bound);
    }

    /// <summary>Per-mille, 0 to 999.</summary>
    public static int Permille(ulong seed, string stream, long index) => (int)Below(seed, stream, index, 1000);
}