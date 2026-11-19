using System.Numerics;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// The automatic choice of the runs at each stage (<see cref="AutoEfforts"/>, <c>--efforts auto</c>): the rule worked by hand, the pooling of a pilot, the refusals, the pilot counted in the totals, the main run independent of the pilot, and the
/// estimate checked against the gambler's ruin whose answer is exact (the same model as <see cref="SplittingTests"/>, with the work of a run counted).
/// </summary>
public sealed class AutoEffortsTests
{
    private sealed record Walk(long Position, long Time);

    /// <summary>The gambler's ruin of <see cref="SplittingTests"/>, here counting the steps each run lives so the rule has a cost to weigh.</summary>
    private sealed class Ruin(long[] levels) : ISplittingModel<Walk>
    {
        public int Stages => levels.Length;

        public StageOutcome<Walk> First(long seed) => Go(new Walk(1, 0), 1, seed);

        public StageOutcome<Walk> Next(Walk checkpoint, int stage, long seed) => checkpoint.Position >= levels[stage - 1] ? new StageOutcome<Walk>(checkpoint) : Go(checkpoint, stage, seed);

        private StageOutcome<Walk> Go(Walk from, int stage, long seed)
        {
            var position = from.Position;
            var time = from.Time;
            long work = 0;
            while (position > 0 && position < levels[stage - 1] && time < 100_000)
            {
                position += DeterministicRoll.Below((ulong)seed, "walk", time, 1000) < 400 ? 1 : -1;
                time++;
                work++;
            }
            return position >= levels[stage - 1] ? new StageOutcome<Walk>(new Walk(position, time), work) : new StageOutcome<Walk>(null, work);
        }
    }

    private const long ExactPpb = 3_883_605;                         // 4096 / 1,054,690, see SplittingTests

    // ---- the rule ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRuleIsWorkedByHand()
    {
        // Two stages, 99 pilot runs each, 9 and 49 hits: p = 9.5/100 and 49.5/100, so (1 - p)/p = 9.526 and 1.0202 and the weights (their roots) are 3.0864 and 1.0101;
        // of a budget of 100 the shares are 75.3 and 24.7, rounded half up to 75 and 25.
        Assert.Equal([75, 25], AutoEfforts.Allocate([99, 99], [9, 49], [0, 0], 100));
        // Equal fractions, but a run of the second stage costs four times a run of the first: half the runs, so 200 and 100 of 300.
        Assert.Equal([200, 100], AutoEfforts.Allocate([100, 100], [49, 49], [100, 400], 300));
        // Three stages with the work counted (cost per run 10, 3 and 2): the first stage is cheap per hit but has p = 0.5, the last is the rarest; shares 64, 231 and 605 of 900.
        Assert.Equal([64, 231, 605], AutoEfforts.Allocate([200, 200, 200], [100, 40, 10], [2000, 600, 400], 900));
    }

    [Fact]
    public void AStageIsKeptToItsLimitsAndAStageWithoutAHitStillGetsAFiniteFraction()
    {
        // No hit at stage 1 (p = 0.5/101) and 99 of 100 at stage 2: nearly the whole budget goes to stage 1, and stage 2 is held at the least effort.
        Assert.Equal([991, 10], AutoEfforts.Allocate([100, 100], [0, 99], [0, 0], 1000));
        // A budget the cap cannot hold: stage 1 is cut to the most runs a stage may start.
        Assert.Equal([BalanceCodes.MaxEffort, 172], AutoEfforts.Allocate([100, 100], [0, 99], [0, 0], 20_000));
        // A budget of one run a stage cannot give each stage ten.
        Assert.Equal([1, 1], AutoEfforts.Allocate([10, 10], [5, 5], [0, 0], 1));
    }

    [Fact]
    public void TheRuleHasNoFloatingPointAndAHugeCountDoesNotOverflow()
    {
        var efforts = AutoEfforts.Allocate([long.MaxValue / 4, long.MaxValue / 4], [long.MaxValue / 8, 1], [0, 0], 400);
        Assert.Equal(2, efforts.Count);
        Assert.All(efforts, e => Assert.InRange(e, 1, BalanceCodes.MaxEffort));
    }

    // ---- the pilot ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePilotIsPooledOverTheReplicationsThatReachedEachStage()
    {
        // Three pilot replications of 10 runs a stage: the first reaches stage 2 with 5 hits and 2, the second has 3 hits and then none at stage 2, the third has no hit at stage 1 and never starts stage 2 (its 7 steps there are not counted).
        var pilot = new List<SplitReplication>
        {
            new(1, [5, 2], 20, 0, 160, [100, 60]),
            new(2, [3, 0], 20, 0, 140, [100, 40]),
            new(3, [0, 0], 10, 0, 100, [100, 7]),
        };
        var plan = AutoEfforts.Plan(pilot, 10, 2, 100);
        Assert.Equal([30L, 20L], plan.Runs);
        Assert.Equal([8L, 2L], plan.Hits);
        Assert.Equal([300L, 100L], plan.Work);
        Assert.Equal(50, plan.TotalRuns);
        Assert.Equal(400, plan.TotalWork);
        Assert.Equal(AutoEfforts.Allocate(plan.Runs, plan.Hits, plan.Work, 100), plan.Efforts);
    }

