using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>Coverage of the pacing-free SimEngine surface and the events the host layer relies on.</summary>
public sealed class SimEngineTests
{
    private static SimGame Quick(string contentDir, bool objectives, SimEventOutbox? outbox = null)
    {
        var modules = new List<GameModule> { new ContentAccessModule(), new WorldSetupModule(), new QuickGame.Engine.QuickModule() };
        modules.Add(new ObjectiveModule()); // the shared content declares it
        modules.Add(new DirectorBridgeModule(new DirectorTickets(), Array.Empty<IDirectorEffect>(), outbox));
        var definition = new GameDefinition("quick-game", modules);
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, contentDir));
        return new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).NewGame(new NewGameRequest { Scenario = "quick", Seed = 5 });
    }

    [Fact]
    public void UpdateRunsExactlyTheNamedTickAndRefusesAnyOther()
    {
        var game = Quick("ObjectiveContent", false);
        Assert.True(game.Update(1));
        var hash = game.Hash();
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Update(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Update(1));
        Assert.Equal(1, game.Tick);
        Assert.Equal(hash, game.Hash());
    }

    [Fact]
    public void SameSeedAndTicksGiveTheSameStateAndSaveRestoresIt()
    {
        var a = Quick("ObjectiveContent", false);
        var b = Quick("ObjectiveContent", false);
        for (var t = 1; t <= 5; t++) { a.Update(t); b.Update(t); }
        Assert.Equal(a.Hash(), b.Hash());
        var definition = new GameDefinition("quick-game", new GameModule[] { new ContentAccessModule(), new WorldSetupModule(), new QuickGame.Engine.QuickModule(), new ObjectiveModule(),
            new DirectorBridgeModule(new DirectorTickets(), Array.Empty<IDirectorEffect>()) });
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "ObjectiveContent"));
        var restored = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).Load(a.Save());
        Assert.Equal(a.Hash(), restored.Hash());
        a.Update(6); restored.Update(6);
        Assert.Equal(a.Hash(), restored.Hash());
    }

    [Fact]
    public void ObjectiveChangesAreAnnouncedAsEventsWhenTheyHappen()
    {
        var outbox = new SimEventOutbox();
        var game = Quick("ObjectiveContent", true, outbox);
        var component = new SimComponent(game, null, outbox);
        var heard = new List<string>();
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        session.AddComponent(component, 1);
        session.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.event.objective-changed") heard.Add(e.PayloadJson); });
        session.Init();
        session.Update(3 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
        Assert.Contains(heard, h => h.Contains("\"Active\""));
        Assert.Contains(heard, h => h.Contains("\"Completed\""));
    }

    [Fact]
    public void CashChangesAreAnnouncedOncePerChangeAndNeverWhenNothingChanged()
    {
        var outbox = new SimEventOutbox();
        var tickets = new DirectorTickets();
        var game = GameRuntime.Headless.TycoonWorld.Factory(GameRuntime.Headless.TycoonWorld.Definition(tickets, outbox, [new GameRuntime.Headless.CreditEffect()]))
            .NewGame(new NewGameRequest { Scenario = "accounts-only", Seed = 3 });
        var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new NullReporter());
        var changes = new List<string>();
        session.AddComponent(new SimComponent(game, null, outbox), 1);
        session.AddComponent(new SimEffectDispatcher(game, tickets), 2);
        session.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.event.cash-changed") changes.Add(e.PayloadJson); });
        session.Init();
        session.Update(2 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
        Assert.Empty(changes);
        tickets.Issue("effect:x", "credit", "100");
        game.Seat(0).Submit(new DirectorEffectCommand { Key = "effect:x", Effect = "credit", Payload = "100" });
        session.Update(2 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
        Assert.Single(changes);
        session.Update(2 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
        Assert.Single(changes);
    }

    [Fact]
    public void ADirectorCommandWithoutATicketIsRefusedByTheSim()
    {
        var game = Quick("ObjectiveContent", false);
        var seq = game.Seat(0).Submit(new DirectorEffectCommand { Key = "forged", Effect = "credit", Payload = "1" });
        game.Update(1);
        var answers = new List<Beep.SimEngine.CommandResult>(); long cursor = 0;
        game.Seat(0).ReadResults(ref cursor, answers);
        Assert.Equal("gf.director.no-ticket", answers.Single(a => a.Sequence == seq).Refusal.Id);
    }

    private sealed class NullReporter : IRuntimeReporter { public void Report(string code, string message, Exception? exception) { } }
}
