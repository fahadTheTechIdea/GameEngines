using GameRuntime.Combat.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The combat component on the game layer, and the headless skirmish game run in-process exactly as the executable runs.</summary>
public sealed class CombatRuntimeTests : IDisposable
{
    private static readonly string[] WarbandVsWarband =
        ["--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband", "--frame-ms", "100", "--seconds", "300", "--player", "aggressor", "--distance", "40", "--round-seconds", "3"];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("combat-game-");

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
        var exit = SkirmishHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, error, extra);
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

    private static SkirmishOptions Options(params string[] args) => SkirmishOptions.Parse(args);

    private static SkirmishSession Session(CollectingReporter reporter, ulong seed = 1)
    {
        var forces = SkirmishForceSource.Load(Options("--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband"));
        return SkirmishSession.Create(forces, seed, reporter);
    }

    private static void Ticks(SkirmishSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++) session.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond);
    }

    // ---- nothing happens until Update -------------------------------------------------------------------------------------

    [Fact]
    public void AnEngagementRequestedOrInputWrittenChangesNothingUntilTheNextUpdate()
    {
        var reporter = new CollectingReporter();
        var session = Session(reporter);
        var heard = new List<object>();
        session.Runtime.Subscribe<Engagement>(e => heard.Add(e));
        session.Runtime.Subscribe<Casualty>(e => heard.Add(e));
        session.Runtime.Init();
        var before = session.Combat.Fingerprint();
        var healthBefore = session.Combat.Groups.Values.Select(g => g.Health).ToArray();

        session.Runtime.Submit(new EngagementRequested("host-1", "a", "b", 100, 10, "open", "open", true));
        session.Input.Press(SkirmishActions.Engage, "a>b|100|10|open|open|mutual");
        session.Input.Release(SkirmishActions.Engage);

        Assert.Equal(before, session.Combat.Fingerprint());
        Assert.Equal(healthBefore, session.Combat.Groups.Values.Select(g => g.Health).ToArray());
        Assert.Empty(heard);
        Assert.False(session.Input.IsQuiet);
        Assert.Equal(0, session.Clock.Tick);
        Assert.Empty(reporter.Codes);
    }

    [Fact]
    public void ASubmittedRequestIsResolvedInTheNextTickAndACommanderOrderInTheTickAfter()
    {
        var session = Session(new CollectingReporter());
        var engagements = new List<Engagement>();
        session.Runtime.Subscribe<Engagement>(engagements.Add);
        session.Runtime.Init();

        // A host submit is delivered at the start of tick 1, before the rules component runs, so tick 1 answers it.
        session.Runtime.Submit(new EngagementRequested("host-1", "a", "b", 2, 5, "open", "open", false));
        Ticks(session, 1);
        Assert.Equal(["host-1"], engagements.Select(e => e.RequestId).ToArray());
        Assert.Equal(1, engagements[0].Tick);
        Assert.True(engagements[0].Fired > 0);

        // The commander runs after the rules component: what it raises in tick 2 can only be answered in tick 3.
        session.Input.Press(SkirmishActions.Engage, "a>b|2|5|open|open|one-way");
        session.Input.Release(SkirmishActions.Engage);
        var fingerprint = session.Combat.Fingerprint();
        Ticks(session, 1);
        Assert.Single(engagements);
        Assert.False(session.Combat.IsQuiet);
        Assert.NotEqual(fingerprint, session.Combat.Fingerprint());   // the tick moved and the request waits, but no fight happened
        Ticks(session, 1);
        Assert.Equal(2, engagements.Count);
        Assert.Equal(3, engagements[1].Tick);
    }

    [Fact]
    public void ABadRequestIsRefusedWithACodeAndChangesNoGroup()
    {
        var session = Session(new CollectingReporter());
        var refused = new List<EngagementRefused>();
        session.Runtime.Subscribe<EngagementRefused>(refused.Add);
        session.Runtime.Init();
        var healthBefore = session.Combat.Groups.Values.Select(g => g.Health).ToArray();
        session.Runtime.Submit(new EngagementRequested("r1", "a", "nobody", 100, 10, "open", "open", true));
        session.Runtime.Submit(new EngagementRequested("r2", "a", "a", 100, 10, "open", "open", true));
        session.Runtime.Submit(new EngagementRequested("r3", "a", "b", 100, 10, "fortress", "open", true));
        Ticks(session, 1);
        Assert.Equal([CombatComponent.UnknownGroup, CombatComponent.SameGroup], refused.Take(2).Select(r => r.Code).ToArray());
        Assert.Equal(3, refused.Count);
        Assert.StartsWith("COMBAT_", refused[2].Code);
        Assert.Equal(healthBefore, session.Combat.Groups.Values.Select(g => g.Health).ToArray());
        Assert.Equal(0, session.Combat.Engagements);
    }

    // ---- the headless game --------------------------------------------------------------------------------------------------

    [Fact]
    public void ALaterEraBeatsAnEarlierOneThroughTheWholeGamePipeline()
    {
        var run = Play("--pack-a", "arms-modern", "--pack-b", "arms-ancient", "--side-a", "arms-modern.rifle-platoon", "--side-b", "arms-ancient.iron-warband",
            "--frame-ms", "33", "--seconds", "60", "--player", "skirmisher", "--distance", "300");
        Assert.Equal(0, run.Exit);
        Assert.Contains("ending=side-1-wins", run.Result);
        Assert.Contains(run.Lines, l => l.Contains("standing b destroyed") || l.Contains("standing b routed"));
        Assert.Contains(run.Lines, l => l.Contains("commander decide skirmisher orders "));
        Assert.Contains(run.Lines, l => l.Contains(" ammo a arms-modern."));
    }

    [Fact]
    public void EveryEraRunsTheSameGameWithItsOwnFormations()
    {
        foreach (var (era, a, b) in new[]
        {
            ("ancient", "arms-ancient.iron-warband", "arms-ancient.hunting-band"),
            ("medieval", "arms-medieval.longbow-company", "arms-medieval.pike-and-shot"),
            ("modern", "arms-modern.rifle-platoon", "arms-modern.rifle-platoon"),
        })
        {
            var run = Play("--era", era, "--side-a", a, "--side-b", b, "--frame-ms", "100", "--seconds", "120", "--player", "aggressor", "--distance", "200");
            Assert.True(run.Exit == 0, era + ": " + run.Error + string.Join("\n", run.Lines.TakeLast(3)));
            Assert.Contains($"a={a}@arms-{era}#1 b={b}@arms-{era}#2", run.Lines[0]);
        }
    }

    [Fact]
    public void TheSameSeedAndPolicyPlayTheSameGameAndAnotherSeedDoesNot()
    {
        var first = Play([.. WarbandVsWarband, "--seed", "1"]);
        var again = Play([.. WarbandVsWarband, "--seed", "1"]);
        var other = Play([.. WarbandVsWarband, "--seed", "2"]);
        Assert.Equal(0, first.Exit);
        Assert.Equal(first.Hash, again.Hash);
        Assert.NotEqual(first.Hash, other.Hash);
        Assert.Contains(first.Lines, l => l.Contains(" ended "));
    }

    [Fact]
    public void AScriptedHumanOrdersEngagementsThroughTheInputState()
    {
        var script = Script("human.txt", "at 1 engage a>b|200|5|open|open|one-way\nat 3 engage b>a|50|5|light|open|mutual\n");
        var run = Play("--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.hunting-band", "--frame-ms", "100", "--seconds", "6", "--script", script);
        Assert.Equal(0, run.Exit);
        Assert.Contains("host input engage a>b|200|5|open|open|one-way", run.Lines);
        Assert.Equal(2, run.Lines.Count(l => l.Contains(" engagement e")));
        Assert.Contains(run.Lines, l => l.Contains("commander raise engage b>a|50|5|light|open|mutual"));
    }

    [Fact]
    public void SavingAndLoadingContinuesExactlyLikeAnUninterruptedRun()
    {
        var whole = Play([.. WarbandVsWarband, "--seed", "1"]);
        var save = Path("part.save");
        var first = Play("--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband", "--frame-ms", "100", "--seconds", "3", "--player", "aggressor",
            "--distance", "40", "--round-seconds", "3", "--seed", "1", "--save", save);
        Assert.Equal(0, first.Exit);
        Assert.Contains(first.Lines, l => l.StartsWith("checkpoint tick=", StringComparison.Ordinal));
        Assert.DoesNotContain("state=Ended", first.Result);
        var second = Play([.. WarbandVsWarband, "--seed", "1", "--load", save]);
        Assert.Equal(0, second.Exit);
        Assert.Contains(second.Lines, l => l.StartsWith("resumed tick=", StringComparison.Ordinal));
        Assert.Equal(whole.Result, second.Result);
    }

    [Fact]
    public void AJournalReplaysTickForTickAndAnotherSeedDiverges()
    {
        var journal = Path("run.journal");
        var recorded = Play([.. WarbandVsWarband, "--seed", "1", "--journal", journal]);
        Assert.Equal(0, recorded.Exit);

        var forces = new[] { "--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband", "--frame-ms", "100" };
        var replay = Play([.. forces, "--replay", journal, "--seed", "1"]);
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay.Lines);

        var wrongSeed = Play([.. forces, "--replay", journal, "--seed", "2"]);
        Assert.Equal(SkirmishHeadlessGame.JournalRefused, wrongSeed.Exit);
        Assert.Contains(wrongSeed.Lines, l => l.StartsWith("replay matches=False code=REPLAY_", StringComparison.Ordinal));
    }

    [Fact]
    public void ADamagedCheckpointOrOtherForcesOrArgumentIsRefusedWithItsOwnExitCode()
    {
        var args = new[] { "--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.hunting-band", "--frame-ms", "100" };
        var save = Path("ok.save");
        Assert.Equal(0, Play([.. args, "--seconds", "3", "--player", "aggressor", "--save", save]).Exit);
        var bytes = File.ReadAllBytes(save);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(Path("bad.save"), bytes);
        Assert.Equal(SkirmishHeadlessGame.CheckpointRefused, Play([.. args, "--seconds", "3", "--load", Path("bad.save")]).Exit);

        var otherForces = Play("--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband", "--frame-ms", "100", "--seconds", "3", "--load", save);
        Assert.Equal(SkirmishHeadlessGame.CheckpointRefused, otherForces.Exit);

        Assert.Equal(SkirmishHeadlessGame.Refused, Play([.. args, "--player", "wizard"]).Exit);
        Assert.Equal(SkirmishHeadlessGame.Refused, Play("--era", "ancient", "--side-a", "arms-ancient.iron-warband").Exit);
        Assert.Equal(SkirmishHeadlessGame.Refused, Play("--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.nothing").Exit);
        Assert.Equal(SkirmishHeadlessGame.Refused, Play([.. args, "--script", Script("bad.txt", "at 1 charge a>b\n")]).Exit);
        Assert.Equal(SkirmishHeadlessGame.Refused, Play([.. args, "--load", save, "--journal", Path("x.journal")]).Exit);
    }

    [Fact]
    public void AFailingComponentStopsTheSessionAndTheRunExitsWithAFault()
    {
        var run = Play([new Thrower()], "--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.hunting-band", "--frame-ms", "100", "--seconds", "5");
        Assert.Equal(SkirmishHeadlessGame.Faulted, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains("fault thrower: deliberate failure"));
        Assert.Contains("state=Faulted", run.Result);
    }
}