    [Fact]
    public void AStageNoPilotReplicationReachedIsRefusedNotGuessed()
    {
        var pilot = new List<SplitReplication> { new(1, [0, 0], 10, 0), new(2, [0, 0], 10, 0) };
        var refusal = Assert.Throws<EngineRefusedException>(() => AutoEfforts.Plan(pilot, 10, 2, 100));
        Assert.Equal(BalanceCodes.AutoPilotEmpty, refusal.Code);
        Assert.Contains("stage 2 of 2", refusal.Message, StringComparison.Ordinal);
    }

    // ---- the run -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePilotIsCountedInTheRunsAndTheWorkAndTheMainRunIsTheExplicitRunAtTheChosenEfforts()
    {
        var model = new Ruin([3, 6, 9, 12]);
        var report = FixedEffortSplitting.RunAuto(model, 7, 200, 9, 60, 4);
        var plan = report.Pilot!;
        Assert.True(plan.TotalRuns is > 0 and <= 4 * 4 * 60);                              // the pilot started at most 4 replications x 4 stages x 60 runs
        var summary = SplitSummary.Of(report);
        Assert.Equal(plan.TotalRuns + report.Replications.Sum(r => (long)r.Runs), summary.Runs);
        // The main run is exactly the explicit run at those efforts under the same root seed: the pilot's seeds are a different stream, so it did not touch the main run.
        var explicitRun = FixedEffortSplitting.Run(model, 7, report.Efforts!, 9);
        Assert.Equal(explicitRun.Replications.Select(r => r.Ppb), report.Replications.Select(r => r.Ppb));
        Assert.Equal(AutoEfforts.Allocate(plan.Runs, plan.Hits, plan.Work, plan.Budget), report.Efforts);
        Assert.Equal(800, plan.Budget);                                                    // 4 stages x the effort asked for
    }

    [Fact]
    public void TheWorkOfAReplicationIsKeptByStageAndSumsToItsWork()
    {
        var report = FixedEffortSplitting.Run(new Ruin([3, 6, 9, 12]), 11, 100, 8);
        foreach (var replication in report.Replications)
        {
            Assert.Equal(replication.Work, replication.StageWork!.Sum());
            for (var stage = 1; stage < 4; stage++)
                if (replication.Hits[stage - 1] > 0) Assert.True(replication.StageWork![stage] > 0, "stage " + (stage + 1) + " started runs but spent no work");
        }
        Assert.Contains(report.Replications, r => r.Hits[1] > 0);                   // the check above was reached
    }

    [Fact]
    public void ThePilotDrawsItsOwnSeedsNotTheMainRunsOwn()
    {
        // If the pilot used the root seed it would repeat the main run's first replications. The uniform run at the pilot's effort and replications under the same root is exactly that repeat, and the pilot's pooled counts must not equal its counts.
        var model = new Ruin([3, 6, 9, 12]);
        var plan = FixedEffortSplitting.RunAuto(model, 5, 200, 3, 150, 4).Pilot!;
        var repeat = FixedEffortSplitting.Run(model, 5, 150, 4);
        var repeated = Enumerable.Range(0, 4).Select(stage => repeat.Replications.Sum(r => (long)r.Hits[stage])).ToArray();
        Assert.NotEqual(repeated, plan.Hits);
        Assert.NotEqual(repeat.Replications.Sum(r => r.Work), plan.Work.Sum());
    }

    [Fact]
    public void TheReportNamesThePilotRepeatsByteForByteAndCountsThePilotInItsTotals()
    {
        var model = new Ruin([3, 6, 9, 12]);
        var first = FixedEffortSplitting.Render("h", "t", FixedEffortSplitting.RunAuto(model, 3, 100, 6, 40, 3), rows: true);
        Assert.Equal(first, FixedEffortSplitting.Render("h", "t", FixedEffortSplitting.RunAuto(model, 3, 100, 6, 40, 3), rows: true));
        var lines = first.Split('\n');
        var pilotLine = Assert.Single(lines, l => l.StartsWith("rare-auto ", StringComparison.Ordinal));
        Assert.Contains("pilot-effort 40 pilot-replications 3 pilot-runs ", pilotLine, StringComparison.Ordinal);
        Assert.Contains("pilot-work-steps ", pilotLine, StringComparison.Ordinal);
        var summary = Assert.Single(lines, l => l.StartsWith("rare-estimate ", StringComparison.Ordinal));
        var pilotRuns = long.Parse(Word(pilotLine, "pilot-runs"));
        var mainRuns = lines.Where(l => l.StartsWith("replication ", StringComparison.Ordinal)).Sum(l => long.Parse(Word(l, "runs")));
        Assert.Equal(pilotRuns + mainRuns, long.Parse(Word(summary, "runs")));
        Assert.Contains(" efforts ", summary, StringComparison.Ordinal);
        // The explicit run carries no pilot line.
        Assert.DoesNotContain("rare-auto", FixedEffortSplitting.Render("h", "t", FixedEffortSplitting.Run(model, 3, [30, 20, 10, 10], 6), rows: true), StringComparison.Ordinal);
    }

