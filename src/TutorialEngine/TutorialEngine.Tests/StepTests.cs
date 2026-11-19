using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Steps: availability by prerequisites, completion by events since the step began and by host facts, branches, parallel steps, skipping, replaying and finishing.</summary>
public sealed class StepTests
{
    private static string S(TutorialRuntime r, string id) => Fixture.Status(r, id);

    [Fact]
    public void TheFirstEvaluationTeachesTheFirstStepAndOnlyIt()
    {
        var runtime = Fixture.Ready();
        var changes = runtime.Advance(0, Fixture.Host());
        Assert.Equal([new StepChanged(0, StepStatus.Pending, StepStatus.Active)], changes.Steps);
        Assert.Equal("active", S(runtime, "s1"));
        Assert.Equal("pending", S(runtime, "s2"));
        Assert.Equal(0, runtime.StateOf("s1").ActivatedAt);
        Assert.Equal(-1, runtime.StateOf("s1").DoneAt);
    }

    [Fact]
    public void NothingIsTaughtBeforeTheTutorialStartsAndTheStartIsOnlyFromIdle()
    {
        var runtime = TutorialRuntime.Create(Fixture.Mini());
        Assert.False(runtime.Advance(5, Fixture.Host()).Steps.Any());
        Assert.Equal(TutorialPhase.Idle, runtime.Phase);
        runtime.Start();
        Assert.Equal(TutorialPhase.Running, runtime.Phase);
        Assert.Equal(TutorialCodes.Phase, Fixture.Refusal(() => runtime.Start()));
    }

    [Fact]
    public void WhatHappenedBeforeAStepBeganDoesNotCountForItButWhatHappensAfterDoes()
    {
        var runtime = Fixture.Ready();
        runtime.Observe("e1");                                    // before s1 is taught: it counts for the skill, not for the step
        runtime.Advance(0, Fixture.Host());
        Assert.Equal("active", S(runtime, "s1"));
        runtime.Advance(3, Fixture.Host());
        Assert.Equal("active", S(runtime, "s1"));
        runtime.MoveTo(4);
        runtime.Observe("e1");
        var changes = runtime.Evaluate(Fixture.Host());
        Assert.Equal("done", S(runtime, "s1"));
        Assert.Equal(4, runtime.StateOf("s1").DoneAt);
        Assert.Contains(new StepChanged(0, StepStatus.Active, StepStatus.Done), changes.Steps);
        Assert.Contains(new StepChanged(1, StepStatus.Pending, StepStatus.Active), changes.Steps);
    }

