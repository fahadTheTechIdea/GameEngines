using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The tutorial component: requests only queue and are answered in Update, in order; every request is answered; events are deferred, bounded per Update and owed; time waits for a host that does not drain; checkpoints are refused while anything waits.</summary>
public sealed class ComponentTests
{
    private sealed class BackwardsClock(params long[] steps) : ITutorialClock
    {
        private int _next;
        public long StepAt(long tick) => steps[Math.Min(_next++, steps.Length - 1)];
    }

    [Fact]
    public void ARequestOnlyQueuesAndIsAnsweredInTheNextUpdate()
    {
        var host = new TutHost();
        host.Ask("observe", "e1");
        Assert.Empty(host.Of<TutorialApplied>());
        Assert.Equal(0, host.Component.Answered);
        host.Tick();
        Assert.Single(host.Of<TutorialApplied>());
        Assert.Equal(1, host.Component.Answered);
        Assert.Equal(1, host.Runtime.EventCounts[0]);
    }

    [Fact]
    public void TheTutorialIsEvaluatedInTheUpdateWithTheStepOfTheClockAndNothingMovesBefore()
    {
        var host = new TutHost();
        Assert.Equal("pending", Fixture(host, "s1"));
        host.Tick();
        Assert.Equal("active", Fixture(host, "s1"));
        Assert.Equal(1, host.Runtime.StepState("s1").ActivatedAt);
        Assert.Contains(new TutorialStepChanged("tutorial", "", 1, "s1", "pending", "active"), host.Of<TutorialStepChanged>());
    }

    private static string Fixture(TutHost host, string step) => TutorialFacts.StatusName(host.Runtime.StepState(step).Status);

    [Fact]
    public void WhatThePlayerDidCompletesTheStepInTheSameUpdateStampedWithTheStepOfTheTick()
    {
        var host = new TutHost();
        host.Tick(3);
        host.Ask("observe", "e1");
        host.Tick();
        Assert.Equal("done", Fixture(host, "s1"));
        Assert.Equal(4, host.Runtime.StepState("s1").DoneAt);
        Assert.Equal("active", Fixture(host, "s2"));
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrivedEachWithItsOwnAnswer()
    {
        var host = new TutHost();
        var a = host.Ask("observe", "e1");
        var b = host.Ask("observe", "nope");
        var c = host.Ask("touch");
        host.Tick();
        var answers = host.All.Where(o => o is TutorialApplied or TutorialRefused).Select(o => o is TutorialApplied applied ? "applied " + applied.RequestId : "refused " + ((TutorialRefused)o).RequestId + " " + ((TutorialRefused)o).Code).ToList();
        Assert.Equal(["applied " + a, "refused " + b + " " + TutorialCodes.UnknownEvent, "applied " + c], answers);
        Assert.Equal(3, host.Component.Answered);
    }

    [Fact]
    public void ARequestOfAnUnknownKindIsRefusedNotDropped()
    {
        var host = new TutHost();
        host.Ask("dance");
        var before = host.Runtime.StateDigest();
        host.Tick();
        var refused = Assert.Single(host.Of<TutorialRefused>());
        Assert.Equal(TutorialComponent.UnknownKind, refused.Code);
        Assert.Equal("tutorial", refused.Source);
        Assert.Equal(1, host.Component.Answered);
        Assert.Equal(host.Runtime.StateDigest().Length, before.Length);
    }

    [Fact]
    public void ANumberTooBigForTheRequestIsRefusedWithTheCodeOfWhatItWasFor()
    {
        var host = new TutHost();
        host.Ask("observe", "e1", 10_000_000_000);
        host.Ask("text-scale", "", 10_000_000_000);
        host.Ask("text-scale", "", 1);
        host.Tick();
        Assert.Equal([TutorialCodes.Count, TutorialCodes.Setting, TutorialCodes.Setting], host.Of<TutorialRefused>().Select(r => r.Code));
        Assert.Equal(1000, host.Runtime.TextScale);
    }

    [Fact]
    public void ARequestForAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = new TutHost();
        host.Ask("observe", "e1", 1, "ghost");
        host.Tick();
        var refused = Assert.Single(host.Of<TutorialRefused>());
        Assert.Equal(TutorialChannels.UnknownChannel, refused.Code);
        Assert.Equal(TutorialChannels.ComponentId, refused.Source);
        Assert.Equal("ghost", refused.Channel);
        Assert.Equal(0, host.Runtime.EventCounts[0]);
    }

