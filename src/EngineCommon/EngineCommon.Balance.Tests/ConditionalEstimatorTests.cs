using System.Numerics;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>
/// The conditional estimator on a case whose answer is exact. A chain of opportunities: at each one an onset arises with a chance that depends on what the previous opportunity did (it is higher right after an onset that did not reach the event,
/// the way a cluster excites itself), and an onset reaches the event with chance q. The chance of at least one event within the horizon is computed exactly by dynamic programming over the chain's two states with whole-number fractions, so it is
/// independent of the code under test. The chain's randomness is the shared counter-based generator, as the engines' is.
/// </summary>
public sealed class ConditionalEstimatorTests
{
    private const long Million = 1_000_000;

    private sealed class Chain(int horizon, long quiet, long excited, long reach) : IConditionalModel
    {
        public ConditionalOutcome Run(long seed)
        {
            BigInteger weight = 0;
            long work = 0;
            var state = 0;
            for (var t = 0; t < horizon; t++)
            {
                var chance = state == 0 ? quiet : excited;
                work++;
                weight += chance * reach;
                var onset = DeterministicRoll.Below((ulong)seed, "chain", 2L * t, Million) < chance;
                var reaches = onset && DeterministicRoll.Below((ulong)seed, "chain", 2L * t + 1, Million) < reach;
                if (reaches) break;
                state = onset ? 1 : 0;
            }
            return new ConditionalOutcome(weight, true, work);
        }
    }

    /// <summary>The exact chance in ppb (floored) of at least one event, by dynamic programming over (previous opportunity quiet, previous opportunity an onset that missed the event), every step with the denominator 10^12.</summary>
    private static long Exact(int horizon, long quiet, long excited, long reach)
    {
        BigInteger[] alive = [1, 0];
        BigInteger scale = 1;
        var step = BigInteger.Pow(10, 12);
        for (var t = 0; t < horizon; t++)
        {
            BigInteger[] next = [0, 0];
            long[] chances = [quiet, excited];
            for (var s = 0; s < 2; s++)
            {
                next[0] += alive[s] * (Million - chances[s]) * Million;
                next[1] += alive[s] * chances[s] * (Million - reach);
            }
            alive = next;
            scale *= step;
        }
        var survive = alive[0] + alive[1];
        return (long)((scale - survive) * 1_000_000_000 / scale);
    }

    [Fact]
    public void OneOpportunityIsWorkedByHand()
    {
        // A single opportunity: an onset with chance 20,000 ppm that reaches the event with chance 100,000 ppm is 2,000 ppm of 10^6 x 10^6 / 10^12 = 0.002, 2,000,000 ppb; every run weighs exactly that.
        Assert.Equal(2_000_000, Exact(1, 20_000, 300_000, 100_000));
        var report = ConditionalEstimator.Run(new Chain(1, 20_000, 300_000, 100_000), 5, 7, 4);
        Assert.All(report.Replications, r => Assert.Equal(2_000_000, r.Ppb));
        Assert.Equal(1, report.Stages);
    }

    [Fact]
    public void TwoOpportunitiesAreWorkedByHandWithTheExcitationAfterAnOnset()
    {
        // P(event) = c0 q + (1 - c0 q) x [chance of the second]: the second opportunity has chance c1 if the first was an onset that missed (c0 (1 - q)) and c0 otherwise, so
        // P = c0 q + (c0 (1-q)) c1 q + (1 - c0) c0 q, all over 10^12 steps: 0.002 + 0.018 x 0.3 x 0.1 + 0.98 x 0.002 = 0.002 + 0.00054 + 0.00196 = 0.0045.
        Assert.Equal(4_500_000, Exact(2, 20_000, 300_000, 100_000));
    }

    [Fact]
    public void TheEstimatesAgreeWithTheExactValueAndAreTighterThanPlainMonteCarlo()
    {
        const int horizon = 30;
        var truth = Exact(horizon, 20_000, 300_000, 100_000);
        BigInteger sum = 0;
        var covered = 0;
        BigInteger conditionalSe = 0;
        const int roots = 20;
        for (var root = 1; root <= roots; root++)
        {
            var summary = SplitSummary.Of(ConditionalEstimator.Run(new Chain(horizon, 20_000, 300_000, 100_000), root, 200, 21));
            if (summary.LowPpb <= truth && truth <= summary.HighPpb) covered++;
            sum += summary.MeanPpb;
            conditionalSe += summary.SePpb;
        }
        var grand = (long)(sum / roots);
        Assert.True(Math.Abs(grand - truth) * 100 <= truth, "the mean of 20 estimates " + grand + " is more than 1% from the exact " + truth);
        Assert.True(covered >= 17, "only " + covered + " of " + roots + " bands covered " + truth);
        // Plain Monte Carlo with the same number of runs (200 a replication, 21 replications): the standard error of the mean of the 0/1 outcomes, in ppb, is sqrt(p (1 - p) / 4200).
        var p = (double)truth / 1e9;
        var plainSe = Math.Sqrt(p * (1 - p) / 4200) * 1e9;
        Assert.True((double)conditionalSe / roots * 2 < plainSe, "the conditional se " + (double)conditionalSe / roots + " is not under half the plain " + plainSe);
    }

