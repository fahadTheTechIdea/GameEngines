using System.Text.Json.Nodes;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The component: time and events, requests and their answers, channels, back-pressure, faults of the clock and the host, checkpoints.</summary>
public sealed class ComponentTests
{
    private static DipSet WithHost() => Samples.Calm(root => Samples.Realm(root, "cinder")["control"] = "host");

    // ---- time and events ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheDiplomacyLivesToTheStepOfTheClockInTheUpdateAndRaisesWhatHappenedInOrder()
    {
        var host = new DiplomacyHost();
        host.Tick(3);
        Assert.Equal(3, host.Runtime.Step);
        var season = Assert.Single(host.Of<DiplomacySeasonChanged>());
        Assert.Equal(("spring", "", 0L), (season.Season, season.From, season.Step));
        Assert.Equal("diplomacy", season.Source);
        Assert.Equal("", season.Channel);
        host.Runtime.Propose("aurel", "brass", "embargo", 30, 0);
        Assert.Empty(host.Of<DiplomacyTreatyChanged>());       // a call made on the runtime directly raises nothing: the component raises what it answers and what time did
    }

    [Fact]
    public void ARequestOnlyQueuesAndIsAnsweredInTheNextUpdate()
    {
        var host = new DiplomacyHost();
        host.Tick();
        var id = host.Ask("propose", "aurel", "brass", "nap", 30, 0);
        Assert.Empty(host.Of<DiplomacyApplied>());
        Assert.Equal(0, host.Component.Answered);
        host.Tick();
        var applied = Assert.Single(host.Of<DiplomacyApplied>());
        Assert.Equal((id, "diplomacy", host.Component.Tick), (applied.RequestId, applied.Source, applied.Tick));
        var made = Assert.Single(host.Of<DiplomacyProposalChanged>());
        Assert.Equal(("made", "nap", "aurel", "brass"), (made.Change, made.Type, made.From, made.To));
        Assert.Equal(1, host.Component.Answered);
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrivedEachWithItsOwnAnswer()
    {
        var host = new DiplomacyHost();
        var a = host.Ask("propose", "aurel", "brass", "nap", 30);
        var b = host.Ask("propose", "aurel", "nowhere", "nap", 30);
        var c = host.Ask("recruit", "dune", "agent-dune-2", number: 400);
        host.Tick();
        var answers = host.All.Where(e => e is DiplomacyApplied or DiplomacyRefused).Select(e => e is DiplomacyApplied applied ? (applied.RequestId, "applied") : (((DiplomacyRefused)e).RequestId, ((DiplomacyRefused)e).Code)).ToList();
        Assert.Equal([(a, "applied"), (b, DipCodes.UnknownRealm), (c, "applied")], answers);
    }

    [Fact]
    public void ARequestOfAnUnknownKindIsRefusedNotDropped()
    {
        var host = new DiplomacyHost();
        var id = host.Ask("dance", "a");
        host.Tick();
        var refused = Assert.Single(host.Of<DiplomacyRefused>());
        Assert.Equal((id, DiplomacyComponent.UnknownKind), (refused.RequestId, refused.Code));
    }

    [Fact]
    public void EveryKindOfRequestIsServedAndNoneIsTakenForUnknown()
    {
        var host = new DiplomacyHost(WithHost());
        host.Tick(2);
        var kinds = Enum.GetValues<DiplomacyRequestKind>();
        var ids = new Dictionary<DiplomacyRequestKind, string>
        {
            [DiplomacyRequestKind.Propose] = host.Ask("propose", "aurel", "cinder", "nap", 30),
            [DiplomacyRequestKind.Respond] = host.Ask("respond", number: 0, number2: 1),
            [DiplomacyRequestKind.Withdraw] = host.Ask("withdraw", "aurel", number: 5),
            [DiplomacyRequestKind.Cancel] = host.Ask("cancel", "aurel", number: 0),
            [DiplomacyRequestKind.Declare] = host.Ask("declare", "aurel", "ember", ""),
            [DiplomacyRequestKind.Join] = host.Ask("join", "dune", "aurel", number: 0),
            [DiplomacyRequestKind.Leave] = host.Ask("leave", "dune", number: 0),
            [DiplomacyRequestKind.Score] = host.Ask("score", "aurel", "battle", number: 0, number2: 500),
            [DiplomacyRequestKind.Fulfil] = host.Ask("fulfil", number: 0, number2: 1),
            [DiplomacyRequestKind.Mission] = host.Ask("mission", "agent-brass-1", "aurel", "spy"),
            [DiplomacyRequestKind.Recruit] = host.Ask("recruit", "dune", "agent-dune-2", number: 400),
            [DiplomacyRequestKind.Release] = host.Ask("release", "agent-cinder-1"),
            [DiplomacyRequestKind.Grant] = host.Ask("grant", "brass", "dune", "border", number: 30),
            [DiplomacyRequestKind.Deed] = host.Ask("deed", "aurel", "dune", "gift", number: 500),
            [DiplomacyRequestKind.Happen] = host.Ask("happen", "aurel", "disaster", number: 100),
            [DiplomacyRequestKind.Succeed] = host.Ask("succeed", "ember"),
            [DiplomacyRequestKind.Might] = host.Ask("might", "dune", number: 260000),
            [DiplomacyRequestKind.Leader] = host.Ask("leader", "dune", "aurel", number: 100),
            [DiplomacyRequestKind.Evaluate] = host.Ask("evaluate", "aurel", "brass", "nap", 30, 0),
            [DiplomacyRequestKind.Standing] = host.Ask("standing", "aurel", "brass"),
            [DiplomacyRequestKind.Estimate] = host.Ask("estimate", "brass", "aurel"),
        };
        Assert.Equal(kinds.Length, ids.Count);
        host.Tick();
        foreach (var (kind, id) in ids)
        {
            var answers = host.All.Where(e => e switch { DiplomacyApplied a => a.RequestId == id, DiplomacyRefused r => r.RequestId == id, DiplomacyEvaluated v => v.RequestId == id, DiplomacyStandingAnswered s => s.RequestId == id, DiplomacyEstimated x => x.RequestId == id, _ => false }).ToList();
            Assert.True(answers.Count == 1, kind + " got " + answers.Count + " answers");
            if (answers[0] is DiplomacyRefused refused) Assert.NotEqual(DiplomacyComponent.UnknownKind, refused.Code);
        }
        Assert.Equal(kinds.Length, host.Component.Answered);
        Assert.Equal(new[] { "propose", "respond", "withdraw", "cancel", "declare", "join", "leave", "score", "fulfil", "mission", "recruit", "release", "grant", "deed", "happen", "succeed", "might", "leader", "evaluate", "standing", "estimate" }, kinds.Select(k => DiplomacyRequestNames.NameOf(k)).ToArray());
        Assert.All(kinds, k => Assert.Equal(k, DiplomacyRequestNames.KindOf(DiplomacyRequestNames.NameOf(k))));
        Assert.Null(DiplomacyRequestNames.KindOf("dance"));
    }

    [Fact]
    public void TheRequestsThatChangeThingsDoTheSameAsTheRuntimeCalls()
    {
        var host = new DiplomacyHost(WithHost());
        host.Tick();
        host.Ask("propose", "aurel", "cinder", "nap", 30);
        host.Tick();
        host.Ask("respond", number: 0, number2: 1);
        host.Ask("declare", "ember", "dune", "");
        host.Ask("score", "ember", "battle", number: 0, number2: 1000);
        host.Ask("recruit", "dune", "agent-dune-2", number: 400);
        host.Ask("mission", "agent-dune-2", "aurel", "spy");
        host.Ask("grant", "aurel", "cinder", "border", number: 20);
        host.Ask("deed", "aurel", "dune", "gift", number: 800);
        host.Ask("happen", "ember", "disaster", number: 500);
        host.Ask("might", "dune", number: 123456);
        host.Ask("leader", "dune", "aurel", number: 300);
        host.Tick();
        var runtime = host.Runtime;
        Assert.Single(runtime.Treaties);
        Assert.Single(runtime.Wars);
        Assert.Equal(300, runtime.Wars[0].Score);
        Assert.Equal(2, runtime.Agents.Count(a => a.Owner == 3));
        Assert.Single(runtime.Missions);
        Assert.True(runtime.Holds("aurel", "cinder", "border"));
        Assert.Equal(123456, runtime.Realms[3].Might);
        Assert.Equal(300, runtime.State.Leader[3 * 5 + 0]);
        Assert.Empty(host.Of<DiplomacyRefused>());
    }

    [Fact]
    public void ANumberTooBigForTheEngineIsRefusedAndNeverWrapsRoundIntoAnother()
    {
        var host = new DiplomacyHost();
        host.Ask("propose", "aurel", "brass", "nap", number: 4_294_967_296L + 30);
        host.Ask("score", "aurel", "battle", number: 0, number2: 4_294_967_296L + 5);
        host.Ask("grant", "brass", "dune", "border", number: -1);
        host.Tick();
        Assert.Equal([DipCodes.Terms, DipCodes.Terms, DipCodes.Terms], host.Of<DiplomacyRefused>().Select(r => r.Code).ToArray());
        Assert.Empty(host.Runtime.Proposals);
    }

    [Fact]
    public void TheQueriesAnswerFromTheState()
    {
        var host = new DiplomacyHost();
        host.Tick();
        host.Runtime.Remember("aurel", "brass", "gift", 1000);
        host.Ask("evaluate", "aurel", "brass", "nap", 30, 0);
        host.Ask("standing", "brass", "aurel");
        host.Ask("standing", "aurel");
        host.Ask("estimate", "brass", "aurel");
        host.Tick();
        var evaluated = Assert.Single(host.Of<DiplomacyEvaluated>());
        Assert.Equal(("aurel", "brass", "nap", 1000), (evaluated.From, evaluated.To, evaluated.Type, evaluated.Chance));
        Assert.Equal(4900, evaluated.Score);                      // 5000 less the 100 of brass being shady
        Assert.StartsWith("  bias: 1000 x 5000 / 1000 = 5000\n  traits: 1000 x -100 / 1000 = -100", evaluated.Why);
        var standings = host.Of<DiplomacyStandingAnswered>().ToList();
        Assert.Equal(2, standings.Count);
        Assert.Equal(("brass", "aurel", "stable", host.Runtime.Realms[1].Stability, 0L, 0), (standings[0].Realm, standings[0].Other, standings[0].Band, standings[0].Stability, standings[0].Trust, standings[0].Wars));
        Assert.True(standings[0].Regard > 0 || standings[0].Reverse > 0);
        Assert.Equal(host.Runtime.RegardOf("brass", "aurel"), standings[0].Regard);
        Assert.Equal(host.Runtime.RegardOf("aurel", "brass"), standings[0].Reverse);
        Assert.False(standings[0].AtWar);
        Assert.Equal(("aurel", "", host.Runtime.Realms[0].Stability, host.Runtime.Realms[0].Trust / 1000), (standings[1].Realm, standings[1].Other, standings[1].Stability, standings[1].Trust));
        var estimate = Assert.Single(host.Of<DiplomacyEstimated>());
        Assert.Equal(("brass", "aurel", "none"), (estimate.Observer, estimate.Target, estimate.Level));
        Assert.Equal(estimate.Estimate * 2, estimate.High);
    }

    [Fact]
    public void AQueryThatNamesWhatIsNotThereIsRefused()
    {
        var host = new DiplomacyHost();
        host.Ask("standing", "nobody");
        host.Ask("standing", "aurel", "nobody");
        host.Ask("standing", "aurel", "aurel");
        host.Ask("evaluate", "aurel", "brass", "nothing", 30, 0);
        host.Ask("estimate", "aurel", "aurel");
        host.Tick();
        Assert.Equal([DipCodes.UnknownRealm, DipCodes.UnknownRealm, DipCodes.SameRealm, DipCodes.UnknownType, DipCodes.SameRealm], host.Of<DiplomacyRefused>().Select(r => r.Code).ToArray());
    }

    // ---- channels -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestForAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = new DiplomacyHost();
        host.Ask("propose", "aurel", "brass", "nap", 30, 0, channel: "elsewhere");
        host.Tick();
        var refused = Assert.Single(host.Of<DiplomacyRefused>());
        Assert.Equal((DiplomacyChannels.ComponentId, "elsewhere", DiplomacyChannels.UnknownChannel), (refused.Source, refused.Channel, refused.Code));
        Assert.Empty(host.Of<DiplomacyApplied>());
        Assert.True(host.Channels.IsQuiet);
        Assert.Equal(1, host.Channels.Refused);
    }

