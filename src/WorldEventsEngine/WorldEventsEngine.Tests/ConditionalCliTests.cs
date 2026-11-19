using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using EngineCommon.Balance;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The conditional estimator of the world-events tool (<c>simulate ... --rare hazard:rung --conditional</c>): the run adds, at every trial of the hazard, the engine's own chance of the trial times the tail of the rung, and stops at the first
/// onset that reaches it. Its oracles are independent of it: for the lab worlds (one region, one hazard that lasts one step) an exact renewal chain computed here in whole numbers, which includes the engine's rule that the step after an
/// onset has no trial (so it is a little under the 1 - (1 - c t)^365 of the earlier passes: 0.001% at 20 ppm, 0.2% at 2,000 ppm), and for the worlds with excitation a plain seed sweep of the same document.
/// </summary>
public sealed class ConditionalCliTests
{
    private static string LabText(int rate, Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(RareCliTests.LabDocument(1, rate))!.AsObject();
        edit?.Invoke(root);
        return root.ToJsonString();
    }

    /// <summary>
    /// The exact chance in ppb (floored) that a natural onset with the tail reaches the rung within <paramref name="steps"/> steps in the lab world: each step the hazard is free, blocked (the step after an onset) or, until step
    /// <paramref name="openFrom"/>, vetoed. Numerators over 10^12 per step.
    /// </summary>
    internal static long ExactLab(string document, int tailPpm, int steps, int openFrom = 0)
    {
        var set = WorldLoader.Load(document);
        var chance = WorldRuntime.Create(set).OnsetChance(set.Hazards[0], 0, 0);
        BigInteger free = 1;
        BigInteger blocked = 0;
        BigInteger event_ = 0;
        var all = BigInteger.Pow(10, 12);
        for (var t = 0; t < steps; t++)
        {
            if (t < openFrom)
            {
                free = (free + blocked) * all;
                blocked = 0;
                event_ *= all;
                continue;
            }
            var next = free * (1_000_000 - chance) * 1_000_000 + blocked * all;
            var block = free * chance * (1_000_000 - tailPpm);
            event_ = event_ * all + free * chance * tailPpm;
            free = next;
            blocked = block;
        }
        var scale = BigInteger.Pow(all, steps);
        return (long)(event_ * 1_000_000_000 / scale);
    }

    private static string Summary(string report) => RareCliTests.Summary(report);

    private static long Mean(string report) => long.Parse(RareCliTests.Word(Summary(report), "mean-ppb"), CultureInfo.InvariantCulture);

    [Fact]
    public void TheRenewalChainOfTheLabIsWorkedByHandForTwoSteps()
    {
        // Two steps, chance c = 2,000 ppm, tail t = 16,000 ppm: the event in step 1 is c t = 3.2e-5; if step 1 had a non-reaching onset (c (1 - t)) step 2 has no trial; otherwise (1 - c) step 2 has the same chance.
        // P = c t + (1 - c) c t = 3.2e-5 + 0.998 x 3.2e-5 = 6.3936e-5 = 63,936 ppb.
        Assert.Equal(63_936, ExactLab(LabText(2000), 16_000, 2));
    }

