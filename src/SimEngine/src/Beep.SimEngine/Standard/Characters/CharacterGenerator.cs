using System;
using System.Collections.Generic;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// The one function that makes a character: archetype, seed and index in, a <see cref="CharacterRecord"/> out. A squad slot, an
    /// individual spawned directly and a materialized member all come from here, so they are the same character for the same inputs.
    /// The order of draws is fixed: given name, family name, each stat, each skill, then the traits.
    /// </summary>
    public static class CharacterGenerator
    {
        public static CharacterRecord Generate(CharacterArchetypeDef archetype, ulong seed, int index)
        {
            if (archetype == null) throw new ArgumentNullException(nameof(archetype));
            var random = new CharacterRandom(seed, index);
            string given = archetype.GivenNames.Names[random.Next(archetype.GivenNames.Names.Count)];
            string family = archetype.FamilyNames.Names[random.Next(archetype.FamilyNames.Names.Count)];

            var stats = new int[archetype.Stats.Count];
            for (int i = 0; i < stats.Length; i++)
            {
                StatRange range = archetype.Stats[i];
                stats[i] = range.Min + random.Next(range.Max - range.Min + 1);
            }

            var skills = new int[archetype.Skills.Count];
            for (int i = 0; i < skills.Length; i++)
            {
                SkillRange range = archetype.Skills[i];
                skills[i] = range.Min + random.Next(range.Max - range.Min + 1);
            }

            return new CharacterRecord(given + " " + family, stats, skills, Traits(archetype, ref random));
        }

        private static string[] Traits(CharacterArchetypeDef archetype, ref CharacterRandom random)
        {
            int count = archetype.TraitMin + random.Next(archetype.TraitMax - archetype.TraitMin + 1);
            var remaining = new List<TraitChoice>(archetype.TraitPool);
            var chosen = new string[count];
            for (int n = 0; n < count; n++)
            {
                long total = 0;
                for (int i = 0; i < remaining.Count; i++) total += remaining[i].Weight;
                long pick = random.Next(checked((int)total));
                int at = 0;
                while (pick >= remaining[at].Weight) { pick -= remaining[at].Weight; at++; }
                chosen[n] = remaining[at].Trait.Id;
                remaining.RemoveAt(at);
            }
            return chosen;
        }
    }
}
