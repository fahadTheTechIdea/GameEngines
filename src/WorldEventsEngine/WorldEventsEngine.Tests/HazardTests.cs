using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>Hazards: a Bernoulli onset per step at the rate, the ladder of severities by their tails, the phases and their intensity, escalation, exclusion, cooldown, reach, pacing and capacity.</summary>
public sealed class HazardTests
{
    private static List<WorldChange> Hazards(IEnumerable<WorldChange> changes) => changes.Where(c => c.Kind == ChangeKind.Hazard).ToList();

    private static void Rate(JsonObject root, int ppm) => Tiny.Hazard(root)["ratePpm"] = ppm;

    [Fact]
    public void AnOnsetIsATrialPerStepAtTheRateOnlyWhereTheHazardCouldStart()
    {
        var runtime = Fixture.MiniWorld(root => { Rate(root, 100_000); Tiny.Hazard(root)["cooldown"] = 0; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        var eligible = 0;
        var started = 0;
        for (var step = 0; step < 20_000; step++)
        {
            var could = runtime.ActiveAt(0, 0) is null && runtime.HazardReadyAt[0] <= step;
            var changes = runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            if (!could) continue;
            eligible++;
            if (changes.Items.Any(c => c.Kind == ChangeKind.Hazard && c.Region == 0 && c.Sub2 == (int)HazardChange.Started)) started++;
        }
        Assert.True(eligible > 5000);
        Assert.InRange((double)started / eligible, 0.09, 0.11);
    }

    [Fact]
    public void ARateOfZeroNeverStartsAndACertainRateStartsAsSoonAsTheHazardMay()
    {
        var never = Fixture.MiniWorld();
        Assert.Empty(Hazards(Fixture.Live(never, 500)));
        var certain = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["cooldown"] = 0; });
        var changes = Hazards(Fixture.Live(certain, 40));
        Assert.Equal(0, changes.First().Step);
        Assert.Equal((int)HazardChange.Started, changes.First().Sub2);
    }

