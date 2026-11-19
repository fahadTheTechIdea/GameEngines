using System.Numerics;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// The fixed-effort splitting sampler on a case whose answer is known exactly: a gambler's ruin. A walk starts at 1, steps up with chance 2/5 and down with 3/5, and is lost at 0; the rare event is reaching 12 first. With r = q/p = 3/2 the
/// chance is (3^x - 2^x) 2^B / (2^x (3^B - 2^B)) = 4096 / 1,054,690 = 0.003883..., rare enough that a plain run needs hundreds of runs per hit, and plain Monte Carlo can still reach it, so the estimates can be checked against both
/// the exact value and a plain run. The walk's checkpoint is its position and time; its randomness is the shared counter-based generator, so a checkpoint continued under another seed is an independent future, as the engines' are.
/// </summary>
public sealed class SplittingTests
{
    private sealed record Walk(long Position, long Time);

    private sealed class RuinModel(long start, long[] levels, int upPermille) : ISplittingModel<Walk>
    {
        public int Stages => levels.Length;

        public StageOutcome<Walk> First(long seed) => Go(new Walk(start, 0), 1, seed);

        public StageOutcome<Walk> Next(Walk checkpoint, int stage, long seed) => checkpoint.Position >= levels[stage - 1] ? new StageOutcome<Walk>(checkpoint) : Go(checkpoint, stage, seed);

        private StageOutcome<Walk> Go(Walk from, int stage, long seed)
        {
            var position = from.Position;
            var time = from.Time;
            while (position > 0 && position < levels[stage - 1] && time < 100_000)
            {
                position += DeterministicRoll.Below((ulong)seed, "walk", time, 1000) < upPermille ? 1 : -1;
                time++;
            }
            return position >= levels[stage - 1] ? new StageOutcome<Walk>(new Walk(position, time)) : new StageOutcome<Walk>(null);
        }
    }

    /// <summary>The exact chance in ppb of reaching <paramref name="top"/> before 0 from <paramref name="start"/> with up chance 2/5 (floored).</summary>
    private static long Exact(int start, int top) =>
        (long)((BigInteger.Pow(3, start) - BigInteger.Pow(2, start)) * BigInteger.Pow(2, top) * 1_000_000_000 / (BigInteger.Pow(2, start) * (BigInteger.Pow(3, top) - BigInteger.Pow(2, top))));

    private static RuinModel Ruin() => new(1, [3, 6, 9, 12], 400);

    [Fact]
    public void TheExactValueIsWorkedByHand()
    {
        Assert.Equal(400_000_000, Exact(1, 2));                  // up at once (2/5), else lost: 0.4
        Assert.Equal(3_883_605, Exact(1, 12));                   // 4096 / 1,054,690 = 0.003883605..., worked on paper and by exact fractions
    }

    [Fact]
    public void TheSplittingEstimateAgreesWithTheExactValueWithinItsStatedBand()
    {
        var truth = Exact(1, 12);
        var covered = 0;
        BigInteger meanSum = 0;
        const int roots = 20;
        for (var root = 1; root <= roots; root++)
        {
            var summary = SplitSummary.Of(FixedEffortSplitting.Run(Ruin(), root, 200, 21));
            if (summary.LowPpb <= truth && truth <= summary.HighPpb) covered++;
            meanSum += summary.MeanPpb;
        }
        // The band is mean +- 2 se (about 95%): at least 16 of 20 independent estimates should cover the truth (17 did when this was written: the band is a normal approximation of a skewed estimate, so it runs a little under 95%), and the mean of all of them (unbiased) should be within 5% of it.
        Assert.True(covered >= 16, "only " + covered + " of " + roots + " bands covered the exact value " + truth);
        var grand = (long)(meanSum / roots);
        Assert.True(Math.Abs(grand - truth) * 20 <= truth, "the mean of the estimates " + grand + " is more than 5% from the exact " + truth);
    }

    [Fact]
    public void ThePlainRunReachesTheSameValueAndSplittingAtTheSameCostIsTighter()
    {
        var truth = Exact(1, 12);
        var split = SplitSummary.Of(FixedEffortSplitting.Run(Ruin(), 7, 200, 21));
        var model = new RuinModel(1, [12], 400);
        long hits = 0;
        var runs = split.Runs;
        for (var i = 0; i < runs; i++)
            if (model.First(FixedEffortSplitting.DerivedSeed(7, 0, 1, i)).Reached) hits++;
        var (plain, band) = PlainEstimate.Of(hits, runs);
        Assert.True(Math.Abs(plain - truth) <= band, "the plain estimate " + plain + " is more than its band " + band + " from " + truth);
        Assert.True(2 * split.SePpb < band, "splitting se " + split.SePpb + " should be tighter than the plain band " + band + " for the same " + runs + " runs");
    }

