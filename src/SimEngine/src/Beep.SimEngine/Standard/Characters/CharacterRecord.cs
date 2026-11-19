using System;
using System.Collections.Generic;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// A character as the generator draws it: a pure function of the archetype, a seed and an index. Stats and skills follow the order
    /// of the archetype's lists; traits are in the order they were drawn.
    /// </summary>
    public sealed class CharacterRecord
    {
        public CharacterRecord(string name, int[] stats, int[] skills, string[] traits)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            Traits = traits ?? throw new ArgumentNullException(nameof(traits));
        }

        public string Name { get; }
        public int[] Stats { get; }
        public int[] Skills { get; }
        public string[] Traits { get; }

        public bool SameAs(CharacterRecord other)
        {
            if (other == null || Name != other.Name) return false;
            return Equal(Stats, other.Stats) && Equal(Skills, other.Skills) && Equal(Traits, other.Traits);
        }

        private static bool Equal<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) where T : IEquatable<T>
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }
    }
}
