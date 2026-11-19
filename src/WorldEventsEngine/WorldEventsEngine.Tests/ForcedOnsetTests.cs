using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Forcing an onset in one step of a branched world (<see cref="WorldRuntime.BranchForcingOnset"/>): the trial of one hazard in one region passes, its severity is drawn again below a bound by the engine's own draw, the hazard's other
/// regions do not take their trials in that step, and every other step is as it was. The small document with its hazard at certainty makes the contrast plain: unforced, every exposed region starts every step it can.
/// </summary>
public sealed class ForcedOnsetTests
{
    private static WorldSet Certain(long seed)
    {
        var root = Tiny.Root();
        Tiny.Certain(root);
        root["seed"] = seed;
        Tiny.Hazard(root)["cooldown"] = 0;
        Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 };          // no spread, so each region takes its own trial
        foreach (var rung in Tiny.Hazard(root)["ladder"]!.AsArray())
        {
            var one = rung!.AsObject();
            one["minDuration"] = 1;                              // an onset lasts one step, so every step is a new trial in every region
            one["maxDuration"] = 1;
            one["onsetSteps"] = 0;
            one["recoverySteps"] = 0;
        }
        return WorldLoader.Load(root.ToJsonString());
    }

    private static WorldRuntime Lived(long seed, long to)
    {
        var runtime = WorldRuntime.Create(Certain(seed));
        Fixture.Live(runtime, to);
        return runtime;
    }

    private static List<WorldChange> Naturals(IEnumerable<WorldChange> changes) =>
        changes.Where(c => c.Kind == ChangeKind.Hazard && c.Sub2 == (int)HazardChange.Started && c.Amount == 1000).ToList();

    [Fact]
    public void WithoutTheConditionEveryExposedRegionStartsInAStepAtCertainty()
    {
        var runtime = WorldRuntime.Create(Certain(1));
        var step = Naturals(Fixture.Live(runtime, 0));
        Assert.Equal([0, 1], step.Select(c => c.Region).Order());
    }

    [Fact]
    public void TheForcedStepStartsOnlyTheForcedRegionAtTheRungTheBoundAllows()
    {
        var hazard = Certain(1).Hazards[0].Index;
        for (var seed = 2L; seed < 22; seed++)
        {
            // The ladder is low (tail 1,000,000) and high (300,000), so a bound at 300,000 can only draw high.
            var branch = Lived(1, 3).BranchForcingOnset(Certain(seed), new ForcedOnset(hazard, 1, 4, 300_000));
            var natural = Naturals(Fixture.Live(branch, 4).Where(c => c.Step == 4));
            var only = Assert.Single(natural);
            Assert.Equal(1, only.Region);
            Assert.Equal(1, only.Sub);                                  // the rung 'high'
            Assert.Equal(1, branch.ActiveHazards.Count(h => h.Hazard == hazard));          // the other region took no trial
        }
    }

    [Fact]
    public void ABoundOfTheWholeLadderDrawsEveryRungInTheLadderSShares()
    {
        var hazard = Certain(1).Hazards[0].Index;
        var high = 0;
        const int runs = 400;
        for (var seed = 100L; seed < 100 + runs; seed++)
        {
            var branch = Lived(1, 3).BranchForcingOnset(Certain(seed), new ForcedOnset(hazard, 0, 4, 1_000_000));
            var only = Assert.Single(Naturals(Fixture.Live(branch, 4).Where(c => c.Step == 4)));
            if (only.Sub == 1) high++;
        }
        // The tail of 'high' is 30%: 120 of 400, with a standard error of 9; the bounds are about five standard errors.
        Assert.InRange(high, 75, 165);
    }

    [Fact]
    public void TheStepsAfterTheForcedOneAreUnforcedAndTheOriginalIsUntouched()
    {
        var hazard = Certain(1).Hazards[0].Index;
        var original = Lived(1, 3);
        var before = original.StateDigest();
        var branch = original.BranchForcingOnset(Certain(5), new ForcedOnset(hazard, 1, 4, 300_000));
        Assert.Equal(before, original.StateDigest());
        var later = Fixture.Live(branch, 10).Where(c => c.Step > 4).ToList();
        Assert.Contains(Naturals(later), c => c.Region == 0);          // region 0 takes its trials again from step 5 on
    }

    [Fact]
    public void TheRefusalsNameWhatIsNotThere()
    {
        var hazard = Certain(1).Hazards[0].Index;
        var runtime = Lived(1, 3);
        var set = Certain(2);
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(-1, 0, 4, 300_000)), "hazard below"));
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(5, 0, 4, 300_000)), "hazard above"));
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(hazard, 2, 4, 300_000)), "region above"));
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(hazard, 0, 3, 300_000)), "step not after"));
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(hazard, 0, 4, 0)), "bound 0"));
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(hazard, 0, 4, 1_000_001)), "bound above"));
        var unexposed = WorldLoader.Load(UnexposedText());
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(unexposed, new ForcedOnset(hazard, 1, 4, 300_000)), "unexposed region"));
    }

    private static string UnexposedText()
    {
        var root = Tiny.Root();
        Tiny.Certain(root);
        Tiny.Region(root, "b")["exposure"] = new JsonArray();
        return root.ToJsonString();
    }
}
