using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The multi-seed sweep of the economy tool: one seed is the single run, the report repeats byte for byte, the sample's seed-insensitivity is stated by the sweep, and a bad command line is refused with its code.</summary>
public sealed class BalanceCliTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("economy-balance-");

    public void Dispose() => _dir.Delete(true);

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = EconomyCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static (int Code, string Out, string Err) Sweep(string document, params string[] extra) => Run(["simulate", document, Sample("town.scenario.json"), .. extra]);

    private static (long Min, long Max) Spread(string report, string metric)
    {
        var line = report.Split('\n').Single(l => l.StartsWith("metric " + metric + " ", StringComparison.Ordinal)).Split(' ');
        return (long.Parse(line[Array.IndexOf(line, "min") + 1]), long.Parse(line[Array.IndexOf(line, "max") + 1]));
    }

    [Fact]
    public void OneSeedAtTheDocumentsOwnSeedIsTheSingleRun()
    {
        var single = Run("simulate", Sample("town.economy.json"), Sample("town.scenario.json"), "60").Out;
        var sweep = Sweep(Sample("town.economy.json"), "--seeds", "1", "--from-seed", Fixture.Town.Seed.ToString(), "60");
        Assert.Equal(0, sweep.Code);
        var market = "";
        var checkedPrices = 0;
        foreach (var raw in single.Split('\n'))
        {
            var words = raw.Trim().Split(' ');
            if (words[0] == "market")
            {
                market = words[1];
                Assert.Equal(long.Parse(words[3]), Spread(sweep.Out, "treasury.end." + market).Min);
            }
            else if (words.Length > 6 && words[1] == "price" && words[3] == "ratio" && market.Length > 0)
            {
                Assert.Equal(long.Parse(words[4]), Spread(sweep.Out, "ratio.end." + market + "." + words[0]).Min);
                checkedPrices++;
            }
            else if (words[0] == "currency" && words[2] == "rate")
            {
                Assert.Equal(long.Parse(words[3]), Spread(sweep.Out, "rate.end." + words[1]).Min);
                Assert.Equal(long.Parse(words[Array.IndexOf(words, "money") + 1]), Spread(sweep.Out, "money.end." + words[1]).Min);
            }
        }
        Assert.Equal(22, checkedPrices);
    }

    [Fact]
    public void TheReportRepeatsByteForByteAndDaysAreWholePeriods()
    {
        var first = Sweep(Sample("town.economy.json"), "--seeds", "4", "--from-seed", "3", "40", "--rows");
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Sweep(Sample("town.economy.json"), "--seeds", "4", "--from-seed", "3", "40", "--rows").Out);
        Assert.StartsWith("balance economy valley-economy digest " + Fixture.Town.Digest + " seeds 4 from 3 horizon 40 periods\n", first.Out, StringComparison.Ordinal);
        var days = 40L * Fixture.Town.PeriodSteps / Fixture.Town.StepsPerDay;
        Assert.Equal(0, 40L * Fixture.Town.PeriodSteps % Fixture.Town.StepsPerDay);
        Assert.Equal(first.Out, Sweep(Sample("town.economy.json"), "--seeds", "4", "--from-seed", "3", "--days", days.ToString(), "--rows").Out);
    }

    [Fact]
    public void DaysThatAreNotWholePeriodsAreRefused()
    {
        var twoDays = Fixture.TownText.Replace("\"periodSteps\": 24", "\"periodSteps\": 48", StringComparison.Ordinal);
        Assert.NotEqual(Fixture.TownText, twoDays);
        var path = Path.Combine(_dir.FullName, "two-day.economy.json");
        File.WriteAllText(path, twoDays);
        var broken = Sweep(path, "--seeds", "4", "--days", "3");
        Assert.Equal(2, broken.Code);
        Assert.StartsWith("refused BAL_DAYS_RANGE:", broken.Err, StringComparison.Ordinal);
        var whole = Sweep(path, "--seeds", "2", "--days", "4");                 // two periods of two days
        Assert.Equal(0, whole.Code);
        Assert.Contains(" horizon 2 periods\n", whole.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutPriceNoiseTheSeedChangesNothingAndTheSweepSaysSo()
    {
        // The shipped town has a noise of 20 (pass 49); a copy with none is the quiet economy the sweep must say is seed-insensitive.
        var quiet = Fixture.TownText.Replace("\"noise\": 20,", "\"noise\": 0,", StringComparison.Ordinal);
        Assert.NotEqual(Fixture.TownText, quiet);
        var quietPath = Path.Combine(_dir.FullName, "quiet.economy.json");
        File.WriteAllText(quietPath, quiet);
        var report = Sweep(quietPath, "--seeds", "8", "--from-seed", "1", "120").Out;
        var lines = report.Split('\n').Where(l => l.StartsWith("metric ", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count > 100);
        foreach (var line in lines)
        {
            var words = line.Split(' ');
            Assert.Equal(words[Array.IndexOf(words, "min") + 1], words[Array.IndexOf(words, "max") + 1]);
        }
    }

    [Fact]
    public void WithPriceNoiseTheSeedsSpreadThePrices()
    {
        var noisy = Fixture.TownText.Replace("\"noise\": 20,", "\"noise\": 60,", StringComparison.Ordinal);
        Assert.NotEqual(Fixture.TownText, noisy);
        var path = Path.Combine(_dir.FullName, "noisy.economy.json");
        File.WriteAllText(path, noisy);
        var report = Sweep(path, "--seeds", "12", "--from-seed", "1", "120").Out;
        var (low, high) = Spread(report, "ratio.max.riverton.grain");
        Assert.True(low < high, "twelve seeds gave one and the same highest grain price");
    }

    [Fact]
    public void TheShippedTownHasANoiseSoItsSeedsSpreadItsPricesAndItsBandsOverAYear()
    {
        Assert.Equal(20, Fixture.Town.Price.Noise);
        var report = Sweep(Sample("town.economy.json"), "--seeds", "12", "--from-seed", "1", "120").Out;
        var lines = report.Split('\n').Where(l => l.StartsWith("metric ", StringComparison.Ordinal)).ToList();
        var varying = lines.Count(line =>
        {
            var words = line.Split(' ');
            return words[Array.IndexOf(words, "min") + 1] != words[Array.IndexOf(words, "max") + 1];
        });
        // Measured (docs/measurements/economy-noise-pass49-2026-10-30.csv): 80 of 196 metrics vary over 50 seeds; twelve seeds still show well over a quarter of them.
        Assert.True(varying > lines.Count / 4, varying + " of " + lines.Count + " metrics varied over twelve seeds");
    }

    [Theory]
    [InlineData("BAL_SEEDS_RANGE", "--seeds", "0", "10")]
    [InlineData("BAL_HORIZON_MISSING", "--seeds", "3")]
    [InlineData("BAL_HORIZON_TWICE", "--seeds", "3", "10", "--days", "10")]
    [InlineData("BAL_OPTION_UNKNOWN", "--seeds", "3", "10", "--verbose")]
    [InlineData("BAL_FROM_SEED_RANGE", "--seeds", "3", "10", "--from-seed", "9000000000000000")]
    [InlineData("BAL_METRIC_UNKNOWN", "--seeds", "3", "10", "--metrics", "nope")]
    [InlineData("BAL_OPTION_VALUE", "--seeds", "3", "100000000000000000000")]
    public void ABadSweepCommandLineExitsWithTwoAndItsCode(string code, params string[] extra)
    {
        var result = Sweep(Sample("town.economy.json"), extra);
        Assert.Equal(2, result.Code);
        Assert.StartsWith("refused " + code + ":", result.Err, StringComparison.Ordinal);
        Assert.Equal("", result.Out);
    }

    [Fact]
    public void ABadScenarioIsStillAScenarioProblem()
    {
        var bad = Path.Combine(_dir.FullName, "bad.json");
        File.WriteAllText(bad, "not json");
        Assert.Equal(2, Run("simulate", Sample("town.economy.json"), bad, "--seeds", "2", "10").Code);
    }
}
