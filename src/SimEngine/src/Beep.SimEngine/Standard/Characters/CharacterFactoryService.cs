using System;
using System.Collections.Generic;
using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Maps;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// The single owner of creating characters (see <see cref="ICharacterFactory"/>). It holds no state of its own: everything it makes is
    /// state of the entities, so it is saved, loaded and hashed with them. It creates entities only through the world's own
    /// PrepareCreation and CommitCreation, so identity, ownership and placement are admitted exactly as for any other entity.
    /// </summary>
    public sealed partial class CharacterFactoryService : ICharacterFactory
    {
        private readonly World _world;

        public CharacterFactoryService(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Admits a whole batch of entities of one definition before any is made: identities are available, the layout fits the definition,
        /// and each placement passes the world's own creation admission. Blocking definitions need a spacing that keeps their footprints apart,
        /// so the batch cannot conflict with itself and the entities can then be made one after another without a second refusal.
        /// </summary>
        private Code CheckBatch(EntityDef def, PlayerId company, int count, SpawnLayout layout, out List<SpatialPlacement> placements)
        {
            placements = null;
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            _world.RequireHealthyUpdate();
            if ((long)_world.Entities.NextIdentity.Value + count >= int.MaxValue) return CharacterCodes.IdentitiesExhausted;
            ISpatialPresenceDefinition presence = def.Component<ISpatialPresenceDefinition>();
            if (presence == null)
            {
                if (layout.Placed) return CharacterCodes.UnexpectedPlacement;
                return _world.PrepareCreation(def, company, EntityId.None, null, out _);
            }
            if (!layout.Placed) return CharacterCodes.NoPlacement;
            if (!(_world.Space is ITileSpatialSpace space) || space.Id != presence.Space) return CharacterCodes.UnsupportedSpace;
            ITilePresenceDefinition tile = def.Component<ITilePresenceDefinition>();
            if (tile == null) return CharacterCodes.UnsupportedSpace;
            if (layout.Columns < 1 || layout.Spacing < 0) return CharacterCodes.BadLayout;
            if (tile.Blocks && layout.Spacing < Math.Max(tile.Width, tile.Height)) return CharacterCodes.BadLayout;

            var made = new List<SpatialPlacement>(count);
            for (int i = 0; i < count; i++)
            {
                long x = layout.Origin.X + (long)(i % layout.Columns) * layout.Spacing;
                long y = layout.Origin.Y + (long)(i / layout.Columns) * layout.Spacing;
                if (x < 0 || y < 0 || x >= space.Width || y >= space.Height) return CharacterCodes.BadLayout;
                var coord = new TileCoord((int)x, (int)y);
                var placement = new TilePlacement(space.Id, coord, FixVec2.Centre(coord));
                Code refusal = _world.PrepareCreation(def, company, EntityId.None, placement, out _);
                if (!refusal.IsNone) return refusal;
                made.Add(placement);
            }
            placements = made;
            return Code.None;
        }

        /// <summary>Creates one entity of a definition with the world's own admission and commit.</summary>
        private Entity Create(EntityDef def, PlayerId company, SpatialPlacement placement)
        {
            Code refusal = _world.PrepareCreation(def, company, EntityId.None, placement, out IEntityCreation creation);
            if (!refusal.IsNone) throw new InvalidOperationException("creation was admitted for the batch but refused for '" + def.Id + "': " + refusal.Id);
            return _world.CommitCreation(creation);
        }

        private SquadComponent SquadOf(EntityId id)
        {
            Entity entity = _world.Entities.Get(id);
            return entity?.Get<SquadComponent>();
        }

        private CharacterFormationDef FormationOf(SquadComponent squad) =>
            _world.Content.Find<CharacterFormationDef>(squad.Formation) ?? throw new InvalidOperationException("squad '" + squad.Entity.Id + "' names formation '" + squad.Formation + "' that content does not define");

        public CharacterRecord Describe(EntityId squad, int slot)
        {
            SquadComponent state = SquadOf(squad) ?? throw new ArgumentException("no such squad", nameof(squad));
            if (slot < 0 || slot >= state.Total) throw new ArgumentOutOfRangeException(nameof(slot));
            CharacterFormationDef formation = FormationOf(state);
            return CharacterGenerator.Generate(formation.Members[state.RosterIndexOf(slot)].Archetype, state.Seed, slot);
        }
    }
}
