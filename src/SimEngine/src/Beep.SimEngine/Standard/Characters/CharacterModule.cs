using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// Characters from archetype definitions. It declares the character and squad component keys, the sections that define stats, skills,
    /// traits, name pools, archetypes and formations, and the events of the factory, and it binds the one <see cref="ICharacterFactory"/>.
    /// It depends on no other module: a game with no map can raise squads and characters without a place.
    /// </summary>
    public sealed class CharacterModule : GameModule
    {
        public const string ModuleId = "sim.characters";

        public override string Id => ModuleId;
        public override ModuleVersion Version => new ModuleVersion(1, 0, 0);

        public override void DeclareContent(ContentTypes types)
        {
            types.Section<CharacterStatDef>("character_stats");
            types.Section<CharacterSkillDef>("character_skills");
            types.Section<CharacterTraitDef>("character_traits");
            types.Section<NamePoolDef>("name_pools");
            types.Section<CharacterArchetypeDef>("character_archetypes");
            types.Section<CharacterFormationDef>("character_formations");
            types.Component<CharacterDef>("character", 1);
            types.Component<SquadDef>("squad", 2);
        }

        public override void Compose(WorldComposer composer)
        {
            composer.Events.Register<FormationSpawned>(FormationSpawned.EventId, 1);
            composer.Events.Register<CharactersSpawned>(CharactersSpawned.EventId, 1);
            composer.Events.Register<CharacterMaterialized>(CharacterMaterialized.EventId, 1);
            composer.Events.Register<SquadCasualties>(SquadCasualties.EventId, 1);
            composer.Events.Register<SquadReinforced>(SquadReinforced.EventId, 1);
            composer.Services.Bind<ICharacterFactory>(new CharacterFactoryService(composer.World));
        }
    }
}
