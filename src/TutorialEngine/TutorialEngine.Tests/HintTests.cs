using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Hints escalate by idle time in steps, counted from the later of the player's latest activity and the step's beginning; the player acting starts the count again but does not take the hint away.</summary>
public sealed class HintTests
{
    private static TutorialRuntime Taught()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host());          // s1 is taught from step 0; hints at idle 10, 30, 60
        return runtime;
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(59, 2)]
    [InlineData(60, 3)]
    [InlineData(100000, 3)]
    public void TheHintLevelIsTheNumberOfHintsWhoseIdleTimeHasPassed(int step, int level)
    {
        var runtime = Taught();
        runtime.Advance(step, Fixture.Host());
        Assert.Equal(level, runtime.StateOf("s1").Hint);
    }

    [Fact]
    public void ALongJumpGivesOneChangeToTheStrongestHintReached()
    {
        var runtime = Taught();
        var changes = runtime.Advance(500, Fixture.Host());
        Assert.Equal([new HintChanged(0, 3)], changes.Hints);
        Assert.False(runtime.Advance(501, Fixture.Host()).Hints.Any());
    }

    [Fact]
    public void EachLevelIsRaisedOnceAsTimePasses()
    {
        var runtime = Taught();
        var levels = new List<int>();
        for (var step = 1; step <= 70; step++)
            foreach (var hint in runtime.Advance(step, Fixture.Host()).Hints) levels.Add(hint.Level);
        Assert.Equal([1, 2, 3], levels);
    }

    [Fact]
    public void ActivityStartsTheCountAgainForTheNextLevelButKeepsTheHint()
    {
        var runtime = Taught();
        runtime.Advance(10, Fixture.Host());                      // level 1
        runtime.MoveTo(25);
        runtime.Touch();                                          // activity at 25: the second level is now due at 25 + 30
        runtime.Evaluate(Fixture.Host());
        Assert.Equal(1, runtime.StateOf("s1").Hint);
        runtime.Advance(54, Fixture.Host());
        Assert.Equal(1, runtime.StateOf("s1").Hint);
        runtime.Advance(55, Fixture.Host());
        Assert.Equal(2, runtime.StateOf("s1").Hint);
    }

    [Fact]
    public void AnObservationOfAnyEventIsActivityEvenWhenItDoesNotCompleteTheStep()
    {
        var runtime = Taught();
        runtime.MoveTo(9);
        runtime.Observe("e3");
        runtime.Advance(18, Fixture.Host());                      // 18 steps from the start but only 9 since the activity
        Assert.Equal(0, runtime.StateOf("s1").Hint);
        runtime.Advance(19, Fixture.Host());
        Assert.Equal(1, runtime.StateOf("s1").Hint);
    }

    [Fact]
    public void ACompletedStepTakesItsHintAwayAndTheNextStepStartsWithNone()
    {
        var runtime = Taught();
        runtime.Advance(35, Fixture.Host());
        Assert.Equal(2, runtime.StateOf("s1").Hint);
        runtime.MoveTo(36); runtime.Observe("e1");
        var changes = runtime.Evaluate(Fixture.Host());
        Assert.Contains(new HintChanged(0, 0), changes.Hints);
        Assert.Equal(0, runtime.StateOf("s1").Hint);
        Assert.Equal(0, runtime.StateOf("s2").Hint);
        Assert.Empty(runtime.Hints());
    }

    [Fact]
    public void ASkippedStepAlsoTakesItsHintAway()
    {
        var runtime = Taught();
        runtime.Advance(35, Fixture.Host());
        Assert.Contains(new HintChanged(0, 0), runtime.SkipStep("s1").Hints);
    }

    [Fact]
    public void ResumingStartsTheIdleCountAgainSoTimeAwayIsNotTakenForBeingStuck()
    {
        var withoutResume = Taught();
        withoutResume.Advance(1000, Fixture.Host());
        Assert.Equal(3, withoutResume.StateOf("s1").Hint);

        var resumed = Taught();
        resumed.MoveTo(1000);
        resumed.Resume();
        resumed.Evaluate(Fixture.Host());
        Assert.Equal(0, resumed.StateOf("s1").Hint);
        resumed.Advance(1010, Fixture.Host());
        Assert.Equal(1, resumed.StateOf("s1").Hint);
    }

    [Fact]
    public void TheHintToDrawIsTheKeyKindAndTargetOfTheLevelReachedWithTheMotionTheSettingsAllow()
    {
        var runtime = Taught();
        runtime.Advance(30, Fixture.Host());
        var view = Assert.Single(runtime.Hints());
        Assert.Equal(new HintView("s1", 2, HintKind.Explain, "k.h2", "t1", Motion.Pulse), view);
        runtime.SetReducedMotion(true);
        Assert.Equal(Motion.None, Assert.Single(runtime.Hints()).Motion);
        runtime.Advance(60, Fixture.Host());
        Assert.Equal(("k.h3", HintKind.Show), (runtime.Hints()[0].Key, runtime.Hints()[0].Kind));
    }

    [Fact]
    public void AStepWithoutHintsNeverHasOneAndHintsStopWhenTheTutorialIsSkipped()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r => r["steps"]!.AsArray()[0]!["hints"] = new System.Text.Json.Nodes.JsonArray()));
        runtime.Advance(100000, Fixture.Host());
        Assert.Equal(0, runtime.StateOf("s1").Hint);
        var other = Taught();
        other.Advance(40, Fixture.Host());
        other.SkipTutorial();
        Assert.Empty(other.Hints());
        Assert.False(other.Advance(5000, Fixture.Host()).Hints.Any());
    }

    [Fact]
    public void ReplayingTakesTheHintsAwayAndTheyEscalateAgainFromTheReplayStart()
    {
        var runtime = Taught();
        runtime.Advance(65, Fixture.Host());
        Assert.Equal(3, runtime.StateOf("s1").Hint);
        var changes = runtime.Replay();
        Assert.Contains(new HintChanged(0, 0), changes.Hints);
        runtime.Advance(66, Fixture.Host());
        Assert.Equal(0, runtime.StateOf("s1").Hint);
        runtime.Advance(75, Fixture.Host());                       // 10 steps after the replay
        Assert.Equal(0, runtime.StateOf("s1").Hint);
        runtime.Advance(76, Fixture.Host());
        Assert.Equal(1, runtime.StateOf("s1").Hint);
    }
}
