using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// The watcher on a runtime (<see cref="IHazardWatcher"/>): it sees every onset trial with the chance the engine is about to use, and every onset that started with its rung, and changes nothing the engine does. The conditional
/// estimator of the command line is built on exactly these two facts, so each is checked against the engine's own state rather than a restatement of its rules.
/// </summary>
public sealed class WatchTests
{
    private sealed class Recorder(Action<long, int, int, long>? onTrial = null) : IHazardWatcher
    {
        public readonly List<(long Step, int Hazard, int Region, long Chance)> Trials = [];
        public readonly List<(long Step, int Hazard, int Region, int Rung)> Starts = [];

        public void Trial(long step, int hazard, int region, long chancePpm)
        {
            Trials.Add((step, hazard, region, chancePpm));
            onTrial?.Invoke(step, hazard, region, chancePpm);
        }

        public void Started(long step, int hazard, int region, int rung) => Starts.Add((step, hazard, region, rung));
    }

    private static List<(ChangeKind, long, int, int, int, int, long)> Flat(IEnumerable<WorldChange> changes) =>
        changes.Select(c => (c.Kind, c.Step, c.Region, c.Index, c.Sub, c.Sub2, c.Amount)).ToList();

    private static void AddSecondHazard(JsonObject root)
    {
        var second = Tiny.Copy(Tiny.Hazard(root)).AsObject();
        second["id"] = "h2";
        second["key"] = "h.h2";
        Tiny.Hazard(root)["excludes"] = new JsonArray("h2");
        second["excludes"] = new JsonArray("h");
        Tiny.Array(root, "hazards").Add(second);
        foreach (var region in new[] { "a", "b" }) Tiny.Region(root, region)["exposure"]!.AsArray().Add(new JsonObject { ["hazard"] = "h2", ["permille"] = 1000 });
    }

    [Fact]
    public void AWatcherChangesNothingTheEngineDoes()
    {
        void Edit(JsonObject root)
        {
            Tiny.Hazard(root)["ratePpm"] = 300_000;
            Tiny.Hazard(root)["paced"] = true;
            AddSecondHazard(root);
        }
        var bare = Fixture.MiniWorld(Edit);
        var watched = Fixture.MiniWorld(Edit);
        var recorder = new Recorder();
        watched.Watch(recorder);
        Assert.Equal(Flat(Fixture.Live(bare, 400)), Flat(Fixture.Live(watched, 400)));
        Assert.Equal(bare.Suppressed, watched.Suppressed);
        Assert.NotEmpty(recorder.Trials);
        Assert.NotEmpty(recorder.Starts);
    }

    [Fact]
    public void TheChanceReportedIsTheOnePartsOfTheEngineStateAddUpToIncludingTheExcitationOfEarlierOnsets()
    {
        // The lab's hazard lasts one step and excites itself; both seasons have the same rate, so the engine's own OnsetChance(hazard, 0, 0) is the chance of the trial that is about to be taken.
        var runtime = Lab.HawkesWorld(5, 20_000, 150_000, 800, 400_000);
        var hazard = runtime.Set.Hazards[0];
        var seen = new List<long>();
        var recorder = new Recorder((_, _, _, chance) =>
        {
            Assert.Equal(runtime.OnsetChance(hazard, 0, 0), chance);
            seen.Add(chance);
        });
        runtime.Watch(recorder);
        Fixture.Live(runtime, 299);
        Assert.All(recorder.Starts, s => Assert.DoesNotContain(recorder.Trials, t => t.Step == s.Step + 1));       // the step after an onset has no trial: the hazard is not ready again until the step after it ends
        Assert.Equal(300 - recorder.Starts.Count(s => s.Step < 299), recorder.Trials.Count);
        Assert.Contains(seen, c => c > 20_000);                          // the excitation raised it above the base after an onset
        Assert.Equal(20_000, seen.Min());
        Assert.All(recorder.Starts, s => Assert.Contains(recorder.Trials, t => t.Step == s.Step && t.Region == s.Region && t.Hazard == s.Hazard && t.Chance > 0));
    }

