using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The world component: requests only queue and are answered in Update, in order; every request is answered; events are deferred, bounded per Update and owed; time waits for a host that does not drain; checkpoints are refused while anything waits.</summary>
public sealed class ComponentTests
{
    private sealed class FixedClock(params long[] steps) : IWorldClock
    {
        private int _next;
        public long StepAt(long tick) => steps[Math.Min(_next++, steps.Length - 1)];
        public string SeasonAt(long step) => step % 20 < 10 ? "warm" : "cold";
    }

    private static WorldSet Busy() => Samples.Edited(root =>
    {
        Tiny.Certain(root);
        Tiny.Disease(root)["seedPpm"] = 500_000;
        Tiny.Disease(root)["seedCases"] = 2;
    });

    /// <summary>A component that asks the world for the weather, once, in an Update that runs after the world's.</summary>
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
            _events!.Emit(new WorldRequested("asker", "weather", "a"));
        }
    }

    private sealed class Tiny
    {
        public static JsonObject Hazard(JsonObject root) => root["hazards"]!.AsArray()[0]!.AsObject();
        public static JsonObject Disease(JsonObject root) => root["diseases"]!.AsArray()[0]!.AsObject();
        public static void Certain(JsonObject root) => Hazard(root)["ratePpm"] = 1_000_000;
    }

    [Fact]
    public void TheWorldLivesToTheStepOfTheClockInTheUpdateAndRaisesWhatHappenedInOrder()
    {
        var host = new WorldHost(Busy());
        host.Tick(3);
        Assert.Equal(3, host.Runtime.Step);
        var first = host.All[0];
        var season = Assert.IsType<WorldSeasonChanged>(first);
        Assert.Equal(("warm", "", 0L), (season.Season, season.From, season.Step));
        var steps = host.All.Select(o => o switch { WorldHazardChanged h => h.Step, WorldOutbreakChanged b => b.Step, WorldCardPlayed c => c.Step, WorldWeatherChanged w => w.Step, _ => -1L }).Where(s => s >= 0).ToList();
        Assert.Equal(steps.OrderBy(s => s).ToList(), steps);              // the steps are told in the order they were lived
    }

    [Fact]
    public void ARequestOnlyQueuesAndIsAnsweredInTheNextUpdate()
    {
        var host = new WorldHost();
        host.Ask("intervene", "a", "quar", number: 1);
        Assert.Empty(host.Of<WorldApplied>());
        Assert.Equal(0, host.Component.Answered);
        host.Tick();
        Assert.Single(host.Of<WorldApplied>());
        Assert.Equal(1, host.Component.Answered);
        Assert.Equal(1, host.Runtime.Regions[0].Interventions[0]);
        var changed = Assert.Single(host.Of<WorldInterventionChanged>());
        Assert.Equal(("a", "quar", true, false), (changed.Region, changed.Intervention, changed.On, changed.Automatic));
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrivedEachWithItsOwnAnswer()
    {
        var host = new WorldHost();
        var a = host.Ask("intervene", "a", "quar");
        var b = host.Ask("intervene", "zz", "quar");
        var c = host.Ask("intervene", "a", "quar");             // already on
        var d = host.Ask("storyteller", "calm");
        host.Tick();
        var answers = host.All.Where(o => o is WorldApplied or WorldRefused).Select(o => o is WorldApplied applied ? "applied " + applied.RequestId : "refused " + ((WorldRefused)o).RequestId + " " + ((WorldRefused)o).Code).ToList();
        Assert.Equal(["applied " + a, "refused " + b + " " + WorldCodes.UnknownRegion, "refused " + c + " " + WorldCodes.AlreadyActive, "applied " + d], answers);
        Assert.Equal(4, host.Component.Answered);
        Assert.Equal(1, host.Runtime.Storyteller);
    }

    [Fact]
    public void ARequestOfAnUnknownKindIsRefusedNotDropped()
    {
        var host = new WorldHost();
        host.Ask("dance");
        var before = host.Runtime.StateDigest();
        host.Tick();
        var refused = Assert.Single(host.Of<WorldRefused>());
        Assert.Equal(WorldComponent.UnknownKind, refused.Code);
        Assert.Equal("world", refused.Source);
        Assert.Equal(1, host.Component.Answered);
        Assert.NotEmpty(before);
    }

    [Fact]
    public void AllEightRequestKindsAreServedAndTheQueriesAnswerFromTheState()
    {
        var host = new WorldHost();
        host.Tick(2);
        host.Ask("hazard", "a", "h", "high");
        host.Ask("outbreak", "a", "d", number: 40);
        host.Ask("card", "c-global");
        host.Ask("difficulty", "hard");
        host.Ask("weather", "a");
        host.Ask("disease", "a", "d");
        host.Tick();
        Assert.Empty(host.Of<WorldRefused>());
        var weather = Assert.Single(host.Of<WorldWeatherAnswered>());
        Assert.Equal("a", weather.Region);
        Assert.Equal(host.Runtime.Regions[0].Temp, weather.Temp);
        Assert.Equal(host.Runtime.SoilPermille(0), weather.Soil);
        var disease = Assert.Single(host.Of<WorldDiseaseAnswered>());
        Assert.Equal((10_000 - 40, 40L, "introduced"), (disease.Susceptible, disease.Exposed, disease.State));
        Assert.Contains(host.Of<WorldHazardChanged>(), h => h.Change == "started" && h.Severity == "high" && h.Region == "a" && h.Kind == "flood");
        Assert.Contains(host.Of<WorldCardPlayed>(), c => c.Card == "c-global" && c.Reason == "forced" && c.Region == "");
        Assert.Equal(1, host.Runtime.Difficulty);
        Assert.Equal(WorldCodes.UnknownRegion, Samples.Code(() => host.Runtime.Facts("", "zz")));
        host.Ask("weather", "zz");
        host.Ask("disease", "a", "zz");
        host.Tick();
        Assert.Equal([WorldCodes.UnknownRegion, WorldCodes.UnknownDisease], host.Of<WorldRefused>().Select(r => r.Code));
    }

    [Fact]
    public void ARequestForAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = new WorldHost();
        host.Ask("weather", "a", channel: "ghost");
        host.Tick();
        var refused = Assert.Single(host.Of<WorldRefused>());
        Assert.Equal(WorldChannels.UnknownChannel, refused.Code);
        Assert.Equal(WorldChannels.ComponentId, refused.Source);
        Assert.Equal("ghost", refused.Channel);
        Assert.Empty(host.Of<WorldWeatherAnswered>());
    }

    [Fact]
    public void TwoWorldsOnTwoChannelsAreSeparate()
    {
        var both = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), both);
        var registry = new WorldChannels();
        registry.Install(session);
        var a = new WorldComponent(WorldRuntime.Create(Samples.Tiny), new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, "world-a", "a");
        var b = new WorldComponent(WorldRuntime.Create(Samples.Tiny), new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, "world-b", "b");
        session.AddComponent(a, 10);
        session.AddComponent(b, 11);
        var seen = new List<WorldApplied>();
        session.Subscribe<WorldApplied>(seen.Add);
        session.Init();
        session.Submit(new WorldRequested("q1", "intervene", "a", "quar", "", 1, "a"));
        session.Submit(new WorldRequested("q2", "storyteller", "calm", "", "", 1, "b"));
        for (var i = 0; i < 2; i++) Assert.Equal(1, session.Update(20_000));
        Assert.Equal([("world-a", "q1"), ("world-b", "q2")], seen.Select(s => (s.Source, s.RequestId)).OrderBy(s => s.Source));
        Assert.Equal(1, a.Runtime.Regions[0].Interventions[0]);
        Assert.Equal(0, b.Runtime.Regions[0].Interventions[0]);
        Assert.Equal(1, b.Runtime.Storyteller);
        Assert.Equal(0, a.Runtime.Storyteller);
    }

    [Fact]
    public void ASecondComponentOnTheSameChannelIsRefusedAtInit()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var registry = new WorldChannels();
        registry.Install(session);
        session.AddComponent(new WorldComponent(WorldRuntime.Create(Samples.Tiny), new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, "one"), 10);
        session.AddComponent(new WorldComponent(WorldRuntime.Create(Samples.Tiny), new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, "two"), 11);
        Assert.Equal(WorldChannels.DuplicateChannel, Samples.Code(() => session.Init()));
    }

    [Fact]
    public void AComponentWhoseRegistryDoesNotRunOnItsSessionFailsItsFirstUpdate()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        new WorldChannels().Install(session);
        var elsewhere = new WorldChannels();
        var other = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        elsewhere.Install(other);
        other.Init();
        session.AddComponent(new WorldComponent(WorldRuntime.Create(Samples.Tiny), new TickWorldClock(Samples.Tiny), () => Samples.Alert(), elsewhere), 10);
        session.Init();
        session.Update(20_000);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Contains(reporter.Reports, r => r.StartsWith("RUNTIME_FAULT", StringComparison.Ordinal) && r.Contains("does not run on this session", StringComparison.Ordinal));
    }

    /// <summary>Steps 1 to 40, one a tick, and then the world stays at step 40 so that a limited run can finish telling what it owes.</summary>
    private static FixedClock ToFortyAndStay() => new(Enumerable.Range(1, 40).Select(i => (long)i).Append(40).ToArray());

    [Fact]
    public void EventsAreRaisedAtMostMaxEventsPerUpdateAndTheRestIsOwedInOrderNoneLost()
    {
        var limited = new WorldHost(Busy(), maxEvents: 3, clock: ToFortyAndStay());
        var free = new WorldHost(Busy(), clock: ToFortyAndStay());
        var previous = 0;
        for (var tick = 1; tick <= 40; tick++)
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
        Assert.Equal(free.LinesWithoutTicks(), limited.LinesWithoutTicks());
        Assert.Equal(free.Runtime.StateDigest(), limited.Runtime.StateDigest());
    }

    [Fact]
    public void WhenTheOwedQueueIsFullTimeWaitsForTheHostAndNothingIsLost()
    {
        var host = new WorldHost(Busy(), maxEvents: 1, maxOwed: 5, maxSteps: 1);
        var unbounded = new WorldHost(Busy(), maxEvents: 1, maxSteps: 1);
        var most = 0;
        for (var i = 0; i < 400; i++)
        {
            host.Tick();
            unbounded.Tick();
            most = Math.Max(most, host.Component.Owed);
        }
        Assert.True(most < 150, "owed up to " + most);                                       // bounded: none is added while 5 or more are owed, so only what one call adds can pile on top
        Assert.True(unbounded.Component.Owed > 3 * most, "the unbounded run owes " + unbounded.Component.Owed + ", the bounded one never more than " + most);         // and without the bound it would just grow
        Assert.True(host.Component.Held > 0);
        Assert.True(host.Runtime.Step < 400);                                                // the world waited
        Assert.Empty(host.Faults.Reports);
    }

    [Fact]
    public void AMissingHostFactIsPublishedOnceWhileItLastsAndTheWorldDoesNotMove()
    {
        var host = new WorldHost();
        host.HostFacts.Clear();
        host.Tick(3);
        var refused = host.Of<WorldRefused>().ToList();
        Assert.Equal([WorldCodes.FactMissing], refused.Select(r => r.Code));
        Assert.Equal(WorldComponent.AdvanceRequestId, refused[0].RequestId);
        Assert.Equal(-1, host.Runtime.Step);
        host.HostFacts["ext.alert"] = FactValue.From(2L);
        host.Tick();
        Assert.True(host.Runtime.Step > 0);
        host.HostFacts["ext.alert"] = FactValue.From("x");
        host.Tick(2);
        Assert.Equal([WorldCodes.FactMissing, WorldCodes.FactInvalid], host.Of<WorldRefused>().Select(r => r.Code));
    }

    [Fact]
    public void AClockThatGoesBackwardsIsPublishedOncePerKindOfFailureAndTheWorldStaysWhereItWas()
    {
        var host = new WorldHost(clock: new FixedClock(5, 3, 3, 7, 2, 8));
        host.Tick(2);                                                                        // 5, then back to 3
        Assert.Equal(5, host.Runtime.Step);
        Assert.Equal([WorldCodes.StepOrder], host.Of<WorldRefused>().Select(r => r.Code));
        host.Tick(1);                                                                        // 3 again: the same fault, not told twice
        Assert.Single(host.Of<WorldRefused>());
        Assert.Equal(5, host.Runtime.Step);
        host.Tick(1);                                                                        // 7: works, which clears the fault
        Assert.Equal(7, host.Runtime.Step);
        host.Tick(2);                                                                        // 2: back again after a success: told again; then 8
        Assert.Equal(2, host.Of<WorldRefused>().Count());
        Assert.Equal(8, host.Runtime.Step);
        Assert.All(host.Of<WorldRefused>(), r => Assert.Equal(WorldComponent.ClockRequestId, r.RequestId));
    }

    [Fact]
    public void AClockFarAheadIsCaughtUpABoundedNumberOfStepsAnUpdateAndNothingIsSkipped()
    {
        var capped = new WorldHost(clock: new FixedClock(1000), maxSteps: 100, journal: true);
        capped.Tick();
        Assert.Equal(99, capped.Runtime.Step);                                               // steps 0 to 99: a hundred
        capped.Tick(4);
        Assert.Equal(499, capped.Runtime.Step);
        capped.Tick(20);
        Assert.Equal(1000, capped.Runtime.Step);
        var straight = WorldRuntime.Create(Samples.Tiny);
        straight.Advance(1000, straight.CycleSeasonAt, Samples.Alert());
        Assert.Equal(straight.StateDigest(), capped.Runtime.StateDigest());                  // the same world as living them all at once
        Assert.Equal(WorldComponent.DefaultMaxStepsPerUpdate, 366);
        Assert.Throws<ArgumentException>(() => new WorldHost(maxSteps: 0));
        Assert.Throws<ArgumentException>(() => new WorldHost(maxSteps: WorldRuntime.MaxAdvance + 1));
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestIsWaitingOrAnEventIsOwed()
    {
        var owing = new WorldHost(Busy(), maxEvents: 1);
        owing.Tick(3);
        Assert.True(owing.Component.Owed > 0);
        Assert.Equal(WorldComponent.NotQuiet, Samples.Code(() => WorldCheckpoint.Capture(owing.Component)));
        var asker = new Asker();
        var host = new WorldHost(extra: [(asker, 20)]);
        host.Tick(4);
        Assert.True(host.Component.IsQuiet && host.Channels.IsQuiet);
        asker.AskOnce = true;
        host.Tick();                                                                         // another component asked in this tick, after the world's Update: the request waits in the world for the next one
        Assert.False(host.Component.IsQuiet);
        Assert.Equal(WorldComponent.NotQuiet, Samples.Code(() => WorldCheckpoint.Capture(host.Component)));
        host.Tick();
        Assert.True(host.Component.IsQuiet);
        Assert.NotEmpty(WorldCheckpoint.Capture(host.Component));
        Assert.Equal("channels=" + string.Empty + " refused=0 pending=0", host.Channels.Fingerprint());
    }

    [Fact]
    public void AQuietCheckpointIsTheWorldsSaveAndReopens()
    {
        var host = new WorldHost();
        host.Tick(8);
        Assert.True(host.Component.IsQuiet);
        var bytes = WorldCheckpoint.Capture(host.Component);
        Assert.Equal(WorldSave.Write(host.Runtime), bytes);
        var reopened = WorldCheckpoint.Open(Samples.Tiny, bytes);
        Assert.Equal(host.Runtime.StateDigest(), reopened.StateDigest());
        Assert.Contains("world tick=8 step=8", host.Component.Fingerprint());
    }

    [Fact]
    public void TheComponentFactsAreTheWorldsFactsForTheOtherLayers()
    {
        var host = new WorldHost();
        host.Tick(4);
        var facts = host.Component.Facts("ext.", "a");
        Assert.Equal("warm", facts["ext.world.season"].Text);
        Assert.Equal(host.Runtime.Regions[0].Temp, facts["ext.world.a.temp"].Integer);
    }

    [Fact]
    public void TheConstructorRefusesWhatItCannotRun()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var registry = new WorldChannels();
        var runtime = WorldRuntime.Create(Samples.Tiny);
        Assert.Throws<ArgumentException>(() => new WorldComponent(runtime, new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, maxEvents: 0));
        Assert.Throws<ArgumentException>(() => new WorldComponent(runtime, new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry, maxEvents: 10, maxOwed: 5));
        Assert.Throws<ArgumentNullException>(() => new WorldComponent(null!, new TickWorldClock(Samples.Tiny), () => Samples.Alert(), registry));
        Assert.Equal(WorldCodes.Range, Samples.Code(() => new TickWorldClock(Samples.Tiny, 0)));
        Assert.Equal(WorldCodes.StepOrder, Samples.Code(() => new TickWorldClock(Samples.Tiny, 1, -1)));
        Assert.NotNull(session);
    }

    [Fact]
    public void AClockWithSeveralTicksPerStepMovesTheWorldOnlyWhenAStepIsComplete()
    {
        var host = new WorldHost(clock: new TickWorldClock(Samples.Tiny, ticksPerStep: 4));
        host.Tick(3);
        Assert.Equal(0, host.Runtime.Step);
        host.Tick(1);
        Assert.Equal(1, host.Runtime.Step);
        host.Tick(8);
        Assert.Equal(3, host.Runtime.Step);
    }

    [Fact]
    public void ACardsEffectsReachTheOtherLayersAsEffectEvents()
    {
        var host = new WorldHost();
        host.Tick();
        host.Ask("card", "c-global");
        host.Ask("hazard", "a", "h", "high");
        host.Tick();
        var effects = host.Of<WorldEffectIssued>().ToList();
        Assert.Contains(effects, e => e.Kind == "signal" && e.Name == "sig.global" && e.Region == "" && e.Origin == "card:c-global");
        Assert.Contains(effects, e => e.Kind == "shock" && e.Name == "sh1" && e.Region == "" && e.Market == "" && e.Scale == 500);
        Assert.Contains(effects, e => e.Kind == "shock" && e.Name == "sh1" && e.Region == "a" && e.Market == "ma" && e.Scale == 700 && e.Origin == "hazard:h");
        Assert.Contains(effects, e => e.Kind == "shock" && e.Name == "sh1" && e.Region == "b" && e.Market == "" && e.Scale == 350);
    }
}
