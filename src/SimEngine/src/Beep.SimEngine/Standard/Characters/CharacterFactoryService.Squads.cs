using System;
using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Maps;

namespace Beep.SimEngine.Standard
{
    public sealed partial class CharacterFactoryService
    {
        private const ulong CasualtyKeyMask = 0xA5A5A5A5DEADBEEFUL;

        public Code CheckReinforce(EntityId squad, int rosterEntry, int count)
        {
            SquadComponent state = SquadOf(squad);
            if (state == null) return CharacterCodes.UnknownSquad;
            if (count < 1) return CharacterCodes.BadCount;
            if (rosterEntry < 0 || rosterEntry >= FormationOf(state).Members.Count) return CharacterCodes.BadRosterEntry;
            if ((long)state.Total + count > state.Entity.Def.Component<ISquadDefinition>().MaxMembers) return CharacterCodes.TooMany;
            return Code.None;
        }

        public Code CheckReinforceByRoster(EntityId squad, int count) => CheckReinforce(squad, 0, count);

        public void ReinforceByRoster(EntityId squad, int count)
        {
            Code refusal = CheckReinforceByRoster(squad, count);
            if (!refusal.IsNone) throw new InvalidOperationException("reinforce refused: " + refusal.Id);
            SquadComponent state = SquadOf(squad);
            var members = FormationOf(state).Members;
            long total = 0;
            for (int i = 0; i < members.Count; i++) total += members[i].Count;
            var shares = new int[members.Count];
            var remainders = new long[members.Count];
            int given = 0;
            for (int i = 0; i < members.Count; i++)
            {
                long exact = (long)count * members[i].Count;
                shares[i] = (int)(exact / total);
                remainders[i] = exact % total;
                given += shares[i];
            }
            // What is left over goes one each to the entries with the largest remainders, the earlier entry winning a tie.
            for (int left = count - given; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < remainders.Length; i++) if (remainders[i] > remainders[best]) best = i;
                shares[best]++;
                remainders[best] = -1;
            }
            for (int i = 0; i < shares.Length; i++) if (shares[i] > 0) state.Reinforce(i, shares[i]);
            state.Entity.Touch();
            _world.Events.Publish(new SquadReinforced(squad, count, state.Headcount));
        }

        public void Reinforce(EntityId squad, int rosterEntry, int count)
        {
            Code refusal = CheckReinforce(squad, rosterEntry, count);
            if (!refusal.IsNone) throw new InvalidOperationException("reinforce refused: " + refusal.Id);
            SquadComponent state = SquadOf(squad);
            state.Reinforce(rosterEntry, count);
            state.Entity.Touch();
            _world.Events.Publish(new SquadReinforced(squad, count, state.Headcount));
        }

        public Code CheckMaterialize(EntityId squad, int slot)
        {
            SquadComponent state = SquadOf(squad);
            if (state == null) return CharacterCodes.UnknownSquad;
            if (slot < 0 || slot >= state.Total) return CharacterCodes.BadSlot;
            if (state.IsLost(slot)) return CharacterCodes.SlotLost;
            if (state.TryMaterialized(slot, out _)) return CharacterCodes.SlotMaterialized;
            CharacterArchetypeDef archetype = FormationOf(state).Members[state.RosterIndexOf(slot)].Archetype;
            ITilePresenceDefinition tile = archetype.Entity.Component<ITilePresenceDefinition>();
            if (tile != null && tile.Blocks) return CharacterCodes.Blocking;
            return CheckBatch(archetype.Entity, state.Entity.Owner, 1, LayoutAt(state, archetype), out _);
        }

        public EntityId Materialize(EntityId squad, int slot)
        {
            Code refusal = CheckMaterialize(squad, slot);
            if (!refusal.IsNone) throw new InvalidOperationException("materialize refused: " + refusal.Id);
            SquadComponent state = SquadOf(squad);
            CharacterArchetypeDef archetype = FormationOf(state).Members[state.RosterIndexOf(slot)].Archetype;
            CheckBatch(archetype.Entity, state.Entity.Owner, 1, LayoutAt(state, archetype), out System.Collections.Generic.List<Beep.SimEngine.Kernel.SpatialPlacement> placements);
            Entity entity = Create(archetype.Entity, state.Entity.Owner, placements?[0]);
            entity.Get<CharacterComponent>().Assign(archetype.Id, CharacterGenerator.Generate(archetype, state.Seed, slot), state.Seed, slot, squad, slot);
            state.Materialize(slot, entity.Id);
            entity.Touch();
            state.Entity.Touch();
            _world.Events.Publish(new CharacterMaterialized(squad, slot, entity.Id));
            return entity.Id;
        }

        /// <summary>A materialized member stands where its squad stands; a squad with no place in a space gives an individual with none.</summary>
        private SpawnLayout LayoutAt(SquadComponent squad, CharacterArchetypeDef archetype)
        {
            if (archetype.Entity.Component<ISpatialPresenceDefinition>() == null) return SpawnLayout.None;
            ITilePresence presence = squad.Entity.Get<ITilePresence>();
            if (presence == null || !presence.Placed) return SpawnLayout.None;
            return SpawnLayout.Grid(presence.Tile, 1, 0);
        }

        public Code CheckCasualties(EntityId squad, int count)
        {
            SquadComponent state = SquadOf(squad);
            if (state == null) return CharacterCodes.UnknownSquad;
            if (count < 1) return CharacterCodes.BadCount;
            if (count > state.Aggregate) return CharacterCodes.NotEnoughMembers;
            return Code.None;
        }

        public void Casualties(EntityId squad, int count)
        {
            Code refusal = CheckCasualties(squad, count);
            if (!refusal.IsNone) throw new InvalidOperationException("casualties refused: " + refusal.Id);
            SquadComponent state = SquadOf(squad);
            ulong key = state.Seed ^ CasualtyKeyMask;
            int removed = 0;
            while (removed < count)
            {
                if (state.CasualtyCursor >= state.Total) throw new InvalidOperationException("squad casualty order is exhausted before the admitted count was removed");
                int slot = SlotPermutation.Permute(state.CasualtyCursor, state.Total, key);
                state.AdvanceCursor(1);
                // A slot is skipped when it is an individual or was already lost (an individual that died, or a slot met in an earlier order).
                if (state.TryMaterialized(slot, out _) || state.IsLost(slot)) continue;
                state.MarkLost(slot);
                removed++;
            }
            state.Entity.Touch();
            _world.Events.Publish(new SquadCasualties(squad, count, state.Headcount));
        }

        public Code CheckKill(EntityId character)
        {
            Entity entity = _world.Entities.Get(character);
            CharacterComponent state = entity?.Get<CharacterComponent>();
            if (state == null || !state.Squad.IsValid) return CharacterCodes.NotMaterialized;
            SquadComponent squad = SquadOf(state.Squad);
            if (squad == null) return CharacterCodes.UnknownSquad;
            return _world.CheckDespawn(entity);
        }

        public void Kill(EntityId character)
        {
            Code refusal = CheckKill(character);
            if (!refusal.IsNone) throw new InvalidOperationException("kill refused: " + refusal.Id);
            Entity entity = _world.Entities.Get(character);
            CharacterComponent state = entity.Get<CharacterComponent>();
            SquadComponent squad = SquadOf(state.Squad);
            squad.Release(state.Slot);
            squad.MarkLost(state.Slot);
            _world.Despawn(entity);
            squad.Entity.Touch();
            _world.Events.Publish(new SquadCasualties(squad.Entity.Id, 1, squad.Headcount));
        }
    }
}