    private static string Word(string line, string key)
    {
        var words = line.Split(' ');
        return words[Array.IndexOf(words, key) + 1];
    }

    [Fact]
    public void TheEstimatesAgreeWithTheExactValueAndThePilotCostIsPaidFor()
    {
        var model = new Ruin([3, 6, 9, 12]);
        var covered = 0;
        BigInteger sum = 0;
        long extra = 0;
        const int roots = 20;
        for (var root = 1; root <= roots; root++)
        {
            var report = FixedEffortSplitting.RunAuto(model, root, 200, 21, 100, 5);
            var summary = SplitSummary.Of(report);
            if (summary.LowPpb <= ExactPpb && ExactPpb <= summary.HighPpb) covered++;
            sum += summary.MeanPpb;
            extra += report.Pilot!.TotalRuns;
        }
        Assert.True(covered >= 15, "only " + covered + " of " + roots + " bands covered " + ExactPpb);
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - ExactPpb) * 20 <= ExactPpb, "the mean of the estimates " + grand + " is more than 5% from " + ExactPpb);
        Assert.True(extra > 0);
    }

    [Fact]
    public void ARunWithABadBudgetOrPilotIsRefusedWithTheRangeCodes()
    {
        var model = new Ruin([3, 6]);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.RunAuto(model, 1, 0, 3, 10, 2)).Code);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.RunAuto(model, 1, 10, 3, BalanceCodes.MaxEffort + 1, 2)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.RunAuto(model, 1, 10, 0, 10, 2)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.RunAuto(model, 1, 10, 3, 10, 0)).Code);
    }

    // ---- the command line ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheOptionLineReadsAutoAndRefusesWhatItCannotMean()
    {
        var auto = RareOptions.Parse(["365", "--rare", "a:b", "--efforts", "auto", "--effort", "300", "--pilot-effort", "50", "--pilot-replications", "7"]);
        Assert.True(auto.Auto);
        Assert.Null(auto.Efforts);
        Assert.Equal((300, 50, 7), (auto.Effort, auto.PilotEffort, auto.PilotReplications));
        var defaults = RareOptions.Parse(["365", "--rare", "a:b", "--efforts", "auto"]);
        Assert.Equal((RareOptions.DefaultEffort, RareOptions.DefaultPilotEffort, RareOptions.DefaultPilotReplications), (defaults.Effort, defaults.PilotEffort, defaults.PilotReplications));
        Assert.False(RareOptions.Parse(["365", "--rare", "a:b", "--efforts", "5,6"]).Auto);

        (string Code, string[] Args)[] refused =
        [
            (BalanceCodes.PilotWithoutAuto, ["365", "--rare", "a:b", "--pilot-effort", "50"]),
            (BalanceCodes.PilotWithoutAuto, ["365", "--rare", "a:b", "--efforts", "5,6", "--pilot-replications", "3"]),
            (BalanceCodes.ConditionalMix, ["365", "--rare", "a:b", "--conditional", "--efforts", "auto"]),
            (BalanceCodes.OptionTwice, ["365", "--rare", "a:b", "--efforts", "auto", "--efforts", "5,6"]),
            (BalanceCodes.OptionTwice, ["365", "--rare", "a:b", "--efforts", "5,6", "--efforts", "auto"]),
            (BalanceCodes.OptionTwice, ["365", "--rare", "a:b", "--efforts", "auto", "--pilot-effort", "5", "--pilot-effort", "6"]),
            (BalanceCodes.EffortRange, ["365", "--rare", "a:b", "--efforts", "auto", "--pilot-effort", "0"]),
            (BalanceCodes.EffortRange, ["365", "--rare", "a:b", "--efforts", "auto", "--pilot-effort", "5001"]),
            (BalanceCodes.ReplicationsRange, ["365", "--rare", "a:b", "--efforts", "auto", "--pilot-replications", "0"]),
            (BalanceCodes.OptionValue, ["365", "--rare", "a:b", "--efforts", "auto", "--pilot-effort"]),
            (BalanceCodes.OptionValue, ["365", "--rare", "a:b", "--efforts", "automatic"]),
        ];
        foreach (var (code, args) in refused)
            Assert.Equal(code, Assert.Throws<EngineRefusedException>(() => RareOptions.Parse(args)).Code);
    }
}
