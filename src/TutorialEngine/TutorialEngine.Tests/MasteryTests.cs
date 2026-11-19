using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Mastery: events are evidence for skills; at the mastery points the player is taken to know the skill, its steps are not taught and its tips are not shown, until the player asks for the whole tutorial.</summary>
public sealed class MasteryTests
{
    private static TutorialSet SkillOnFlag() => Fixture.Mini(r => Tiny.Step(r, "s1")["complete"] = Tiny.Cond("flag", "eq", true));

    [Fact]
    public void PointsAddUpToTheMasteryAndAreReportedOnceWhenTheyGetThere()
    {
        var runtime = Fixture.Ready();
        Assert.Empty(runtime.Observe("e1").Skills);
        Assert.Empty(runtime.Observe("e1").Skills);
        Assert.False(runtime.IsMastered(0));
        Assert.Equal([new SkillChanged(0)], runtime.Observe("e1").Skills);
        Assert.True(runtime.IsMastered(0));
        Assert.Empty(runtime.Observe("e1").Skills);                    // already there: nothing to report
        Assert.Equal(3, runtime.SkillPoints[0]);
    }

    [Fact]
    public void OneObservationOfSeveralTimesCanReachTheMasteryAndStopsAtIt()
    {
        var runtime = Fixture.Ready();
        Assert.Equal([new SkillChanged(0)], runtime.Observe("e1", 50).Skills);
        Assert.Equal(3, runtime.SkillPoints[0]);
    }

    [Fact]
    public void AnEventThatIsNotEvidenceGivesNoPoints()
    {
        var runtime = Fixture.Ready();
        runtime.Observe("e2", 10);
        Assert.Equal(0, runtime.SkillPoints[0]);
    }

    [Fact]
    public void AStepForASkillThePlayerAlreadyHasIsMasteredNotTaughtAndTheNextOneBegins()
    {
        var runtime = Fixture.Ready(SkillOnFlag());
        runtime.Observe("e1", 3);
        var changes = runtime.Advance(0, Fixture.Host());
        Assert.Contains(new StepChanged(0, StepStatus.Pending, StepStatus.Mastered), changes.Steps);
        Assert.DoesNotContain(changes.Steps, c => c.Step == 0 && c.To == StepStatus.Active);
        Assert.Equal("active", Fixture.Status(runtime, "s2"));
    }

    [Fact]
    public void AStepBeingTaughtIsMasteredAtOnceWhenThePlayerGainsTheSkillElsewhere()
    {
        var runtime = Fixture.Ready(SkillOnFlag());
        runtime.Advance(0, Fixture.Host());
        Assert.Equal("active", Fixture.Status(runtime, "s1"));
        runtime.MoveTo(4);
        runtime.Observe("e1", 3);
        var changes = runtime.Evaluate(Fixture.Host());
        Assert.Equal("mastered", Fixture.Status(runtime, "s1"));
        Assert.Contains(new StepChanged(0, StepStatus.Active, StepStatus.Mastered), changes.Steps);
        Assert.Equal(4, runtime.StateOf("s1").DoneAt);
    }

    [Fact]
    public void AReplayTeachesEverythingEvenForSkillsThePlayerHas()
    {
        var runtime = Fixture.Ready(SkillOnFlag());
        runtime.Observe("e1", 3);
        runtime.Advance(0, Fixture.Host());
        Assert.Equal("mastered", Fixture.Status(runtime, "s1"));
        runtime.Replay();
        runtime.Advance(1, Fixture.Host());
        Assert.Equal("active", Fixture.Status(runtime, "s1"));
        Assert.True(runtime.IsMastered(0));
    }

    [Fact]
    public void AStepThatTeachesNoSkillIsNeverMastered()
    {
        var runtime = Fixture.Ready();
        runtime.Observe("e1", 3);
        runtime.Observe("e2", 3);
        runtime.Advance(0, Fixture.Host());
        Assert.Equal("mastered", Fixture.Status(runtime, "s1"));
        Assert.Equal("active", Fixture.Status(runtime, "s2"));            // s2 teaches no skill
    }

    [Fact]
    public void TheSkillFactsReadTheSameNumbers()
    {
        var runtime = Fixture.Ready();
        runtime.Observe("e1", 2);
        var facts = runtime.ReadFacts();
        Assert.Equal(2, facts["skill.sk.points"].Integer);
        Assert.False(facts["skill.sk.mastered"].Boolean);
        runtime.Observe("e1");
        Assert.True(runtime.ReadFacts()["skill.sk.mastered"].Boolean);
    }

    [Fact]
    public void ThePlayerWhoAlreadyKnowsTheGameSkipsTheTeachingOfTheSample()
    {
        var runtime = Fixture.Ready(Fixture.Valley);
        var host = Fixture.ValleyHost();
        runtime.Observe("moved", 20);
        runtime.Observe("menu_opened", 3);
        runtime.Observe("structure_built", 2);
        runtime.Observe("trade_made", 2);
        runtime.Advance(0, host);
        Assert.Equal("mastered", Fixture.Status(runtime, "move"));
        Assert.Equal("active", Fixture.Status(runtime, "look-mouse"));          // teaches no skill
        Assert.Equal("pending", Fixture.Status(runtime, "menu"));
        runtime.MoveTo(1);
        runtime.Observe("camera_moved");
        runtime.Evaluate(host);
        Assert.Equal("mastered", Fixture.Status(runtime, "menu"));
        Assert.Equal("mastered", Fixture.Status(runtime, "build"));
        Assert.Equal("active", Fixture.Status(runtime, "road"));
    }
}
