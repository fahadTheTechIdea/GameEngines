using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.Headless;

/// <summary>The one owner of the tycoon world: its code modules and its authored content directory.</summary>
public static class TycoonWorld
{
    public const string Scenario = "company";

    public static string ContentDirectory => Path.Combine(Path.GetDirectoryName(typeof(TycoonWorld).Assembly.Location)!, "Content");

    /// <summary>Every module the Content/manifest.json names. The director bridge forwards the engine's in-world events to <paramref name="outbox"/>.</summary>
    public static GameDefinition Definition(DirectorTickets tickets, SimEventOutbox outbox, IEnumerable<IDirectorEffect> effects)
    {
        var tycoon = new ModuleRequirement(TycoonModule.ModuleId, new ModuleVersion(1, 0, 0));
        return new GameDefinition("tycoon-world", new GameModule[]
        {
            new ContentOnlyModule("proof.business"), new TycoonModule(), new ContentOnlyModule("proof.modes"),
            new ContentOnlyModule("proof.business-values", tycoon),
            new ContentAccessModule(), new WorldSetupModule(), new ObjectiveModule(),
            new ContentOnlyModule("tycoon.company",
                new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
                new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)), tycoon),
            new DirectorBridgeModule(tickets, effects, outbox),
        });
    }

    public static GameFactory Factory(GameDefinition definition, string? contentDirectory = null) =>
        new(definition, ContentSet.Load(new DiskFileSource(contentDirectory ?? ContentDirectory), definition.ContentTypes));
}
