using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>The multi-seed sweep of the relationship tool: one seed is the single run, the report repeats byte for byte, the seeds do change what the gossip spreads, and a bad command line is refused with its code.</summary>
public sealed class BalanceCliTests
{
    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = RelationshipCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static (int Code, string Out, string Err) Sweep(params string[] extra) => Run(["simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), .. extra]);

    private static (long Min, long Max) Spread(string report, string metric)
    {
        var line = report.Split('\n').Single(l => l.StartsWith("metric " + metric + " ", StringComparison.Ordinal)).Split(' ');
        return (long.Parse(line[Array.IndexOf(line, "min") + 1]), long.Parse(line[Array.IndexOf(line, "max") + 1]));
    }

    [Fact]
    public void OneSeedAtTheDocumentsOwnSeedIsTheSingleRun()
    {
        var single = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "20").Out;
        var seed = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Sample("village.relationship.json"))).RootElement.GetProperty("seed").GetInt64().ToString();
        var sweep = Sweep("--seeds", "1", "--from-seed", seed, "20");
        Assert.Equal(0, sweep.Code);
        var actors = 0;
        foreach (var raw in single.Split('\n'))
        {
            var words = raw.TrimEnd((char)13).Split((char)32);
            if (words.Length > 5 && words[1] == "mood" && words[4] == "needs" && !raw.StartsWith(' '))
            {
                Assert.Equal(long.Parse(words[2]), Spread(sweep.Out, "mood.end." + words[0]).Min);
                Assert.Equal(long.Parse(words[Array.IndexOf(words, "standing") + 1]), Spread(sweep.Out, "standing.end." + words[0]).Min);
                Assert.Equal(long.Parse(words[Array.IndexOf(words, "edges") + 1]), Spread(sweep.Out, "edges.end." + words[0]).Min);
                actors++;
            }
            else if (raw.StartsWith("state at step", StringComparison.Ordinal))
            {
                Assert.Equal(long.Parse(words[Array.IndexOf(words, "edges,") - 1]), Spread(sweep.Out, "edges.total").Min);
                Assert.Equal(long.Parse(words[Array.IndexOf(words, "rolls") - 1]), Spread(sweep.Out, "rolls").Min);
            }
        }
        Assert.Equal(4, actors);
    }

    [Fact]
    public void TheReportRepeatsByteForByte()
    {
        var first = Sweep("--seeds", "6", "--from-seed", "100", "--days", "30", "--rows");
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Sweep("--seeds", "6", "--from-seed", "100", "--days", "30", "--rows").Out);
        Assert.Equal(first.Out, Sweep("--seeds", "6", "--from-seed", "100", "30", "--rows").Out);
        Assert.StartsWith("balance relationship village digest ", first.Out, StringComparison.Ordinal);
        Assert.Contains(" seeds 6 from 100 horizon 30 days\n", first.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSeedDecidesWhatTheGossipSpreads()
    {
        var (low, high) = Spread(Sweep("--seeds", "40", "--from-seed", "0", "90").Out, "changes.memories");
        Assert.True(low < high, "forty seeds all spread the same number of memories");
    }

    [Theory]
    [InlineData("BAL_SEEDS_RANGE", "--seeds", "100001", "10")]
    [InlineData("BAL_HORIZON_MISSING", "--seeds", "3")]
    [InlineData("BAL_HORIZON_TWICE", "--seeds", "3", "10", "--days", "10")]
    [InlineData("BAL_OPTION_UNKNOWN", "--seeds", "3", "10", "--save", "x")]
    [InlineData("BAL_FROM_SEED_RANGE", "--seeds", "3", "10", "--from-seed", "9000000000000000")]
    [InlineData("BAL_METRIC_UNKNOWN", "--seeds", "3", "10", "--metrics", "nope")]
    [InlineData("BAL_OPTION_VALUE", "--seeds", "3", "ten")]
    [InlineData("BAL_OPTION_VALUE", "--seeds", "3", "4000")]
    public void ABadSweepCommandLineExitsWithTwoAndItsCode(string code, params string[] extra)
    {
        var result = Sweep(extra);
        Assert.Equal(2, result.Code);
        Assert.StartsWith("refused " + code + ":", result.Err, StringComparison.Ordinal);
        Assert.Equal("", result.Out);
    }
}
