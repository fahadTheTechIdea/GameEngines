using System;
using System.Collections.Generic;
using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// What kind of character the factory makes: the entity definition an individual of it uses, the name pools it draws from, the
    /// ranges of its stats and skills, the traits it may have, the catalog items it carries and its roles. Every field is stated; the
    /// engine fills none of them in.
    /// </summary>
    public sealed class CharacterArchetypeDef : Def
    {
        private string _entity, _given, _family;
        private readonly List<(string Id, int Min, int Max)> _stats = new List<(string, int, int)>();
        private readonly List<(string Id, int Min, int Max)> _skills = new List<(string, int, int)>();
        private readonly List<(string Id, int Weight)> _traits = new List<(string, int)>();

        public EntityDef Entity { get; private set; }
        public NamePoolDef GivenNames { get; private set; }
        public NamePoolDef FamilyNames { get; private set; }
        public IReadOnlyList<StatRange> Stats { get; private set; }
        public IReadOnlyList<SkillRange> Skills { get; private set; }
        public IReadOnlyList<TraitChoice> TraitPool { get; private set; }
        public int TraitMin { get; private set; }
        public int TraitMax { get; private set; }
        public IReadOnlyList<EquipmentLink> Equipment { get; private set; }
        public IReadOnlyList<string> Roles { get; private set; }
        /// <summary>The most individuals of this archetype one spawn command may create.</summary>
        public int MaxSpawn { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            _entity = reader.String("entity");
            MaxSpawn = reader.Int("max_spawn");
            if (MaxSpawn < 1) throw reader.Error("max_spawn must be at least 1");
            _given = reader.String("given_names");
            _family = reader.String("family_names");
            foreach (JsonReader entry in CharacterJson.Entries(reader, "stats"))
            { _stats.Add((entry.String("stat"), entry.Int("min"), entry.Int("max"))); entry.End(); }
            foreach (JsonReader entry in CharacterJson.Entries(reader, "skills"))
            { _skills.Add((entry.String("skill"), entry.Int("min"), entry.Int("max"))); entry.End(); }
            JsonReader traits = reader.Child("traits") ?? throw reader.Error("missing 'traits'");
            TraitMin = traits.Int("min");
            TraitMax = traits.Int("max");
            foreach (JsonReader entry in CharacterJson.Entries(traits, "pool"))
            { _traits.Add((entry.String("trait"), entry.Int("weight"))); entry.End(); }
            traits.End();
            Equipment = CharacterJson.Equipment(reader, "equipment").AsReadOnly();
            string[] roles = CharacterJson.RequiredStrings(reader, "roles");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string role in roles)
                if (string.IsNullOrWhiteSpace(role) || !seen.Add(role)) throw reader.Error("roles must be non-blank and unique, but found '" + role + "'");
            Roles = Array.AsReadOnly(roles);
        }

        protected override void ResolveCore(ContentResolver content)
        {
            Entity = content.Def<EntityDef>(_entity);
            if (Entity.Component<ICharacterDefinition>() == null) throw content.Fail("entity '" + _entity + "' has no character component");
            GivenNames = content.Def<NamePoolDef>(_given);
            FamilyNames = content.Def<NamePoolDef>(_family);

            var stats = new List<StatRange>();
            var seenStats = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, min, max) in _stats)
            {
                if (!seenStats.Add(id)) throw content.Fail("stat '" + id + "' is listed twice");
                CharacterStatDef stat = content.Def<CharacterStatDef>(id);
                if (min > max || min < stat.Min || max > stat.Max) throw content.Fail("stat '" + id + "' range " + min + " to " + max + " must lie inside " + stat.Min + " to " + stat.Max);
                stats.Add(new StatRange(stat, min, max));
            }
            Stats = stats.AsReadOnly();

            var skills = new List<SkillRange>();
            var seenSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, min, max) in _skills)
            {
                if (!seenSkills.Add(id)) throw content.Fail("skill '" + id + "' is listed twice");
                CharacterSkillDef skill = content.Def<CharacterSkillDef>(id);
                if (min < 0 || min > max || max > skill.MaxLevel) throw content.Fail("skill '" + id + "' range " + min + " to " + max + " must lie inside 0 to " + skill.MaxLevel);
                skills.Add(new SkillRange(skill, min, max));
            }
            Skills = skills.AsReadOnly();

            var pool = new List<TraitChoice>();
            var seenTraits = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, weight) in _traits)
            {
                if (!seenTraits.Add(id)) throw content.Fail("trait '" + id + "' is listed twice");
                if (weight < 1) throw content.Fail("trait '" + id + "' needs a weight of at least 1");
                pool.Add(new TraitChoice(content.Def<CharacterTraitDef>(id), weight));
            }
            if (TraitMin < 0 || TraitMin > TraitMax || TraitMax > pool.Count)
                throw content.Fail("traits need 0 <= min <= max <= pool size, but min " + TraitMin + ", max " + TraitMax + ", pool " + pool.Count);
            TraitPool = pool.AsReadOnly();
        }
    }
}
