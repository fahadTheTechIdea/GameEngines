using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using EngineCommon.Balance;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The rare-event mode of the world-events tool (<c>simulate ... --rare hazard:rung</c>): fixed-effort splitting over the engine's own scenario loop, restarting from checkpoints with <see cref="WorldRuntime.Branch"/>. It is checked
/// against a world whose answer is exact: one region, one hazard that lasts one step, a rate of 2,000 ppm a step and a ladder of three rungs whose tails are 1,000,000, 126,000 and 16,000 ppm, so the chance that some onset within 365
/// steps reaches a rung with tail t is 1 - (1 - r t / 10^12)^365 (every step is an independent trial: an onset ends before the next step's chance is drawn). A plain seed sweep of the same world reaches the same value.
/// </summary>
public sealed class RareCliTests
{
    internal const int Steps = 365;
    internal const int RatePpm = 2000;

    internal static string LabDocument(long seed = 1, int rate = RatePpm)
    {
        var root = Lab.Hawkes(seed, rate, 0, 1000, 0);
        var ladder = Tiny.Hazard(root)["ladder"]!.AsArray();
        var low = ladder[0]!.AsObject();
        foreach (var (id, tail) in new[] { ("mid", 126_000), ("great", 16_000) })
        {
            var rung = Tiny.Copy(low).AsObject();
            rung["id"] = id;
            rung["key"] = "r." + id;
            rung["tailPpm"] = tail;
            ladder.Add(rung);
        }
        return root.ToJsonString();
    }

    internal const string Scenario = "{ \"facts\": { \"ext.alert\": 0 }, \"script\": [] }";

    /// <summary>The exact chance in ppb that some natural onset within <see cref="Steps"/> steps reaches a rung with the tail, floored.</summary>
    internal static long Exact(int tailPpm, int rate = RatePpm)
    {
        var set = WorldLoader.Load(LabDocument(1, rate));
        var runtime = WorldRuntime.Create(set);
        var chance = runtime.OnsetChance(set.Hazards[0], 0, 0);
        Assert.Equal(chance, runtime.OnsetChance(set.Hazards[0], 0, 1));          // both seasons have the same rate, so every step is the same trial
        var miss = BigInteger.Pow(10, 12) - chance * tailPpm;
        var all = BigInteger.Pow(10, 12);
        return (long)((BigInteger.Pow(all, Steps) - BigInteger.Pow(miss, Steps)) * 1_000_000_000 / BigInteger.Pow(all, Steps));
    }

    internal sealed class Files : IDisposable
    {
        private readonly List<string> _paths = [];

        public string Write(string text)
        {
            var path = Path.Combine(Path.GetTempPath(), "rare-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, text);
            _paths.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var path in _paths) File.Delete(path);
        }
    }