    [Fact]
    public void TwoPlayersOnTwoChannelsAreSeparate()
    {
        var both = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), both);
        var registry = new TutorialChannels();
        registry.Install(session);
        var a = new TutorialComponent(Samples.Done(Samples.Small), new TickTutorialClock(), () => Samples.Flag(), registry, "tutorial-a", "a");
        var b = new TutorialComponent(Samples.Done(Samples.Small), new TickTutorialClock(), () => Samples.Flag(), registry, "tutorial-b", "b");
        session.AddComponent(a, 10);
        session.AddComponent(b, 11);
        var seen = new List<TutorialApplied>();
        session.Subscribe<TutorialApplied>(seen.Add);
        session.Init();
        session.Submit(new TutorialRequested("x", "observe", "e1", 3, "a"));
        session.Submit(new TutorialRequested("y", "observe", "e2", 2, "b"));
        session.Update(20_000);
        Assert.Equal([3L, 0L], new[] { a.Runtime.EventCounts[0], a.Runtime.EventCounts[1] });
        Assert.Equal([0L, 2L], new[] { b.Runtime.EventCounts[0], b.Runtime.EventCounts[1] });
        Assert.Equal([("a", "tutorial-a", "x"), ("b", "tutorial-b", "y")], seen.OrderBy(e => e.Channel, StringComparer.Ordinal).Select(e => (e.Channel, e.Source, e.RequestId)));
        Assert.Equal(["a", "b"], registry.Served.Keys);
    }

    [Fact]
    public void ASecondComponentOnAChannelIsRefusedAtInit()
    {
        var host = new TutHost();
        Assert.Equal(TutorialChannels.DuplicateChannel, Samples.Code(() => host.Channels.Serve("", "other")));
    }

    [Fact]
    public void AComponentWhoseRegistryIsNotRunningOnItsSessionFailsItsFirstUpdate()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        new TutorialChannels().Install(session);
        var elsewhere = new TutorialChannels();
        var other = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        elsewhere.Install(other);
        other.Init();
        session.AddComponent(new TutorialComponent(Samples.Done(Samples.Small), new TickTutorialClock(), () => Samples.Flag(), elsewhere), 10);
        session.Init();
        session.Update(20_000);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Contains(reporter.Reports, r => r.StartsWith("RUNTIME_FAULT", StringComparison.Ordinal) && r.Contains("does not run on this session", StringComparison.Ordinal));
    }

    [Fact]
    public void ABadArgumentIsRefusedAtTheConstructorAndTheClocks()
    {
        var channels = new TutorialChannels();
        var runtime = Samples.Done(Samples.Small);
        Func<IReadOnlyDictionary<string, FactValue>> facts = () => Samples.Flag();
        Assert.Throws<ArgumentException>(() => new TutorialComponent(runtime, new TickTutorialClock(), facts, channels, maxEvents: 0));
        Assert.Throws<ArgumentException>(() => new TutorialComponent(runtime, new TickTutorialClock(), facts, channels, maxEvents: 10, maxOwed: 9));
        Assert.Throws<ArgumentNullException>(() => new TutorialComponent(null!, new TickTutorialClock(), facts, channels));
        Assert.Throws<ArgumentNullException>(() => new TutorialComponent(runtime, null!, facts, channels));
        Assert.Throws<ArgumentNullException>(() => new TutorialComponent(runtime, new TickTutorialClock(), null!, channels));
        Assert.Throws<ArgumentNullException>(() => new TutorialComponent(runtime, new TickTutorialClock(), facts, null!));
        Assert.Equal(TutorialCodes.Range, Samples.Code(() => new TickTutorialClock(0)));
        Assert.Equal(TutorialCodes.StepOrder, Samples.Code(() => new TickTutorialClock(1, -1)));
        Assert.Equal(7, new TickTutorialClock(10, 5).StepAt(25));
    }

    // ---- saving ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AChangeTheSavedProgressHoldsIsFollowedByOneSaveNeededAndATimeDrivenHintIsNot()
    {
        var host = new TutHost();
        host.Tick();                                              // s1 begins: saved progress changed
        Assert.Single(host.Of<TutorialSaveNeeded>());
        host.Tick(4);
        Assert.Single(host.Of<TutorialSaveNeeded>());
        host.Tick();                                              // tick 6: the first hint, which is time, not progress
        Assert.Contains(new TutorialHintChanged("tutorial", "", 6, "s1", 1, "nudge", "k.h1", "t1", "pulse"), host.Of<TutorialHintChanged>());
        Assert.Single(host.Of<TutorialSaveNeeded>());
        host.Ask("language", "fr"); host.Ask("intent", "x"); host.Ask("observe", "e2");
        host.Tick();                                              // three persisted changes in one Update: one SaveNeeded
        Assert.Equal(2, host.Of<TutorialSaveNeeded>().Count());
    }

    [Fact]
    public void SaveNeededComesAfterTheEventsOfTheChangeItFollows()
    {
        var host = new TutHost();
        host.Ask("skip-tutorial");
        host.Tick();
        var order = host.Transcript.Lines.Select(l => l[..l.IndexOf('{', StringComparison.Ordinal)]).ToList();
        Assert.Equal("TutorialSaveNeeded", order[^1]);
        Assert.Contains("TutorialPhaseChanged", order);
    }

    [Fact]
    public void AProgressQueryIsAnsweredWithTheNumbersTheProgressBarNeeds()
    {
        var host = new TutHost();
        host.Tick();
        host.Ask("observe", "e1");
        host.Ask("progress");
        host.Tick();                                              // a query is answered after the tutorial has looked at what the player did
        var answer = Assert.Single(host.Of<TutorialProgressAnswered>());
        Assert.Equal((1, 3, "running", "done", "s2", "en", 333), (answer.Satisfied, answer.Total, answer.Phase, answer.Journey, answer.Active, answer.Language, answer.Permille));
    }

    // ---- events: deferred, bounded and owed ----------------------------------------------------------------------------------------------

    private static TutorialSet Crowd(int tips) => TutorialLoader.Load(Mutate(Samples.SmallText, root =>
    {
        root["limits"]!["maxVisibleTips"] = tips;
        var template = root["tips"]![0]!.AsObject();
        var all = root["tips"]!.AsArray();
        var copy = JsonNode.Parse(template.ToJsonString())!.AsObject();
        all.Clear();
        for (var i = 0; i < tips; i++)
        {
            var tip = JsonNode.Parse(copy.ToJsonString())!.AsObject();
            tip["id"] = "t" + i;
            all.Add(tip);
        }
    }));

    private static string Mutate(string text, Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(text)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    [Fact]
    public void ChangeEventsAreRaisedAtMostTheMaximumPerUpdateTheRestAreOwedAndNoneIsLostOrReordered()
    {
        var set = Crowd(20);
        var small = new TutHost(set, maxEvents: 4, maxOwed: 1_000);
        var large = new TutHost(set, maxEvents: 100_000, maxOwed: 100_000);
        small.HostFacts["flag"] = FactValue.From(true);
        large.HostFacts["flag"] = FactValue.From(true);
        var sawOwed = false;
        var before = 0;
        var perTick = new List<int>();
        for (var tick = 0; tick < 60; tick++)
        {
            small.Tick(); large.Tick();
            var count = small.Transcript.Lines.Count(l => l.StartsWith("TutorialTipChanged", StringComparison.Ordinal) || l.StartsWith("TutorialStepChanged", StringComparison.Ordinal) || l.StartsWith("TutorialSaveNeeded", StringComparison.Ordinal));
            perTick.Add(count - before);
            before = count;
            if (small.Component.Owed > 0)
            {
                sawOwed = true;
                Assert.False(small.Component.IsQuiet);
                Assert.Equal(TutorialComponent.NotQuiet, Samples.Code(() => TutorialCheckpoint.Capture(small.Component)));
            }
        }
        Assert.True(sawOwed, "20 tips should have produced more than four changes");
        Assert.All(perTick, n => Assert.InRange(n, 0, 4));
        Assert.Equal(0, small.Component.Owed);
        // Nothing lost and nothing reordered: the events are the same, in the same order, as with a host that takes them all at once.
        var smallLines = small.LinesWithoutTicks().Where(l => !l.StartsWith("TutorialSaveNeeded", StringComparison.Ordinal)).ToList();
        var largeLines = large.LinesWithoutTicks().Where(l => !l.StartsWith("TutorialSaveNeeded", StringComparison.Ordinal)).ToList();
        Assert.Equal(largeLines, smallLines);
        Assert.Equal(small.Runtime.StateDigest(), large.Runtime.StateDigest());
    }

    [Fact]
    public void TimeWaitsWhileMoreEventsAreOwedThanTheHostHasTakenAndThenGoesOn()
    {
        var host = new TutHost(Crowd(20), maxEvents: 4, maxOwed: 4);
        host.HostFacts["flag"] = FactValue.From(true);
        host.Tick();
        Assert.True(host.Component.Owed >= 4);
        var stepAtFirst = host.Runtime.Step;
        host.Tick();
        Assert.Equal(stepAtFirst, host.Runtime.Step);                       // time waited: the tutorial did not move
        Assert.Equal(2, host.Component.Tick);
        for (var i = 0; i < 400 && !(host.Component.Owed == 0 && host.Runtime.Step == host.Component.Tick); i++) host.Tick();
        Assert.Equal(host.Component.Tick, host.Runtime.Step);               // caught up once the host had taken everything and the tips had run their course
        Assert.Equal(0, host.Component.Owed);
    }

    [Fact]
    public void TimeWaitsWhileAnyEventIsOwedAtAllWhenTheLimitIsOne()
    {
        var host = new TutHost(Crowd(20), maxEvents: 1, maxOwed: 1);
        host.HostFacts["flag"] = FactValue.From(true);
        var waited = 0;
        for (var tick = 0; tick < 150; tick++)
        {
            var owed = host.Component.Owed;
            var step = host.Runtime.Step;
            host.Tick();
            if (owed >= 1)
            {
                waited++;
                Assert.Equal(step, host.Runtime.Step);               // exactly one owed event is enough to make time wait
            }
        }
        Assert.True(waited > 20, "time should have waited many times, it waited " + waited);
    }

    [Fact]
    public void ATouchAndAProgressQueryAreNotSomethingToSaveAtOnce()
    {
        var host = new TutHost();
        host.Tick(2);
        var before = host.Of<TutorialSaveNeeded>().Count();
        host.Ask("touch");
        host.Ask("progress");
        host.Ask("resume");
        host.Tick();
        Assert.Equal(before, host.Of<TutorialSaveNeeded>().Count());
        host.Ask("skip-step", "s1");
        host.Tick();
        Assert.Equal(before + 1, host.Of<TutorialSaveNeeded>().Count());
    }

    [Fact]
    public void ARequestIsStillAnsweredWhileTimeWaits()
    {
        var host = new TutHost(Crowd(20), maxEvents: 4, maxOwed: 4);
        host.HostFacts["flag"] = FactValue.From(true);
        host.Tick();
        host.Ask("progress");
        host.Tick();
        Assert.Single(host.Of<TutorialProgressAnswered>());
    }

    [Fact]
    public void AClockThatGoesBackwardsIsReportedOnceAndTheTutorialDoesNotMove()
    {
        var host = new TutHost(clock: new BackwardsClock(5, 3, 3, 3, 4, 2, 6));
        host.Tick(7);
        var refusals = host.Of<TutorialRefused>().ToList();
        Assert.Equal([TutorialComponent.ClockRequestId], refusals.Select(r => r.RequestId));      // the clock was wrong for five ticks and is reported once, until it is right again
        Assert.All(refusals, r => Assert.Equal(TutorialCodes.StepOrder, r.Code));
        Assert.Equal(6, host.Runtime.Step);
    }

    [Fact]
    public void AMissingHostFactIsReportedOnceNothingMovesAndTheTutorialRecoversWhenItIsGiven()
    {
        var host = new TutHost();
        host.Tick(2);
        var before = host.Runtime.StateDigest();
        host.HostFacts.Remove("flag");
        host.Tick(3);
        var refusal = Assert.Single(host.Of<TutorialRefused>());
        Assert.Equal((TutorialComponent.EvaluateRequestId, TutorialCodes.FactMissing), (refusal.RequestId, refusal.Code));
        Assert.NotEqual(before, host.Runtime.StateDigest());                // time moved (the step); nothing else did
        Assert.Equal("active", Fixture(host, "s1"));
        host.HostFacts["flag"] = FactValue.From(false);
        host.Tick();
        host.HostFacts["flag"] = FactValue.From(3);
        host.Tick(2);
        Assert.Equal([TutorialCodes.FactMissing, TutorialCodes.FactInvalid], host.Of<TutorialRefused>().Select(r => r.Code));
    }

    // ---- checkpoints -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AQuietComponentIsCheckpointedAndTheCheckpointOpensToTheSameState()
    {
        var host = new TutHost();
        host.Tick(2);
        Assert.True(host.Component.IsQuiet);
        var bytes = TutorialCheckpoint.Capture(host.Component);
        Assert.Equal(host.Runtime.StateDigest(), TutorialCheckpoint.Open(Samples.Small, bytes).StateDigest());
        Assert.Throws<ArgumentNullException>(() => TutorialCheckpoint.Capture(null!));
    }

    [Fact]
    public void ACheckpointResumedOnANewSessionGoesOnExactlyAsTheOriginal()
    {
        var live = new TutHost();
        live.Tick(8);
        live.Ask("observe", "e1");
        live.Tick();
        var bytes = TutorialCheckpoint.Capture(live.Component);
        var tick = live.Component.Tick;
        var resumed = new TutHost(runtime: TutorialCheckpoint.Open(Samples.Small, bytes), startTick: tick);
        for (var i = 0; i < 12; i++)
        {
            if (i == 4) { live.Ask("observe", "e2"); resumed.Ask("observe", "e2"); }
            live.Tick(); resumed.Tick();
            Assert.Equal(live.Runtime.StateDigest(), resumed.Runtime.StateDigest());
        }
    }

    // ---- what the events carry -------------------------------------------------------------------------------------------------------

    [Fact]
    public void AHintEventCarriesTheKindTheKeyTheTargetAndTheMotionThePlayersSettingsAllow()
    {
        var host = new TutHost();
        host.Ask("reduced-motion", "", 1);
        host.Tick(11);
        var hints = host.Of<TutorialHintChanged>().ToList();
        Assert.Equal(["nudge:k.h1:t1:none", "explain:k.h2:t1:none"], hints.Select(h => h.Kind + ":" + h.Key + ":" + h.Target + ":" + h.Motion));
        host.Ask("observe", "e1");
        host.Tick();
        var cleared = host.Of<TutorialHintChanged>().Last();
        Assert.Equal((0, "", "", "none"), (cleared.Level, cleared.Kind, cleared.Key, cleared.Motion));
    }

    [Fact]
    public void ATipEventCarriesWhereItEndsAndEnlargedTextLengthensIt()
    {
        var host = new TutHost();
        host.Ask("text-scale", "", 2000);
        host.HostFacts["flag"] = FactValue.From(true);
        host.Tick(2);
        var shown = Assert.Single(host.Of<TutorialTipChanged>());
        Assert.Equal(("shown", "t1", "k.t1", "pulse", 1, 1 + 6), (shown.Change, shown.Tip, shown.Key, shown.Motion, shown.Shows, (int)shown.Until));
        Assert.Contains(new TutorialSettingChanged("tutorial", "", 1, "text_scale", 2000), host.Of<TutorialSettingChanged>());
    }

    [Fact]
    public void AllTheControlsWorkAsRequestsAndEachChangeIsAnEvent()
    {
        var host = new TutHost(start: false);
        host.Runtime.SelectLanguage("fr");
        host.Ask("language", "en"); host.Ask("start"); host.Ask("tips", "", 0); host.Ask("tips", "", 0); host.Ask("reduced-motion", "", 1);
        host.Tick(3);
        host.Ask("skip-step", "s1"); host.Ask("resume"); host.Ask("reset-tips"); host.Ask("replay"); host.Ask("skip-tutorial"); host.Ask("replay-story"); host.Ask("story-next"); host.Ask("story-back"); host.Ask("story-skip");
        host.Tick();
        Assert.Empty(host.Of<TutorialRefused>());
        Assert.Equal(TutorialPhase.Skipped, host.Runtime.Phase);
        Assert.Equal(JourneyState.Done, host.Runtime.Journey.State);
        Assert.Equal(["tips", "reduced_motion"], host.Of<TutorialSettingChanged>().Select(s => s.Name));
        Assert.Equal(["idle -> running", "running -> skipped"], host.Of<TutorialPhaseChanged>().Select(p => p.From + " -> " + p.To));
        Assert.Equal(["done -> done", "done -> story", "story -> story", "story -> story", "story -> done"], host.Of<TutorialJourneyChanged>().Select(j => j.From + " -> " + j.To));
    }
}
