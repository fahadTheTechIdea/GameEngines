using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// The one owner of the workshop's world: its code modules and its authored content directory. The content is SimEngine's own
/// Planet X production set (resources, two recipes, a staffed logistics world) plus the discovery gate document a game authors to say
/// which technology unlocks which recipe.
/// </summary>
public static class WorkshopWorld
{
    public const string Scenario = "planet-x";

    public static string ContentDirectory => Path.Combine(AppContext.BaseDirectory, "Workshop");

    public static GameDefinition Definition(SimRecipeViewHolder holder, DirectorTickets tickets, SimEventOutbox outbox) =>
        new("alchemist-workshop", new GameModule[]
        {
            new ContentAccessModule(), new InventoryModule(), new OperationsModule(), new MotionModule(), new WorkModule(), new LogisticsModule(),
            new TransformationModule(), new CompanyKnowledgeModule(), new TechnologyModule(), new WorldSetupModule(), new TileNavigationModule(),
            new WorkshopModule(),
            new DirectorBridgeModule(tickets, [new DiscoverRecipeEffect(holder)], outbox),
            new AlchemySimModule(holder),
        });

    public static GameFactory Factory(GameDefinition definition, string? contentDirectory = null) =>
        Factory(definition, new DiskFileSource(contentDirectory ?? ContentDirectory));

    public static GameFactory Factory(GameDefinition definition, IFileSource files) =>
        new(definition, ContentSet.Load(files, definition.ContentTypes));

    public static SimGame NewGame(GameFactory factory, ulong seed)
    {
        var request = new NewGameRequest { Scenario = Scenario, Seed = seed };
        request.Seats.Add(Controller.Human);
        request.Seats.Add(Controller.Human);
        return factory.NewGame(request);
    }
}