    [Fact]
    public void ACountIsReadFromTheStepsOwnBaselineSoTwoObservationsAreNeededForAThreshold()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r => Tiny.Step(r, "s1")["complete"] = Tiny.Cond("flag", "eq", true)));
        runtime.Advance(0, Fixture.Host(flag: false));
        runtime.Advance(1, Fixture.Host(flag: true));             // s1 and s2 and s3 are all decided by the flag and the events
        Assert.Equal("done", S(runtime, "s1"));
        Assert.Equal("active", S(runtime, "s2"));
        runtime.Observe("e2");
        runtime.Advance(2, Fixture.Host(flag: true));
        Assert.Equal("active", S(runtime, "s2"));
        runtime.Observe("e2");
        runtime.Advance(3, Fixture.Host(flag: true));
        Assert.Equal("done", S(runtime, "s2"));
        Assert.Equal("done", S(runtime, "s3"));                   // the flag already holds: s3 begins and completes in the same evaluation
        Assert.Equal(TutorialPhase.Finished, runtime.Phase);
        Assert.Equal(1000, runtime.ProgressPermille());
    }

    [Fact]
    public void CompletionChainsThroughSeveralStepsInOneEvaluationInDocumentOrder()
    {
        var set = Fixture.Mini(r =>
        {
            Tiny.Step(r, "s1")["complete"] = Tiny.Cond("flag", "eq", true);
            Tiny.Step(r, "s2")["complete"] = Tiny.Cond("flag", "eq", true);
        });
        var runtime = Fixture.Ready(set);
        var changes = runtime.Advance(7, Fixture.Host(flag: true));
        Assert.Equal(
            [new StepChanged(0, StepStatus.Pending, StepStatus.Active), new StepChanged(0, StepStatus.Active, StepStatus.Done), new StepChanged(1, StepStatus.Pending, StepStatus.Active),
             new StepChanged(1, StepStatus.Active, StepStatus.Done), new StepChanged(2, StepStatus.Pending, StepStatus.Active), new StepChanged(2, StepStatus.Active, StepStatus.Done)], changes.Steps);
        Assert.Equal([new PhaseChanged(TutorialPhase.Running, TutorialPhase.Finished)], changes.Phases);
    }

    [Fact]
    public void AStepWaitsForEveryPrerequisiteAndForOneOfTheAlternatives()
    {
        var set = Fixture.Mini(r =>
        {
            Tiny.Step(r, "s1")["skill"] = "";
            r["limits"]!["maxActiveSteps"] = 3;
            var s4 = Tiny.Copy(Tiny.Step(r, "s3")); s4["id"] = "s4"; s4["requires"] = new JsonArray("s1", "s2"); s4["complete"] = Tiny.Cond("event.e3.since_step", "gte", 1); r["steps"]!.AsArray().Add(s4);
            var s5 = Tiny.Copy(Tiny.Step(r, "s3")); s5["id"] = "s5"; s5["requires"] = new JsonArray(); s5["requiresAny"] = new JsonArray("s2", "s3"); s5["complete"] = Tiny.Cond("event.e4.since_step", "gte", 1); r["steps"]!.AsArray().Add(s5);
            Tiny.Step(r, "s3")["requires"] = new JsonArray("s1");
        });
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host());
        Assert.Equal(["active", "pending", "pending", "pending", "pending"], new[] { "s1", "s2", "s3", "s4", "s5" }.Select(id => S(runtime, id)));
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host());
        Assert.Equal(["done", "active", "active", "pending", "pending"], new[] { "s1", "s2", "s3", "s4", "s5" }.Select(id => S(runtime, id)));   // s2 and s3 both need only s1; s4 needs s2 too
        runtime.MoveTo(2); runtime.Observe("e2", 2);
        runtime.Evaluate(Fixture.Host());
        Assert.Equal(["done", "done", "active", "active", "active"], new[] { "s1", "s2", "s3", "s4", "s5" }.Select(id => S(runtime, id)));         // s5 needed one of s2 and s3: s2 was enough
    }

    [Fact]
    public void ABranchNotTakenIsBypassedAndItsDependantsGoOn()
    {
        var set = Fixture.Mini(r => Tiny.Step(r, "s2")["when"] = Tiny.Cond("flag", "eq", true));
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: false));
        runtime.MoveTo(1); runtime.Observe("e1");
        var changes = runtime.Evaluate(Fixture.Host(flag: false));
        Assert.Equal("bypassed", S(runtime, "s2"));
        Assert.Equal("active", S(runtime, "s3"));
        Assert.Contains(new StepChanged(1, StepStatus.Pending, StepStatus.Bypassed), changes.Steps);
        Assert.Equal(1, runtime.StateOf("s2").DoneAt);
    }

    [Fact]
    public void ABranchTakenTeachesTheStepAsUsual()
    {
        var set = Fixture.Mini(r => Tiny.Step(r, "s2")["when"] = Tiny.Cond("flag", "eq", true));
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: true));
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host(flag: true));
        Assert.Equal("active", S(runtime, "s2"));
    }

    [Fact]
    public void AtMostTheLimitOfStepsAreTaughtAtOnceAndTheRestWaitForAPlace()
    {
        var set = Fixture.Mini(r =>
        {
            r["limits"]!["maxActiveSteps"] = 2;
            foreach (var id in new[] { "s2", "s3" }) Tiny.Step(r, id)["requires"] = new JsonArray();
            Tiny.Step(r, "s1")["skill"] = "";
        });
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host());
        Assert.Equal(["active", "active", "pending"], new[] { "s1", "s2", "s3" }.Select(id => S(runtime, id)));
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host());
        Assert.Equal(["done", "active", "active"], new[] { "s1", "s2", "s3" }.Select(id => S(runtime, id)));
    }

    [Fact]
    public void AHostFactCompletesAStepAndAStepsOwnStatusCanBeReadByAnother()
    {
        var set = Fixture.Mini(r =>
        {
            Tiny.Step(r, "s1")["skill"] = "";
            Tiny.Step(r, "s2")["complete"] = Tiny.Cond("level", "gte", 50);
            Tiny.Step(r, "s3")["complete"] = Tiny.Cond("step.s2.done", "eq", true);
        });
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host());
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host(level: 49));
        Assert.Equal("active", S(runtime, "s2"));
        runtime.Advance(2, Fixture.Host(level: 50));
        Assert.Equal("done", S(runtime, "s2"));
        Assert.Equal("done", S(runtime, "s3"));
    }

    [Fact]
    public void ASkippableTaughtStepCanBeSkippedAndTheNextOneBegins()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(3, Fixture.Host());
        var changes = runtime.SkipStep("s1");
        Assert.Equal([new StepChanged(0, StepStatus.Active, StepStatus.Skipped)], changes.Steps);
        Assert.True(changes.Persist);
        Assert.Equal(3, runtime.StateOf("s1").DoneAt);
        runtime.Evaluate(Fixture.Host());
        Assert.Equal("active", S(runtime, "s2"));
    }

    [Fact]
    public void ASkipIsRefusedForAStepThatIsNotBeingTaughtOneThatMaynotBeSkippedAndOneThatDoesNotExist()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        Assert.Equal(TutorialCodes.StepState, Fixture.Refusal(() => runtime.SkipStep("s2")));      // pending
        runtime.MoveTo(1); runtime.Observe("e1"); runtime.Evaluate(Fixture.Host());
        Assert.Equal(TutorialCodes.NotSkippable, Fixture.Refusal(() => runtime.SkipStep("s2")));   // taught but not skippable
        Assert.Equal(TutorialCodes.StepState, Fixture.Refusal(() => runtime.SkipStep("s1")));      // done
        Assert.Equal(TutorialCodes.UnknownStep, Fixture.Refusal(() => runtime.SkipStep("nope")));
        Assert.Equal("active", S(runtime, "s2"));
    }

    [Fact]
    public void SkippingTheWholeTutorialSkipsWhatWaitsAndTeachesNothingMore()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(2, Fixture.Host());
        var changes = runtime.SkipTutorial();
        Assert.Equal(TutorialPhase.Skipped, runtime.Phase);
        Assert.Equal(["skipped", "skipped", "skipped"], new[] { "s1", "s2", "s3" }.Select(id => S(runtime, id)));
        Assert.Equal(3, changes.Steps.Count);
        Assert.Equal([new PhaseChanged(TutorialPhase.Running, TutorialPhase.Skipped)], changes.Phases);
        runtime.Observe("e1");
        Assert.False(runtime.Advance(9, Fixture.Host()).Steps.Any());
        Assert.Equal(TutorialCodes.Phase, Fixture.Refusal(() => runtime.SkipTutorial()));
    }

    [Fact]
    public void ATutorialThatWasNeverStartedCanBeSkippedButNotReplayed()
    {
        var runtime = TutorialRuntime.Create(Fixture.Mini());
        Assert.Equal(TutorialCodes.Phase, Fixture.Refusal(() => runtime.Replay()));
        runtime.SkipTutorial();
        Assert.Equal(TutorialPhase.Skipped, runtime.Phase);
    }

    [Fact]
    public void AReplayStartsEveryStepAgainKeepsWhatThePlayerDidAndCountsOnlyNewDoing()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        runtime.MoveTo(1); runtime.Observe("e1"); runtime.Evaluate(Fixture.Host());
        runtime.SkipTutorial();
        var changes = runtime.Replay();
        Assert.Equal(TutorialPhase.Running, runtime.Phase);
        Assert.True(runtime.IgnoreMastery);
        Assert.Equal(3, changes.Steps.Count);
        Assert.All(new[] { "s1", "s2", "s3" }, id => Assert.Equal("pending", S(runtime, id)));
        Assert.Equal(1, runtime.EventCounts[0]);                       // what the player did stays
        runtime.Advance(5, Fixture.Host());
        Assert.Equal("active", S(runtime, "s1"));                     // e1 happened before: it does not complete the step again
        runtime.MoveTo(6); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host());
        Assert.Equal("done", S(runtime, "s1"));
    }

    [Fact]
    public void AFinishedTutorialCanBeReplayedAndFinishesAgain()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r => { Tiny.Step(r, "s1")["complete"] = Tiny.Cond("flag", "eq", true); Tiny.Step(r, "s2")["complete"] = Tiny.Cond("flag", "eq", true); }));
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Equal(TutorialPhase.Finished, runtime.Phase);
        runtime.Replay();
        Assert.Equal(TutorialPhase.Running, runtime.Phase);
        runtime.Advance(1, Fixture.Host(flag: true));
        Assert.Equal(TutorialPhase.Finished, runtime.Phase);
    }

    [Fact]
    public void TimeNeverGoesBackAndTheSameStepIsAllowed()
    {
        var runtime = Fixture.Ready(step: 10);
        runtime.Advance(10, Fixture.Host());
        runtime.Advance(11, Fixture.Host());
        Assert.Equal(TutorialCodes.StepOrder, Fixture.Refusal(() => runtime.MoveTo(10)));
        Assert.Equal(11, runtime.Step);
        Assert.Equal(TutorialCodes.StepOrder, Fixture.Refusal(() => TutorialRuntime.Create(Fixture.Mini(), -1)));
    }

    [Fact]
    public void AnObservationIsCheckedAndASkillGainsItsPoints()
    {
        var runtime = Fixture.Ready();
        Assert.Equal(TutorialCodes.UnknownEvent, Fixture.Refusal(() => runtime.Observe("nope")));
        Assert.Equal(TutorialCodes.Count, Fixture.Refusal(() => runtime.Observe("e1", 0)));
        Assert.Equal(TutorialCodes.Count, Fixture.Refusal(() => runtime.Observe("e1", TutorialRuntime.MaxObserve + 1)));
        runtime.Observe("e1", TutorialRuntime.MaxObserve);
        Assert.Equal(TutorialRuntime.MaxObserve, runtime.EventCounts[0]);
        Assert.Equal(3, runtime.SkillPoints[0]);                         // capped at the mastery
    }

    [Fact]
    public void AWrongOrMissingHostFactIsRefusedAndNothingMoves()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());
        var before = runtime.StateDigest();
        runtime.MoveTo(1); runtime.Observe("e1");
        before = runtime.StateDigest();
        Assert.Equal(TutorialCodes.FactMissing, Fixture.Refusal(() => runtime.Evaluate(new Dictionary<string, ScenarioEngine.Contracts.FactValue>())));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(new Dictionary<string, ScenarioEngine.Contracts.FactValue> { ["flag"] = ScenarioEngine.Contracts.FactValue.From(1), ["level"] = ScenarioEngine.Contracts.FactValue.From(0) })));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(Fixture.Host(level: 101))));
        Assert.Equal(TutorialCodes.FactInvalid, Fixture.Refusal(() => runtime.Evaluate(Fixture.Host(level: -1))));
        Assert.Equal(before, runtime.StateDigest());
        Assert.Equal("active", S(runtime, "s1"));
    }

    [Fact]
    public void TheValleySampleIsTaughtThroughToTheEndByTheRightBranch()
    {
        var runtime = Fixture.Ready(Fixture.Valley);
        var host = Fixture.ValleyHost(gamepad: true);
        runtime.Advance(0, host);
        runtime.MoveTo(10); runtime.Observe("moved"); runtime.Evaluate(host);
        Assert.Equal("bypassed", S(runtime, "look-mouse"));
        Assert.Equal("active", S(runtime, "look-pad"));
        runtime.MoveTo(20); runtime.Observe("camera_moved"); runtime.Evaluate(host);
        Assert.Equal("active", S(runtime, "menu"));
        Assert.Equal(3, runtime.Satisfied());                         // move done, the mouse look bypassed, the pad look done
    }
}