    [Fact]
    public void TwoDiplomaciesOnTwoChannelsAreSeparate()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var registry = new DiplomacyChannels();
        registry.Install(session);
        var one = DipRuntime.Create(Samples.Calm());
        var two = DipRuntime.Create(Samples.Calm());
        var a = new DiplomacyComponent(one, new TickDiplomacyClock(one.Set), () => Samples.Crisis(), registry, "north-diplomacy", "north");
        var b = new DiplomacyComponent(two, new TickDiplomacyClock(two.Set), () => Samples.Crisis(), registry, "south-diplomacy", "south");
        session.AddComponent(a, 10);
        session.AddComponent(b, 11);
        var seen = new List<DiplomacyApplied>();
        session.Subscribe<DiplomacyApplied>(seen.Add);
        session.Init();
        session.Submit(new DiplomacyRequested("q1", "declare", "aurel", "brass", "", 0, 0, "north"));
        session.Submit(new DiplomacyRequested("q2", "might", "aurel", "", "", 5, 0, "south"));
        for (var i = 0; i < 2; i++) Assert.Equal(1, session.Update(20_000));
        Assert.Equal([("north-diplomacy", "north", "q1"), ("south-diplomacy", "south", "q2")], seen.Select(s => (s.Source, s.Channel, s.RequestId)).OrderBy(s => s.Source).ToArray());
        Assert.Single(one.Wars);
        Assert.Empty(two.Wars);
        Assert.Equal(5, two.Realms[0].Might);
        Assert.Equal(500000, one.Realms[0].Might);
    }

    [Fact]
    public void ASecondComponentOnTheSameChannelIsRefusedAtInit()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var registry = new DiplomacyChannels();
        registry.Install(session);
        var one = DipRuntime.Create(Samples.Calm());
        var two = DipRuntime.Create(Samples.Calm());
        session.AddComponent(new DiplomacyComponent(one, new TickDiplomacyClock(one.Set), () => Samples.Crisis(), registry, "one"), 10);
        session.AddComponent(new DiplomacyComponent(two, new TickDiplomacyClock(two.Set), () => Samples.Crisis(), registry, "two"), 11);
        Assert.Equal(DiplomacyChannels.DuplicateChannel, Samples.Code(() => session.Init()));
    }

    [Fact]
    public void AComponentWhoseRegistryDoesNotRunOnItsSessionFailsItsFirstUpdate()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        var other = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        new DiplomacyChannels().Install(session);
        var channels = new DiplomacyChannels();
        channels.Install(other);
        other.Init();
        var runtime = DipRuntime.Create(Samples.Calm());
        session.AddComponent(new DiplomacyComponent(runtime, new TickDiplomacyClock(runtime.Set), () => Samples.Crisis(), channels), 1);
        session.Init();
        Assert.Equal(0, session.Update(20_000));
        Assert.Contains(reporter.Reports, r => r.Contains("does not run on this session", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRegistryHasItsOwnRankAndItsOwnCodes()
    {
        Assert.Equal("DIP_CHANNEL_UNKNOWN", DiplomacyChannels.UnknownChannel);
        Assert.Equal("DIP_CHANNEL_DUPLICATE", DiplomacyChannels.DuplicateChannel);
        Assert.Equal("diplomacy-channels", DiplomacyChannels.ComponentId);
        var channels = new DiplomacyChannels();
        Assert.Equal("diplomacy-channels", channels.Id);
    }

    // ---- bounded events -------------------------------------------------------------------------------------------------------------

    private static DipSet Lively() => Samples.Calm(root =>
    {
        foreach (var realm in root["realms"]!.AsArray()) realm!["successionPpm"] = 80000;
        Samples.Rules(root, "balance")["interval"] = 3;
        Samples.Rules(root, "balance")["threatLine"] = 250;
        Samples.Rules(root, "balance")["relaxLine"] = 150;
    });

    /// <summary>A clock that goes to step 40 at once and stays there, so every event of forty steps is made in the first Update.</summary>
    private sealed class ToFortyClock(DipSet set) : IDiplomacyClock
    {
        public long StepAt(long tick) => 40;
        public string SeasonAt(long step) => set.CycleSeasonAt(step);
    }

    [Fact]
    public void EventsAreRaisedAtMostMaxEventsPerUpdateAndTheRestIsOwedInOrderNoneLost()
    {
        var limited = new DiplomacyHost(Lively(), maxEvents: 3, clock: new ToFortyClock(Lively()));
        var free = new DiplomacyHost(Lively(), clock: new ToFortyClock(Lively()));
        var previous = 0;
        for (var tick = 1; tick <= 10; tick++)
        {
            limited.Tick();
            free.Tick();
            Assert.True(limited.Transcript.Lines.Count - previous <= 3, "tick " + tick + " raised " + (limited.Transcript.Lines.Count - previous));
            previous = limited.Transcript.Lines.Count;
        }
        Assert.True(limited.Component.Owed > 0, "the cap was never reached");
        Assert.Equal(0, free.Component.Owed);
        var guard = 0;
        while (limited.Component.Owed > 0 && guard++ < 10_000) limited.Tick();
        Assert.Equal(0, limited.Component.Owed);
        Assert.True(free.Transcript.Lines.Count > 40, "the lively sample should raise events: " + free.Transcript.Lines.Count);
        Assert.Equal(free.LinesWithoutTicks(), limited.LinesWithoutTicks());
        Assert.Equal(free.Runtime.StateDigest(), limited.Runtime.StateDigest());
    }

    [Fact]
    public void WhenTheOwedQueueIsFullTimeWaitsForTheHostAndNothingIsLost()
    {
        var reference = new DiplomacyHost(Lively(), maxSteps: 1);
        reference.Tick(30);
        var host = new DiplomacyHost(Lively(), maxEvents: 1, maxOwed: 5, maxSteps: 1);
        host.Tick(30);
        Assert.True(host.Component.Held > 0, "time should have waited");
        Assert.True(host.Runtime.Step < reference.Runtime.Step, "back-pressure should have held the diplomacy back");
        var guard = 0;
        while ((host.Component.Owed > 0 || host.Runtime.Step < reference.Runtime.Step) && guard++ < 100_000) host.Tick();
        Assert.Equal(reference.Runtime.Step, host.Runtime.Step);
    }

    [Fact]
    public void TheOwedQueueMayHoldExactlyWhatOneUpdateRaises()
    {
        var host = new DiplomacyHost(maxEvents: 5, maxOwed: 5);
        host.Tick(10);
        Assert.Equal(0, host.Component.Held);
        Assert.Throws<ArgumentException>(() => new DiplomacyComponent(host.Runtime, new TickDiplomacyClock(host.Runtime.Set), () => host.HostFacts, host.Channels, maxEvents: 5, maxOwed: 4));
    }

    // ---- faults of the clock and the host --------------------------------------------------------------------------------------------

    [Fact]
    public void AMissingHostFactIsPublishedOnceWhileItLastsAndTheDiplomacyDoesNotMove()
    {
        var host = new DiplomacyHost();
        host.Tick();
        host.HostFacts.Clear();
        host.Tick(4);
        var refused = host.Of<DiplomacyRefused>().ToList();
        Assert.Equal([(DiplomacyComponent.AdvanceRequestId, DipCodes.FactMissing)], refused.Select(r => (r.RequestId, r.Code)).ToArray());
        Assert.Equal(1, host.Runtime.Step);
        host.HostFacts["game.crisis"] = FactValue.From(1L);
        host.Tick();
        Assert.Equal(6, host.Runtime.Step);
        host.HostFacts.Clear();
        host.Tick();
        Assert.Equal(2, host.Of<DiplomacyRefused>().Count());
    }

    [Fact]
    public void AnInvalidHostFactIsRefusedAsTheAdvanceAndNotAsTheClock()
    {
        var host = new DiplomacyHost();
        host.HostFacts["game.crisis"] = FactValue.From(99L);
        host.Tick();
        var refused = Assert.Single(host.Of<DiplomacyRefused>());
        Assert.Equal((DiplomacyComponent.AdvanceRequestId, DipCodes.FactInvalid), (refused.RequestId, refused.Code));
    }

    [Fact]
    public void AClockThatGoesBackwardsIsPublishedOncePerKindOfFailureAndTheDiplomacyStaysWhereItWas()
    {
        var steps = new List<long> { 5, 5, 2, 2, 8 };
        var clock = new SequenceClock(steps);
        var host = new DiplomacyHost(clock: clock);
        host.Tick(5);
        Assert.Equal(8, host.Runtime.Step);
        var refused = host.Of<DiplomacyRefused>().ToList();
        Assert.Equal([(DiplomacyComponent.ClockRequestId, DipCodes.StepOrder)], refused.Select(r => (r.RequestId, r.Code)).ToArray());
    }

    private sealed class SequenceClock(List<long> steps) : IDiplomacyClock
    {
        private int _next;
        public long StepAt(long tick) => steps[Math.Min(_next++, steps.Count - 1)];
        public string SeasonAt(long step) => "spring";
    }

    [Fact]
    public void AClockThatCannotNameASeasonIsAClockFaultAndNothingMoves()
    {
        var host = new DiplomacyHost(clock: new MuteSeasonClock());
        host.Tick(3);
        Assert.Equal(-1, host.Runtime.Step);
        var refused = Assert.Single(host.Of<DiplomacyRefused>());
        Assert.Equal((DiplomacyComponent.ClockRequestId, DipCodes.UnknownSeason), (refused.RequestId, refused.Code));
    }

    private sealed class MuteSeasonClock : IDiplomacyClock
    {
        public long StepAt(long tick) => tick;
        public string SeasonAt(long step) => throw new EngineRefusedException(DipCodes.UnknownSeason, "no season");
    }

    [Fact]
    public void AClockFarAheadIsCaughtUpABoundedNumberOfStepsAnUpdateAndNothingIsSkipped()
    {
        var host = new DiplomacyHost(clock: new SequenceClock(Enumerable.Repeat(1000L, 10).ToList()), maxSteps: 100);
        host.Tick();
        Assert.Equal(99, host.Runtime.Step);
        host.Tick(3);
        Assert.Equal(399, host.Runtime.Step);
        host.Tick(7);
        Assert.Equal(1000, host.Runtime.Step);
    }

    [Fact]
    public void AClockWithSeveralTicksPerStepMovesTheDiplomacyOnlyWhenAStepIsComplete()
    {
        var runtime = DipRuntime.Create(Samples.Calm());
        var host = new DiplomacyHost(runtime: runtime, clock: new TickDiplomacyClock(runtime.Set, ticksPerStep: 5));
        host.Tick(4);
        Assert.Equal(0, runtime.Step);
        host.Tick();
        Assert.Equal(1, runtime.Step);
        host.Tick(5);
        Assert.Equal(2, runtime.Step);
        Assert.Throws<EngineRefusedException>(() => new TickDiplomacyClock(runtime.Set, ticksPerStep: 0));
        Assert.Throws<EngineRefusedException>(() => new TickDiplomacyClock(runtime.Set, firstStep: -1));
    }

    [Fact]
    public void TheConstructorRefusesWhatItCannotRun()
    {
        var runtime = DipRuntime.Create(Samples.Calm());
        var channels = new DiplomacyChannels();
        var clock = new TickDiplomacyClock(runtime.Set);
        Assert.Throws<ArgumentException>(() => new DiplomacyComponent(runtime, clock, () => Samples.Crisis(), channels, maxEvents: 0));
        Assert.Throws<ArgumentException>(() => new DiplomacyComponent(runtime, clock, () => Samples.Crisis(), channels, maxStepsPerUpdate: 0));
        Assert.Throws<ArgumentException>(() => new DiplomacyComponent(runtime, clock, () => Samples.Crisis(), channels, maxStepsPerUpdate: DipRuntime.MaxAdvance + 1));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyComponent(null!, clock, () => Samples.Crisis(), channels));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyComponent(runtime, null!, () => Samples.Crisis(), channels));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyComponent(runtime, clock, null!, channels));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyComponent(runtime, clock, () => Samples.Crisis(), null!));
        var fine = new DiplomacyComponent(runtime, clock, () => Samples.Crisis(), channels, maxStepsPerUpdate: DipRuntime.MaxAdvance, maxEvents: 1, maxOwed: 1);
        Assert.Equal("diplomacy", fine.Id);
        Assert.Equal("", fine.Channel);
    }

    [Fact]
    public void AnUpdateBeforeInitFails()
    {
        var runtime = DipRuntime.Create(Samples.Calm());
        var component = new DiplomacyComponent(runtime, new TickDiplomacyClock(runtime.Set), () => Samples.Crisis(), new DiplomacyChannels());
        Assert.Throws<InvalidOperationException>(() => component.Update(new UpdateContext(1, 0, 0.0, 0)));
    }

    // ---- checkpoints and facts --------------------------------------------------------------------------------------------------------

    /// <summary>A component that asks the diplomacy for something in its own Update, after the diplomacy's: the request waits for the next one.</summary>
    private sealed class Asker : IGameComponent
    {
        private IEventPort? _events;
        public bool AskOnce { get; set; }
        public string Id => "asker";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context)
        {
            if (!AskOnce) return;
            AskOnce = false;
            _events!.Emit(new DiplomacyRequested("late", "standing", "aurel"));
        }
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestIsWaitingOrAnEventIsOwed()
    {
        var owing = new DiplomacyHost(Lively(), maxEvents: 1);
        owing.Tick(3);
        Assert.True(owing.Component.Owed > 0);
        Assert.Equal(DiplomacyComponent.NotQuiet, Samples.Code(() => DiplomacyCheckpoint.Capture(owing.Component)));
        var asker = new Asker();
        var host = new DiplomacyHost(extra: [(asker, 20)]);
        host.Tick(4);
        Assert.True(host.Component.IsQuiet && host.Channels.IsQuiet);
        asker.AskOnce = true;
        host.Tick();
        Assert.False(host.Component.IsQuiet);
        Assert.Equal(DiplomacyComponent.NotQuiet, Samples.Code(() => DiplomacyCheckpoint.Capture(host.Component)));
        host.Tick();
        Assert.True(host.Component.IsQuiet);
        Assert.NotEmpty(DiplomacyCheckpoint.Capture(host.Component));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCheckpoint.Capture(null!));
    }

    [Fact]
    public void AQuietCheckpointIsTheDiplomacysSaveAndReopens()
    {
        var host = new DiplomacyHost();
        host.Tick(5);
        host.Ask("propose", "aurel", "brass", "nap", 90);
        host.Tick(5);
        var bytes = DiplomacyCheckpoint.Capture(host.Component);
        var reopened = DiplomacyCheckpoint.Open(host.Runtime.Set, bytes);
        Assert.Equal(host.Runtime.StateDigest(), reopened.StateDigest());
        Assert.Equal(bytes, DipSave.Write(host.Runtime));
    }

    [Fact]
    public void TheComponentFactsAreTheDiplomacysFactsForTheOtherLayers()
    {
        var host = new DiplomacyHost();
        host.Tick(2);
        Assert.Equal(host.Runtime.Facts("ext.", "aurel", "brass"), host.Component.Facts("ext.", "aurel", "brass"));
        Assert.Contains("diplomacy.aurel.stability", host.Component.Facts("", "aurel").Keys);
        Assert.StartsWith("diplomacy tick=2 step=2 state=", host.Component.Fingerprint());
        Assert.EndsWith(" answered=0 pending=0 owed=0", host.Component.Fingerprint());
    }

    // ---- what the others see ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATreatysEffectsReachTheOtherLayersWithTheMarketsOfTheRealms()
    {
        var host = new DiplomacyHost();
        host.Tick();
        host.Ask("propose", "aurel", "dune", "trade", 0, 0);
        host.Tick(3);
        var shocks = host.Of<DiplomacyEffectIssued>().Where(e => e.Kind == "shock").ToList();
        Assert.Equal([("trade-boom", "aurel", "dune", "aurel-market", "dune-market", 500L), ("trade-boom", "dune", "aurel", "dune-market", "aurel-market", 500L)],
            shocks.Select(e => (e.Name, e.Realm, e.Other, e.Market, e.OtherMarket, e.Scale)).ToArray());
        Assert.All(shocks, e => Assert.Equal("treaty:trade", e.Origin));
    }

    [Fact]
    public void APaymentIsAnEffectFromThePayersMarketToThePayeesAndWaitsForTheHost()
    {
        var host = new DiplomacyHost();
        host.Tick();
        host.Ask("propose", "brass", "dune", "vassalage", 0, 700);
        for (var i = 0; i < 80 && !host.Of<DiplomacyObligationChanged>().Any(); i++) host.Tick();
        var payment = host.Of<DiplomacyEffectIssued>().Single(e => e.Kind == "payment");
        Assert.Equal(("vassalage", "dune", "brass", "dune-market", "brass-market", 700L), (payment.Name, payment.Realm, payment.Other, payment.Market, payment.OtherMarket, payment.Scale));
        var due = host.Of<DiplomacyObligationChanged>().Single();
        Assert.Equal(("tribute", "dune", "brass", "due", 700L), (due.Clause, due.Owing, due.Owed, due.Change, due.Amount));
        host.Ask("fulfil", number: due.Treaty, number2: 1);
        host.Tick();
        Assert.Equal("paid", host.Of<DiplomacyObligationChanged>().Last().Change);
    }

    [Fact]
    public void WarsCoalitionsMissionsAndPoliticsAreRaisedWithTheirNames()
    {
        var host = new DiplomacyHost(Samples.Calm(root =>
        {
            Samples.Rules(root, "balance")["interval"] = 5;
            Samples.Rules(root, "balance")["threatLine"] = 250;
            Samples.Rules(root, "balance")["relaxLine"] = 150;
            Samples.Rules(root, "politics")["inertiaPpm"] = 1_000_000;
            Samples.Rules(root, "politics")["legitimacyWeight"] = 0;
            foreach (var faction in Samples.Realm(root, "ember")["factions"]!.AsArray()) faction!["satisfaction"] = 100;
        }));
        host.Tick();
        host.Ask("declare", "aurel", "dune", "");
        host.Ask("mission", "agent-brass-1", "aurel", "spy");
        host.Ask("grant", "dune", "aurel", "border", number: 5);
        host.Tick(20);
        var war = host.Of<DiplomacyWarChanged>().First();
        Assert.Equal(("aurel", "dune", "declared", ""), (war.Attacker, war.Defender, war.Change, war.CasusBelli));
        Assert.Contains(host.Of<DiplomacyCoalitionChanged>(), c => c.Change == "formed" && c.Target == "aurel");
        Assert.Contains(host.Of<DiplomacyMissionChanged>(), m => m.Change == "started" && m.Agent == "agent-brass-1" && m.Type == "spy");
        Assert.Contains(host.Of<DiplomacyGrantChanged>(), g => g.Change == "granted" && g.CasusBelli == "border");
        Assert.Contains(host.Of<DiplomacyGrantChanged>(), g => g.Change == "expired");
        Assert.Contains(host.Of<DiplomacyIncidentLogged>(), i => i.Kind == "war_declared" && i.A == "aurel" && i.B == "dune");
        Assert.Contains(host.Of<DiplomacyPoliticsChanged>(), p => p.Realm == "ember" && p.Change == "revolt");
        Assert.Contains(host.Of<DiplomacyWarChanged>(), w => w.Change == "declared");
    }

    [Fact]
    public void JoiningAWarNamesTheRealmThatJoinedAndAnAgentRecruitedOrCapturedIsNamed()
    {
        var host = new DiplomacyHost();
        host.Tick();
        host.Ask("declare", "aurel", "brass", "");
        host.Ask("join", "dune", "brass", number: 0);
        host.Ask("recruit", "dune", "agent-dune-2", number: 400);
        host.Tick();
        var joined = host.Of<DiplomacyWarChanged>().Single(w => w.Change == "joined");
        Assert.Equal("dune", joined.Realm);
        var left = new DiplomacyHost();
        left.Tick();
        left.Ask("declare", "aurel", "brass", "");
        left.Ask("join", "dune", "brass", number: 0);
        left.Ask("leave", "dune", number: 0);
        left.Tick();
        Assert.Equal("dune", left.Of<DiplomacyWarChanged>().Single(w => w.Change == "left").Realm);
        Assert.Equal("", left.Of<DiplomacyWarChanged>().Single(w => w.Change == "declared").Realm);
        var agent = host.Of<DiplomacyAgentChanged>().Single();
        Assert.Equal(("agent-dune-2", "dune", "recruited"), (agent.Agent, agent.Owner, agent.Change));
    }

    [Fact]
    public void KnowledgeSeasonsAndSuccessionAreRaisedToo()
    {
        var host = new DiplomacyHost(Samples.Calm(root => Samples.Rules(root, "espionage")["intelDecayPpm"] = 200000));
        host.Tick();
        host.Runtime.State.Intel[1 * 5 + 0] = 400000;
        host.Tick(4);
        Assert.Contains(host.Of<DiplomacyIntelChanged>(), i => i.Observer == "brass" && i.Target == "aurel" && i.Before == "report" && i.Level == "rumour");
        host.Ask("succeed", "aurel");
        host.Tick();
        Assert.Contains(host.Of<DiplomacyPoliticsChanged>(), p => p.Realm == "aurel" && p.Change is "succession" or "contested_succession");
        var seasons = host.Of<DiplomacySeasonChanged>().ToList();
        Assert.Single(seasons);
    }

    [Fact]
    public void AnObligationAndItsClauseAreNamedByTheCallAndTheWord()
    {
        var host = new DiplomacyHost(Samples.Calm(root => Samples.Rules(root, "war")["callBase"] = 0));
        host.Tick();
        host.Ask("propose", "brass", "dune", "alliance", 0, 0);
        host.Tick(4);
        host.Ask("declare", "aurel", "dune", "");
        host.Tick();
        var called = host.Of<DiplomacyObligationChanged>().Single();
        Assert.Equal(("defend", "brass", "dune", "called"), (called.Clause, called.Owing, called.Owed, called.Change));
        host.Tick(6);
        Assert.Contains(host.Of<DiplomacyObligationChanged>(), o => o.Change == "ignored");
        Assert.Contains(host.Of<DiplomacyTreatyChanged>(), t => t.Change == "broken" && t.Type == "alliance");
        Assert.Contains(host.Of<DiplomacyProposalChanged>(), p => p.Change == "accepted" && p.Type == "alliance");
    }
}
