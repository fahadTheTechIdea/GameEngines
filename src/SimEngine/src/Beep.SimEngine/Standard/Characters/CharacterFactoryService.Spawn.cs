using System;
using System.Collections.Generic;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    public sealed partial class CharacterFactoryService
    {
        public Code CheckSpawnFormation(SpawnFormationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            CharacterFormationDef formation = _world.Content.Find<CharacterFormationDef>(request.Formation);
            if (formation == null) return CharacterCodes.UnknownFormation;
            if (request.Squads < 1) return CharacterCodes.BadCount;
            if (request.Squads > formation.MaxSquads) return CharacterCodes.TooMany;
            return CheckBatch(formation.SquadEntity, request.Company, request.Squads, request.Layout, out _);
        }

        public IReadOnlyList<EntityId> SpawnFormation(SpawnFormationRequest request)
        {
            Code refusal = CheckSpawnFormation(request);
            if (!refusal.IsNone) throw new InvalidOperationException("formation spawn refused: " + refusal.Id);
            CharacterFormationDef formation = _world.Content.Find<CharacterFormationDef>(request.Formation);
            CheckBatch(formation.SquadEntity, request.Company, request.Squads, request.Layout, out List<Beep.SimEngine.Kernel.SpatialPlacement> placements);

            var counts = new int[formation.Members.Count];
            for (int i = 0; i < counts.Length; i++) counts[i] = formation.Members[i].Count;
            var ids = new List<EntityId>(request.Squads);
            for (int i = 0; i < request.Squads; i++)
            {
                Entity entity = Create(formation.SquadEntity, request.Company, placements?[i]);
                entity.Get<SquadComponent>().Raise(formation.Id, CharacterRandom.Derive(request.Seed, i), counts);
                entity.Touch();
                ids.Add(entity.Id);
            }
            _world.Events.Publish(new FormationSpawned(request.Company, formation.Id, request.Squads, checked(request.Squads * formation.Strength), ids[0]));
            return ids.AsReadOnly();
        }

        public Code CheckSpawnIndividuals(SpawnIndividualsRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            CharacterArchetypeDef archetype = _world.Content.Find<CharacterArchetypeDef>(request.Archetype);
            if (archetype == null) return CharacterCodes.UnknownArchetype;
            if (request.Count < 1) return CharacterCodes.BadCount;
            if (request.Count > archetype.MaxSpawn) return CharacterCodes.TooMany;
            return CheckBatch(archetype.Entity, request.Company, request.Count, request.Layout, out _);
        }

        public IReadOnlyList<EntityId> SpawnIndividuals(SpawnIndividualsRequest request)
        {
            Code refusal = CheckSpawnIndividuals(request);
            if (!refusal.IsNone) throw new InvalidOperationException("individual spawn refused: " + refusal.Id);
            CharacterArchetypeDef archetype = _world.Content.Find<CharacterArchetypeDef>(request.Archetype);
            CheckBatch(archetype.Entity, request.Company, request.Count, request.Layout, out List<Beep.SimEngine.Kernel.SpatialPlacement> placements);

            var ids = new List<EntityId>(request.Count);
            for (int i = 0; i < request.Count; i++)
            {
                Entity entity = Create(archetype.Entity, request.Company, placements?[i]);
                entity.Get<CharacterComponent>().Assign(archetype.Id, CharacterGenerator.Generate(archetype, request.Seed, i), request.Seed, i, EntityId.None, -1);
                entity.Touch();
                ids.Add(entity.Id);
            }
            _world.Events.Publish(new CharactersSpawned(request.Company, archetype.Id, request.Count, ids[0]));
            return ids.AsReadOnly();
        }
    }
}
