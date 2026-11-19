using GameRuntime.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The headless tycoon game, run in-process exactly as the executable runs: arguments in, event lines and an exit code out.</summary>
public sealed class HeadlessGameTests : IDisposable
{
    private const string WinScript = "at 1 sell 10\nat 2 sell 10\nat 4 sell 10\nat 5 sell 10\n";
    private static readonly string[] Fixed = ["--frame-ms", "100", "--seconds", "30"];

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("headless-game-");

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
        public long EndTick => long.Parse(Result[(Result.IndexOf(" tick=", StringComparison.Ordinal) + 6)..].Split(' ')[0]);
    }

    private async Task<Run> Play(IReadOnlyList<IGameComponent>? extra, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await HeadlessGame.RunAsync([.. args, "--audit", Path("audit.jsonl")], output, error, extra);
        return new Run(exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    private Task<Run> Play(params string[] args) => Play(null, args);

    private sealed class Thrower : IGameComponent
    {
        public string Id => "thrower";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context) => throw new InvalidOperationException("deliberate failure");
    }

    // ---- scripted human ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AScriptedHumanWinsAndTwoRunsProduceIdenticalOutput()
    {
        var script = Script("win.txt", WinScript);
        var first = await Play([.. Fixed, "--script", script]);
        var second = await Play([.. Fixed, "--script", script]);
        Assert.Equal(0, first.Exit);
        Assert.Contains("ending=profit", first.Result);
        Assert.Contains("host input sell 10", first.Lines);
        Assert.Contains(first.Lines, l => l.Contains("player raise sell 10") && l.EndsWith("from input"));
        Assert.Contains(first.Lines, l => l.Contains("sim.event.cash-changed"));
        Assert.Contains(first.Lines, l => l.Contains("mission gf_tycoon.founded"));
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Lines, second.Lines);
    }

    [Fact]
    public async Task AnUnplayedRunEndsInTheTimeoutEndingWithExitZero()
    {
        var run = await Play("--seconds", "40", "--frame-ms", "100");
        Assert.Equal(0, run.Exit);
        Assert.Contains("ending=timeout", run.Result);
        Assert.Contains("tick=900", run.Result);
    }

    [Fact]
    public async Task HoldingAKeyActsOnEveryTickAndLettingGoStopsIt()
    {
        var script = Script("auto.txt", "at 0.5 autosell 2\nat 1 autosell off\n");
        var run = await Play("--frame-ms", "100", "--seconds", "3", "--script", script);
        var held = run.Lines.Where(l => l.Contains(" from held")).ToArray();
        Assert.InRange(held.Length, 14, 16);   // 0.5 s .. 1.0 s at 30 ticks per second
        var ticks = held.Select(l => int.Parse(l[1..l.IndexOf(' ')])).ToArray();
        Assert.Equal(ticks.Select((t, i) => ticks[0] + i), ticks);   // one per tick, no gaps
        Assert.Contains("host input autosell off", run.Lines);
    }

    [Fact]
    public async Task SellingMoreThanIsInStockIsRefusedByTheMission()
    {
        // Five taps in one frame are polled on one tick; the profit from the first four has not reached the mission yet.
        var run = await Play("--frame-ms", "100", "--seconds", "3", "--script", Script("s.txt", string.Concat(Enumerable.Repeat("at 0.5 sell 10\n", 5))));
        Assert.Equal(0, run.Exit);
        Assert.Contains("command p16-4 mission applied=False NO_STOCK", run.Lines);
        Assert.Contains("command p16-3 mission applied=True APPLIED", run.Lines);
    }

    [Fact]
    public async Task PausingTheHostClockStopsTheTicksUntilItResumes()
    {
        var pausedForGood = await Play("--frame-ms", "100", "--seconds", "2", "--script", Script("p.txt", "at 0.5 pause\n"));
        Assert.Contains("tick=15", pausedForGood.Result);
        var resumed = await Play("--frame-ms", "100", "--seconds", "2", "--script", Script("r.txt", "at 0.5 pause\nat 1 resume\n"));
        Assert.Contains("tick=45", resumed.Result);
    }

    // ---- the player -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("greedy")]
    [InlineData("cautious")]
    public async Task APlayerDrivenRunReachesAResultWithoutAnyScript(string policy)
    {
        var run = await Play([.. Fixed, "--player", policy]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("ending=profit", run.Result);
        Assert.Contains($"player={policy}", run.Lines[0]);
        Assert.Contains(run.Lines, l => l.Contains($"player decide {policy} decides sell"));
        Assert.Contains(run.Lines, l => l.Contains("player raise sell") && l.EndsWith("from input"));
        Assert.DoesNotContain(run.Lines, l => l.StartsWith("host input", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheTwoPoliciesPlayVisiblyDifferently()
    {
        var greedy = await Play([.. Fixed, "--player", "greedy"]);
        var cautious = await Play([.. Fixed, "--player", "cautious"]);

        Assert.NotEqual(greedy.Hash, cautious.Hash);
        Assert.True(greedy.EndTick < cautious.EndTick, $"greedy ended at {greedy.EndTick}, cautious at {cautious.EndTick}");

        // Greedy speeds the game up, sells every tick, hits NO_STOCK and shrinks its lot to retry.
        Assert.Contains(greedy.Lines, l => l.Contains("greedy decides speed 2/1"));
        Assert.Contains(greedy.Lines, l => l.EndsWith("mission applied=False NO_STOCK"));
        var greedyLots = greedy.Lines.Where(l => l.Contains("greedy decides sell")).Select(l => l.Split("decides sell ")[1].Split(' ')[0]).Distinct().ToArray();
        Assert.True(greedyLots.Length > 1, "greedy never changed its lot after a refusal");

        // Cautious never changes the speed, waits for each answer and is never refused.
        Assert.DoesNotContain(cautious.Lines, l => l.Contains("decides speed"));
        Assert.DoesNotContain(cautious.Lines, l => l.Contains("NO_STOCK"));
        Assert.True(cautious.Lines.Count(l => l.Contains("cautious decides sell")) > 5, "cautious sells in many small lots");
    }

    [Theory]
    [InlineData("greedy")]
    [InlineData("cautious")]
    public async Task TheSameSeedAndPolicyTwiceGiveIdenticalOutput(string policy)
    {
        var first = await Play([.. Fixed, "--player", policy, "--seed", "3"]);
        var second = await Play([.. Fixed, "--player", policy, "--seed", "3"]);
        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.Hash, second.Hash);
    }

    [Fact]
    public async Task TheSeedChangesHowThePolicyPlays()
    {
        var one = await Play([.. Fixed, "--player", "cautious", "--seed", "1"]);
        var three = await Play([.. Fixed, "--player", "cautious", "--seed", "3"]);
        Assert.NotEqual(one.Hash, three.Hash);
        Assert.NotEqual(one.EndTick, three.EndTick);
    }

    [Fact]
    public async Task AHumanScriptAndABotCanPlayTogether()
    {
        var run = await Play([.. Fixed, "--player", "cautious", "--script", Script("h.txt", "at 0.2 sell 5\n")]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("host input sell 5", run.Lines);
        Assert.Contains(run.Lines, l => l.Contains("from input"));
    }

    [Fact]
    public async Task TheRealTimeLoopReadsTheStopwatchAndStillPlaysTheGame()
    {
        var run = await Play("--seconds", "20", "--player", "greedy");
        Assert.Equal(0, run.Exit);
        Assert.Contains("mode=real-time", run.Lines[0]);
        Assert.Contains("ending=profit", run.Result);
    }

    // ---- journal and checkpoint -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("greedy")]
    [InlineData("cautious")]
    public async Task AJournalOfAPlayerDrivenRunReplaysToTheSameStateOnEveryTick(string policy)
    {
        var journal = Path("run.journal");
        var recorded = await Play([.. Fixed, "--player", policy, "--journal", journal]);
        Assert.Equal(0, recorded.Exit);

        var replay = await Play("--replay", journal);
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES", string.Join('\n', replay.Lines));
    }

    [Fact]
    public async Task TheJournalRecordsEveryInputEdgeWithTheTickThatDeliveredIt()
    {
        var journal = Path("run.journal");
        // A tap at 0.3 s is written between Updates, after tick 9, so the first Update to see it is tick 10.
        var run = await Play("--frame-ms", "100", "--seconds", "2", "--script", Script("t.txt", "at 0.3 sell 5\n"), "--journal", journal);
        Assert.Contains(run.Lines, l => l.StartsWith("t10 player raise sell 5", StringComparison.Ordinal));
        var recorded = SessionJournal.Deserialize(File.ReadAllBytes(journal));
        Assert.Equal([10L, 10L], recorded.Inputs.Select(i => i.Tick).ToArray());   // the press and the release
        Assert.All(recorded.Inputs, i => Assert.Equal("input-edge", i.Type));
        Assert.Contains("\"Pressed\":true", recorded.Inputs[0].Json);
        Assert.Contains("\"Pressed\":false", recorded.Inputs[1].Json);

        // A bot's decision at tick 1 is a key press that tick 2 polls.
        var botJournal = Path("bot.journal");
        await Play([.. Fixed, "--player", "cautious", "--journal", botJournal]);
        Assert.Equal(2, SessionJournal.Deserialize(File.ReadAllBytes(botJournal)).Inputs[0].Tick);
    }

    [Fact]
    public async Task AReplayAgainstADifferentGameDivergesAndExitsWithFour()
    {
        var journal = Path("run.journal");
        await Play([.. Fixed, "--player", "cautious", "--journal", journal]);

        var replay = await Play("--replay", journal, "--seed", "2");
        Assert.Equal(HeadlessGame.JournalRefused, replay.Exit);
        Assert.Contains("replay matches=False code=REPLAY_STATE", string.Join('\n', replay.Lines));
    }

    [Fact]
    public async Task ATamperedJournalIsRefusedWithExitFour()
    {
        var journal = Path("run.journal");
        await Play("--frame-ms", "100", "--seconds", "2", "--journal", journal);
        var bytes = File.ReadAllBytes(journal);
        bytes[bytes.Length / 2] ^= 1;
        File.WriteAllBytes(journal, bytes);

        var replay = await Play("--replay", journal);
        Assert.Equal(HeadlessGame.JournalRefused, replay.Exit);
    }

    [Fact]
    public async Task ASavedCheckpointResumesToTheSameWorldAsAnUninterruptedRun()
    {
        var checkpoint = Path("a.ckpt");
        var first = await Play("--frame-ms", "100", "--seconds", "3.5", "--script", Script("a.txt", "at 1 sell 10\nat 2 sell 10\n"), "--save", checkpoint);
        Assert.Equal(0, first.Exit);
        Assert.Contains("checkpoint tick=105", string.Join('\n', first.Lines));

        var resumed = await Play("--frame-ms", "100", "--seconds", "10", "--script", Script("b.txt", "at 0.5 sell 10\nat 1.5 sell 10\n"), "--load", checkpoint);
        var straight = await Play("--frame-ms", "100", "--seconds", "10", "--script", Script("c.txt", WinScript));

        Assert.Equal(0, resumed.Exit);
        Assert.Contains("resumed tick=105", resumed.Lines);
        Assert.Equal(straight.Result, resumed.Result);
        var tail = straight.Lines.Where(l => l.StartsWith("t1", StringComparison.Ordinal) && int.Parse(l[1..l.IndexOf(' ')]) > 105).ToArray();
        Assert.NotEmpty(tail);
        Assert.Equal(tail, resumed.Lines.Where(l => l.StartsWith("t1", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public async Task ACheckpointWithDamagedBytesIsRefusedWithExitThree()
    {
        var checkpoint = Path("a.ckpt");
        await Play("--frame-ms", "100", "--seconds", "2", "--save", checkpoint);
        var bytes = File.ReadAllBytes(checkpoint);
        bytes[bytes.Length / 2] ^= 1;
        File.WriteAllBytes(checkpoint, bytes);

        var run = await Play("--load", checkpoint);
        Assert.Equal(HeadlessGame.CheckpointRefused, run.Exit);
        Assert.Contains("CHECKPOINT_", run.Error);
    }

    // ---- refusals and faults ----------------------------------------------------------------------------------------

    [Fact]
    public async Task AComponentFaultStopsTheSessionAndExitsNonzero()
    {
        var run = await Play([new Thrower()], "--seconds", "5", "--frame-ms", "100");
        Assert.Equal(HeadlessGame.Faulted, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains(" fault thrower: deliberate failure"));
        Assert.Contains("state=Faulted", run.Result);
    }

    [Fact]
    public async Task BadArgumentsAndBadScriptsExitTwoWithoutRunning()
    {
        Assert.Equal(HeadlessGame.Refused, (await Play("--bogus", "1")).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--seconds", "0")).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--player", "reckless")).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--script", Script("bad.txt", "at 1 dance\n"))).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--script", Path("missing.txt"))).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--load", Path("x"), "--journal", Path("y"))).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--load", Path("x"), "--player", "greedy")).Exit);
        Assert.Equal(HeadlessGame.Refused, (await Play("--replay", Path("x"), "--player", "greedy")).Exit);
    }
}
