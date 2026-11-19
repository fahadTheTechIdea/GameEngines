using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The multi-seed sweep of the world-events tool: one seed is the single run, the report repeats byte for byte, and a bad command line is refused with its code.</summary>
public sealed class BalanceCliTests
{
    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = WorldCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static (int Code, string Out, string Err) Sweep(params string[] extra) =>
        Run(["simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), .. extra]);

    /// <summary>The (min, max) of a metric line.</summary>
    private static (long Min, long Max) Spread(string report, string metric)
    {
        var line = report.Split('\n').Single(l => l.StartsWith("metric " + metric + " ", StringComparison.Ordinal)).Split(' ');
        return (long.Parse(line[Array.IndexOf(line, "min") + 1]), long.Parse(line[Array.IndexOf(line, "max") + 1]));
    }

    [Fact]
    public void OneSeedAtTheDocumentsOwnSeedIsTheSingleRun()
    {
        var single = Run("simulate", Sample("valley.world.json"), Sample("valley.world.scenario.json"), "365").Out.TrimEnd().Split('\n').Last();
        var words = single.Split(' ');
        string After(string key) => words[Array.IndexOf(words, key) + 1];
        var sweep = Sweep("--seeds", "1", "--from-seed", Fixture.Valley.Seed.ToString(), "--days", "365");
        Assert.Equal(0, sweep.Code);
        Assert.Equal(long.Parse(After("stress")), Spread(sweep.Out, "stress.end").Min);
        Assert.Equal(long.Parse(After("played")), Spread(sweep.Out, "director.played").Min);
        Assert.Equal(long.Parse(After("held-back")), Spread(sweep.Out, "director.held-back").Min);
        Assert.Equal(long.Parse(After("dropped")), Spread(sweep.Out, "director.dropped").Min);
        Assert.True(Spread(sweep.Out, "hazards.concurrent-max").Min >= long.Parse(After("hazards-going")));       // the peak is never under the number going at the end
        foreach (var part in After("changes").Split(','))
        {
            var pair = part.Split(':');
            Assert.Equal(long.Parse(pair[1]), Spread(sweep.Out, "changes." + pair[0]).Min);
        }
    }

    [Fact]
    public void TheReportRepeatsByteForByteAndDaysEqualStepsWhenThereIsOneStepADay()
    {
        var first = Sweep("--seeds", "6", "--from-seed", "1", "200", "--rows");
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Sweep("--seeds", "6", "--from-seed", "1", "200", "--rows").Out);
        Assert.Equal(first.Out, Sweep("--seeds", "6", "--from-seed", "1", "--days", "200", "--rows").Out);
        Assert.StartsWith("balance world-events valley.world digest " + Fixture.Valley.Digest + " seeds 6 from 1 horizon 200 steps\n", first.Out, StringComparison.Ordinal);
        Assert.Contains("\nsample stress.end 1 ", first.Out, StringComparison.Ordinal);
        Assert.Contains("\nsample stress.end 6 ", first.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", first.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentSeedsGiveADifferentWorldAndTheSweepSeesIt()
    {
        var report = Sweep("--seeds", "12", "--from-seed", "1", "365").Out;
        var (low, high) = Spread(report, "changes.weather");
        Assert.True(low < high, "twelve seeds all drew the same number of weather changes");
        Assert.Equal(4, Spread(report, "changes.season").Max);       // the calendar is not random
        Assert.Equal(4, Spread(report, "changes.season").Min);
    }

    [Fact]
    public void OnlyTheNamedMetricsAreReported()
    {
        var report = Sweep("--seeds", "3", "365", "--metrics", "stress.end,director.played").Out;
        Assert.Equal(["stress.end", "director.played"], report.Split('\n').Where(l => l.StartsWith("metric ", StringComparison.Ordinal)).Select(l => l.Split(' ')[1]).OrderByDescending(n => n, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData("BAL_SEEDS_RANGE", "--seeds", "0", "10")]
    [InlineData("BAL_HORIZON_MISSING", "--seeds", "3")]
    [InlineData("BAL_HORIZON_TWICE", "--seeds", "3", "10", "--days", "10")]
    [InlineData("BAL_OPTION_UNKNOWN", "--seeds", "3", "10", "--save", "x.json")]
    [InlineData("BAL_FROM_SEED_RANGE", "--seeds", "3", "10", "--from-seed", "9000000000000000")]
    [InlineData("BAL_METRIC_UNKNOWN", "--seeds", "3", "10", "--metrics", "nope")]
    [InlineData("BAL_OPTION_VALUE", "--seeds", "3", "99999999999")]
    public void ABadSweepCommandLineExitsWithTwoAndItsCode(string code, params string[] extra)
    {
        var result = Sweep(extra);
        Assert.Equal(2, result.Code);
        Assert.StartsWith("refused " + code + ":", result.Err, StringComparison.Ordinal);
        Assert.Equal("", result.Out);
    }

    [Fact]
    public void ABadDocumentOrScenarioIsStillRefusedTheOrdinaryWay()
    {
        var folder = Directory.CreateTempSubdirectory("world-balance-");
        try
        {
            var bad = Path.Combine(folder.FullName, "bad.json");
            File.WriteAllText(bad, "{\"format\": 3}");
            var refused = Run("simulate", bad, Sample("valley.world.scenario.json"), "--seeds", "2", "10");
            Assert.Equal(1, refused.Code);
            Assert.StartsWith("refused WE_", refused.Out, StringComparison.Ordinal);
            File.WriteAllText(bad, "not json");
            Assert.Equal(2, Run("simulate", Sample("valley.world.json"), bad, "--seeds", "2", "10").Code);
            Assert.Equal(2, Run("simulate", Sample("valley.world.json"), Path.Combine(folder.FullName, "absent.json"), "--seeds", "2", "10").Code);
        }
        finally
        {
            folder.Delete(true);
        }
    }
}