    [Fact]
    public void ADirectorsPacingReportsACertainHazardAtZeroExactlyWhenItHoldsTheOnsetBack()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["paced"] = true; Tiny.Hazard(root)["cooldown"] = 0; });
        var recorder = new Recorder();
        runtime.Watch(recorder);
        Fixture.Live(runtime, 600);
        Assert.True(runtime.Suppressed > 0);
        Assert.Equal(runtime.Suppressed, recorder.Trials.Count(t => t.Chance == 0));       // the rate is certainty, so a trial reported at 0 is one the pacing vetoed, and each of those was counted
        Assert.All(recorder.Starts, s => Assert.Contains(recorder.Trials, t => t.Step == s.Step && t.Region == s.Region && t.Chance > 0));
    }

    [Fact]
    public void AnExcludedHazardIsReportedAtZeroExactlyWhileTheOtherIsGoingInTheRegion()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Hazard(root)["ratePpm"] = 500_000;
            Tiny.Hazard(root)["cooldown"] = 0;
            AddSecondHazard(root);
            Tiny.Array(root, "hazards")[1]!["ratePpm"] = 500_000;
        });
        var zeros = 0;
        var recorder = new Recorder((_, hazard, region, chance) =>
        {
            var other = hazard == 0 ? 1 : 0;
            var excluded = runtime.ActiveAt(other, region) is not null;
            Assert.Equal(excluded, chance == 0);
            if (excluded) zeros++;
        });
        runtime.Watch(recorder);
        Fixture.Live(runtime, 400);
        Assert.True(zeros > 0, "no trial met an excluded hazard, so the test saw nothing");
        Assert.Equal(0, runtime.Suppressed);                        // an exclusion is not the director's pacing and is not counted as held back
        Assert.True(recorder.Starts.Count > 0);
    }

    [Fact]
    public void AConditionThatDoesNotHoldReportsZero()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Certain(root); Tiny.Hazard(root)["when"] = Tiny.Cond("temp", "gt", 1300); Tiny.Hazard(root)["cooldown"] = 0; Tiny.Hazard(root)["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 }; });
        var recorder = new Recorder();
        runtime.Watch(recorder);
        Fixture.Live(runtime, 600);
        Assert.Contains(recorder.Trials, t => t.Chance == 0);
        Assert.Contains(recorder.Trials, t => t.Chance > 0);
        Assert.All(recorder.Starts, s => Assert.Contains(recorder.Trials, t => t.Step == s.Step && t.Region == s.Region && t.Chance > 0));
        Assert.Equal(0, runtime.Suppressed);                        // nor is a condition that does not hold
        Assert.Equal(recorder.Starts.Count, recorder.Trials.Count(t => t.Chance > 0));        // at certainty, every trial that was not vetoed started (an onset of one step, so the next trial is free again)
    }

    [Fact]
    public void ABranchAndARestoredWorldDoNotCarryTheWatcherAndNullStopsIt()
    {
        var set = Lab.HawkesWorld(1, 2000, 0, 1000, 0).Set;
        var runtime = WorldRuntime.Create(set);
        var recorder = new Recorder();
        runtime.Watch(recorder);
        Fixture.Live(runtime, 9);
        Assert.Equal(10, recorder.Trials.Count);
        var branch = runtime.Branch(set);
        Fixture.Live(branch, 20);
        Assert.Equal(10, recorder.Trials.Count);                        // the branch lived eleven more steps unwatched
        runtime.Watch(null);
        Fixture.Live(runtime, 15);
        Assert.Equal(10, recorder.Trials.Count);
    }

    [Fact]
    public void AForcedStepIsNotReportedForItsHazardAndTheOtherStepsStillAre()
    {
        var set = Lab.HawkesWorld(1, 2000, 0, 1000, 0).Set;
        var runtime = WorldRuntime.Create(set);
        Fixture.Live(runtime, 2);
        var branch = runtime.BranchForcingOnset(set, new ForcedOnset(0, 0, 5, 1_000_000));
        var recorder = new Recorder();
        branch.Watch(recorder);
        Fixture.Live(branch, 8);
        Assert.Equal([3L, 4, 7, 8], recorder.Trials.Select(t => t.Step));       // step 5 is the forced trial, which is not a chance, and the forced onset blocks step 6 like any onset
        Assert.DoesNotContain(recorder.Starts, s => s.Step == 5);                 // nor is the onset it forces: only the world's own onsets are
    }
}
