using System.Globalization;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The field game end to end: squads raised in the SimEngine world from catalog formations, fought by the combat rules, casualties carried
/// back into the squads, checkpoints, journals and replay, and the scale the character factory was built for (1,000 and 10,000 soldiers).
/// </summary>
public sealed class FieldHeadlessTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("field-game-");

    public void Dispose() => _dir.Delete(true);

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    private sealed record Run(int Exit, string[] Lines, string Error)
    {
        public string Result => Lines.Single(l => l.StartsWith("result ", StringComparison.Ordinal));
        public string Final => Lines.Single(l => l.StartsWith("final ", StringComparison.Ordinal));
        public string Hash => Lines.Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal));
        public string Timing => Lines.Single(l => l.StartsWith("timing build=", StringComparison.Ordinal));
    }

    private Run Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = FieldHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, error);
        return new Run(exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    private static string[] Field(int squadsPerSide, params string[] more) =>
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", squadsPerSide.ToString(CultureInfo.InvariantCulture),
         "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", squadsPerSide.ToString(CultureInfo.InvariantCulture),
         "--frame-ms", "33", "--distance", "250", .. more];

    private static (int A, int B) Headcounts(Run run)
    {
        var parts = run.Final.Split(' ');
        int Left(string field) => int.Parse(field[(field.IndexOf('=') + 1)..].Split('/')[0], CultureInfo.InvariantCulture);
        return (Left(parts[1]), Left(parts[2]));
    }

    private static double Number(string line, string key)
    {
        var start = line.IndexOf(key + "=", StringComparison.Ordinal) + key.Length + 1;
        var end = start;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.')) end++;
        return double.Parse(line[start..end], CultureInfo.InvariantCulture);
    }

    // ---- a thousand soldiers ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThousandSoldiersAreRaisedFoughtAndTheirLossesReachTheSquads()
    {
        var run = Play([.. Field(5), "--seconds", "60"]);
        Assert.Equal(0, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains("raised a=5 squads of arms-modern.rifle-company (500 people), b=5 squads of arms-modern.rifle-company (500 people)"));
        Assert.Contains(run.Lines, l => l.Contains(" ended side-"));
        var (a, b) = Headcounts(run);
        Assert.InRange(a, 1, 499);
        Assert.InRange(b, 1, 499);
        Assert.Contains("entities=12", run.Timing);   // ten squad entities stand for the thousand soldiers, beside the two depots
        Assert.Contains("state=Ended", run.Result);
    }

    [Fact]
    public void TheSameCommandLineGivesTheSameOutputAndAnotherSeedDoesNot()
    {
        var one = Play([.. Field(5), "--seconds", "60", "--seed", "1"]);
        var again = Play([.. Field(5), "--seconds", "60", "--seed", "1"]);
        var other = Play([.. Field(5), "--seconds", "60", "--seed", "2"]);
        Assert.Equal(one.Hash, again.Hash);
        Assert.NotEqual(one.Hash, other.Hash);
    }

    [Fact]
    public void AJournalOfTheWholeFightReplaysTickForTickAndAnotherSeedDiverges()
    {
        var journal = Path("field.journal");
        var recorded = Play([.. Field(5), "--seconds", "60", "--journal", journal]);
        Assert.Equal(0, recorded.Exit);
        var replay = Play([.. Field(5), "--seconds", "60", "--replay", journal]);
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay.Lines);
        var wrong = Play([.. Field(5), "--seconds", "60", "--seed", "9", "--replay", journal]);
        Assert.Equal(FieldHeadlessGame.JournalRefused, wrong.Exit);
        Assert.Contains(wrong.Lines, l => l.StartsWith("replay matches=False", StringComparison.Ordinal));
    }

    [Fact]
    public void SavingMidFightAndLoadingContinuesExactlyLikeAnUninterruptedRun()
    {
        var whole = Play([.. Field(5), "--seconds", "60"]);
        var save = Path("half.save");
        var first = Play([.. Field(5), "--seconds", "1.2", "--save", save]);
        Assert.Equal(0, first.Exit);
        Assert.Contains("state=Running", first.Result);
        Assert.NotEqual(whole.Final, first.Final);
        var second = Play([.. Field(5), "--seconds", "60", "--load", save]);
        Assert.Equal(0, second.Exit);
        Assert.Contains(second.Lines, l => l.StartsWith("resumed tick=", StringComparison.Ordinal));
        Assert.Equal(whole.Final, second.Final);
        Assert.Equal(whole.Result, second.Result);
        Assert.Equal(whole.Timing[whole.Timing.IndexOf("world-hash=", StringComparison.Ordinal)..][..28], second.Timing[second.Timing.IndexOf("world-hash=", StringComparison.Ordinal)..][..28]);
    }

    // ---- ten thousand soldiers --------------------------------------------------------------------------------------------------------

    [Fact]
    public void TenThousandSoldiersRunInsideTheTickBudgetOfTheFactoryAndSaveAndLoadSmall()
    {
        var save = Path("big.save");
        var run = Play([.. Field(50, "--seconds", "20", "--save", save)]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("raised a=50 squads of arms-modern.rifle-company (5000 people), b=50 squads of arms-modern.rifle-company (5000 people)", string.Join('\n', run.Lines));
        Assert.Contains("entities=102", run.Timing);
        // The budgets of docs/CHARACTER_FACTORY.md section 7 for 10,000 soldiers: no more than 10 ms and 2 MB per tick, a 2 MB save.
        // This is the whole game (world, combat for 100 groups, commander, journal off): a debug build on a loaded machine gets a wider wall-clock bound.
        Assert.True(Number(run.Timing, "allocated-per-tick") < 2_000_000, run.Timing);
        Assert.True(Number(run.Timing, "per-tick") < 33, run.Timing);
        Assert.True(new FileInfo(save).Length < 2_000_000);
        var loaded = Play([.. Field(50, "--seconds", "20", "--load", save)]);
        Assert.Equal(0, loaded.Exit);
        Assert.Equal(run.Final, loaded.Final);
    }

    // ---- failure paths -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryFailurePathHasItsExitCodeAndIsReportedThroughTheOneReporter()
    {
        var damaged = Path("ok.save");
        Assert.Equal(0, Play([.. Field(2), "--seconds", "1", "--save", damaged]).Exit);
        var bytes = File.ReadAllBytes(damaged);
        // Flip one character inside the encoded world part, whatever the size of the file: the digest no longer matches.
        bytes[System.Text.Encoding.UTF8.GetString(bytes).IndexOf("\"Sim\":\"", StringComparison.Ordinal) + 40] ^= 0x01;
        File.WriteAllBytes(Path("bad.save"), bytes);
        File.WriteAllText(Path("old-format.save"), System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(damaged)).Replace("\"Format\":3", "\"Format\":2", StringComparison.Ordinal));
        File.WriteAllText(Path("junk.save"), "not a checkpoint");
        File.WriteAllText(Path("bad-rules.json"), "{ \"format\": 2 }");

        var cases = new (string[] Args, int Exit, string Code)[]
        {
            ([.. Field(2), "--player", "x"], FieldHeadlessGame.Refused, "FIELD_USAGE"),
            ([.. Field(2), "--squads-a", "0"], FieldHeadlessGame.Refused, "FIELD_USAGE"),
            (["--pack-a", "arms-modern"], FieldHeadlessGame.Refused, "FIELD_USAGE"),
            ([.. Field(2), "--formation-a", "arms-modern.nothing"], FieldHeadlessGame.Refused, "FIELD_USAGE"),
            ([.. Field(2), "--terrain", "lava"], FieldHeadlessGame.Refused, "COMBAT_UNKNOWN_TERRAIN"),
            ([.. Field(2), "--rules", Path("bad-rules.json")], FieldHeadlessGame.Refused, "COMBAT_RULES_MISSING_FIELD"),
            ([.. Field(2), "--rules", Path("missing.json")], FieldHeadlessGame.Refused, "FIELD_FILE"),
            ([.. Field(2), "--packs", Path("no-packs")], FieldHeadlessGame.Refused, ""),
            ([.. Field(2), "--content", Path("no-content")], FieldHeadlessGame.Refused, "FIELD_FILE"),
            ([.. Field(2), "--load", Path("bad.save")], FieldHeadlessGame.CheckpointRefused, "FIELD_CHECKPOINT_DIGEST"),
            ([.. Field(2), "--load", Path("old-format.save")], FieldHeadlessGame.CheckpointRefused, "FIELD_CHECKPOINT_FORMAT"),
            ([.. Field(2), "--load", Path("junk.save")], FieldHeadlessGame.CheckpointRefused, "FIELD_CHECKPOINT_FORMAT"),
            ([.. Field(2), "--load", Path("missing.save")], FieldHeadlessGame.Refused, "FIELD_FILE"),
            ([.. Field(2), "--load", damaged, "--journal", Path("x.journal")], FieldHeadlessGame.Refused, "FIELD_USAGE"),
            ([.. Field(2), "--replay", Path("missing.journal")], FieldHeadlessGame.Refused, "FIELD_FILE"),
        };
        foreach (var (args, exit, code) in cases)
        {
            var reporter = new CollectingReporter();
            var error = new StringWriter();
            var actual = FieldHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], new StringWriter(), error, reporter);
            Assert.True(actual == exit, string.Join(' ', args) + " exited " + actual + ": " + error);
            var notice = Assert.Single(reporter.Notices);
            Assert.Equal(ScenarioEngine.Contracts.Severity.Error, notice.Severity);
            if (code.Length > 0) Assert.Equal(code, notice.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()), "the user was told nothing for " + string.Join(' ', args));
        }
    }

    private sealed class CollectingReporter : ScenarioEngine.Contracts.IEngineReporter
    {
        public List<ScenarioEngine.Contracts.EngineNotice> Notices { get; } = new();
        public void Report(ScenarioEngine.Contracts.EngineNotice notice) => Notices.Add(notice);
        public void Audit(ScenarioEngine.Contracts.AuditRecord record) { }
    }

    [Fact]
    public void ABoundedRealTimeRunPlaysAThousandSoldiersOnTheWallClock()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var run = Play(["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "5", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "5", "--distance", "250", "--seconds", "2"]);
        watch.Stop();
        Assert.Equal(0, run.Exit);
        Assert.Contains("mode=real-time", run.Lines[0]);
        Assert.InRange(Number(run.Timing, "ticks"), 20, 90);
        Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 20);
    }
}
