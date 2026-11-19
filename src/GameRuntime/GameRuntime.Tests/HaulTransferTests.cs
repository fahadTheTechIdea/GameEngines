using Beep.SimEngine.Commands;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>A worker-driven, authored transfer: a hauler carries lab.sample from the bench to the annex on the engine's normal haul order.</summary>
public sealed class HaulTransferTests
{
    private sealed class NullReporter : IRuntimeReporter { public void Report(string code, string message, Exception? exception) { } }

    private sealed class LabModule : GameModule
    {
        public override string Id => "lab";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    [Fact]
    public void AnAuthoredHaulOrderMovesStockAndArrivesAsTransferFlows()
    {
        var outbox = new SimEventOutbox();
        var definition = new GameDefinition("haul-lab", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new TileNavigationModule(),
            new WorkModule(), new LogisticsModule(), new LabModule(),
            new DirectorBridgeModule(new DirectorTickets(), Array.Empty<IDirectorEffect>(), outbox) });
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "HaulContent"));
        var content = ContentSet.Load(files, definition.ContentTypes);
        var game = new GameFactory(definition, content).NewGame(new NewGameRequest { Scenario = "lab", Seed = 11 });
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var heard = new List<ObservationPosted>();
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.event.stock-changed") heard.Add(e); });
        session.Init();
        long step = 2 * 1_000_000L / game.AuthoredTicksPerSecond + 10;
        session.Update(step);
        var seq = game.Seat(0).Submit(new HaulResourcesCommand {
            Source = new Beep.SimEngine.EntityId(1), Destination = new Beep.SimEngine.EntityId(2), Worker = new Beep.SimEngine.EntityId(3),
            Resource = new Beep.SimEngine.ResourceId(1), Ceiling = Beep.SimEngine.Fix.FromInt(3), Priority = 50 });
        session.Update(step);
        var answers = new List<Beep.SimEngine.CommandResult>(); long cursor = 0; game.Seat(0).ReadResults(ref cursor, answers);
        var answer = answers.Single(a => a.Sequence == seq);
        Assert.True(answer.Accepted, "haul order refused: " + answer.Refusal.Id);
        for (var i = 0; i < 60 && !heard.Any(h => h.PayloadJson.Contains("\"isTransfer\":true")); i++) session.Update(5 * 1_000_000L);
        var transfers = heard.Where(h => h.PayloadJson.Contains("\"isTransfer\":true")).ToList();
        Assert.NotEmpty(transfers);
        Assert.All(transfers, t => Assert.Contains("lab.sample", t.PayloadJson));
        Assert.Contains(transfers, t => t.PayloadJson.Contains("\"StockTransfer\""));
    }
}
