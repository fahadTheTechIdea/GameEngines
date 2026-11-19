using AlchemyEngine;
using GameRuntime.Alchemy.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The alchemy component on the game layer, and the headless alchemy game run in-process exactly as the executable runs.</summary>
public sealed class AlchemyRuntimeTests : IDisposable
{
    private static readonly string[] Fixed = ["--frame-ms", "1000", "--seconds", "5000"];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("alchemy-game-");

    public void Dispose() => _dir.Delete(true);

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    private string Script(string name, string text)
    {
        var path = Path(name);
        File.WriteAllText(path, text);
        return path;
    }

    private sealed record Run(int Exit, string[] Lines, string Error)
    {
        public string Result => Lines.Single(l => l.StartsWith("result ", StringComparison.Ordinal));
        public string Hash => Lines.Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal));
    }

    private Run Play(IReadOnlyList<IGameComponent>? extra, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = AlchemyHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, error, extra);
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

    private static AlchemyContent Demo() => AlchemyContentLoader.Load(File.ReadAllText(AlchemyContentSource.DefaultContentPath));

    private static long TicksFor(AlchemySession session, int ticks)
    {
        long ran = 0;
        for (var i = 0; i < ticks; i++) ran += session.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond);
        return ran;
    }

    // ---- nothing happens until Update -------------------------------------------------------------------------------------

    [Fact]
    public void InputWrittenOrEventsSubmittedChangeNothingUntilTheNextUpdate()
    {
        var reporter = new CollectingReporter();
        var session = AlchemySession.Create(Demo(), 1, reporter);
        var heard = new List<object>();
        session.Runtime.Subscribe<Combined>(e => heard.Add(e));
        session.Runtime.Subscribe<DiscoveryMade>(e => heard.Add(e));
        session.Runtime.Init();
        var before = session.Alchemy.Book.Hash();

        session.Input.Press(AlchemyActions.Combine, "fire+water");
        session.Input.Release(AlchemyActions.Combine);
        session.Runtime.Submit(new CombineRequested("host-1", "earth+water", ""));

        Assert.Equal(before, session.Alchemy.Book.Hash());
        Assert.Empty(heard);
        Assert.False(session.Input.IsQuiet);
        Assert.Equal(0, session.Clock.Tick);
        Assert.Empty(reporter.Codes);
    }

    [Fact]
    public void ASubmittedRequestIsAnsweredInTheNextTickAndAPlayerRaisedOneInTheTickAfter()
    {
        var session = AlchemySession.Create(Demo(), 1, new CollectingReporter());
        var combined = new List<Combined>();
        session.Runtime.Subscribe<Combined>(combined.Add);
        session.Runtime.Init();

        // A host submit is delivered at the start of tick 1, before the rules component runs, so tick 1 answers it.
        session.Runtime.Submit(new CombineRequested("host-1", "earth+water", ""));
        TicksFor(session, 1);
        Assert.Equal(["host-1"], combined.Select(c => c.RequestId).ToArray());
        Assert.Equal(1, combined[0].Tick);
        Assert.Equal("reacted", combined[0].Outcome);

        // The player runs after the rules component: what it raises in tick 2 can only be answered in tick 3.
        session.Input.Press(AlchemyActions.Combine, "fire+water");
        session.Input.Release(AlchemyActions.Combine);
        var hashBefore = session.Alchemy.Book.Hash();
        TicksFor(session, 1);
        Assert.Single(combined);
        Assert.Equal(hashBefore, session.Alchemy.Book.Hash());
        Assert.False(session.Alchemy.IsQuiet);
        TicksFor(session, 1);
        Assert.Equal(2, combined.Count);
        Assert.Equal(3, combined[1].Tick);
        Assert.Equal("make-steam", combined[1].RecipeId);
        Assert.True(session.Alchemy.Book.Knows("steam"));
    }

    [Fact]
    public void ARequestForSomethingNotYetDiscoveredIsRefusedAndTeachesNothing()
    {
        var session = AlchemySession.Create(Demo(), 1, new CollectingReporter());
        var refused = new List<CombineRefused>();
        session.Runtime.Subscribe<CombineRefused>(refused.Add);
        session.Runtime.Init();
        var before = session.Alchemy.Book.Hash();
        session.Runtime.Submit(new CombineRequested("r1", "steam+fire", ""));
        session.Runtime.Submit(new CombineRequested("r2", "", ""));
        session.Runtime.Submit(new CombineRequested("r3", "nothing+fire", ""));
        TicksFor(session, 1);
        Assert.Equal([AlchemyCodes.CombineNotDiscovered, AlchemyCodes.CombineEmpty, AlchemyCodes.CombineUnknownElement], refused.Select(r => r.Code).ToArray());
        Assert.Equal(before, session.Alchemy.Book.Hash());
    }

    // ---- the headless game --------------------------------------------------------------------------------------------------

    [Fact]
    public void AScriptedHumanDiscoversThroughTheInputState()
    {
        var script = Script("human.txt", "at 1 combine fire+water\nat 3 combine air+steam\nat 5 combine steam+fire\n");
        var run = Play("--frame-ms", "100", "--seconds", "10", "--script", script);
        Assert.Equal(0, run.Exit);
        Assert.Contains("host input combine fire+water", run.Lines);
        Assert.Contains(run.Lines, l => l.Contains("combined fire+water -> reacted via make-steam makes steam"));
        Assert.Contains(run.Lines, l => l.Contains("combined air+steam -> reacted via make-cloud makes cloud"));
        Assert.Contains(run.Lines, l => l.Contains("discovered element cloud"));
        Assert.Contains(run.Lines, l => l.Contains("combined fire+steam -> no-reaction") || l.Contains("combined steam+fire -> no-reaction"));
        Assert.Contains("elements=6/17", run.Result);
    }

    [Fact]
    public void AMethodicalPlayerFinishesTheBookWithoutAnyScript()
    {
        var run = Play([.. Fixed, "--player", "methodical", "--seed", "1"]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("state=Ended ending=all-discovered", run.Result);
        Assert.Contains("elements=17/17 recipes=12/12", run.Result);
        Assert.Contains(run.Lines, l => l.Contains("player decide methodical tries"));
        Assert.Contains(run.Lines, l => l.Contains("player raise combine") && l.EndsWith("from input"));
        Assert.Contains(run.Lines, l => l.Contains("combined cloud+fire -> reacted via storm makes lightning,ash"));
        Assert.Contains(run.Lines, l => l.Contains("-> reacted via grow-plant makes plant keeps air"));
        Assert.Contains(run.Lines, l => l.Contains("-> reacted via fire-brick"));
        Assert.Contains(run.Lines, l => l.Contains("smelt-metal"));
    }

    [Fact]
    public void TheTwoPoliciesPlayVisiblyDifferentlyAndTheSameSeedAndPolicyPlayTheSameGame()
    {
        var methodical = Play([.. Fixed, "--player", "methodical", "--seed", "1"]);
        var methodicalAgain = Play([.. Fixed, "--player", "methodical", "--seed", "1"]);
        var curious = Play("--frame-ms", "1000", "--seconds", "120", "--player", "curious", "--seed", "3");
        var curiousAgain = Play("--frame-ms", "1000", "--seconds", "120", "--player", "curious", "--seed", "3");
        var curiousOtherSeed = Play("--frame-ms", "1000", "--seconds", "120", "--player", "curious", "--seed", "4");
        Assert.Equal(methodical.Hash, methodicalAgain.Hash);
        Assert.Equal(curious.Hash, curiousAgain.Hash);
        Assert.NotEqual(methodical.Hash, curious.Hash);
        Assert.NotEqual(curious.Hash, curiousOtherSeed.Hash);
        Assert.Contains("player decide curious tries", string.Join('\n', curious.Lines));
        Assert.DoesNotContain("state=Ended", curious.Result);
    }

    [Fact]
    public void TheRealTimeLoopAndTheFixedFrameLoopPlayTheSameGame()
    {
        // Real time reads the Stopwatch, so a short run only proves the loop runs; the game logic it feeds is the same code.
        var real = Play("--seconds", "1", "--player", "methodical");
        Assert.Equal(0, real.Exit);
        Assert.Contains("mode=real-time", real.Lines[0]);
        Assert.Contains(real.Lines, l => l.Contains("player decide methodical"));
        var elapsedTicks = long.Parse(real.Result[(real.Result.IndexOf(" tick=", StringComparison.Ordinal) + 6)..].Split(' ')[0]);
        Assert.InRange(elapsedTicks, 1, 31);
    }

    [Fact]
    public void SavingAndLoadingContinuesExactlyLikeAnUninterruptedRun()
    {
        var whole = Play([.. Fixed, "--player", "methodical", "--seed", "1"]);
        var save = Path("part.save");
        var first = Play("--frame-ms", "1000", "--seconds", "20", "--player", "methodical", "--seed", "1", "--save", save);
        Assert.Equal(0, first.Exit);
        Assert.Contains(first.Lines, l => l.StartsWith("checkpoint tick=", StringComparison.Ordinal));
        Assert.DoesNotContain("state=Ended", first.Result);
        var second = Play([.. Fixed, "--player", "methodical", "--seed", "1", "--load", save]);
        Assert.Equal(0, second.Exit);
        Assert.Contains(second.Lines, l => l.StartsWith("resumed tick=", StringComparison.Ordinal));
        Assert.Equal(whole.Result, second.Result);
    }

    [Fact]
    public void ASavedCheckpointResumesAHumanScriptToo()
    {
        var save = Path("human.save");
        var first = Play("--frame-ms", "100", "--seconds", "3", "--script", Script("one.txt", "at 1 combine fire+water\n"), "--save", save);
        Assert.Equal(0, first.Exit);
        var second = Play("--frame-ms", "100", "--seconds", "3", "--script", Script("two.txt", "at 1 combine air+steam\n"), "--load", save);
        Assert.Equal(0, second.Exit);
        Assert.Contains(second.Lines, l => l.Contains("combined air+steam -> reacted via make-cloud"));
        Assert.Contains("elements=6/17", second.Result);
    }

    [Fact]
    public void AJournalOfAPlayerDrivenRunReplaysTickForTickAndAnotherSeedDiverges()
    {
        var journal = Path("run.journal");
        var recorded = Play([.. Fixed, "--player", "methodical", "--seed", "1", "--journal", journal]);
        Assert.Equal(0, recorded.Exit);
        Assert.Contains(recorded.Lines, l => l.StartsWith("journal inputs=", StringComparison.Ordinal));

        var replay = Play("--replay", journal, "--seed", "1");
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay.Lines);

        var wrongSeed = Play("--replay", journal, "--seed", "2");
        Assert.Equal(AlchemyHeadlessGame.JournalRefused, wrongSeed.Exit);
        Assert.Contains(wrongSeed.Lines, l => l.StartsWith("replay matches=False code=REPLAY_STATE diverged-at=", StringComparison.Ordinal));
    }

    [Fact]
    public void ADamagedCheckpointJournalOrArgumentIsRefusedWithItsOwnExitCode()
    {
        var save = Path("ok.save");
        Assert.Equal(0, Play("--frame-ms", "1000", "--seconds", "20", "--player", "methodical", "--save", save).Exit);
        var bytes = File.ReadAllBytes(save);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(Path("bad.save"), bytes);
        var damaged = Play("--frame-ms", "1000", "--seconds", "5", "--load", Path("bad.save"));
        Assert.Equal(AlchemyHeadlessGame.CheckpointRefused, damaged.Exit);

        var journal = Path("ok.journal");
        Assert.Equal(0, Play("--frame-ms", "1000", "--seconds", "20", "--player", "methodical", "--journal", journal).Exit);
        var journalBytes = File.ReadAllBytes(journal);
        journalBytes[journalBytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(Path("bad.journal"), journalBytes);
        Assert.Equal(AlchemyHeadlessGame.JournalRefused, Play("--replay", Path("bad.journal")).Exit);

        Assert.Equal(AlchemyHeadlessGame.Refused, Play("--player", "wizard").Exit);
        Assert.Equal(AlchemyHeadlessGame.Refused, Play("--load", save, "--journal", Path("x.journal")).Exit);
        Assert.Equal(AlchemyHeadlessGame.Refused, Play("--script", Script("bad.txt", "at 1 mix fire+water\n")).Exit);
        Assert.Equal(AlchemyHeadlessGame.Refused, Play("--content", Script("bad-content.json", "{ \"format\": 1, \"elements\": [], \"recipes\": [] }")).Exit);
    }

    [Fact]
    public void AFailingComponentStopsTheSessionAndTheRunExitsWithAFault()
    {
        var run = Play([new Thrower()], "--frame-ms", "100", "--seconds", "5");
        Assert.Equal(AlchemyHeadlessGame.Faulted, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains("fault thrower: deliberate failure"));
        Assert.Contains("state=Faulted", run.Result);
    }
}
