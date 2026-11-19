using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>Composes SimEngine content generated from a pack into a world using SimEngine's own readers.</summary>
internal static class SimWorld
{
    private sealed class CatalogWorldModule(bool motion, Action<ResolvedContent>? capture) : GameModule
    {
        public override void Compose(WorldComposer composer) => capture?.Invoke(composer.Content);

        public override string Id => CatalogToSim.WorldModuleId;
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => motion
            ? [new(ContentAccessModule.ModuleId, Version), new(TransformationModule.ModuleId, Version), new(WorldSetupModule.ModuleId, Version), new(MotionModule.ModuleId, Version), new(TileNavigationModule.ModuleId, Version)]
            : [new(ContentAccessModule.ModuleId, Version), new(TransformationModule.ModuleId, Version), new(WorldSetupModule.ModuleId, Version)];
    }

    /// <summary>The resolved content of the composed world: the transformations SimEngine will run, as its own readers made them.</summary>
    public static ResolvedContent Content(SimConversion conversion)
    {
        ResolvedContent? content = null;
        Compose(conversion, c => content = c);
        Assert.NotNull(content);
        return content!;
    }

    public static SimGame Compose(SimConversion conversion) => Compose(conversion, null);

    private static SimGame Compose(SimConversion conversion, Action<ResolvedContent>? capture)
    {
        var motion = conversion.Vehicles.Count > 0;
        var modules = new List<GameModule> { new ContentAccessModule(), new InventoryModule(), new OperationsModule(), new TransformationModule(), new WorldSetupModule() };
        if (motion) { modules.Add(new MotionModule()); modules.Add(new TileNavigationModule()); }
        modules.Add(new CatalogWorldModule(motion, capture));
        var definition = new GameDefinition("catalog-world", modules.ToArray());
        var files = new PreloadedFileSource();
        foreach (var (path, text) in conversion.Files) files.Add(path, text);
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var request = new NewGameRequest { Scenario = "catalog", Seed = 1 };
        request.Seats.Add(Controller.Human);
        return factory.NewGame(request);
    }
}
