namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// A SplitMix64 stream for one character: seeded only by the caller's seed and the character's index, never by a clock, a culture or the
    /// order things were iterated in. The same pair always gives the same draws on every platform.
    /// </summary>
    public struct CharacterRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public CharacterRandom(ulong seed, int index)
        {
            unchecked
            {
                _state = Mix(seed ^ ((ulong)(uint)index + 1UL) * Gamma);
            }
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += Gamma;
                return Mix(_state);
            }
        }

        /// <summary>A value from 0 up to but not including <paramref name="bound"/>, without modulo bias.</summary>
        public int Next(int bound)
        {
            if (bound < 1) throw new System.ArgumentOutOfRangeException(nameof(bound));
            unchecked
            {
                ulong range = (ulong)bound;
                ulong threshold = (0UL - range) % range;
                ulong value;
                do { value = NextUInt64(); } while (value < threshold);
                return (int)(value % range);
            }
        }

        /// <summary>The seed of the <paramref name="index"/>th member of a group seeded with <paramref name="seed"/>.</summary>
        public static ulong Derive(ulong seed, int index)
        {
            var stream = new CharacterRandom(seed, index);
            return stream.NextUInt64();
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