    internal static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = WorldCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString());
    }

    internal static string Word(string line, string key)
    {
        var words = line.Split(' ');
        return words[Array.IndexOf(words, key) + 1];
    }

    internal static string Summary(string report) => report.Split('\n').Single(l => l.StartsWith("rare-estimate ", StringComparison.Ordinal));

    [Fact]
    public void TheExactValuesAreWhatTheLadderAndTheRateImply()
    {
        // Worked by hand for one step: a trial succeeds with 2,000 x 126,000 / 10^12 = 2.52e-4; over 365 steps 1 - (1 - 2.52e-4)^365 = 0.0879...
        Assert.InRange(Exact(126_000), 87_000_000, 89_000_000);
        Assert.InRange(Exact(16_000), 11_000_000, 12_000_000);
        Assert.InRange(Exact(1_000_000), 515_000_000, 525_000_000);
    }

    [Theory]
    [InlineData("h:mid", 126_000)]
    [InlineData("h:great", 16_000)]
    public void TheSplittingEstimateAgreesWithTheExactValueWithinItsStatedBand(string target, int tail)
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        var truth = Exact(tail);
        var covered = 0;
        BigInteger sum = 0;
        const int roots = 6;
        for (var root = 1; root <= roots; root++)
        {
            var run = Run("simulate", world, scenario, Steps.ToString(CultureInfo.InvariantCulture), "--rare", target, "--effort", "100", "--replications", "15", "--from-seed", root.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, run.Code);
            var line = Summary(run.Out);
            var band = Word(line, "band-ppb").Split("..");
            if (long.Parse(band[0]) <= truth && truth <= long.Parse(band[1])) covered++;
            sum += long.Parse(Word(line, "mean-ppb"));
        }
        Assert.True(covered >= roots - 2, "only " + covered + " of " + roots + " bands covered the exact " + truth);
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - truth) * 8 <= truth, "the mean of the estimates " + grand + " is more than 12.5% from the exact " + truth);
    }

    [Fact]
    public void APlainSweepOfTheSameWorldReachesTheSameValue()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        const int seeds = 2000;
        var run = Run("simulate", world, scenario, Steps.ToString(CultureInfo.InvariantCulture), "--seeds", seeds.ToString(CultureInfo.InvariantCulture), "--from-seed", "1", "--onsets", "--rows", "--metrics", "stress.end");
        Assert.Equal(0, run.Code);
        var rows = run.Out.Split('\n').Where(l => l.StartsWith("onset hazard.h.rung ", StringComparison.Ordinal)).Select(l => l.Split(' ')).ToList();
        var seedsWithMid = rows.Where(r => long.Parse(r[3]) >= 1).Select(r => r[2]).Distinct().Count();
        var (plain, band) = PlainEstimate.Of(seedsWithMid, seeds);
        var truth = Exact(126_000);
        Assert.True(Math.Abs(plain - truth) <= band, "plain " + plain + " is more than " + band + " from the exact " + truth);
    }

    [Fact]
    public void ARareReportRepeatsByteForByteAndCountsItsRuns()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        string[] args = ["simulate", world, scenario, "100", "--rare", "h:great", "--effort", "20", "--replications", "6", "--from-seed", "3", "--rows"];
        var first = Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Run(args).Out);
        Assert.Equal(first.Out, Run(["simulate", world, scenario, "--days", "100", "--rare", "h:great", "--effort", "20", "--replications", "6", "--from-seed", "3", "--rows"]).Out);      // one step a day
        Assert.Equal(6, first.Out.Split('\n').Count(l => l.StartsWith("replication ", StringComparison.Ordinal)));
        var line = Summary(first.Out);
        Assert.InRange(long.Parse(Word(line, "runs")), 6 * 20, 6 * 3 * 20);                  // every replication runs stage 1; later stages only while there are hits
        Assert.StartsWith("rare world-events ", first.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void TheValleyCanBeSampledAndItsReportRepeats()
    {
        var args = new[] { "simulate", Path.Combine(AppContext.BaseDirectory, "samples", "valley.world.json"), Path.Combine(AppContext.BaseDirectory, "samples", "valley.world.scenario.json"), "365", "--rare", "quake:quake", "--effort", "15", "--replications", "6", "--from-seed", "1" };
        var first = Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Run(args).Out);
        Assert.Contains("target quake:quake stages 2 effort 15 replications 6 runs ", first.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void BadTargetsAndOptionsAreRefusedWithTheirCodesAndExitTwo()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        (string Expect, string[] Extra)[] cases =
        [
            (BalanceCodes.RareTarget, ["--rare", "h"]),
            (BalanceCodes.RareTarget, ["--rare", "h:mid:extra"]),
            (BalanceCodes.RareTarget, ["--rare", "nope:mid"]),
            (BalanceCodes.RareTarget, ["--rare", "h:nope"]),
            (BalanceCodes.EffortRange, ["--rare", "h:mid", "--effort", "0"]),
            (BalanceCodes.ReplicationsRange, ["--rare", "h:mid", "--replications", "0"]),
            (BalanceCodes.OptionUnknown, ["--rare", "h:mid", "--seeds", "3"]),
            (BalanceCodes.OptionTwice, ["--rare", "h:mid", "--rare", "h:great"]),
        ];
        foreach (var (expect, extra) in cases)
        {
            var run = Run(["simulate", world, scenario, "50", .. extra]);
            Assert.Equal(2, run.Code);
            Assert.StartsWith("refused " + expect + ":", run.Err, StringComparison.Ordinal);
        }
        Assert.Equal(2, Run("simulate", world, scenario, "--rare", "h:mid").Code);                         // no horizon
        Assert.Equal(2, Run("simulate", world, files.Write("not json"), "50", "--rare", "h:mid").Code);    // a bad scenario
        Assert.Equal(1, Run("simulate", files.Write("{}"), scenario, "50", "--rare", "h:mid").Code);        // a refused document
    }

    [Fact]
    public void AHazardThatSpreadsAtFullPowerCannotHaveItsNaturalOnsetsTold()
    {
        var root = JsonNode.Parse(LabDocument())!.AsObject();
        Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 1, ["falloff"] = 1000 };
        using var files = new Files();
        var run = Run("simulate", files.Write(root.ToJsonString()), files.Write(Scenario), "50", "--rare", "h:mid");
        Assert.Equal(2, run.Code);
        Assert.StartsWith("refused " + BalanceCodes.OnsetAmbiguous + ":", run.Err, StringComparison.Ordinal);
        var sweep = Run("simulate", files.Write(root.ToJsonString()), files.Write(Scenario), "50", "--seeds", "2", "--onsets");
        Assert.Equal(2, sweep.Code);
        Assert.StartsWith("refused " + BalanceCodes.OnsetAmbiguous + ":", sweep.Err, StringComparison.Ordinal);
    }
}
