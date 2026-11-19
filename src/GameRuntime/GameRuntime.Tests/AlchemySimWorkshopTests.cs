using AlchemyEngine;
using Beep.SimEngine;
using Beep.SimEngine.Commands;
using GameRuntime.AlchemySim.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The alchemist workshop: discovery gates the sim's own production. The session is driven from outside exactly as a host would, and
/// the gate is checked against the sim's command itself, not against the player or any UI.
/// </summary>
public sealed class AlchemySimWorkshopTests : IDisposable
{
    private const string Smelt = "space.smelt.planetx";
    private const string Assemble = "space.assemble.parts";
    private static readonly string[] Fixed = ["--frame-ms", "1000", "--seconds", "120", "--seed", "1"];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("alchemy-workshop-");

    public void Dispose() => _dir.Delete(true);

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    private sealed record Run(int Exit, string[] Lines, string Error)
    {
        public string Result => Lines.Single(l => l.StartsWith("result ", StringComparison.Ordinal));
        public string Hash => Lines.Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal));
    }

    private Run Play(IReadOnlyList<IGameComponent>? extra, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = WorkshopHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, error, extra);
        return new Run(exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    private Run Play(params string[] args) => Play(null, args);

    private sealed class Thrower : IGameComponent
    {
        public string Id => "thrower";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context) => throw new InvalidOperationException("deliberate failure");
    }

    private sealed class CollectingReporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    private static WorkshopSession NewSession(out CollectingReporter reporter)
    {
        reporter = new CollectingReporter();
        var session = WorkshopSession.Create(1, reporter);
        session.Runtime.Init();
        return session;
    }

    private static void Tick(WorkshopSession session, int ticks)
    {
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;
        for (var i = 0; i < ticks; i++) session.Runtime.Update(oneTick);
    }

    /// <summary>Submits the engine's own enqueue command straight to the sim, the way any client could, and returns the engine's answer.</summary>
    private static CommandResult Enqueue(WorkshopSession session, string transformation, string processor)
    {
        var entity = session.Game.Seat(0).Snapshot.Entities.Single(e => e.Def == processor).Id;
        var sequence = session.Game.Seat(0).Submit(new EnqueueTransformationCommand
        {
            Transformer = entity, Capability = new Code("space.operation"), Transformation = transformation, Count = 1,
        });
        Tick(session, 2);
        var results = new List<CommandResult>();
        long cursor = 0;
        session.Game.Seat(0).ReadResults(ref cursor, results);
        return results.Single(r => r.Sequence == sequence);
    }

    private static void Combine(WorkshopSession session, string items)
    {
        session.Input.Press(WorkshopActions.Combine, items);
        session.Input.Release(WorkshopActions.Combine);
    }

    // ---- the engine enforces the gate ---------------------------------------------------------------------------------------

    [Fact]
    public void TheSimItselfRefusesAnUndiscoveredRecipeAndAcceptsItOnceDiscovered()
    {
        var session = NewSession(out var reporter);

        // Before discovery: the engine's command is refused with the engine's own locked code. No player, no UI is involved.
        var before = Enqueue(session, Smelt, "space.smelter");
        Assert.False(before.Accepted);
        Assert.Equal("technology.locked", before.Refusal.Id);
        Assert.Empty(session.Alchemy.Book.Recipes);

        // Discovery: the player tries ore with the crucible; the book learns the recipe, then the sim's knowledge follows.
        Combine(session, "space.planetx.ore+space.crucible");
        Tick(session, 8);
        Assert.Contains(Smelt, session.Alchemy.Book.Recipes);
        Assert.Empty(session.Drift());

        var after = Enqueue(session, Smelt, "space.smelter");
        Assert.True(after.Accepted, "refused: " + after.Refusal.Id);
        Assert.Empty(reporter.Codes);
    }

    [Fact]
    public void DiscoveringOneRecipeUnlocksOnlyThatRecipe()
    {
        var session = NewSession(out _);
        Combine(session, "space.planetx.ore+space.crucible");
        Tick(session, 8);
        Assert.True(Enqueue(session, Smelt, "space.smelter").Accepted);
        var other = Enqueue(session, Assemble, "space.assembler");
        Assert.False(other.Accepted);
        Assert.Equal("technology.locked", other.Refusal.Id);
        Assert.DoesNotContain(Assemble, session.Alchemy.Book.Recipes);
    }

    [Fact]
    public void NothingChangesInTheSimBeforeTheUpdateThatAnswersTheDiscovery()
    {
        var session = NewSession(out _);
        Tick(session, 1);
        var before = session.Fingerprint();
        Combine(session, "space.blank+space.fixture");
        Assert.Equal(before, session.Fingerprint());
        Tick(session, 1); // the player raises the request
        Assert.DoesNotContain(Assemble, session.Alchemy.Book.Recipes);
        Tick(session, 1); // the alchemy component answers it and the unlock is requested
        Assert.Contains(Assemble, session.Alchemy.Book.Recipes);
        Assert.NotEmpty(session.Drift());       // the book knows it; the sim has not been told yet
        Tick(session, 2);                       // the sim applies the unlock effect
        Assert.Empty(session.Drift());
    }

    [Fact]
    public void ADisagreementBetweenTheBookAndTheSimIsReportedAndBlocksACheckpoint()
    {
        var session = NewSession(out _);
        Tick(session, 1);
        session.Alchemy.Book.LearnRecipe(Assemble); // a tampering host teaches the book without the sim
        var drift = Assert.Single(session.Drift());
        Assert.Equal(new RecipeDrift(Assemble, InBook: true, InSim: false), drift);
        var refused = Assert.Throws<AlchemyRefusedException>(() => WorkshopCheckpoint.Capture(session));
        Assert.Equal(WorkshopCheckpoint.DriftCode, refused.Code);
    }

    [Fact]
    public void ARecipeTheSimLearnedByOtherMeansIsTaughtToTheBookWithoutAskingTheSimAgain()
    {
        var session = NewSession(out _);
        var requests = new List<EffectRequested>();
        var discoveries = new List<DiscoveryMade>();
        session.Runtime.Subscribe<EffectRequested>(requests.Add);
        session.Runtime.Subscribe<DiscoveryMade>(discoveries.Add);
        Tick(session, 1);
        // What the director bridge forwards when finished research records the technology (a stand-in observation here: no research is run).
        session.Runtime.Submit(new ObservationPosted("sim.event.technology-learned", "{\"company\":0,\"technology\":\"space.discover.assemble\"}", 1));
        Tick(session, 4);
        Assert.Contains(Assemble, session.Alchemy.Book.Recipes);
        Assert.True(session.Alchemy.Book.Knows("space.finished"));
        Assert.Empty(requests);
        Assert.All(discoveries, d => Assert.StartsWith("external:", d.RequestId));
    }

    // ---- the whole game, run as the executable runs it ----------------------------------------------------------------------

    [Fact]
    public void ThePlayerDiscoversThenOrdersAndTheSimProducesWhatItWasShown()
    {
        var run = Play([.. Fixed, "--player", "alchemist"]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("state=Ended ending=produced", run.Result);
        Assert.Contains("elements=7/7 recipes=2/2", run.Result);
        Assert.Contains("drift=0", run.Result);
        Assert.Contains(run.Lines, l => l.Contains("combined space.blank+space.fixture -> reacted via space.assemble.parts makes space.finished keeps space.fixture"));
        Assert.Contains(run.Lines, l => l.Contains("accepted=True APPLIED") && l.Contains(" order o"));
        // The sim's own stock events: inputs hauled, three blanks consumed, one finished part produced.
        Assert.Contains(run.Lines, l => l.Contains("\"resource\":\"space.blank\"") && l.Contains("\"kind\":\"Consumed\"") && l.Contains("\"amount\":12884901888"));
        Assert.Contains(run.Lines, l => l.Contains("\"resource\":\"space.finished\"") && l.Contains("\"kind\":\"Produced\""));
        Assert.Contains(run.Lines, l => l.Contains("\"kind\":\"StockTransfer\"") && l.Contains("\"isTransfer\":true"));
    }

    [Fact]
    public void EachStepIsAnsweredATickAfterItWasRaised()
    {
        var run = Play([.. Fixed, "--player", "alchemist"]);
        long TickOf(string needle) => long.Parse(run.Lines.First(l => l.Contains(needle))[1..].Split(' ')[0]);
        var discovered = TickOf("discovered recipe space.assemble.parts");
        var learned = TickOf("sim.event.technology-learned") ;
        var raised = TickOf("player raise order space.assemble.parts");
        var accepted = TickOf("order o");
        Assert.Equal(discovered + 1, learned);   // the unlock request is answered by the sim in the next tick
        Assert.True(raised > learned);           // the player orders only after it has seen the discovery
        Assert.True(accepted > raised);          // and the order is answered in a later tick than it was raised
        Assert.Contains(run.Lines, l => l.StartsWith("command effect:unlock:space.assemble.parts sim applied=True QUEUED"));
    }

    [Fact]
    public void TheSameSeedPlaysTheSameGameTwice()
    {
        Assert.Equal(Play([.. Fixed, "--player", "alchemist"]).Hash, Play([.. Fixed, "--player", "alchemist"]).Hash);
    }

    [Fact]
    public void ASavedCheckpointResumesToExactlyTheUninterruptedResult()
    {
        var whole = Play([.. Fixed, "--player", "alchemist"]);
        var save = Path("mid.save");
        var first = Play("--frame-ms", "1000", "--seconds", "3", "--player", "alchemist", "--seed", "1", "--save", save);
        Assert.Equal(0, first.Exit);
        Assert.Contains(first.Lines, l => l.StartsWith("checkpoint tick=", StringComparison.Ordinal));
        Assert.DoesNotContain("state=Ended", first.Result);
        var second = Play([.. Fixed, "--player", "alchemist", "--load", save]);
        Assert.Equal(0, second.Exit);
        Assert.Contains(second.Lines, l => l.StartsWith("resumed tick=", StringComparison.Ordinal));
        Assert.Equal(whole.Result, second.Result);
    }

    [Fact]
    public void AJournalOfThePlayersRunReplaysEveryTick()
    {
        var journal = Path("run.journal");
        var recorded = Play([.. Fixed, "--player", "alchemist", "--journal", journal]);
        Assert.Equal(0, recorded.Exit);
        var replay = Play("--replay", journal, "--seed", "1");
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay.Lines);

        var bytes = File.ReadAllBytes(journal);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(Path("bad.journal"), bytes);
        Assert.Equal(WorkshopHeadlessGame.JournalRefused, Play("--replay", Path("bad.journal")).Exit);
    }

    [Fact]
    public void ADamagedCheckpointAndBadArgumentsAreRefusedWithTheirOwnExitCodes()
    {
        var save = Path("ok.save");
        Assert.Equal(0, Play("--frame-ms", "1000", "--seconds", "3", "--player", "alchemist", "--save", save).Exit);
        var bytes = File.ReadAllBytes(save);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(Path("bad.save"), bytes);
        Assert.Equal(WorkshopHeadlessGame.CheckpointRefused, Play("--frame-ms", "1000", "--seconds", "5", "--load", Path("bad.save")).Exit);
        Assert.Equal(WorkshopHeadlessGame.Refused, Play("--player", "wizard").Exit);
        Assert.Equal(WorkshopHeadlessGame.Refused, Play("--load", save, "--journal", Path("x.journal")).Exit);
        File.WriteAllText(Path("bad.txt"), "at 1 mix a+b\n");
        Assert.Equal(WorkshopHeadlessGame.Refused, Play("--script", Path("bad.txt")).Exit);
    }

    [Fact]
    public void AScriptedHumanCanDiscoverAndOrderToo()
    {
        File.WriteAllText(Path("human.txt"), "at 1 combine space.blank+space.fixture\nat 3 order space.assemble.parts\n");
        var run = Play("--frame-ms", "1000", "--seconds", "120", "--script", Path("human.txt"));
        Assert.Equal(0, run.Exit);
        Assert.Contains("host input order space.assemble.parts", run.Lines);
        Assert.Contains("state=Ended ending=produced", run.Result);
        // An order placed before the recipe is known would have been refused by the sim, which is why the script waits.
        Assert.Contains(run.Lines, l => l.Contains("accepted=True"));
    }

    [Fact]
    public void AnOrderForARecipeNotYetDiscoveredIsRefusedByTheSimWithItsOwnCode()
    {
        File.WriteAllText(Path("early.txt"), "at 0 order space.smelt.planetx\n");
        var run = Play("--frame-ms", "1000", "--seconds", "5", "--script", Path("early.txt"));
        Assert.Equal(0, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains(" order o") && l.Contains("accepted=False technology.locked"));
    }

    [Fact]
    public void AFailingComponentStopsTheSessionAndTheRunExitsWithAFault()
    {
        var run = Play([new Thrower()], "--frame-ms", "100", "--seconds", "5");
        Assert.Equal(WorkshopHeadlessGame.Faulted, run.Exit);
        Assert.Contains("state=Faulted", run.Result);
    }
}