    [Fact]
    public void TheLadderTailsAreHowOftenAnOnsetIsAtLeastThatBad()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["cooldown"] = 0; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        var origin = Hazards(Fixture.Live(runtime, 12_000)).Where(c => c.Sub2 == (int)HazardChange.Started && c.Amount == 1000).ToList();
        Assert.True(origin.Count > 1500);
        var high = (double)origin.Count(c => c.Sub == 1) / origin.Count;
        Assert.InRange(high, 0.26, 0.34);           // the second rung's tail is 300,000 ppm
    }

    [Fact]
    public void AHazardRisesHoldsAndRecoversAndEndsOnTheSteps()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var begun = runtime.BeginHazard("a", "h", "low");
        Assert.Contains(begun.Items, c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started && c.Region == 0);
        var instance = runtime.ActiveAt(0, 0)!;
        var seen = new List<(long Step, HazardPhase Phase, int Intensity)>();
        var log = new List<WorldChange>();
        for (var step = 1; step <= 9; step++)
        {
            log.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items);
            if (runtime.ActiveAt(0, 0) is { } live) seen.Add((step, live.Phase, runtime.Intensity(live)));
        }
        // Begun at step 0: onset steps 0 and 1 (intensity 500, 1000 at step 1), peak 2 to 4, recovery 5 and 6, gone at 7.
        Assert.Equal([(1L, HazardPhase.Onset, 1000), (2L, HazardPhase.Peak, 1000), (3L, HazardPhase.Peak, 1000), (4L, HazardPhase.Peak, 1000), (5L, HazardPhase.Recovery, 667), (6L, HazardPhase.Recovery, 334)], seen.Select(s => (s.Step, s.Phase, s.Intensity)).ToList());
        var mine = Hazards(log).Where(c => c.Region == 0).ToList();
        Assert.Equal([((long)5, HazardChange.Receding), (7, HazardChange.Ended)], mine.Select(c => (c.Step, (HazardChange)c.Sub2)).ToList());
        Assert.Equal(0, instance.Region);
        Assert.Null(runtime.ActiveAt(0, 0));
    }

    [Fact]
    public void ARungWithNoOnsetStartsAtItsPeak()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "high");
        var instance = runtime.ActiveAt(0, 0)!;
        Assert.Equal(HazardPhase.Peak, instance.Phase);
        Assert.Equal(1000, runtime.Intensity(instance));
    }

    [Fact]
    public void AHazardAtItsPeakMayEscalateToTheNextRungAndAskTheEffectsOfIt()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Rung(root, 0)["escalatePpm"] = 1_000_000);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.BeginHazard("a", "h", "low");
        var changes = Fixture.Live(runtime, 6);
        var escalated = Hazards(changes).Where(c => c.Sub2 == (int)HazardChange.Escalated).ToList();
        Assert.Single(escalated, c => c.Region == 0);
        Assert.Equal(1, escalated[0].Sub);
        Assert.Contains(changes, c => c.Kind == ChangeKind.Effect && c.Name == "sh1" && c.Region == 0 && c.Amount == 700);
        Assert.Equal(1, runtime.ActiveAt(0, 0) is { } live ? live.Rung : 1);
    }

    [Fact]
    public void ARungsEffectsAreIssuedOnceWhenItStartsAndAShockIsScaledByThePower()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var changes = runtime.BeginHazard("a", "h", "high").Items;
        var effects = changes.Where(c => c.Kind == ChangeKind.Effect).ToList();
        Assert.Contains(effects, c => c.Name == "sig.high" && c.Region == 0 && c.Source == "hazard:h");
        Assert.Contains(effects, c => c.Name == "sh1" && c.Region == 0 && c.Amount == 700);
        Assert.Contains(effects, c => c.Name == "sh1" && c.Region == 1 && c.Amount == 350);          // spread to b at half strength: half the scale
        Assert.Equal(2, effects.Count(c => c.Name == "sh1"));
    }

    [Fact]
    public void AnOnsetReachesTheLinkedRegionsWeakerByTheFalloff()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var changes = runtime.BeginHazard("a", "h", "low").Items.Where(c => c.Kind == ChangeKind.Hazard).ToList();
        Assert.Equal([(0, 1000L), (1, 500L)], changes.Select(c => (c.Region, c.Amount)).ToList());
        Assert.Equal(250, runtime.Intensity(runtime.ActiveAt(0, 1)!));               // onset step one of two: half, at half power
    }

    [Fact]
    public void ReachStopsAtTheHopsAndAHazardIsNotSpreadToARegionNotExposedToIt()
    {
        var far = Fixture.MiniWorld(root =>
        {
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 2, ["falloff"] = 500 };
            var c = Tiny.Copy(Tiny.Region(root, "a")).AsObject();
            c["id"] = "c";
            c["key"] = "g.c";
            Tiny.Array(root, "regions").Add(c);
            Tiny.Array(root, "links").Add(new JsonObject { ["a"] = "b", ["b"] = "c", ["flowPpm"] = 1 });
        });
        far.Advance(0, far.CycleSeasonAt, Fixture.Host());
        far.BeginHazard("a", "h", "low");
        Assert.Equal(250, far.ActiveAt(0, 2)!.Power);                      // two hops: a quarter
        var shut = Fixture.MiniWorld(root =>
        {
            Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 2, ["falloff"] = 500 };
            Tiny.Region(root, "b")["exposure"] = new JsonArray();
        });
        shut.Advance(0, shut.CycleSeasonAt, Fixture.Host());
        shut.BeginHazard("a", "h", "low");
        Assert.Null(shut.ActiveAt(0, 1));
    }

    [Fact]
    public void ACooldownKeepsTheHazardFromStartingAgainAtOnce()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["cooldown"] = 10; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        var starts = Hazards(Fixture.Live(runtime, 80)).Where(c => c.Region == 0 && c.Sub2 == (int)HazardChange.Started).Select(c => c.Step).ToList();
        Assert.True(starts.Count >= 3);
        for (var i = 1; i < starts.Count; i++) Assert.True(starts[i] - starts[i - 1] >= 3 + 1 + 10, "starts " + starts[i - 1] + " and " + starts[i]);      // the shortest hazard (the second rung) lasts 3 steps, then rests ten
    }

    [Fact]
    public void ExcludedHazardsNeverGoTogetherInOneRegion()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Certain(root);
            var second = Tiny.Copy(Tiny.Hazard(root)).AsObject();
            second["id"] = "h2";
            second["key"] = "h.h2";
            Tiny.Hazard(root)["excludes"] = new JsonArray("h2");
            second["excludes"] = new JsonArray("h");
            Tiny.Array(root, "hazards").Add(second);
            foreach (var region in new[] { "a", "b" }) Tiny.Region(root, region)["exposure"]!.AsArray().Add(new JsonObject { ["hazard"] = "h2", ["permille"] = 1000 });
        });
        for (var step = 0; step < 400; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            for (var region = 0; region < 2; region++) Assert.False(runtime.ActiveAt(0, region) is not null && runtime.ActiveAt(1, region) is not null, "both hazards in region " + region + " at step " + step);
        }
        Assert.True(runtime.ActiveHazards.Count > 0 || runtime.Step > 0);
    }

    [Fact]
    public void ASeasonalShareOfZeroKeepsTheHazardOutOfThatSeason()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["seasonal"] = new JsonArray(1000, 0); Tiny.Hazard(root)["cooldown"] = 0; });
        var starts = Hazards(Fixture.Live(runtime, 400)).Where(c => c.Sub2 == (int)HazardChange.Started).ToList();
        Assert.NotEmpty(starts);
        Assert.All(starts, c => Assert.Equal("warm", Fixture.Season(c.Step)));
    }

    [Fact]
    public void AConditionDecidesWhereAndWhenTheRateApplies()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["when"] = Tiny.Cond("temp", "gt", 1300); Tiny.Hazard(root)["cooldown"] = 0; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        var temperatures = new Dictionary<long, long>();
        var starts = new List<WorldChange>();
        for (var step = 0; step < 600; step++)
        {
            starts.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items.Where(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started));
            temperatures[step * 10 + 0] = runtime.Regions[0].Temp;
            temperatures[step * 10 + 1] = runtime.Regions[1].Temp;
        }
        Assert.NotEmpty(starts);
        Assert.All(starts, c => Assert.True(temperatures[c.Step * 10 + c.Region] > 1300));
    }

    [Fact]
    public void AnExposureOfZeroIsNeverHitAndTheExposureScalesTheChance()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Region(root, "b")["exposure"] = new JsonArray(); Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        Assert.All(Hazards(Fixture.Live(runtime, 200)), c => Assert.Equal(0, c.Region));
        var half = Fixture.MiniWorld(root => { Rate(root, 400_000); Tiny.Region(root, "a")["exposure"]![0]!["permille"] = 500; });
        var hazard = half.Set.Hazards[0];
        Assert.Equal(200_000, half.OnsetChance(hazard, 0, 0));
        Assert.Equal(400_000, half.OnsetChance(hazard, 1, 0));
    }

    [Fact]
    public void TheDifficultyScalesTheChanceOfAnOnset()
    {
        var runtime = Fixture.MiniWorld(root => Rate(root, 100_000));
        var hazard = runtime.Set.Hazards[0];
        Assert.Equal(100_000, runtime.OnsetChance(hazard, 0, 0));
        runtime.UseDifficulty("hard");
        Assert.Equal(200_000, runtime.OnsetChance(hazard, 0, 0));
    }

    [Fact]
    public void APacedHazardStartsOnlyWhenTheDirectorWasBuildingAndIsHeldBackAndCountedOtherwise()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["paced"] = true; Tiny.Hazard(root)["cooldown"] = 0; });
        var phaseBefore = DirectorPhase.Build;
        var peaks = 0;
        var starts = 0;
        for (var step = 0; step < 600; step++)
        {
            var changes = runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            foreach (var change in changes.Items.Where(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started && c.Amount == 1000))
            {
                starts++;
                Assert.Equal(DirectorPhase.Build, phaseBefore);
            }
            if (runtime.Phase == DirectorPhase.Peak) peaks++;
            phaseBefore = runtime.Phase;
        }
        Assert.True(starts > 10);
        Assert.True(peaks > 0);
        Assert.True(runtime.Suppressed > 0);
    }

    [Fact]
    public void AnUnpacedHazardIgnoresTheDirectorAndIsNeverHeldBack()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["cooldown"] = 0; });
        Fixture.Live(runtime, 600);
        Assert.Equal(0, runtime.Suppressed);
    }

    [Fact]
    public void ANaturalHazardAddsItsRungsStressToTheDirector()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Teller(root, "t")["decay"] = 0);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(0, runtime.Stress);
        runtime.BeginHazard("a", "h", "low");
        Assert.Equal(100, runtime.Stress);
        Assert.Equal(WorldCodes.AlreadyActive, Fixture.Refusal(() => runtime.BeginHazard("b", "h", "high")));       // b already has it by reach
        Assert.Equal(100, runtime.Stress);                           // its reach added none of its own: only the origin's stress counts
    }

    [Fact]
    public void WhenTheListOfActiveHazardsIsFullAnOnsetIsDroppedAndCounted()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); root["limits"]!["maxHazards"] = 1; Tiny.Hazard(root)["cooldown"] = 0; });
        Fixture.Live(runtime, 30);
        Assert.True(runtime.ActiveHazards.Count <= 1);
        Assert.True(runtime.Dropped > 0);
    }

    [Fact]
    public void ABegunHazardIgnoresTheRateTheSeasonAndTheDirector()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Hazard(root)["seasonal"] = new JsonArray(0, 0));
        Fixture.Live(runtime, 12);                                    // cold
        runtime.BeginHazard("b", "h", "high");
        Assert.NotNull(runtime.ActiveAt(0, 1));
    }
}
