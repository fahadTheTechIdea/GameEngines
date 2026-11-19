using System;
using System.Collections.Generic;
using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// A formation: the entity a squad of it uses, how many characters of each archetype it holds, the catalog unit it was authored from
    /// (explicit null for none) and the items it carries as a whole. The roster is the single source of its headcount.
    /// </summary>
    public sealed class CharacterFormationDef : Def
    {
        private string _squadEntity;
        private readonly List<(string Archetype, int Count)> _members = new List<(string, int)>();

        public EntityDef SquadEntity { get; private set; }
        public IReadOnlyList<RosterEntry> Members { get; private set; }
        public int MaxSquads { get; private set; }
        public string CatalogUnit { get; private set; }
        public IReadOnlyList<EquipmentLink> Equipment { get; private set; }
        /// <summary>The characters of one squad: the sum of the roster counts.</summary>
        public int Strength { get; private set; }

        protected override void ReadCore(JsonReader reader)
        {
            _squadEntity = reader.String("squad_entity");
            foreach (JsonReader entry in CharacterJson.Entries(reader, "members"))
            { _members.Add((entry.String("archetype"), entry.Int("count"))); entry.End(); }
            MaxSquads = reader.Int("max_squads");
            if (MaxSquads < 1) throw reader.Error("max_squads must be at least 1");
            if (!reader.Has("catalog_unit")) throw reader.Error("missing 'catalog_unit' (a unit id, or explicit null)");
            CatalogUnit = reader.NullableString("catalog_unit");
            if (CatalogUnit != null && string.IsNullOrWhiteSpace(CatalogUnit)) throw reader.Error("catalog_unit must not be blank");
            Equipment = CharacterJson.Equipment(reader, "equipment").AsReadOnly();
        }

        protected override void ResolveCore(ContentResolver content)
        {
            SquadEntity = content.Def<EntityDef>(_squadEntity);
            ISquadDefinition squad = SquadEntity.Component<ISquadDefinition>() ?? throw content.Fail("entity '" + _squadEntity + "' has no squad component");
            if (_members.Count == 0) throw content.Fail("a formation needs at least one roster entry");
            var members = new List<RosterEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            foreach (var (archetype, count) in _members)
            {
                if (!seen.Add(archetype)) throw content.Fail("archetype '" + archetype + "' is listed twice");
                if (count < 1) throw content.Fail("archetype '" + archetype + "' needs a count of at least 1");
                members.Add(new RosterEntry(content.Def<CharacterArchetypeDef>(archetype), count));
                total += count;
            }
            if (total > squad.MaxMembers) throw content.Fail("roster of " + total + " exceeds the " + squad.MaxMembers + " slots of entity '" + _squadEntity + "'");
            Strength = (int)total;
            Members = members.AsReadOnly();
        }
    }
}
