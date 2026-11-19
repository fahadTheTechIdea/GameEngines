using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Self-exciting hazards (a Hawkes process in its recursive form). The oracles are independent of the runtime: the recursion written out again in the test with the same counter-based roll, and the textbook figures for a
/// branching process (a cluster has a mean of 1 / (1 - n) onsets; counts in long windows have an index of dispersion of 1 / (1 - n)^2). One thing the textbook does not know: in this engine an onset is a hazard that
/// lives, so the step after one is always a resting step; the tests count it (the branching ratio that really acts is the stated one less the first, strongest, step).
/// </summary>
public sealed class ExcitationTests
{
    private static int Started(IEnumerable<WorldChange> changes) => changes.Count(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started);

    // ---- the recursion, step for step ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1L, 10_000, 80_000, 200, 300_000, false, false)]
    [InlineData(2L, 10_000, 80_000, 200, 300_000, false, false)]
    [InlineData(3L, 20_000, 200_000, 200, 250_000, true, false)]
    [InlineData(4L, 650_000, 100_000, 100, 400_000, true, true)]
    public void TheExcitationAndEveryOnsetFollowTheRecursionOfTheDocumentStepForStep(long seed, int rate, int jump, int decay, int cap, bool expectCap, bool expectCertainty)
    {
        var runtime = Lab.HawkesWorld(seed, rate, jump, decay, cap);
        long expected = 0;
        var lastOnset = long.MinValue;
        var onsets = 0;
        var sawCap = false;
        var sawCertainty = false;
        for (long step = 0; step < 4000; step++)
        {
            var started = Started(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items);
            // The oracle: the excitation fades first, then the chance is the rate plus what is left (at most certainty), rolled with the hazard's own counter-based stream at (step x 4096 + region x 4); the step after an
            // onset is a resting step (the hazard that started is removed at the start of it and the cooldown counts from there).
            expected = expected * (1000 - decay) / 1000;
            var chance = Math.Min(1_000_000L, rate + expected);
            sawCertainty |= rate + expected >= 1_000_000;
            var roll = DeterministicRoll.Below((ulong)seed, "hz:h", step * 4096, 1_000_000);
            var resting = lastOnset == step - 1;
            var wanted = !resting && roll < chance;
            Assert.Equal(wanted, started == 1);
            if (started == 1)
            {
                onsets++;
                lastOnset = step;
                expected = Math.Min(cap, expected + jump);
                sawCap |= expected == cap;
            }
            Assert.Equal(expected, runtime.ExcitationAt(0, 0));
        }
        Assert.True(onsets > 30, "only " + onsets + " onsets: the case shows too little");
        Assert.Equal(expectCap, sawCap);
        Assert.Equal(expectCertainty, sawCertainty);
    }

    [Fact]
    public void TheChanceOfAnOnsetIsTheOwnRatePlusTheExcitationAtMostCertainty()
    {
        var runtime = Lab.HawkesWorld(5, 600_000, 100_000, 500, 600_000);
        var hazard = runtime.Set.Hazards[0];
        Assert.Equal(600_000, runtime.OnsetBase(hazard, 0, 0));
        Assert.Equal(600_000, runtime.OnsetChance(hazard, 0, 0));
        runtime.ExcitationArray[0] = 100_000;
        Assert.Equal(700_000, runtime.OnsetChance(hazard, 0, 0));
        Assert.Equal(600_000, runtime.OnsetBase(hazard, 0, 0));
        runtime.ExcitationArray[0] = 400_000;
        Assert.Equal(1_000_000, runtime.OnsetChance(hazard, 0, 0));
        runtime.ExcitationArray[0] = 600_000;
        Assert.Equal(1_000_000, runtime.OnsetChance(hazard, 0, 0));
    }

    [Fact]
    public void AnExcitationOfZeroChangesNothingAboutTheChance()
    {
        var runtime = Lab.HawkesWorld(6, 4000, 0, 1000, 0);
        Assert.Equal(4000, runtime.OnsetChance(runtime.Set.Hazards[0], 0, 0));
        Fixture.Live(runtime, 500);
        Assert.All(runtime.Excitation, e => Assert.Equal(0, e));
    }

