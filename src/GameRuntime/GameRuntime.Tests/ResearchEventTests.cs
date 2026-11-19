using Beep.SimEngine.Commands;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>Technology and stock changes reach the mission as events, driven by real authored content (ResearchContent).</summary>
public sealed class ResearchEventTests
{
    private sealed class NullReporter : IRuntimeReporter { public void Report(string code, string message, Exception? exception) { } }

    private static (SimGame Game, SimEventOutbox Outbox) Lab()
    {
        var outbox = new SimEventOutbox();
        var definition = new GameDefinition("lab-game", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new WorkModule(),
            new CompanyKnowledgeModule(), new TechnologyModule(), new LabModule(),
            new DirectorBridgeModule(new DirectorTickets(), Array.Empty<IDirectorEffect>(), outbox) });
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "ResearchContent"));
        var game = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).NewGame(new NewGameRequest { Scenario = "lab", Seed = 11 });
        return (game, outbox);
    }

    private sealed class LabModule : GameModule
    {
        public override string Id => "lab";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    [Fact]
    public void StockAndTechnologyChangesArriveAtTheMissionAsEvents()
    {
        var (game, outbox) = Lab();
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var heard = new List<ObservationPosted>();
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.Subscribe<ObservationPosted>(e => heard.Add(e));
        session.Init();
        var seq = game.Seat(0).Submit(new PostOrderCommand { Plan = "lab.basics", Target = new Beep.SimEngine.EntityId(1), Priority = 70 });
        session.Update(2 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
        var answers = new List<Beep.SimEngine.CommandResult>(); long cursor = 0; game.Seat(0).ReadResults(ref cursor, answers);
        var answer = answers.Single(a => a.Sequence == seq);
        Assert.True(answer.Accepted, "research order refused: " + answer.Refusal.Id);
        session.Update(200 * 1_000_000L / game.AuthoredTicksPerSecond);
        Assert.Contains(heard, e => e.Kind == "sim.event.stock-changed" && e.PayloadJson.Contains("lab.sample"));
        var learned = Assert.Single(heard, e => e.Kind == "sim.event.technology-learned");
        Assert.Contains("lab.basics", learned.PayloadJson);
    }

    /// <summary>A mission-style stock change through the engine's own inventory transaction ("produced:3" or "consumed:2").</summary>
    private sealed class ChangeSamples : IDirectorEffect
    {
        public string Name => "samples";
        private static (Beep.SimEngine.ResourceFlowKind Kind, int Count) Read(string payload)
        {
            var parts = payload.Split(':');
            return (parts[0] == "produced" ? Beep.SimEngine.ResourceFlowKind.Produced : Beep.SimEngine.ResourceFlowKind.Consumed, int.Parse(parts[1]));
        }
        private static Beep.SimEngine.ResourceInventoryIntent Intent(World world, string payload)
        {
            var (kind, count) = Read(payload);
            var bill = new Beep.SimEngine.Bill(new[] { new Beep.SimEngine.ResourceAmount(world.Content.ResourceId("lab.sample"), Beep.SimEngine.Fix.FromInt(count)) });
            return new Beep.SimEngine.ResourceInventoryIntent(new Beep.SimEngine.ReservationOwner("gf.director", 1), new Beep.SimEngine.PlayerId(0),
                new Beep.SimEngine.EntityId(1), bill, kind, new Beep.SimEngine.Code("test.samples"));
        }
        public Beep.SimEngine.Code Check(World world, string payload) =>
            world.Services.Get<Beep.SimEngine.IResourceTransaction>().EvaluateInventory(Intent(world, payload)).Ready
                ? Beep.SimEngine.Code.None : new Beep.SimEngine.Code("test.samples-refused");
        public void Apply(World world, string payload)
        {
            if (!world.Services.Get<Beep.SimEngine.IResourceTransaction>().CommitInventory(Intent(world, payload)).Committed)
                throw new InvalidOperationException("sample change refused after admission");
        }
    }

    [Fact]
    public void ProducedAndConsumedStockChangesArriveWithTheirOwnKind()
    {
        var outbox = new SimEventOutbox();
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("lab-game", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new WorkModule(),
            new CompanyKnowledgeModule(), new TechnologyModule(), new LabModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { new ChangeSamples() }, outbox) });
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "ResearchContent"));
        var game = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).NewGame(new NewGameRequest { Scenario = "lab", Seed = 11 });
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var heard = new List<ObservationPosted>();
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.AddComponent(new SimEffectDispatcher(game, tickets), 2);
        session.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.event.stock-changed") heard.Add(e); });
        session.Init();
        long step = 2 * 1_000_000L / game.AuthoredTicksPerSecond + 10;
        session.Update(step);
        foreach (var (key, payload) in new[] { ("effect:a", "produced:3"), ("effect:b", "consumed:2") })
        {
            tickets.Issue(key, "samples", payload);
            game.Seat(0).Submit(new DirectorEffectCommand { Key = key, Effect = "samples", Payload = payload });
            session.Update(step);
        }
        Assert.Contains(heard, e => e.PayloadJson.Contains("\"Initialized\""));
        var produced = Assert.Single(heard, e => e.PayloadJson.Contains("\"Produced\""));
        var consumed = Assert.Single(heard, e => e.PayloadJson.Contains("\"Consumed\""));
        Assert.Contains("lab.sample", produced.PayloadJson);
        Assert.Contains("lab.sample", consumed.PayloadJson);
    }

    /// <summary>Moves samples from the bench to the annex through the engine's own reserved-transfer protocol.</summary>
    private sealed class MoveSamples : IDirectorEffect
    {
        public string Name => "move-samples";
        private static Beep.SimEngine.ResourceTransferIntent Intent(World world, string payload)
        {
            var owner = new Beep.SimEngine.ReservationOwner("gf.director", 1);
            var resource = world.Content.ResourceId("lab.sample");
            var amount = Beep.SimEngine.Fix.FromInt(int.Parse(payload));
            var bench = world.Entities.Get(new Beep.SimEngine.EntityId(1))!.Get<Beep.SimEngine.Standard.IInventory>();
            var annex = world.Entities.Get(new Beep.SimEngine.EntityId(2))!.Get<Beep.SimEngine.Standard.IInventory>();
            return new Beep.SimEngine.ResourceTransferIntent(owner, new Beep.SimEngine.PlayerId(0),
                bench.ReserveOut(owner, resource, amount), annex.ReserveIn(owner, resource, amount), resource, amount);
        }
        public Beep.SimEngine.Code Check(World world, string payload) => Beep.SimEngine.Code.None;
        public void Apply(World world, string payload)
        {
            if (!world.Services.Get<Beep.SimEngine.IResourceTransaction>().Commit(Intent(world, payload)).Committed)
                throw new InvalidOperationException("sample transfer refused");
        }
    }

    [Fact]
    public void AnEndpointToEndpointTransferArrivesAsATransferFlow()
    {
        var outbox = new SimEventOutbox();
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("lab-game", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new WorkModule(),
            new CompanyKnowledgeModule(), new TechnologyModule(), new LabModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { new MoveSamples() }, outbox) });
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "ResearchContent"));
        var game = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).NewGame(new NewGameRequest { Scenario = "lab", Seed = 11 });
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var heard = new List<ObservationPosted>();
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.AddComponent(new SimEffectDispatcher(game, tickets), 2);
        session.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.event.stock-changed") heard.Add(e); });
        session.Init();
        long step = 2 * 1_000_000L / game.AuthoredTicksPerSecond + 10;
        session.Update(step);
        tickets.Issue("effect:move", "move-samples", "2");
        game.Seat(0).Submit(new DirectorEffectCommand { Key = "effect:move", Effect = "move-samples", Payload = "2" });
        session.Update(step);
        var transfers = heard.Where(e => e.PayloadJson.Contains("\"isTransfer\":true")).ToList();
        Assert.NotEmpty(transfers);
        Assert.All(transfers, t => Assert.Contains("lab.sample", t.PayloadJson));
        Assert.Contains(transfers, t => t.PayloadJson.Contains("\"StockTransfer\""));
    }
}