    [Fact]
    public void AReportRepeatsByteForByteHasTheSplittingShapeAndAnEstimatorWord()
    {
        var model = new Chain(30, 20_000, 300_000, 100_000);
        var first = ConditionalEstimator.Render("heading", "a:b", ConditionalEstimator.Run(model, 9, 20, 6), true);
        Assert.Equal(first, ConditionalEstimator.Render("heading", "a:b", ConditionalEstimator.Run(model, 9, 20, 6), true));
        var lines = first.Split('\n');
        Assert.Equal("heading", lines[0]);
        Assert.Equal(6, lines.Count(l => l.StartsWith("replication ", StringComparison.Ordinal)));
        var summary = lines.Single(l => l.StartsWith("rare-estimate ", StringComparison.Ordinal));
        Assert.StartsWith("rare-estimate target a:b estimator conditional stages 1 effort 20 replications 6 runs 120 mean-ppb ", summary, StringComparison.Ordinal);
        Assert.Contains(" band-ppb ", summary, StringComparison.Ordinal);
        Assert.Contains(" median95-ppb ", summary, StringComparison.Ordinal);
        Assert.Contains(" extinct 0 work-steps ", summary, StringComparison.Ordinal);
        Assert.StartsWith("rare-digest ", lines.Single(l => l.StartsWith("rare-digest ", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void AReplicationWithNothingThatCouldHappenIsExtinctAndAFlatZero()
    {
        var report = ConditionalEstimator.Run(new Never(), 1, 3, 2);
        Assert.All(report.Replications, r => Assert.Equal(0, r.Ppb));
        Assert.Equal(2, SplitSummary.Of(report).Extinct);
        Assert.Contains("informative 0/3", ConditionalEstimator.Render("h", "t", report, true), StringComparison.Ordinal);
    }

    private sealed class Never : IConditionalModel
    {
        public ConditionalOutcome Run(long seed) => new(BigInteger.Zero, false, 1);
    }

    [Fact]
    public void TheSeedsAreTheFirstStageSeedsOfTheSplittingProcedure()
    {
        for (var replication = 0; replication < 3; replication++)
            for (var run = 0; run < 4; run++)
                Assert.Equal(FixedEffortSplitting.DerivedSeed(77, replication, 1, run), ConditionalEstimator.SeedOf(77, replication, run));
        Assert.NotEqual(ConditionalEstimator.SeedOf(77, 0, 0), ConditionalEstimator.SeedOf(77, 1, 0));
        Assert.NotEqual(ConditionalEstimator.SeedOf(77, 0, 0), ConditionalEstimator.SeedOf(77, 0, 1));
    }

    [Fact]
    public void RunsAndReplicationsOutsideTheirRangesAreRefusedWithTheirCodes()
    {
        var model = new Never();
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => ConditionalEstimator.Run(model, 1, 0, 3)).Code);
        Assert.Equal(BalanceCodes.EffortRange, Assert.Throws<EngineRefusedException>(() => ConditionalEstimator.Run(model, 1, BalanceCodes.MaxEffort + 1, 3)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => ConditionalEstimator.Run(model, 1, 3, 0)).Code);
        Assert.Equal(BalanceCodes.ReplicationsRange, Assert.Throws<EngineRefusedException>(() => ConditionalEstimator.Run(model, 1, 3, BalanceCodes.MaxReplications + 1)).Code);
        Assert.Equal(BalanceCodes.MaxEffort, ConditionalEstimator.Run(model, 1, BalanceCodes.MaxEffort, 1).Replications[0].Runs);          // the limits themselves are allowed
        Assert.Equal(BalanceCodes.MaxReplications, ConditionalEstimator.Run(model, 1, 1, BalanceCodes.MaxReplications).Replications.Count);
    }

    [Fact]
    public void ARoundedDownReplicationKeepsTheExactMeanOfItsWeights()
    {
        // Three runs of weights 1,000 + 1,000 + 2,500 (units of 10^-12) are 4,500 / 3 = 1,500 units = 1.5 ppb, floored to 1.
        var report = ConditionalEstimator.Run(new Fixed([1000, 1000, 2500]), 1, 3, 1);
        Assert.Equal(1, report.Replications[0].Ppb);
        Assert.Equal(3, report.Replications[0].Hits[0]);       // all three were informative: Hits holds the count of informative runs
        Assert.Equal(6, report.Replications[0].Work);
        Assert.Equal(1000, ConditionalEstimator.WeightPerPpb);
    }

    private sealed class Fixed(long[] weights) : IConditionalModel
    {
        private int _next;

        public ConditionalOutcome Run(long seed) => new(weights[_next++ % weights.Length], true, 2);
    }

    [Fact]
    public void TheConditionalLineParsesAndRefusesWhatItCannotMeanWithTheirCodes()
    {
        Assert.True(RareOptions.Parse(["365", "--rare", "a:b", "--conditional"]).Conditional);
        Assert.False(RareOptions.Parse(["365", "--rare", "a:b"]).Conditional);
        var conditional = RareOptions.Parse(["365", "--rare", "a:b", "--conditional", "--effort", "40", "--replications", "9"]);
        Assert.Equal((40, 9), (conditional.Effort, conditional.Replications));
        Assert.Equal(BalanceCodes.ConditionalMix, Assert.Throws<EngineRefusedException>(() => RareOptions.Parse(["365", "--rare", "a:b", "--conditional", "--predraw"])).Code);
        Assert.Equal(BalanceCodes.ConditionalMix, Assert.Throws<EngineRefusedException>(() => RareOptions.Parse(["365", "--rare", "a:b", "--efforts", "5", "--conditional"])).Code);
        Assert.Equal(BalanceCodes.OptionTwice, Assert.Throws<EngineRefusedException>(() => RareOptions.Parse(["365", "--rare", "a:b", "--conditional", "--conditional"])).Code);
    }
}
