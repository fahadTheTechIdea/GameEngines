using Beep.SimEngine;
using Beep.SimEngine.Commands;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Maps;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// A moving mover is hashed, saved and validated every tick. Its route is walked once per change, not per
/// observation; these tests show a route in flight still saves, loads and continues identically, and a changed
/// order is followed to its new goal.
/// </summary>
public sealed class MoverRouteTests
{
    private sealed class LabModule : GameModule
    {
        public override string Id => "lab";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    private static GameDefinition Definition() => new("route-lab", new GameModule[] {
        new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new TileNavigationModule(),
        new WorkModule(), new LogisticsModule(), new LabModule(),
        new DirectorBridgeModule(new DirectorTickets(), Array.Empty<IDirectorEffect>(), new SimEventOutbox()) });

    private static GameFactory Factory()
    {
        var definition = Definition();
        return new GameFactory(definition, ContentSet.Load(
            new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "HaulContent")), definition.ContentTypes));
    }
    private static SimGame NewGame(GameFactory factory)
    {
        var game = factory.NewGame(new NewGameRequest { Scenario = "lab", Seed = 5 });
        game.Initialize();
        return game;
    }

    private static readonly EntityId Hauler = new(3);

    private static void Order(SimGame game, TileCoord to)
    {
        long seq = game.Seat(0).Submit(new MoveVehicleCommand { Vehicle = Hauler, To = to });
        Run(game, 2);
        var answers = new List<CommandResult>(); long cursor = 0; game.Seat(0).ReadResults(ref cursor, answers);
        var answer = answers.Single(a => a.Sequence == seq);
        Assert.True(answer.Accepted, "move order refused: " + answer.Refusal.Id);
    }

    private static void Run(SimGame game, int ticks) { for (int i = 0; i < ticks; i++) game.Update(game.Tick + 1); }

    [Fact]
    public void ARouteInFlightSavesLoadsAndContinuesToTheSameWorld()
    {
        var factory = Factory();
        var original = NewGame(factory);
        Order(original, new TileCoord(11, 7));
        Run(original, 6);
        var save = original.Save();
        var loaded = factory.Load(save);
        Assert.Equal(original.Hash(), loaded.Hash());
        Run(original, 40); Run(loaded, 40);
        Assert.Equal(original.Hash(), loaded.Hash());
        Assert.Equal(original.Save(), loaded.Save());
    }

    [Fact]
    public void ANewOrderMidRouteReplacesTheRouteAndTheMoverArrivesAtTheNewGoal()
    {
        var factory = Factory();
        var game = NewGame(factory);
        Order(game, new TileCoord(11, 7));
        Run(game, 4);
        Order(game, new TileCoord(0, 0));
        Run(game, 600);
        Assert.True(game.Seat(0).View.TryGetEntity(Hauler, out EntityView view));
        var at = Assert.IsType<TileSpatialSnapshot>(view.Spatial);
        Assert.Equal(new TileCoord(0, 0), at.Tile);
    }

    [Fact]
    public void TwoRunsOfTheSameOrdersHaveTheSameHashAtEveryTick()
    {
        var factory = Factory();
        var a = NewGame(factory); var b = NewGame(factory);
        Order(a, new TileCoord(11, 7)); Order(b, new TileCoord(11, 7));
        for (int i = 0; i < 50; i++) { Run(a, 1); Run(b, 1); Assert.Equal(a.Hash(), b.Hash()); }
    }
}
