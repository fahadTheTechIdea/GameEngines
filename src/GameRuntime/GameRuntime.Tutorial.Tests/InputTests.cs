using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The player's hands on the tutorial: pressed actions are polled inside the Update and become requests; a press the document maps is an observation, the controls are their requests, any other press is activity.</summary>
public sealed class InputTests
{
    private sealed class Rig
    {
        public TutHost Host { get; }
        public InputState Input { get; }

        public Rig(TutorialSet? set = null, string channel = "")
        {
            var state = new InputState(() => 0);
            var host = new TutHost(set ?? Samples.Small, channel: channel, extra: [(new TutorialInputComponent(state, set ?? Samples.Small, channel: channel), 5)]);
            Host = host;
            Input = state;
        }

        public void Tick(int n = 1) => Host.Tick(n);
    }

    [Fact]
    public void AMappedActionIsAnObservationOfItsEventAndItCompletesTheStepInTheSameTick()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Input.Press("press.one");
        rig.Tick();
        Assert.Equal(1, rig.Host.Runtime.EventCounts[0]);
        Assert.Equal(StepStatus.Done, rig.Host.Runtime.StepState("s1").Status);
        Assert.Contains(rig.Host.Of<TutorialApplied>(), a => a.RequestId == "t2-0");
    }

    [Fact]
    public void AnUnmappedActionIsOnlyActivityAndKeepsTheHintAway()
    {
        var rig = new Rig();
        rig.Tick(4);
        rig.Input.Press("something.else");
        rig.Input.Release("something.else");
        rig.Tick();
        Assert.Equal(5, rig.Host.Runtime.LastActivity);
        rig.Tick(4);
        Assert.Equal(0, rig.Host.Runtime.StepState("s1").Hint);                    // the press was at step 5: the hint at 5 idle steps is due at step 10
        rig.Tick(1);
        Assert.Equal(1, rig.Host.Runtime.StepState("s1").Hint);
        Assert.Empty(rig.Host.Of<TutorialRefused>());
    }

    [Fact]
    public void ReleasesAreNotRequestsAndEachEdgeIsPolledOnce()
    {
        var rig = new Rig();
        rig.Input.Press("press.one");
        rig.Input.Release("press.one");
        rig.Input.Press("press.one");
        rig.Tick();
        Assert.Equal(2, rig.Host.Runtime.EventCounts[0]);
        rig.Tick(3);
        Assert.Equal(2, rig.Host.Runtime.EventCounts[0]);
    }

    [Fact]
    public void TheControlsAreTheirRequests()
    {
        var rig = new Rig();
        rig.Tick(2);
        rig.Input.Press(TutorialInputComponent.SkipStep, "s1");
        rig.Tick();
        Assert.Equal(StepStatus.Skipped, rig.Host.Runtime.StepState("s1").Status);
        rig.Input.Press(TutorialInputComponent.Replay);
        rig.Tick();
        Assert.Equal(StepStatus.Pending, rig.Host.Runtime.StepState("s2").Status);
        rig.Input.Press(TutorialInputComponent.SkipTutorial);
        rig.Tick();
        Assert.Equal(TutorialPhase.Skipped, rig.Host.Runtime.Phase);
        rig.Input.Press(TutorialInputComponent.ReplayStory);
        rig.Input.Press(TutorialInputComponent.Next);
        rig.Input.Press(TutorialInputComponent.Back);
        rig.Input.Press(TutorialInputComponent.Language, "fr");
        rig.Input.Press(TutorialInputComponent.SkipStory);
        rig.Tick();
        Assert.Equal((JourneyState.Done, "fr"), (rig.Host.Runtime.Journey.State, rig.Host.Runtime.Journey.Language));
        Assert.Empty(rig.Host.Of<TutorialRefused>());
    }

    [Fact]
    public void ADismissAndAnEntryComeFromTheirArguments()
    {
        var rig = new Rig();
        rig.Host.HostFacts["flag"] = ScenarioEngine.Contracts.FactValue.From(true);
        rig.Tick(2);
        Assert.True(rig.Host.Runtime.Tips[0].Visible);
        rig.Input.Press(TutorialInputComponent.DismissTip, "t1");
        rig.Tick();
        Assert.True(rig.Host.Runtime.Tips[0].Acknowledged);
        rig.Input.Press(TutorialInputComponent.ReplayStory);
        rig.Input.Press(TutorialInputComponent.SkipStory);
        rig.Tick();
        rig.Input.Press(TutorialInputComponent.Entry, "join");
        rig.Tick();
        Assert.Equal(TutorialCodes.JourneyOrder, rig.Host.Of<TutorialRefused>().Single().Code);          // the journey was already done: the entry is refused, not dropped
    }

    [Fact]
    public void AnInputOnAnotherChannelIsNotThisPlayers()
    {
        var state = new InputState(() => 0);
        var host = new TutHost(channel: "a", extra: [(new TutorialInputComponent(state, Samples.Small, channel: "b"), 5)]);
        state.Press("press.one");
        host.Tick(2);
        Assert.Equal(0, host.Runtime.EventCounts[0]);
        Assert.Equal(TutorialChannels.UnknownChannel, host.Of<TutorialRefused>().Single().Code);          // nobody serves "b": refused, not dropped
    }

    [Fact]
    public void ARealPressedInputThroughTheJournalReplaysTheSame()
    {
        var state = new InputState(() => 0);
        var live = new TutHost(journal: true, extra: [(new TutorialInputComponent(state, Samples.Small), 5)]);
        live.Tick(2);
        state.Press("press.one");
        live.Tick(3);
        state.Press("nothing");
        live.Tick(2);
        var fresh = Samples.Done(Samples.Small);
        fresh.Start();
        var replay = TutorialReplay.Run(fresh, TutorialJournal.Parse(live.Journal.ToText()), 0, live.Component.Tick, TutHost.TicksPerSecond);
        Assert.Equal(live.Runtime.StateDigest(), replay.StateDigest);
    }
}