    // ---- who excites ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AScriptedOnsetExcitesItsOwnRegionAndAnOnsetThatOnlySpreadDoesNot()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Hazard(root)["excite"] = new JsonObject { ["jumpPpm"] = 100_000, ["decayPermille"] = 500, ["capPpm"] = 1_000_000 };
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 1, ["falloff"] = 500 };
        });
        Fixture.Live(runtime, 0);
        runtime.BeginHazard("a", "h", "low");
        Assert.Equal(2, runtime.ActiveHazards.Count);                  // it reached b as well
        Assert.Equal(100_000, runtime.ExcitationAt(0, 0));
        Assert.Equal(0, runtime.ExcitationAt(0, 1));
    }

    [Fact]
    public void ACardThatStartsAHazardExcitesItToo()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Hazard(root)["excite"] = new JsonObject { ["jumpPpm"] = 70_000, ["decayPermille"] = 500, ["capPpm"] = 1_000_000 };
            Tiny.Card(root, "c-bad")["effects"] = new JsonArray(new JsonObject { ["hazard"] = "h", ["severity"] = "low" });
        });
        Fixture.Live(runtime, 0);
        runtime.PlayCard("c-bad", "a");
        Assert.NotNull(runtime.ActiveAt(0, 0));
        Assert.Equal(70_000, runtime.ExcitationAt(0, 0));
    }

    [Fact]
    public void TheExcitationFadesEveryStepEvenWhileTheHazardIsGoingAndWhileItRests()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Hazard(root)["excite"] = new JsonObject { ["jumpPpm"] = 100_000, ["decayPermille"] = 500, ["capPpm"] = 1_000_000 });
        Fixture.Live(runtime, 0);
        runtime.BeginHazard("a", "h", "low");
        long expected = 100_000;
        var wasGoing = false;
        var wasResting = false;
        for (var step = 1; step <= 20; step++)
        {
            Fixture.Live(runtime, step);
            expected = expected * 500 / 1000;
            Assert.Equal(expected, runtime.ExcitationAt(0, 0));
            wasGoing |= runtime.ActiveAt(0, 0) is not null;
            wasResting |= runtime.ActiveAt(0, 0) is null && runtime.HazardReadyAt[0] > step;
        }
        Assert.True(wasGoing && wasResting);
        Assert.Equal(0, runtime.ExcitationAt(0, 0));                  // 100,000 halved seventeen times, rounding down, is gone
    }

    // ---- the stationarity bound ---------------------------------------------------------------------------------------------------------

    private static WorldSet Load(int jump, int decay, int cap) => WorldLoader.Load(Lab.Hawkes(1, 0, jump, decay, cap).ToJsonString());

    [Fact]
    public void TheBranchingRatioMustBeBelowOneAndTheEdgeIsExact()
    {
        var below = Load(999_999, 500, 1_000_000).Hazards[0].Excite;
        Assert.Equal(999_999, below.BranchingPpm);
        Assert.True(below.Stationary);
        Assert.Equal(WorldCodes.Nonstationary, Fixture.Refusal(() => Load(1_000_000, 500, 1_000_000)));
        var slow = Load(1001, 1, 1_000_000).Hazards[0].Excite;
        Assert.Equal(999_999, slow.BranchingPpm);
        Assert.Equal(WorldCodes.Nonstationary, Fixture.Refusal(() => Load(1002, 1, 1_000_000)));
        var instant = Load(1_000_000, 1000, 1_000_000).Hazards[0].Excite;
        Assert.Equal(0, instant.BranchingPpm);                         // gone by the next step: it can never cause another
        Assert.True(instant.Stationary);
        Assert.Equal(0, Load(0, 1000, 0).Hazards[0].Excite.BranchingPpm);
        Assert.False(Load(0, 1000, 0).Hazards[0].Excite.On);
        Assert.True(Load(1, 500, 1).Hazards[0].Excite.On);                // the smallest jump there is still excites
        Assert.Equal(1, Load(1, 500, 1).Hazards[0].Excite.BranchingPpm);
    }

    [Fact]
    public void TheBranchingRatioIsJumpTimesTheSurvivingShareOverTheDecay()
    {
        // jump 20,000 ppm, decay 250 per mille: 20,000 x 750 / 250 = 60,000 ppm of an onset more for each onset.
        Assert.Equal(60_000, Load(20_000, 250, 1_000_000).Hazards[0].Excite.BranchingPpm);
        Assert.Equal(1, Load(1_000, 999, 1_000_000).Hazards[0].Excite.BranchingPpm);          // 1,000 x 1 / 999 = 1.001, rounded down
        Assert.Equal(999_000, Load(1_000, 1, 1_000_000).Hazards[0].Excite.BranchingPpm);
    }

    // ---- statistics: the cluster an onset starts and the clustering of counts -----------------------------------------------------------

    [Fact]
    public void TheMeanClusterAnOnsetStartsMatchesTheBranchingTheoryOnceTheRestingStepIsCounted()
    {
        // No rate of its own: each trial begins with one scripted onset and runs until nothing is left of its excitation (50 ppm of an onset, which could still add 0.0005 onsets in all).
        const int jump = 30_000, decay = 100, trials = 12_000;
        long total = 0;
        var maxSteps = 0;
        for (var trial = 0; trial < trials; trial++)
        {
            var runtime = Lab.HawkesWorld(100_000 + trial, 0, jump, decay, 1_000_000);
            Fixture.Live(runtime, 0);
            runtime.BeginHazard("a", "h", "low");
            var size = 1;
            long step = 0;
            do
            {
                step++;
                size += Started(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items);
                Assert.True(step < 2000, "a cluster never ended: the excitation does not fade");
            }
            while (runtime.ExcitationAt(0, 0) > 50 || runtime.ActiveHazards.Count > 0);
            maxSteps = Math.Max(maxSteps, (int)step);
            total += size;
        }
        var mean = (double)total / trials;
        var stated = jump / 1e6 * (1000 - decay) / decay;                                   // 0.27: jump x (1000 - decay) / decay
        var acting = jump / 1e6 * Math.Pow(1 - decay / 1000.0, 2) / (decay / 1000.0);       // 0.243: the first step is a resting step, so only the later ones can cause an onset
        var theory = 1 / (1 - acting);
        Assert.InRange(mean, theory * 0.98, theory * 1.02);
        Assert.True(mean < 1 / (1 - stated) - 0.02, "the cluster is no smaller than the textbook's " + 1 / (1 - stated) + ": " + mean);
        var estimated = 1 - 1 / mean;                                                       // the empirical branching ratio
        Assert.InRange(estimated, acting - 0.015, acting + 0.015);
        Assert.True(estimated < stated);
        Assert.True(maxSteps < 400, "a cluster went on for " + maxSteps + " steps");
    }

    private static (double Mean, double Fano) CountsInWindows(long seed, int rate, int jump, int decay, int cap, int windows, int length)
    {
        var runtime = Lab.HawkesWorld(seed, rate, jump, decay, cap);
        var counts = new List<int>();
        long step = 0;
        for (var w = 0; w < windows; w++)
        {
            var count = 0;
            for (var i = 0; i < length; i++) count += Started(runtime.Advance(step++, runtime.CycleSeasonAt, Fixture.Host()).Items);
            counts.Add(count);
        }
        var mean = counts.Average();
        var variance = counts.Sum(c => (c - mean) * (c - mean)) / (counts.Count - 1);
        return (mean, variance / mean);
    }

    [Fact]
    public void CountsInLongWindowsAreClusteredOnlyWhenTheHazardExcitesItself()
    {
        var plain = CountsInWindows(21, 5000, 0, 1000, 0, 2000, 200);
        var excited = CountsInWindows(21, 5000, 30_000, 100, 1_000_000, 2000, 200);
        Assert.InRange(plain.Fano, 0.9, 1.1);                                              // Bernoulli counts: variance / mean = 1 - rate
        var acting = 0.03 * 0.81 / 0.1;
        var theory = 1 / ((1 - acting) * (1 - acting));                                    // the index of dispersion of a Hawkes count in a long window
        Assert.InRange(excited.Fano, theory * 0.85, theory * 1.2);
        Assert.True(excited.Fano > plain.Fano * 1.5);
        Assert.InRange(excited.Mean / plain.Mean, 1.2, 1.45);                              // the same immigrants, each bringing a cluster of about 1.32
    }

    // ---- save, explain ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheExcitationSurvivesASaveAndTheRunContinuesIdenticallyFromIt()
    {
        var runtime = Lab.HawkesWorld(31, 20_000, 100_000, 200, 400_000);
        Fixture.Live(runtime, 300);
        while (runtime.Excitation.All(e => e == 0)) { Fixture.Live(runtime, runtime.Step + 1); Assert.True(runtime.Step < 5000, "no onset in 5,000 steps"); }        // some excitation to save
        Assert.Contains(runtime.Excitation, e => e > 0);
        var bytes = WorldSave.Write(runtime);
        var reopened = WorldSave.Open(runtime.Set, bytes);
        Assert.Equal(runtime.Excitation, reopened.Excitation);
        var straight = Fixture.Live(runtime, 900);
        var resumed = Fixture.Live(reopened, 900);
        Assert.Equal(straight, resumed);
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(runtime.Excitation, reopened.Excitation);
    }

    [Fact]
    public void AnExcitationOutsideItsRangeIsRefusedAsAStateBeforeTheDigestIsRead()
    {
        var runtime = Lab.HawkesWorld(32, 20_000, 100_000, 200, 400_000);
        Fixture.Live(runtime, 100);
        while (runtime.Excitation.All(e => e == 0)) { Fixture.Live(runtime, runtime.Step + 1); Assert.True(runtime.Step < 5000, "no onset in 5,000 steps"); }
        var set = runtime.Set;
        var bytes = WorldSave.Write(runtime);
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation[0] = 400_001))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation[0] = -1))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Stale(bytes, i => i.Excitation[0] = 400_001))));     // the range is checked before the digest
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation.Add(0)))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation.Clear()))));
        var other = (runtime.Excitation[0] + 1) % 400_000;
        Assert.Equal(WorldCodes.SaveDigest, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Stale(bytes, i => i.Excitation[0] = other))));        // in range, so only the digest can tell
        Assert.Equal(other, WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation[0] = other)).Excitation[0]);
    }

    [Fact]
    public void AHazardThatExcitesNothingCannotHoldAnExcitation()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Excitation[0] = 1))));
    }

    [Fact]
    public void TheExplanationShowsTheOwnRateAndTheExcitationSideBySide()
    {
        var runtime = Lab.HawkesWorld(41, 3000, 80_000, 200, 300_000);
        long onsetAt = -1;
        for (long step = 0; step < 5000 && onsetAt < 0; step++)
            if (Started(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items) == 1) onsetAt = step;
        Assert.True(onsetAt >= 0, "no onset in 5,000 steps");
        Fixture.Live(runtime, onsetAt + 1);                                                // the hazard has ended; the next step is still resting
        var excitation = runtime.ExcitationAt(0, 0);
        Assert.True(excitation > 0);
        var text = WorldExplain.Hazard(runtime, "h", "a", Fixture.Host());
        var chance = Math.Min(1_000_000L, 3000 + excitation);
        Assert.Contains("chance a step in " + runtime.Set.Seasons[runtime.Season].Id + ": " + chance + " ppm (rate 3000, season 1000 per mille)", text, StringComparison.Ordinal);
        Assert.Contains("= own rate 3000 + excitation " + excitation + " ppm (each onset here adds 80000, the excitation falls by 200 per mille a step and never passes 300000)", text, StringComparison.Ordinal);
        Assert.Contains("branching ratio 320000 ppm: one onset causes that many more on average, so a cluster is about 1.470 onsets in all", text, StringComparison.Ordinal);
        var plain = WorldExplain.Hazard(Lab.HawkesWorld(41, 3000, 0, 1000, 0), "h", "a", Fixture.Host());
        Assert.DoesNotContain("excitation", plain, StringComparison.Ordinal);
    }
}
