using System.Globalization;
using System.Numerics;
using EngineCommon.Balance;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The automatic choice of the runs at each stage (<c>simulate ... --rare hazard:rung [--predraw] --efforts auto</c>): a pilot sets the efforts and its runs are in the report's totals. Checked on the lab worlds whose answer the engine's own renewal
/// chain gives exactly (<see cref="ConditionalCliTests.ExactLab"/>), in both restart modes; the rule itself is worked by hand in EngineCommon.Balance.Tests.
/// </summary>
public sealed class AutoEffortCliTests
{
    private static string Lab(int rate) => RareCliTests.LabDocument(1, rate);

    private static string Word(string line, string key) => RareCliTests.Word(line, key);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheEstimateAgreesWithTheEngineExactValueWithinItsStatedBand(bool predraw)
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(Lab(2000));
        var scenario = files.Write(RareCliTests.Scenario);
        var truth = ConditionalCliTests.ExactLab(Lab(2000), 16_000, RareCliTests.Steps);
        var covered = 0;
        BigInteger sum = 0;
        const int roots = 6;
        for (var root = 1; root <= roots; root++)
        {
            string[] args = ["simulate", world, scenario, "365", "--rare", "h:great", .. predraw ? new[] { "--predraw" } : [], "--efforts", "auto", "--effort", "250", "--pilot-effort", "60", "--pilot-replications", "3", "--replications", "15", "--from-seed", root.ToString(CultureInfo.InvariantCulture)];
            var run = RareCliTests.Run(args);
            Assert.Equal(0, run.Code);
            var band = Word(RareCliTests.Summary(run.Out), "band-ppb").Split("..");
            if (long.Parse(band[0], CultureInfo.InvariantCulture) <= truth && truth <= long.Parse(band[1], CultureInfo.InvariantCulture)) covered++;
            sum += long.Parse(Word(RareCliTests.Summary(run.Out), "mean-ppb"), CultureInfo.InvariantCulture);
        }
        Assert.True(covered >= roots - 2, "only " + covered + " of " + roots + " bands covered the engine-exact " + truth);
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - truth) * 8 <= truth, "the mean of the estimates " + grand + " is more than 12.5% from the engine-exact " + truth);
    }

    [Fact]
    public void AReportNamesThePilotRepeatsByteForByteAndCountsThePilotsRunsAndWork()
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(Lab(2000));
        var scenario = files.Write(RareCliTests.Scenario);
        string[] args = ["simulate", world, scenario, "100", "--rare", "h:mid", "--predraw", "--efforts", "auto", "--effort", "40", "--pilot-effort", "60", "--pilot-replications", "3", "--replications", "5", "--from-seed", "3", "--rows"];
        var first = RareCliTests.Run(args);
        Assert.Equal(0, first.Code);
        Assert.Equal(first.Out, RareCliTests.Run(args).Out);
        var lines = first.Out.Split('\n');
        var pilot = Assert.Single(lines, l => l.StartsWith("rare-auto ", StringComparison.Ordinal));
        Assert.Contains("pilot-effort 60 pilot-replications 3 pilot-runs ", pilot, StringComparison.Ordinal);
        Assert.Contains(" budget 80 efforts ", pilot, StringComparison.Ordinal);                                   // 2 stages x 40
        var summary = RareCliTests.Summary(first.Out);
        var mainRuns = lines.Where(l => l.StartsWith("replication ", StringComparison.Ordinal)).Sum(l => long.Parse(Word(l, "runs"), CultureInfo.InvariantCulture));
        Assert.Equal(long.Parse(Word(pilot, "pilot-runs"), CultureInfo.InvariantCulture) + mainRuns, long.Parse(Word(summary, "runs"), CultureInfo.InvariantCulture));
        var mainWork = lines.Where(l => l.StartsWith("replication ", StringComparison.Ordinal)).Sum(l => long.Parse(Word(l, "work-steps"), CultureInfo.InvariantCulture));
        Assert.Equal(long.Parse(Word(pilot, "pilot-work-steps"), CultureInfo.InvariantCulture) + mainWork, long.Parse(Word(summary, "work-steps"), CultureInfo.InvariantCulture));
        Assert.Equal(Word(pilot, "efforts"), Word(summary, "efforts"));                                             // the report's efforts are the pilot's
    }

    [Fact]
    public void AnExplicitCommandPrintsNoPilotAndTheAutoRunIsTheExplicitRunAtItsEfforts()
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(Lab(2000));
        var scenario = files.Write(RareCliTests.Scenario);
        var auto = RareCliTests.Run("simulate", world, scenario, "100", "--rare", "h:mid", "--efforts", "auto", "--effort", "40", "--pilot-effort", "60", "--pilot-replications", "3", "--replications", "5", "--from-seed", "3", "--rows");
        var efforts = Word(Assert.Single(auto.Out.Split('\n'), l => l.StartsWith("rare-auto ", StringComparison.Ordinal)), "efforts");
        var explicitRun = RareCliTests.Run("simulate", world, scenario, "100", "--rare", "h:mid", "--efforts", efforts, "--replications", "5", "--from-seed", "3", "--rows");
        Assert.DoesNotContain("rare-auto", explicitRun.Out, StringComparison.Ordinal);
        string[] Estimates(string report) => report.Split('\n').Where(l => l.StartsWith("replication ", StringComparison.Ordinal)).Select(l => Word(l, "estimate-ppb")).ToArray();
        Assert.Equal(Estimates(explicitRun.Out), Estimates(auto.Out));            // the main run is untouched by the pilot's seeds
    }

    [Fact]
    public void WhatTheAutomaticChoiceCannotMeanIsRefusedAndAPilotThatNeverReachesAStageIsNotGuessedAround()
    {
        using var files = new RareCliTests.Files();
        var world = files.Write(Lab(20));
        var scenario = files.Write(RareCliTests.Scenario);
        (string Expect, string[] Extra)[] cases =
        [
            (BalanceCodes.PilotWithoutAuto, ["--rare", "h:mid", "--pilot-effort", "5"]),
            (BalanceCodes.ConditionalMix, ["--rare", "h:mid", "--conditional", "--efforts", "auto"]),
            (BalanceCodes.OptionTwice, ["--rare", "h:mid", "--efforts", "auto", "--efforts", "auto"]),
            (BalanceCodes.EffortRange, ["--rare", "h:mid", "--efforts", "auto", "--pilot-effort", "0"]),
            (BalanceCodes.ReplicationsRange, ["--rare", "h:mid", "--efforts", "auto", "--pilot-replications", "0"]),
            (BalanceCodes.RareTarget, ["--rare", "h:nope", "--efforts", "auto"]),
            (BalanceCodes.AutoPilotEmpty, ["--rare", "h:great", "--efforts", "auto", "--pilot-effort", "2", "--pilot-replications", "1"]),
        ];
        foreach (var (expect, extra) in cases)
        {
            var run = RareCliTests.Run(["simulate", world, scenario, "50", .. extra]);
            Assert.Equal(2, run.Code);
            Assert.StartsWith("refused " + expect + ":", run.Err, StringComparison.Ordinal);
        }
    }
}