    [Theory]
    [InlineData(20, "great", 16_000, 0.0005)]
    [InlineData(200, "great", 16_000, 0.004)]
    [InlineData(2000, "mid", 126_000, 0.012)]
    [InlineData(2000, "great", 16_000, 0.012)]
    public void TheEstimateOfTheLabWorldIsTheExactValueOfTheEngineToItsSamplingError(int rate, string rung, int tail, double tolerance)
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(LabText(rate));
        var scenario = files.Write(RareCliTests.Scenario);
        var truth = ExactLab(LabText(rate), tail, RareCliTests.Steps);
        BigInteger sum = 0;
        const int roots = 6;
        for (var root = 1; root <= roots; root++)
        {
            var run = RareCliTests.Run("simulate", world, scenario, "365", "--rare", "h:" + rung, "--conditional", "--effort", "50", "--replications", "8", "--from-seed", root.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, run.Code);
            sum += Mean(run.Out);
        }
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - truth) <= truth * tolerance, "the mean " + grand + " is more than " + tolerance * 100 + "% from the exact " + truth);
    }

    [Fact]
    public void AnOnsetThatLastsPastTheHorizonLeavesExactlyOneChanceOfAnEventPerOnsetAndTheLastStepCounts()
    {
        // Every onset lasts 200 steps, the horizon is 100: after the first onset nothing more can start, so the event is "an onset in the horizon, and it reaches the rung": q (1 - (1 - c)^100) exactly, with the onset in the last
        // step counted and what the onset does after the horizon not mattering (the rung is drawn at the onset).
        var document = LabText(2000, root => { foreach (var rung in Tiny.Hazard(root)["ladder"]!.AsArray()) { rung!["minDuration"] = 200; rung["maxDuration"] = 200; } });
        var set = WorldLoader.Load(document);
        var c = WorldRuntime.Create(set).OnsetChance(set.Hazards[0], 0, 0);
        var all = BigInteger.Pow(10, 6);
        var truth = (long)((BigInteger.Pow(all, 100) - BigInteger.Pow(all - c, 100)) * 16_000 * 1_000_000_000 / (BigInteger.Pow(all, 100) * 1_000_000));
        using var files = new RareCliTests.Files();
        BigInteger sum = 0;
        for (var root = 1; root <= 4; root++)
        {
            var run = RareCliTests.Run("simulate", files.Write(document), files.Write(RareCliTests.Scenario), "100", "--rare", "h:great", "--conditional", "--effort", "100", "--replications", "8", "--from-seed", root.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, run.Code);
            sum += Mean(run.Out);
        }
        var grand = (long)(sum / 4);
        Assert.True(Math.Abs(grand - truth) * 100 <= truth, "the mean " + grand + " is more than 1% from the exact " + truth);
    }

    [Fact]
    public void AOneStepHorizonWeighsEveryRunExactlyTheChanceOfTheTrialTimesTheTail()
    {
        using var files = new RareCliTests.Files();
        var set = WorldLoader.Load(LabText(2000));
        var chance = WorldRuntime.Create(set).OnsetChance(set.Hazards[0], 0, 0);
        var run = RareCliTests.Run("simulate", files.Write(LabText(2000)), files.Write(RareCliTests.Scenario), "1", "--rare", "h:great", "--conditional", "--effort", "9", "--replications", "6", "--rows");
        Assert.Equal(0, run.Code);
        Assert.Equal(32_000, chance * 16_000 / 1000);                // 2,000 x 16,000 / 10^12 = 3.2e-5 = 32,000 ppb
        Assert.All(run.Out.Split('\n').Where(l => l.StartsWith("replication ", StringComparison.Ordinal)), l =>
        {
            Assert.EndsWith(" estimate-ppb 32000", l, StringComparison.Ordinal);
            Assert.Contains("informative 9/9 runs 9 work-steps 9 ", l, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ARunStopsAtTheFirstEventAndTrialsAfterItInTheSameStepAreNotCounted()
    {
        // Two regions, an onset certain in each every step, the first rung (tail 10^6) is every onset: the event is the first trial of the first region. Its weight is c t = 1 (a whole probability, 10^9 ppb) and the run lives one step.
        // The second region's trial in that same step comes after the event and must not add to it (the answer would be 2), and the run must stop there (the steps lived are 1, not the horizon).
        var root = Tiny.Root();
        Tiny.Certain(root);
        Tiny.Hazard(root)["cooldown"] = 0;
        Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 };
        var rung = Tiny.Rung(root, 0)["id"]!.GetValue<string>();
        using var files = new RareCliTests.Files();
        var run = RareCliTests.Run("simulate", files.Write(root.ToJsonString()), files.Write(RareCliTests.Scenario), "40", "--rare", "h:" + rung, "--conditional", "--effort", "7", "--replications", "3", "--rows");
        Assert.Equal(0, run.Code);
        Assert.All(run.Out.Split('\n').Where(l => l.StartsWith("replication ", StringComparison.Ordinal)), l => Assert.EndsWith(" runs 7 work-steps 7 estimate-ppb 1000000000", l, StringComparison.Ordinal));
    }

    [Fact]
    public void AConditionOnTheHazardRemovesTheTrialsWhileItDoesNotHoldAndTheExactValueFollows()
    {
        // The hazard's condition is the host fact ext.alert > 5; the scenario raises it at step 120, so the first 120 steps have no onset at all and the exact chance is that of the remaining 245.
        var document = LabText(2000, root => Tiny.Hazard(root)["when"] = Tiny.Cond("ext.alert", "gt", 5));
        const string scenario = "{ \"facts\": { \"ext.alert\": 0 }, \"script\": [ { \"at\": 120, \"do\": \"fact\", \"arg\": \"ext.alert=9\" } ] }";
        var truth = ExactLab(document, 16_000, 365, 120);
        Assert.InRange(truth, 5_000_000, 8_000_000);
        using var files = new RareCliTests.Files();
        BigInteger sum = 0;
        for (var root = 1; root <= 6; root++)
        {
            var run = RareCliTests.Run("simulate", files.Write(document), files.Write(scenario), "365", "--rare", "h:great", "--conditional", "--effort", "50", "--replications", "8", "--from-seed", root.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, run.Code);
            sum += Mean(run.Out);
        }
        var grand = (long)(sum / 6);
        Assert.True(Math.Abs(grand - truth) * 50 <= truth, "the mean " + grand + " is more than 2% from the exact " + truth);
        var never = RareCliTests.Run("simulate", files.Write(document), files.Write(RareCliTests.Scenario), "365", "--rare", "h:great", "--conditional", "--effort", "5", "--replications", "3");
        Assert.Contains(" mean-ppb 0 ", Summary(never.Out), StringComparison.Ordinal);               // the condition never holds: no trial, a flat zero
        Assert.Contains(" extinct 3 ", Summary(never.Out), StringComparison.Ordinal);
    }

    [Fact]
    public void AHazardWithExcitationAgreesWithAPlainSweepOfTheSameDocumentAndDiffersFromTheRateAlone()
    {
        // Each onset adds 200,000 ppm to the next steps' chance (a quarter of it fades each step, up to 600,000): the chance of an event over 60 steps is not that of the base rate.
        var excited = Lab.Hawkes(1, 20_000, 200_000, 250, 600_000);
        var ladder = Tiny.Hazard(excited)["ladder"]!.AsArray();
        foreach (var (id, tail) in new[] { ("mid", 126_000), ("great", 16_000) })
        {
            var rung = Tiny.Copy(ladder[0]!).AsObject();
            rung["id"] = id;
            rung["key"] = "r." + id;
            rung["tailPpm"] = tail;
            ladder.Add(rung);
        }
        var plainRate = Lab.Hawkes(1, 20_000, 0, 1000, 0);
        var plainLadder = Tiny.Hazard(plainRate)["ladder"]!.AsArray();
        plainLadder.Add(Tiny.Copy(ladder[1]!));
        using var files = new RareCliTests.Files();
        var world = files.Write(excited.ToJsonString());
        var scenario = files.Write(RareCliTests.Scenario);
        var sweep = RareCliTests.Run("simulate", world, scenario, "60", "--seeds", "4000", "--from-seed", "1", "--onsets", "--rows", "--metrics", "stress.end");
        Assert.Equal(0, sweep.Code);
        var seedsWithMid = sweep.Out.Split('\n').Where(l => l.StartsWith("onset hazard.h.rung ", StringComparison.Ordinal)).Select(l => l.Split(' ')).Where(r => long.Parse(r[3], CultureInfo.InvariantCulture) >= 1).Select(r => r[2]).Distinct().Count();
        var (plain, band) = PlainEstimate.Of(seedsWithMid, 4000);
        var run = RareCliTests.Run("simulate", world, scenario, "60", "--rare", "h:mid", "--conditional", "--effort", "100", "--replications", "21", "--from-seed", "1");
        Assert.Equal(0, run.Code);
        var conditional = Mean(run.Out);
        Assert.True(Math.Abs(conditional - plain) <= band + band / 2, "conditional " + conditional + " and plain " + plain + " (band " + band + ") disagree");
        var rateOnly = RareCliTests.Run("simulate", files.Write(plainRate.ToJsonString()), scenario, "60", "--rare", "h:mid", "--conditional", "--effort", "100", "--replications", "21", "--from-seed", "1");
        Assert.True(conditional > Mean(rateOnly.Out) * 12 / 10, "the excitation should raise the estimate by more than 20% over the base rate's " + Mean(rateOnly.Out) + ", got " + conditional);
    }

    [Fact]
    public void AReportRepeatsByteForByteHorizonInDaysIsTheSameAndTheRowsCountTheStepsLived()
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(LabText(2000));
        var scenario = files.Write(RareCliTests.Scenario);
        string[] args = ["simulate", world, scenario, "100", "--rare", "h:great", "--conditional", "--effort", "20", "--replications", "6", "--from-seed", "3", "--rows"];
        var first = RareCliTests.Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, RareCliTests.Run(args).Out);
        Assert.Equal(first.Out, RareCliTests.Run("simulate", world, scenario, "--days", "100", "--rare", "h:great", "--conditional", "--effort", "20", "--replications", "6", "--from-seed", "3", "--rows").Out);
        Assert.StartsWith("rare world-events ", first.Out, StringComparison.Ordinal);
        Assert.Contains(" target h:great horizon 100 steps root-seed 3 conditional\n", first.Out, StringComparison.Ordinal);
        Assert.Equal(6, first.Out.Split('\n').Count(l => l.StartsWith("replication ", StringComparison.Ordinal)));
        var summary = Summary(first.Out);
        Assert.Contains(" estimator conditional stages 1 effort 20 replications 6 runs 120 ", summary, StringComparison.Ordinal);
        Assert.InRange(long.Parse(RareCliTests.Word(summary, "work-steps"), CultureInfo.InvariantCulture), 1, 120 * 100);          // a run that ends at an event lived fewer steps than the horizon
    }

    [Fact]
    public void WhatTheConditionalModeCannotMeanIsRefusedAndWhatThePreDrawRestartRefusesIsAccepted()
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(LabText(2000));
        var scenario = files.Write(RareCliTests.Scenario);
        (string Expect, string[] Extra)[] cases =
        [
            (BalanceCodes.ConditionalMix, ["--rare", "h:mid", "--conditional", "--predraw"]),
            (BalanceCodes.ConditionalMix, ["--rare", "h:mid", "--conditional", "--efforts", "5"]),
            (BalanceCodes.OptionTwice, ["--rare", "h:mid", "--conditional", "--conditional"]),
            (BalanceCodes.RareTarget, ["--rare", "nope:mid", "--conditional"]),
            (BalanceCodes.RareTarget, ["--rare", "h:nope", "--conditional"]),
            (BalanceCodes.EffortRange, ["--rare", "h:mid", "--conditional", "--effort", "0"]),
        ];
        foreach (var (expect, extra) in cases)
        {
            var run = RareCliTests.Run(["simulate", world, scenario, "50", .. extra]);
            Assert.Equal(2, run.Code);
            Assert.StartsWith("refused " + expect + ":", run.Err, StringComparison.Ordinal);
        }
        // Hazards the other rare-event modes refuse are handled, because the engine reports the veto in the chance: pacing, and a spread at full power (a trial is a trial whatever it spreads to).
        var paced = JsonNode.Parse(LabText(2000))!.AsObject();
        Tiny.Hazard(paced)["paced"] = true;
        Assert.Equal(0, RareCliTests.Run("simulate", files.Write(paced.ToJsonString()), scenario, "50", "--rare", "h:mid", "--conditional", "--effort", "5", "--replications", "2").Code);
        var spreading = JsonNode.Parse(LabText(2000))!.AsObject();
        Tiny.Hazard(spreading)["reach"] = new JsonObject { ["hops"] = 1, ["falloff"] = 1000 };
        var spread = files.Write(spreading.ToJsonString());
        Assert.Equal(2, RareCliTests.Run("simulate", spread, scenario, "50", "--rare", "h:mid", "--effort", "5", "--replications", "2").Code);
        Assert.Equal(0, RareCliTests.Run("simulate", spread, scenario, "50", "--rare", "h:mid", "--conditional", "--effort", "5", "--replications", "2").Code);
    }

    [Fact]
    public void TheValleyCanBeEstimatedAndItsReportRepeats()
    {
        var args = new[] { "simulate", Path.Combine(AppContext.BaseDirectory, "samples", "valley.world.json"), Path.Combine(AppContext.BaseDirectory, "samples", "valley.world.scenario.json"), "365", "--rare", "quake:quake", "--conditional", "--effort", "6", "--replications", "6", "--from-seed", "1" };
        var first = RareCliTests.Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, RareCliTests.Run(args).Out);
        Assert.Contains("target quake:quake estimator conditional stages 1 effort 6 replications 6 runs 36 ", first.Out, StringComparison.Ordinal);
    }
}
