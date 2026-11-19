using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using EngineCommon;
using EngineCommon.Balance;
using ScenarioEngine.Contracts;
using Xunit;
using static WorldEventsEngine.Tests.RareCliTests;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The pre-draw restart mode (<c>simulate ... --rare hazard:rung --predraw</c>): fixed-effort splitting whose checkpoints lie just before the severity of an onset is drawn, so the rung is drawn again from them, with a number of runs
/// for each stage. Checked on the world whose answer is exact (one region, an onset that lasts one step, so every step is an independent trial): the chance that some onset within 365 steps reaches a rung with tail t is
/// 1 - (1 - r t / 10^12)^365, which is what the pass-50 mode was checked against too.
/// </summary>
public sealed class PredrawCliTests
{
    private static string Estimate(string output, string key) => Word(Summary(output), key);

    [Theory]
    [InlineData("h:mid", 126_000, 2000, "600,150")]
    [InlineData("h:great", 16_000, 2000, "600,150,150")]
    [InlineData("h:great", 16_000, 200, "400,100,100")]
    public void TheEstimateAgreesWithTheExactValueWithinItsStatedBand(string target, int tail, int rate, string efforts)
    {
        using var files = new Files();
        var world = files.Write(LabDocument(1, rate));
        var scenario = files.Write(Scenario);
        var truth = Exact(tail, rate);
        var covered = 0;
        BigInteger sum = 0;
        const int roots = 6;
        for (var root = 1; root <= roots; root++)
        {
            var run = Run("simulate", world, scenario, Steps.ToString(CultureInfo.InvariantCulture), "--rare", target, "--predraw", "--efforts", efforts, "--replications", "11", "--from-seed", root.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, run.Code);
            var band = Estimate(run.Out, "band-ppb").Split("..");
            if (long.Parse(band[0]) <= truth && truth <= long.Parse(band[1])) covered++;
            sum += long.Parse(Estimate(run.Out, "mean-ppb"));
        }
        Assert.True(covered >= roots - 2, "only " + covered + " of " + roots + " bands covered the exact " + truth);
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - truth) * 8 <= truth, "the mean of the estimates " + grand + " is more than 12.5% from the exact " + truth);
    }

    [Fact]
    public void AReportRepeatsByteForByteNamesItsRestartAndCountsItsWork()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        string[] args = ["simulate", world, scenario, "100", "--rare", "h:great", "--predraw", "--efforts", "30,10,10", "--replications", "6", "--from-seed", "3", "--rows"];
        var first = Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, Run(args).Out);
        Assert.Contains(" restart pre-draw\n", first.Out, StringComparison.Ordinal);
        Assert.Contains(" efforts 30,10,10 ", first.Out, StringComparison.Ordinal);
        Assert.Equal(6, first.Out.Split('\n').Count(l => l.StartsWith("replication ", StringComparison.Ordinal) && l.Contains(" work-steps ", StringComparison.Ordinal)));
        var work = long.Parse(Word(Summary(first.Out), "work-steps"));
        Assert.InRange(work, 6 * 30, 6 * (30 * 100 + 10 * 100 + 10 * 100));         // a stage-1 run lives at most the horizon, a restart at most the rest of it
    }

    [Fact]
    public void EqualEffortsGiveTheSameNumbersAsTheOneEffortAndTheOldModeIsUnchanged()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        var one = Run("simulate", world, scenario, "100", "--rare", "h:great", "--predraw", "--effort", "12", "--replications", "6", "--from-seed", "4", "--rows");
        var many = Run("simulate", world, scenario, "100", "--rare", "h:great", "--predraw", "--efforts", "12,12,12", "--replications", "6", "--from-seed", "4", "--rows");
        Assert.Equal(0, one.Code);
        Assert.Equal(0, many.Code);
        // The same seeds give the same runs; only the way the efforts are named in the report differs ('effort 12' against 'efforts 12,12,12' would be the same uniform report, so they are in fact byte-identical).
        Assert.Equal(one.Out, many.Out);
        var old = Run("simulate", world, scenario, "100", "--rare", "h:great", "--effort", "12", "--replications", "6", "--from-seed", "4");
        Assert.DoesNotContain("work-steps", old.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("restart", old.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEffortOptionsAreRefusedWithTheirCodesAndExitTwo()
    {
        using var files = new Files();
        var world = files.Write(LabDocument());
        var scenario = files.Write(Scenario);
        (string Expect, string[] Extra)[] cases =
        [
            (BalanceCodes.EffortsCount, ["--rare", "h:great", "--predraw", "--efforts", "10,10"]),
            (BalanceCodes.EffortsCount, ["--rare", "h:mid", "--efforts", "10,10,10"]),
            (BalanceCodes.EffortsTwice, ["--rare", "h:great", "--effort", "10", "--efforts", "10,10,10"]),
            (BalanceCodes.EffortRange, ["--rare", "h:great", "--efforts", "10,0,10"]),
            (BalanceCodes.EffortRange, ["--rare", "h:great", "--efforts", "10,5001,10"]),
            (BalanceCodes.OptionValue, ["--rare", "h:great", "--efforts", "10,x,10"]),
            (BalanceCodes.OptionTwice, ["--rare", "h:great", "--efforts", "10,10,10", "--efforts", "10,10,10"]),
            (BalanceCodes.OptionTwice, ["--rare", "h:great", "--predraw", "--predraw"]),
        ];
        foreach (var (expect, extra) in cases)
        {
            var run = Run(["simulate", world, scenario, "50", .. extra]);
            Assert.Equal(2, run.Code);
            Assert.StartsWith("refused " + expect + ":", run.Err, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AHazardWhoseOnsetCanBeVetoedAfterItsTrialIsRefusedForTheRestart()
    {
        var root = JsonNode.Parse(LabDocument())!.AsObject();
        Tiny.Hazard(root)["paced"] = true;
        using var files = new Files();
        var world = files.Write(root.ToJsonString());
        var scenario = files.Write(Scenario);
        var run = Run("simulate", world, scenario, "50", "--rare", "h:mid", "--predraw");
        Assert.Equal(2, run.Code);
        Assert.StartsWith("refused " + BalanceCodes.PredrawHazard + ":", run.Err, StringComparison.Ordinal);
        Assert.Equal(0, Run("simulate", world, scenario, "50", "--rare", "h:mid", "--effort", "5", "--replications", "1").Code);     // the pass-50 mode does not mind
    }

    [Fact]
    public void ARestartThatCannotReproduceTheForcedOnsetStopsTheRunInsteadOfCountingIt()
    {
        // An onset that lasts three steps at certainty: after step 1 the hazard is still going in the only region, so a trial forced into step 2 cannot start, and the checkpoint is not reproduced.
        var root = Lab.Hawkes(1, 1_000_000, 0, 1000, 0);
        var low = Tiny.Rung(root, 0);
        low["minDuration"] = 3;
        low["maxDuration"] = 3;
        var text = root.ToJsonString();
        var set = WorldLoader.Load(text);
        var host = new Dictionary<string, FactValue> { ["ext.alert"] = FactValue.From(0) };
        var model = PredrawHazardModel.Create(set, text, host, [], 10, "h:low");
        var runtime = WorldRuntime.Create(set);
        var facts = new Dictionary<string, FactValue>(host);
        WorldCli.Live(runtime, facts, [], 2, (_, _, _) => { }, (_, _, _) => { });
        Assert.Single(runtime.ActiveHazards);
        var refusal = Assert.Throws<EngineRefusedException>(() => model.Next(new PredrawCheckpoint(runtime, facts, 0, 2, 0), 2, 7));
        Assert.Equal(BalanceCodes.PredrawLost, refusal.Code);
    }
}
