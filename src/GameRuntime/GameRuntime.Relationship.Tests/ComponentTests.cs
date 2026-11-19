using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The component (R13): requests only queue and are answered in Update in arrival order, refusals and answers are events, channels, bounded events with none lost, the clock and the host facts.</summary>
public sealed class ComponentTests
{
    // ---- the deferred rule --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestChangesNothingUntilTheNextUpdateAndIsThenAnsweredWithAnEvent()
    {
        var host = new RelHost();
        var before = host.Component.Fingerprint();
        host.Add("a", "kind");
        Assert.Equal(before, host.Component.Fingerprint());
        Assert.Empty(host.Runtime.Actors);
        Assert.Empty(host.All);
        host.Tick();
        Assert.True(host.Runtime.Actors.ContainsKey("a"));
        Assert.Equal(["kind"], host.Runtime.Actor("a").Traits);
        var applied = Assert.Single(host.Of<RelationshipApplied>());
        Assert.Equal(("relationship", "", "r1", 1L), (applied.Source, applied.Channel, applied.RequestId, applied.Tick));
        Assert.True(host.Component.IsQuiet);
        Assert.Equal(1, host.Component.Answered);
        Assert.Empty(host.Faults.Reports);
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrivedAndEachMayDependOnTheOnesBefore()
    {
        var host = new RelHost();
        host.Add("a");
        host.Add("b");
        host.Session.Submit(new RelationshipInteractRequested("chat-1", "chat", "a", "b"));
        host.Session.Submit(new RelationshipInteractRequested("chat-2", "chat", "b", "a"));
        host.Tick();
        var names = host.Transcript.Lines.Select(l => l[..l.IndexOf('{', StringComparison.Ordinal)]).ToList();
        Assert.Equal(["RelationshipApplied", "RelationshipApplied", "RelationshipInteractionPerformed"], names.Take(3));
        var performed = host.Of<RelationshipInteractionPerformed>().ToList();
        Assert.Equal(["chat-1", "chat-2"], performed.Select(p => p.RequestId));
        Assert.All(performed, p => Assert.True(p.Accepted));
        Assert.Equal(30, host.Runtime.Opinion("a", "b", 0));   // each chat moves both sides by 15
    }

    [Fact]
    public void ARequestRaisedByAnotherComponentDuringATickIsAnsweredInTheNextUpdateOfTheComponent()
    {
        var emitter = new Emitter();
        var host = new RelHost(extra: [(emitter, 5)]);
        host.Tick();        // the emitter ran before the component and raised a request at tick 1; it is delivered after the emitter and answered in the same tick, as the component runs later
        Assert.True(host.Runtime.Actors.ContainsKey("emitted"));
    }

    private sealed class Emitter : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "emitter";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context) { if (context.Tick == 1) _events!.Emit(new RelationshipActorAddRequested("from-emitter", "emitted", "")); }
    }

    // ---- refusals -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABadRequestIsRefusedWithItsCodeAndLeavesTheStateExactlyAsItWas()
    {
        var host = new RelHost();
        host.Add("a");
        host.Tick();
        var digest = host.Runtime.StateDigest();
        host.Session.Submit(new RelationshipActivityRequested("bad-1", "zed", "eat"));
        host.Session.Submit(new RelationshipInteractRequested("bad-2", "chat", "a", "a"));
        host.Session.Submit(new RelationshipGrantRequested("bad-3", "a", "no-such-moodlet"));
        host.Session.Submit(new RelationshipActorAddRequested("bad-4", "a", ""));
        host.Session.Submit(new RelationshipActorAddRequested("bad-5", "b", "", "pie=9"));
        host.Session.Submit(new RelationshipActorAddRequested("bad-6", "c", "", "pie"));
        host.Tick();
        Assert.Equal([("bad-1", RelationshipCodes.UnknownActor), ("bad-2", RelationshipCodes.Self), ("bad-3", RelationshipCodes.UnknownMoodlet), ("bad-4", RelationshipCodes.DuplicateActor),
            ("bad-5", RelationshipCodes.Taste), ("bad-6", RelationshipCodes.Taste)], host.Of<RelationshipRefused>().Select(r => (r.RequestId, r.Code)));
        Assert.Equal(digest, host.Runtime.StateDigest());
        Assert.Equal(7, host.Component.Answered);
    }

    [Fact]
    public void ASizeBeyondAPermilleIsRefusedAsARange()
    {
        var host = new RelHost();
        host.Add("a");
        host.Session.Submit(new RelationshipGrantRequested("g", "a", "insulted", 100_000));
        host.Tick();
        Assert.Equal(RelationshipCodes.Range, Assert.Single(host.Of<RelationshipRefused>()).Code);
    }

    // ---- every kind of request ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryKindOfRequestDoesWhatItsNameSaysAndIsAnswered()
    {
        var host = new RelHost();
        host.Add("a", "grumpy;glutton", "flower=2;food=-1");
        host.Add("b");
        host.Session.Submit(new RelationshipActivityRequested(host.NextId(), "a", "eat"));
        host.Session.Submit(new RelationshipGrantRequested(host.NextId(), "a", "festival-joy", 500, "b"));
        host.Session.Submit(new RelationshipRememberRequested(host.NextId(), "a", "b", "shared-feast", 1000));
        host.Session.Submit(new RelationshipNeedRequested(host.NextId(), "b", "fun", -100));
        host.Session.Submit(new RelationshipAxisRequested(host.NextId(), "b", "a", "trust", 70));
        host.Session.Submit(new RelationshipBondRequested(host.NextId(), "a", "b", "sworn-rival", true));
        host.Tick();
        Assert.Empty(host.Of<RelationshipRefused>());
        Assert.Equal(8, host.Of<RelationshipApplied>().Count());
        var a = host.Runtime.Actor("a");
        Assert.Equal("eat", a.Activity);
        Assert.Equal(["flower=2", "food=-1"], a.Tastes.Select(t => t.Key + "=" + t.Value));
        Assert.Equal(150, host.Runtime.MoodletValue(a.Moodlets.Single()));
        Assert.Equal("b", a.Moodlets.Single().Source);
        Assert.Equal(80, host.Runtime.Opinion("a", "b", 0));
        Assert.Equal(500, host.Runtime.NeedLevel(host.Runtime.Actor("b"), 3));
        Assert.Equal(70, host.Runtime.Opinion("b", "a", 1));
        Assert.True(host.Runtime.HasBond("a", "b", 1));
        Assert.Contains(host.Of<RelationshipBondChanged>(), e => e is { Actor: "a", Target: "b", Bond: "sworn-rival", Set: true });
        Assert.Contains(host.Of<RelationshipMemoryAdded>(), e => e is { Holder: "a", Subject: "b", Memory: "shared-feast", Origin: "" });
        Assert.Contains(host.Of<RelationshipMoodletAdded>(), e => e is { Actor: "a", Moodlet: "festival-joy", From: "b" });
        host.Session.Submit(new RelationshipActorRemoveRequested(host.NextId(), "b"));
        host.Tick();
        Assert.False(host.Runtime.Actors.ContainsKey("b"));
        Assert.Null(host.Runtime.Edge("a", "b"));
    }

    [Fact]
    public void AnInteractionRequestRaisesThePerformedEventAndThenEveryChangeItMade()
    {
        var host = new RelHost();
        host.Add("giver", "", "");
        host.Add("lover", "", "flower=2");
        host.Add("witness");
        host.Session.Submit(new RelationshipInteractRequested("gift", "give-gift", "giver", "lover", "witness", "flowers"));
        host.Tick();
        var performed = Assert.Single(host.Of<RelationshipInteractionPerformed>());
        Assert.Equal(("give-gift", "giver", "lover", true), (performed.Interaction, performed.Actor, performed.Target, performed.Accepted));
        var memories = host.Of<RelationshipMemoryAdded>().ToList();
        Assert.Equal([("lover", "giver", "was-gifted"), ("witness", "giver", "saw-kindness")], memories.Select(m => (m.Holder, m.Subject, m.Memory)));
        Assert.Contains(host.Of<RelationshipMoodletAdded>(), m => m is { Actor: "lover", Moodlet: "gift-received" });
        var lines = host.Transcript.Lines.Select(l => l[..l.IndexOf('{', StringComparison.Ordinal)]).ToList();
        Assert.True(lines.IndexOf("RelationshipInteractionPerformed") < lines.IndexOf("RelationshipMemoryAdded"));
    }

    [Fact]
    public void AMoodRequestAndAnOpinionRequestAreAnsweredWithTheValueTheStateAndTheTerms()
    {
        var host = new RelHost();
        host.Add("a", "kind");
        host.Add("b");
        host.Session.Submit(new RelationshipRememberRequested("m", "a", "b", "was-gifted", 1000));
        host.Session.Submit(new RelationshipMoodRequested("mood", "a"));
        host.Session.Submit(new RelationshipOpinionRequested("opinion", "a", "b", "affinity"));
        host.Tick();
        var mood = Assert.Single(host.Of<RelationshipMoodAnswered>());
        var expected = RelationshipExplain.Mood(host.Runtime, "a");
        Assert.Equal((expected.Value, expected.State, expected.Sum), (mood.Mood, mood.State, mood.Sum));
        Assert.StartsWith("base|mood|0;traits|kind|40;need|hunger|240", mood.Terms, StringComparison.Ordinal);
        var opinion = Assert.Single(host.Of<RelationshipOpinionAnswered>());
        Assert.Equal((120L, "friendship acquaintance"), (opinion.Opinion, opinion.State));
        Assert.Equal("base|affinity|0;memory|was-gifted|120", opinion.Terms);
        Assert.Equal(("a", "b", "affinity"), (opinion.From, opinion.To, opinion.Axis));
        Assert.Equal(0, host.Of<RelationshipApplied>().Count(e => e.RequestId is "mood" or "opinion"));   // a query changes nothing, so it is only answered
    }

    // ---- channels -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestOnAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = new RelHost();
        host.Session.Submit(new RelationshipActivityRequested("lost", "a", "eat", "nowhere"));
        host.Tick();
        var refused = Assert.Single(host.Of<RelationshipRefused>());
        Assert.Equal(("relationship-channels", "nowhere", "lost", 1L, RelationshipChannels.UnknownChannel), (refused.Source, refused.Channel, refused.RequestId, refused.Tick, refused.Code));
        Assert.True(host.Channels.IsQuiet);
        Assert.Equal(1, host.Channels.Refused);
    }

    [Fact]
    public void TwoVillagesOnTwoChannelsEachAnswerOnlyTheirOwnRequests()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new RelationshipChannels();
        channels.Install(session);
        var north = new RelationshipRuntime[] { RelationshipRuntime.Create(Samples.Village) }[0];
        var south = RelationshipRuntime.Create(Samples.Village);
        var facts = Samples.Host();
        session.AddComponent(new RelationshipComponent(north, new TickRelationshipClock(50), () => facts, channels, "north", "north"), 10);
        session.AddComponent(new RelationshipComponent(south, new TickRelationshipClock(50), () => facts, channels, "south", "south"), 11);
        var refusals = new List<RelationshipRefused>();
        var applied = new List<RelationshipApplied>();
        session.Subscribe<RelationshipRefused>(refusals.Add);
        session.Subscribe<RelationshipApplied>(applied.Add);
        session.Init();
        session.Submit(new RelationshipActorAddRequested("n", "ned", "", "", "north"));
        session.Submit(new RelationshipActorAddRequested("s", "sue", "", "", "south"));
        session.Update(20_000);
        Assert.Equal(["ned"], north.Actors.Keys);
        Assert.Equal(["sue"], south.Actors.Keys);
        Assert.Equal([("north", "n"), ("south", "s")], applied.Select(a => (a.Source, a.RequestId)).OrderBy(p => p.Source, StringComparer.Ordinal));
        Assert.Empty(refusals);
        Assert.Equal(["north", "south"], channels.Served.Keys);
    }

    [Fact]
    public void ASecondComponentOnAServedChannelIsRefusedAtInit()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new RelationshipChannels();
        channels.Install(session);
        var facts = Samples.Host();
        session.AddComponent(new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(50), () => facts, channels, "one"), 10);
        session.AddComponent(new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(50), () => facts, channels, "two"), 11);
        Assert.Equal(RelationshipChannels.DuplicateChannel, Assert.Throws<EngineRefusedException>(session.Init).Code);
    }

    [Fact]
    public void AComponentWhoseRegistryIsNotRunningOnItsSessionFailsItsFirstUpdate()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        new RelationshipChannels().Install(session);
        var facts = Samples.Host();
        var elsewhere = new RelationshipChannels();
        var other = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        elsewhere.Install(other);
        other.Init();
        session.AddComponent(new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(50), () => facts, elsewhere), 10);
        session.Init();
        session.Update(20_000);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Contains(reporter.Reports, r => r.StartsWith("RUNTIME_FAULT", StringComparison.Ordinal) && r.Contains("does not run on this session", StringComparison.Ordinal));
    }

    [Fact]
    public void ConstructorArgumentsAreChecked()
    {
        var facts = Samples.Host();
        var channels = new RelationshipChannels();
        var runtime = RelationshipRuntime.Create(Samples.Village);
        Assert.Throws<ArgumentNullException>(() => new RelationshipComponent(null!, new TickRelationshipClock(1), () => facts, channels));
        Assert.Throws<ArgumentNullException>(() => new RelationshipComponent(runtime, null!, () => facts, channels));
        Assert.Throws<ArgumentNullException>(() => new RelationshipComponent(runtime, new TickRelationshipClock(1), null!, channels));
        Assert.Throws<ArgumentNullException>(() => new RelationshipComponent(runtime, new TickRelationshipClock(1), () => facts, null!));
        Assert.Throws<ArgumentException>(() => new RelationshipComponent(runtime, new TickRelationshipClock(1), () => facts, channels, maxEvents: 0));
        Assert.Equal(RelationshipCodes.Range, Fixture.Code(() => new TickRelationshipClock(0)));
        Assert.Equal(RelationshipCodes.StepOrder, Fixture.Code(() => new TickRelationshipClock(1, -1)));
    }

    // ---- time -----------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheStepFollowsTheClockOfTheSessionAndIsAnnouncedWhenItMoves()
    {
        var host = new RelHost(ticksPerStep: 50);
        host.Tick(49);
        Assert.Empty(host.Of<RelationshipStepped>());
        host.Tick();
        var stepped = Assert.Single(host.Of<RelationshipStepped>());
        Assert.Equal((0L, 1L, 0L, 50L), (stepped.FromStep, stepped.Step, stepped.Owed, stepped.Tick));
        host.Tick(100);
        Assert.Equal(3, host.Runtime.Step);
        Assert.Equal(new long[] { 1, 2, 3 }, host.Of<RelationshipStepped>().Select(s => s.Step));
        Assert.Equal(9, new TickRelationshipClock(50, 7).StepAt(100));
    }

    [Fact]
    public void ALongJumpIsPaidInBoundedStepsAndTheOwedCountFalls()
    {
        var host = new RelHost(ticksPerStep: 1, startTick: 2000);
        host.Tick(5);
        Assert.Equal([(0L, 480L, 1521L), (480L, 960L, 1042L), (960L, 1440L, 563L), (1440L, 1920L, 84L), (1920L, 2005L, 0L)], host.Of<RelationshipStepped>().Select(s => (s.FromStep, s.Step, s.Owed)));
        Assert.Equal(2005, host.Runtime.Step);
    }

    [Fact]
    public void AHostFactTheDecayRulesNeedAndTheHostDidNotGiveIsReportedOnceAndNothingMoves()
    {
        var host = new RelHost(ticksPerStep: 1);
        host.Add("a");
        host.HostFacts.Remove("cal.season");
        host.Tick(3);
        var refused = Assert.Single(host.Of<RelationshipRefused>());
        Assert.Equal((RelationshipComponent.AdvanceRequestId, RelationshipCodes.FactMissing), (refused.RequestId, refused.Code));
        Assert.Equal(0, host.Runtime.Step);
        host.HostFacts["cal.season"] = FactValue.From("spring");
        host.Tick();
        Assert.Equal(4, host.Runtime.Step);          // the fact is back: the step catches up to the tick at once
        host.HostFacts.Remove("cal.season");
        host.Tick(2);
        Assert.Equal(2, host.Of<RelationshipRefused>().Count());   // a new failure after a success is reported again
    }

    private sealed class BackwardsClock : IRelationshipClock
    {
        private int _calls;
        public long StepAt(long tick) => _calls++ == 0 ? 5 : 3;
    }

    [Fact]
    public void AClockThatGoesBackwardsIsReportedOnceAsAClockFaultAndNothingMoves()
    {
        var host = new RelHost(clock: new BackwardsClock());
        host.Tick(4);
        Assert.Equal(5, host.Runtime.Step);
        var refused = Assert.Single(host.Of<RelationshipRefused>());
        Assert.Equal((RelationshipComponent.ClockRequestId, RelationshipCodes.StepOrder), (refused.RequestId, refused.Code));
    }

    // ---- bounded events, none lost ----------------------------------------------------------------------------------------------------------

    private static readonly HashSet<string> ChangeEvents = ["RelationshipMoodChanged", "RelationshipNeedCritical", "RelationshipStateChanged", "RelationshipBondChanged", "RelationshipMemoryAdded", "RelationshipMoodletAdded", "RelationshipEvicted", "RelationshipCommunityChanged", "RelationshipStandingChanged", "RelationshipBalanceShifted"];

    private static RelHost Crowd(int maxEvents)
    {
        var host = new RelHost(ticksPerStep: 1, maxEvents: maxEvents);
        for (var i = 0; i < 20; i++) host.Add("p" + i.ToString("00"), i % 3 == 0 ? "glutton" : i % 3 == 1 ? "grumpy;sociable" : "");
        return host;
    }

    private static string Name(string line) => line[..line.IndexOf('{', StringComparison.Ordinal)];

    [Fact]
    public void ChangeEventsAreRaisedAtMostTheMaximumPerUpdateTheRestAreOwedAndNoneIsLostOrReordered()
    {
        var small = Crowd(4);
        var large = Crowd(100_000);
        var perTick = new List<int>();
        var sawOwed = false;
        var countBefore = 0;
        for (var tick = 0; tick < 70; tick++)
        {
            small.Tick();
            large.Tick();
            var count = small.Transcript.Lines.Count(l => ChangeEvents.Contains(Name(l)));
            perTick.Add(count - countBefore);
            countBefore = count;
            if (small.Component.Owed > 0)
            {
                sawOwed = true;
                Assert.False(small.Component.IsQuiet);
                Assert.Equal(RelationshipComponent.NotQuiet, Fixture.Code(() => RelationshipCheckpoint.Capture(small.Component)));
            }
        }
        Assert.True(sawOwed, "the crowd should have produced more than four changes in one step");
        Assert.All(perTick, n => Assert.InRange(n, 0, 4));
        for (var guard = 0; small.Component.Owed > 0 && guard < 5000; guard++) small.Tick();
        Assert.Equal(0, small.Component.Owed);
        var smallChanges = small.LinesWithoutTicks().Where(l => ChangeEvents.Contains(Name(l))).ToList();
        for (var guard = 0; large.Component.Owed > 0 && guard < 5000; guard++) large.Tick();
        var largeChanges = large.LinesWithoutTicks().Where(l => ChangeEvents.Contains(Name(l))).ToList();
        Assert.Equal(largeChanges.Take(smallChanges.Count), smallChanges);
        Assert.True(smallChanges.Count > 50);
    }

    // ---- checkpoint ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACheckpointIsRefusedWhileARequestWaitsAndTakenWhenNothingDoes()
    {
        RelHost? holder = null;
        var seen = new List<string>();
        var host = new RelHost(extra: [(new Probe(() => { if (!holder!.Component.IsQuiet) seen.Add(Fixture.Code(() => RelationshipCheckpoint.Capture(holder.Component))); }), 5)]);
        holder = host;
        host.Add("a");
        host.Tick();
        Assert.Equal([RelationshipComponent.NotQuiet], seen);   // the probe ran after the request was delivered and before the component answered it
        var bytes = RelationshipCheckpoint.Capture(host.Component);
        Assert.Equal(RelationshipSave.Write(host.Runtime), bytes);
        var opened = RelationshipCheckpoint.Open(Samples.Village, bytes);
        Assert.Equal(host.Runtime.StateDigest(), opened.StateDigest());
        Assert.Equal(1, host.Component.Tick);
    }

    private sealed class Snapshotter(RelationshipComponent component, List<string> codes) : IGameComponent
    {
        public string Id => "snapshotter";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (!component.Channels.IsQuiet) codes.Add(Fixture.Code(() => RelationshipCheckpoint.Capture(component)));
        }
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARefusalForAnUnservedChannelIsWaitingInTheRegistry()
    {
        var codes = new List<string>();
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new RelationshipChannels();
        channels.Install(session);
        var facts = Samples.Host();
        var component = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(50), () => facts, channels);
        session.AddComponent(component, 10);
        session.AddComponent(new Snapshotter(component, codes), 40);   // runs before the registry, which has the request but has not answered it
        session.Init();
        session.Submit(new RelationshipActivityRequested("lost", "a", "eat", "nowhere"));
        session.Update(20_000);
        Assert.Equal([RelationshipComponent.NotQuiet], codes);
        Assert.True(channels.IsQuiet);
        Assert.NotEmpty(RelationshipCheckpoint.Capture(component));
    }

    [Fact]
    public void TheFingerprintNamesTheTickTheStepTheStateAndWhatIsWaiting()
    {
        var host = new RelHost(ticksPerStep: 1);
        host.Add("a");
        host.Tick(3);
        var fingerprint = host.Component.Fingerprint();
        Assert.StartsWith("relationship tick=3 step=3 state=" + host.Runtime.StateDigest()[..16], fingerprint, StringComparison.Ordinal);
        Assert.EndsWith(" answered=1 pending=0 owed=0", fingerprint, StringComparison.Ordinal);
    }
}

internal static class Fixture
{
    public static string Code(Action action)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException("the call was not refused");
    }
}

internal sealed class Probe(Action act) : IGameComponent
{
    public string Id => "probe";
    public void Init(in ComponentContext context) { }
    public void Update(in UpdateContext context) => act();
}