    [Fact]
    public void TheReportIsAPureFunctionOfItsInputs()
    {
        var first = FixedEffortSplitting.Render("heading", "walk:12", FixedEffortSplitting.Run(Ruin(), 3, 50, 6), true);
        var second = FixedEffortSplitting.Render("heading", "walk:12", FixedEffortSplitting.Run(Ruin(), 3, 50, 6), true);
        Assert.Equal(first, second);
        Assert.NotEqual(first, FixedEffortSplitting.Render("heading", "walk:12", FixedEffortSplitting.Run(Ruin(), 4, 50, 6), true));
        Assert.StartsWith("heading\nreplication 1 hits ", first, StringComparison.Ordinal);
        Assert.Contains("\nrare-estimate target walk:12 stages 4 effort 50 replications 6 runs ", first, StringComparison.Ordinal);
        Assert.EndsWith("\n", first, StringComparison.Ordinal);
        Assert.Equal(1, first.Split('\n').Count(l => l.StartsWith("rare-digest ", StringComparison.Ordinal)));
        Assert.DoesNotContain("replication 1", FixedEffortSplitting.Render("heading", "walk:12", FixedEffortSplitting.Run(Ruin(), 3, 50, 6), false), StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedSeedsAreDistinctPerRunStageAndReplicationAndFitEveryEngine()
    {
        var seen = new HashSet<long>();
        for (var replication = 0; replication < 5; replication++)
            for (var stage = 1; stage <= 4; stage++)
                for (var run = 0; run < 500; run++)
                {
                    var seed = FixedEffortSplitting.DerivedSeed(9, replication, stage, run);
                    Assert.InRange(seed, 0, 1L << 62);
                    Assert.True(seen.Add(seed));
                }
        Assert.NotEqual(FixedEffortSplitting.DerivedSeed(9, 0, 1, 0), FixedEffortSplitting.DerivedSeed(10, 0, 1, 0));
    }

    [Fact]
    public void AStageWithoutAHitEndsTheReplicationAtZeroAndSaysSo()
    {
        var report = FixedEffortSplitting.Run(new RuinModel(1, [3, 1_000_000], 400), 5, 20, 6);
        Assert.All(report.Replications, r =>
        {
            Assert.True(r.Extinct);
            Assert.Equal(0, r.Ppb);
            Assert.Equal(2, r.Hits.Count);
            Assert.Equal(0, r.Hits[1]);
        });
        var summary = SplitSummary.Of(report);
        Assert.Equal(6, summary.Extinct);
        Assert.Equal(0, summary.MeanPpb);
        Assert.Equal(0, summary.HighPpb);
        Assert.Equal(6 * 40, summary.Runs);                              // both stages were run in every replication: stage 2 started from the stage-1 hits and found none
    }

    [Fact]
    public void AStageThatEndsTheReplicationStopsTheStagesAfterIt()
    {
        // Levels 3, 1,000,000, 5: stage 2 finds nothing, so stage 3 must not run at all (it would start from stale checkpoints and its hits would be counted).
        var report = FixedEffortSplitting.Run(new RuinModel(1, [3, 1_000_000, 5], 400), 5, 20, 6);
        Assert.All(report.Replications, r =>
        {
            Assert.Equal(40, r.Runs);
            Assert.Equal(0, r.Hits[2]);
            Assert.Equal(0, r.Ppb);
        });
    }

    private sealed class CountingModel : ISplittingModel<CountingModel.Point>
    {
        public sealed record Point(int Id);

        public readonly int[] Used = new int[8];

        public int Stages => 2;

        public StageOutcome<Point> First(long seed) => new(new Point((int)(seed % 8)));

        public StageOutcome<Point> Next(Point checkpoint, int stage, long seed)
        {
            Used[checkpoint.Id]++;
            return new StageOutcome<Point>(null);
        }
    }

    [Fact]
    public void TheRestartsAreSpreadAcrossTheCheckpointsAboutEqually()
    {
        var model = new CountingModel();
        FixedEffortSplitting.Run(model, 1, 800, 1);
        // Stage 1 hands back one checkpoint per run (800, ids by seed); stage 2 restarts 800 times from them, each checkpoint used as often as its place in the list allows.
        var points = model.Used.Sum();
        Assert.Equal(800, points);
        Assert.True(model.Used.Max() - model.Used.Min() <= 800 / 8, "the restarts should not pile on a few checkpoints: " + string.Join(",", model.Used));
        Assert.True(model.Used.Count(u => u > 0) >= 7, "nearly every distinct checkpoint should be used: " + string.Join(",", model.Used));
    }

    [Fact]
    public void ARepeatedHitIsUsedAboutEquallyAndAStageThatSkipsALevelCostsNoRun()
    {
        // A walk that always jumps to the top reaches every level at once: the later stages cost nothing but still count their hits, and the estimate is certainty.
        var report = FixedEffortSplitting.Run(new RuinModel(12, [3, 6, 9, 12], 400), 1, 10, 6);
        Assert.All(report.Replications, r =>
        {
            Assert.Equal(1_000_000_000, r.Ppb);
            Assert.All(r.Hits, h => Assert.Equal(10, h));
        });
    }

    [Fact]
    public void BadEffortReplicationsAndAnEmptyLadderAreRefusedWithTheirCodes()
    {
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, 0, 6)).Code);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, BalanceCodes.MaxEffort + 1, 6)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, 10, 0)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, 10, BalanceCodes.MaxReplications + 1)).Code);
        Assert.Equal(BalanceCodes.RareTarget, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(new RuinModel(1, [], 400), 1, 10, 6)).Code);
    }

    // ---- a number of runs for each stage ------------------------------------------------------------------------------------------

    /// <summary>A model whose hits are counted, not drawn: stage 1 always reaches; later stages reach on every fourth call; each run costs three units of work.</summary>
    private sealed class EveryFourth : ISplittingModel<EveryFourth.Point>
    {
        public sealed record Point;

        private int _calls;

        public int Stages => 3;

        public StageOutcome<Point> First(long seed) => new(new Point(), 3);

        public StageOutcome<Point> Next(Point checkpoint, int stage, long seed) => new(_calls++ % 4 == 0 ? new Point() : null, 3);
    }

    [Fact]
    public void TheProductOfStageFractionsUsesEachStagesOwnEffortWorkedByHand()
    {
        // Efforts 4, 8, 20: stage 1 hits 4 of 4; stage 2 (calls 0..7) hits calls 0 and 4, so 2 of 8; stage 3 (calls 8..27) hits 8, 12, 16, 20 and 24, so 5 of 20. 4/4 x 2/8 x 5/20 = 1/16 = 62,500,000 ppb.
        var report = FixedEffortSplitting.Run(new EveryFourth(), 1, [4, 8, 20], 1);
        var only = Assert.Single(report.Replications);
        Assert.Equal([4, 2, 5], only.Hits);
        Assert.Equal(62_500_000, only.Ppb);
        Assert.Equal(32, only.Runs);
        Assert.Equal(96, only.Work);                                  // 32 runs at three units each
        var text = FixedEffortSplitting.Render("heading", "t", report, true);
        Assert.Contains("replication 1 hits 4/4,2/8,5/20 runs 32 work-steps 96 estimate-ppb 62500000\n", text, StringComparison.Ordinal);
        Assert.Contains(" stages 3 efforts 4,8,20 replications 1 runs 32 ", text, StringComparison.Ordinal);
        Assert.Contains(" extinct 0 work-steps 96\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EqualEffortsPerStageGiveTheUniformReportByteForByteAndAModelWithoutWorkPrintsNone()
    {
        var uniform = FixedEffortSplitting.Render("h", "walk:12", FixedEffortSplitting.Run(Ruin(), 3, 50, 6), true);
        Assert.Equal(uniform, FixedEffortSplitting.Render("h", "walk:12", FixedEffortSplitting.Run(Ruin(), 3, [50, 50, 50, 50], 6), true));
        Assert.DoesNotContain("work-steps", uniform, StringComparison.Ordinal);
    }

    [Fact]
    public void UnequalEffortsStillEstimateTheExactValueWithinTheirBandAndAreUnbiased()
    {
        var truth = Exact(1, 12);
        var covered = 0;
        BigInteger meanSum = 0;
        const int roots = 20;
        for (var root = 1; root <= roots; root++)
        {
            var report = FixedEffortSplitting.Run(Ruin(), root, [400, 200, 100, 100], 21);
            Assert.All(report.Replications, r => Assert.Equal(800, r.Runs));
            var summary = SplitSummary.Of(report);
            if (summary.LowPpb <= truth && truth <= summary.HighPpb) covered++;
            meanSum += summary.MeanPpb;
        }
        Assert.True(covered >= 16, "only " + covered + " of " + roots + " bands covered the exact value " + truth);
        var grand = (long)(meanSum / roots);
        Assert.True(Math.Abs(grand - truth) * 20 <= truth, "the mean of the estimates " + grand + " is more than 5% from the exact " + truth);
    }

    [Fact]
    public void EffortsThatAreTheWrongNumberOrOutOfRangeAreRefusedWithTheirCodes()
    {
        Assert.Equal(BalanceCodes.EffortsCount, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, [10, 10, 10], 6)).Code);
        Assert.Equal(BalanceCodes.EffortsCount, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, [10, 10, 10, 10, 10], 6)).Code);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, [10, 0, 10, 10], 6)).Code);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, [10, 10, BalanceCodes.MaxEffort + 1, 10], 6)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(Ruin(), 1, [10, 10, 10, 10], 0)).Code);
        Assert.Equal(BalanceCodes.RareTarget, Assert.Throws<EngineRefusedException>(() => FixedEffortSplitting.Run(new RuinModel(1, [], 400), 1, [10], 6)).Code);
    }
}
