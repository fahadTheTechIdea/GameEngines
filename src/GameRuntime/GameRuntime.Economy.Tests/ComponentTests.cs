using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The economy component: requests only queue and are answered in Update in arrival order, refusals leave the economy as it was, change events are deferred, bounded and owed, and the checkpoint and fault rules.</summary>
public sealed class ComponentTests
{
    private static readonly HashSet<string> ChangeNames =
    [
        "EconomyRateChanged", "EconomyShockChanged", "EconomyShipmentArrived", "EconomyBandChanged", "EconomyShortageChanged", "EconomyShipmentLaunched", "EconomyRouteBlocked", "EconomySettled", "EconomyLoanChanged", "EconomyProductionLimited", "EconomyRecipeUnlocked",
    ];

    private static string Name(string line) => line[..line.IndexOf('{', StringComparison.Ordinal)];

    // ---- requests and answers ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestOnlyQueuesAndIsAnsweredInTheNextUpdate()
    {
        var host = new EcoHost();
        host.Trade("riverton", "grain", 50);
        Assert.Empty(host.Of<EconomyTradeExecuted>());
        host.Tick();
        var trade = Assert.Single(host.Of<EconomyTradeExecuted>());
        Assert.Equal(("riverton", "grain", "crown", 50L), (trade.Market, trade.Good, trade.Currency, trade.Quantity));
        Assert.Equal("player", trade.Trader);
        Assert.Equal("economy", trade.Source);
        Assert.Equal(1, trade.Tick);
        Assert.Equal(trade.Value + trade.Tax, trade.Total);
        Assert.True(host.Component.IsQuiet || host.Component.Owed > 0);
        Assert.Equal(1, host.Component.Answered);
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrived()
    {
        var host = new EcoHost();
        host.Trade("riverton", "grain", 10);
        host.Trade("hillfort", "iron", -20);
        host.Trade("seaport", "cloth", 5);
        host.Tick();
        Assert.Equal(["riverton", "hillfort", "seaport"], host.Of<EconomyTradeExecuted>().Select(t => t.Market).ToList());
        Assert.Equal(["r1", "r2", "r3"], host.Of<EconomyTradeExecuted>().Select(t => t.RequestId).ToList());
    }

    [Fact]
    public void EventsAreDeliveredAfterTheUpdateReturnsSoAHandlerSeesTheFinishedTick()
    {
        var host = new EcoHost();
        var stepSeenByTheHandler = -1L;
        var answeredSeen = -1L;
        host.Session.Subscribe<EconomyTradeExecuted>(_ => { stepSeenByTheHandler = host.Runtime.Step; answeredSeen = host.Component.Answered; });
        host.Trade("riverton", "grain", 10);
        host.Tick();
        Assert.Equal(1, stepSeenByTheHandler);        // the economy had already moved to its step of this tick
        Assert.Equal(1, answeredSeen);
    }

    [Fact]
    public void ARequestForAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = new EcoHost();
        host.Trade("riverton", "grain", 10, channel: "ghost");
        host.Tick();
        var refused = Assert.Single(host.Of<EconomyRefused>());
        Assert.Equal(("economy-channels", "ghost", "ECON_CHANNEL_UNKNOWN", 1L), (refused.Source, refused.Channel, refused.Code, refused.Tick));
        Assert.Empty(host.Of<EconomyTradeExecuted>());
        Assert.Equal(1, host.Channels.Refused);
    }

    [Fact]
    public void ASecondComponentOnOneChannelIsRefusedAtInit()
    {
        var host = new EcoHost();
        Assert.Equal(EconomyChannels.DuplicateChannel, Samples.Code(() => host.Channels.Serve("", "other")));
        Assert.Equal("economy", host.Channels.Served[""]);
    }

    [Fact]
    public void TwoEconomiesOnTwoChannelsAnswerOnlyTheirOwnRequests()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new EconomyChannels();
        channels.Install(session);
        var facts = Samples.Host();
        var a = new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels, "overworld", "a");
        var b = new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels, "station", "b");
        session.AddComponent(a, 10);
        session.AddComponent(b, 11);
        var trades = new List<EconomyTradeExecuted>();
        session.Subscribe<EconomyTradeExecuted>(trades.Add);
        session.Init();
        session.Submit(new EconomyTradeRequested("x", "p", "riverton", "grain", 40, "b"));
        session.Update(20_000);
        var trade = Assert.Single(trades);
        Assert.Equal(("station", "b"), (trade.Source, trade.Channel));
        Assert.Equal(0, a.Answered);
        Assert.NotEqual(a.Runtime.StateDigest(), b.Runtime.StateDigest());
        Assert.Equal(0, a.Runtime.Markets[0].Pairs[0].SpotDemand + a.Runtime.Markets[0].Pairs[0].SpotSupply);
        Assert.Equal(["a", "b"], channels.Served.Keys.ToList());
    }

    [Fact]
    public void ARefusedRequestIsAnsweredWithItsCodeAndLeavesTheEconomyUnchanged()
    {
        var host = new EcoHost(ticksPerStep: 1_000_000);
        var digest = host.Runtime.StateDigest();
        host.Trade("nowhere", "grain", 1);
        host.Trade("riverton", "gold", 1);
        host.Trade("riverton", "grain", 99_999_999);
        host.Session.Submit(new EconomyShockRequested("s", "rain", ""));
        host.Session.Submit(new EconomyShockRequested("s2", "drought", "", 0));
        host.Session.Submit(new EconomySettleRequested("t", "payday"));
        host.Session.Submit(new EconomyGrowthRequested("g", "gold", 1));
        host.Session.Submit(new EconomyFundRequested("f", "riverton", -99_999_999));
        host.Session.Submit(new EconomyLendRequested("l", "levy-loan", "world", "hillfort", 10, 0, 2, "bullet", "taxday", 2));
        host.Session.Submit(new EconomyLendRequested("l2", "weird", "world", "hillfort", 10, 0, 2, "weird", "taxday", 2));
        host.Session.Submit(new EconomyQuoteRequested("q", "riverton", "gold", 1));
        host.Session.Submit(new EconomyPriceRequested("p", "riverton", "gold"));
        host.Tick();
        Assert.Equal(
            ["ECON_UNKNOWN_MARKET", "ECON_UNKNOWN_GOOD", "ECON_STOCK", "ECON_UNKNOWN_SHOCK", "ECON_RANGE", "ECON_SCHEDULE", "ECON_UNKNOWN_CURRENCY", "ECON_FUNDS", "ECON_DUPLICATE_LOAN", "ECON_RANGE", "ECON_UNKNOWN_GOOD", "ECON_UNKNOWN_GOOD"],
            host.Of<EconomyRefused>().Select(r => r.Code).ToList());
        Assert.Equal(host.Of<EconomyRefused>().Select(r => r.RequestId).ToList(), ["r1", "r2", "r3", "s", "s2", "t", "g", "f", "l", "l2", "q", "p"]);
        Assert.Equal(digest, host.Runtime.StateDigest());
        Assert.Equal(12, host.Component.Answered);
    }

    [Fact]
    public void EveryKindOfRequestIsCarriedOutAndAnsweredAsApplied()
    {
        var host = new EcoHost();
        host.Session.Submit(new EconomyShockRequested("shock", "drought", "riverton", 500));
        host.Session.Submit(new EconomyGrowthRequested("growth", "florin", 2500));
        host.Session.Submit(new EconomyFundRequested("fund", "hillfort", 700));
        host.Session.Submit(new EconomyLendRequested("lend", "relief", "world", "hillfort", 3000, 1000, 4, "annuity", "taxday", 2));
        host.Session.Submit(new EconomySettleRequested("settle", "decree"));
        host.Tick();
        Assert.Equal(["shock", "growth", "fund", "lend", "settle"], host.Of<EconomyApplied>().Select(a => a.RequestId).ToList());
        Assert.Empty(host.Of<EconomyRefused>());
        Assert.Single(host.Runtime.ActiveShocks);
        Assert.Equal(2500, host.Runtime.Currencies[1].Growth);
        Assert.Contains(host.Runtime.Loans, l => l.Id == "relief" && l.Payment > 0);
        Assert.Contains(host.Of<EconomyShockChanged>(), s => s is { Shock: "drought", Market: "riverton", Started: true });
        Assert.Contains(host.Of<EconomySettled>(), s => s is { Kind: "upkeep", Name: "court" });
    }

    [Fact]
    public void AQuoteAnswersWithoutChangingAnythingAndAPriceRequestExplainsThePrice()
    {
        var host = new EcoHost(ticksPerStep: 1_000_000);
        var digest = host.Runtime.StateDigest();
        host.Session.Submit(new EconomyQuoteRequested("q", "seaport", "cloth", 30));
        host.Session.Submit(new EconomyPriceRequested("p", "seaport", "cloth"));
        host.Tick();
        var quote = Assert.Single(host.Of<EconomyQuoted>());
        Assert.Equal(("seaport", "cloth", "florin", 30L), (quote.Market, quote.Good, quote.Currency, quote.Quantity));
        Assert.Equal(quote.Value + quote.Tax, quote.Total);
        var answer = Assert.Single(host.Of<EconomyPriceAnswered>());
        Assert.Equal(("seaport", "cloth", 24_000L, "fair"), (answer.Market, answer.Good, answer.Price, answer.Band));
        Assert.StartsWith("reference|cloth|24000;bounds|cloth|1000;supply|cloth|0;", answer.Terms, StringComparison.Ordinal);
        Assert.Equal(digest, host.Runtime.StateDigest());
        Assert.Empty(host.Of<EconomyApplied>());
    }

    // ---- time ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheEconomyMovesToTheStepOfTheClockEachUpdateAndSaysSo()
    {
        var host = new EcoHost(ticksPerStep: 2);
        host.Tick(5);
        var steps = host.Of<EconomyStepped>().ToList();
        Assert.Equal([(2L, 0L, 1L), (4L, 1L, 2L)], steps.Select(s => (s.Tick, s.FromStep, s.Step)).ToList());
        Assert.All(steps, s => Assert.Equal(0, s.Owed));
        Assert.Equal(2, host.Runtime.Step);
        Assert.Equal(2, host.Runtime.Period);
    }

    [Fact]
    public void WhenTheClockJumpsFarAheadOnlySixtyFourPeriodsAreLivedPerUpdateAndTheRestIsOwed()
    {
        var host = new EcoHost(ticksPerStep: 1, clock: new JumpClock(200));
        host.Tick();
        var first = Assert.Single(host.Of<EconomyStepped>());
        Assert.Equal((0L, 64L, 136L), (first.FromStep, first.Step, first.Owed));
        host.Tick();
        Assert.Equal(128, host.Runtime.Step);
        host.Tick(2);
        Assert.Equal(200, host.Runtime.Step);
        Assert.Equal(0, host.Of<EconomyStepped>().Last().Owed);
    }

    private sealed class JumpClock(long to) : IEconomyClock
    {
        public long StepAt(long tick) => to;
    }

    private sealed class BackwardsClock : IEconomyClock
    {
        public long StepAt(long tick) => tick switch { 1 => 5, 2 => 3, 3 => 3, 4 => 6, _ => 6 };
    }

    [Fact]
    public void AClockThatGoesBackwardsIsReportedOncePerFaultAndTheEconomyDoesNotMove()
    {
        var host = new EcoHost(clock: new BackwardsClock());
        host.Tick(5);
        var faults = host.Of<EconomyRefused>().ToList();
        var fault = Assert.Single(faults);
        Assert.Equal((EconomyComponent.ClockRequestId, "ECON_STEP_ORDER", 2L), (fault.RequestId, fault.Code, fault.Tick));
        Assert.Equal(6, host.Runtime.Step);
    }

    [Fact]
    public void AMissingHostFactIsReportedOnceAndTheEconomyWaitsForIt()
    {
        var host = new EcoHost();
        host.HostFacts.Remove("cal.season");
        host.Tick(4);
        var fault = Assert.Single(host.Of<EconomyRefused>());
        Assert.Equal((EconomyComponent.AdvanceRequestId, "ECON_FACT_MISSING"), (fault.RequestId, fault.Code));
        Assert.Equal(0, host.Runtime.Step);
        host.HostFacts["cal.season"] = FactValue.From("spring");
        host.Tick(2);
        Assert.Single(host.Of<EconomyRefused>());
        Assert.Equal(6, host.Runtime.Step);
    }

    // ---- bounded, deferred change events -----------------------------------------------------------------------------------------------

    private sealed class CappedClock : IEconomyClock
    {
        public long Limit { get; set; } = long.MaxValue;
        public long StepAt(long tick) => Math.Min(tick, Limit);
    }

    [Fact]
    public void ChangeEventsAreRaisedAtMostTheMaximumPerUpdateTheRestAreOwedAndNoneIsLostOrReordered()
    {
        var smallClock = new CappedClock();
        var largeClock = new CappedClock();
        var small = new EcoHost(maxEvents: 4, clock: smallClock);
        var large = new EcoHost(maxEvents: 100_000, clock: largeClock, maxOwed: 1_000_000);
        var perTick = new List<int>();
        var sawOwed = false;
        var before = 0;
        for (var tick = 0; tick < 80; tick++)
        {
            small.Tick(); large.Tick();
            var count = small.Transcript.Lines.Count(l => ChangeNames.Contains(Name(l)));
            perTick.Add(count - before);
            before = count;
            if (small.Component.Owed > 0)
            {
                sawOwed = true;
                Assert.False(small.Component.IsQuiet);
                Assert.Equal(EconomyComponent.NotQuiet, Samples.Code(() => EconomyCheckpoint.Capture(small.Component)));
            }
        }
        Assert.True(sawOwed, "a period should have produced more than four changes");
        Assert.All(perTick, n => Assert.InRange(n, 0, 4));
        smallClock.Limit = largeClock.Limit = 80;       // time stops, so what is owed can be raised
        for (var guard = 0; (small.Component.Owed > 0 || small.Runtime.Step < 80) && guard < 5000; guard++) small.Tick();
        for (var guard = 0; (large.Component.Owed > 0 || large.Runtime.Step < 80) && guard < 5000; guard++) large.Tick();
        Assert.Equal(0, small.Component.Owed);
        Assert.Equal(80, small.Runtime.Step);
        var smallChanges = small.LinesWithoutTicks().Where(l => ChangeNames.Contains(Name(l))).ToList();
        var largeChanges = large.LinesWithoutTicks().Where(l => ChangeNames.Contains(Name(l))).ToList();
        Assert.Equal(largeChanges, smallChanges);
        Assert.True(smallChanges.Count > 100);
        Assert.Equal(large.Runtime.StateDigest(), small.Runtime.StateDigest());
    }

    [Fact]
    public void WhenTooManyEventsAreOwedTimeStopsMovingUntilTheyAreRaisedSoTheQueueDoesNotGrowWithoutEnd()
    {
        var clock = new CappedClock();
        var host = new EcoHost(maxEvents: 2, maxOwed: 30, clock: clock);
        var largest = 0;
        var lagged = false;
        for (var tick = 1; tick <= 120; tick++)
        {
            var owedBefore = host.Component.Owed;
            var stepBefore = host.Runtime.Step;
            host.Tick();
            if (owedBefore >= 30) Assert.Equal(stepBefore, host.Runtime.Step);
            largest = Math.Max(largest, host.Component.Owed);
            lagged |= host.Runtime.Step < tick;
        }
        Assert.True(lagged, "the economy should have waited for its events at least once");
        Assert.True(largest < 30 + 1000, "the queue held " + largest);      // the limit plus what one period (a tax day: hundreds of settlements) adds
        clock.Limit = 120;
        for (var guard = 0; (host.Component.Owed > 0 || host.Runtime.Step < 120) && guard < 20_000; guard++) host.Tick();
        Assert.Equal(120, host.Runtime.Step);
        Assert.Equal(0, host.Component.Owed);
        var reference = new EcoHost(maxEvents: 100_000, clock: new CappedClock { Limit = 120 }, maxOwed: 1_000_000);
        for (var tick = 0; tick < 200; tick++) reference.Tick();
        Assert.Equal(reference.Runtime.StateDigest(), host.Runtime.StateDigest());
    }

    [Fact]
    public void TheChangesOfARequestAreRaisedWithItsAnswerNotOwed()
    {
        var host = new EcoHost(maxEvents: 1, ticksPerStep: 1_000_000);
        host.Session.Submit(new EconomyShockRequested("a", "devaluation", "seaport"));
        host.Session.Submit(new EconomyShockRequested("b", "drought", ""));
        host.Tick();
        Assert.Equal(2, host.Of<EconomyApplied>().Count());
        Assert.Equal(2, host.Of<EconomyShockChanged>().Count());
        Assert.Single(host.Of<EconomyRateChanged>());
        Assert.Equal(0, host.Component.Owed);
    }

    // ---- checkpoint --------------------------------------------------------------------------------------------------------------------

    /// <summary>Asks for a trade in tick 1 (the event reaches the economy as soon as this Update returns, so a component that runs after this one and before the economy sees the request waiting).</summary>
    private sealed class Requester : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "requester";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context)
        {
            if (context.Tick == 1) _events!.Emit(new EconomyTradeRequested("late", "p", "riverton", "grain", 1));
        }
    }

    private sealed class Taker(EconomyComponent component, List<string> codes, List<int> sizes) : IGameComponent
    {
        public string Id => "taker";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (context.Tick == 1) codes.Add(Samples.Code(() => EconomyCheckpoint.Capture(component)));
            else sizes.Add(EconomyCheckpoint.Capture(component).Length);
        }
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestIsWaitingForTheNextUpdate()
    {
        var codes = new List<string>();
        var sizes = new List<int>();
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new EconomyChannels();
        channels.Install(session);
        var facts = Samples.Host();
        var component = new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels);
        session.AddComponent(new Requester(), 5);
        session.AddComponent(new Taker(component, codes, sizes), 6);
        session.AddComponent(component, 10);
        session.Init();
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal([EconomyComponent.NotQuiet], codes);
        Assert.Single(sizes);
        Assert.True(sizes[0] > 1000);
        Assert.Equal(1, component.Answered);
    }

    private sealed class Snapshotter(EconomyComponent component, List<string> codes) : IGameComponent
    {
        public string Id => "snapshotter";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (!component.Channels.IsQuiet) codes.Add(Samples.Code(() => EconomyCheckpoint.Capture(component)));
        }
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARefusalForAnUnservedChannelIsWaitingInTheRegistry()
    {
        var codes = new List<string>();
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new EconomyChannels();
        channels.Install(session);
        var facts = Samples.Host();
        var component = new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels);
        session.AddComponent(component, 10);
        session.AddComponent(new Snapshotter(component, codes), 40);   // runs before the registry, which has the request but has not answered it
        session.Init();
        session.Submit(new EconomyTradeRequested("lost", "p", "riverton", "grain", 1, "nowhere"));
        session.Update(20_000);
        Assert.Equal([EconomyComponent.NotQuiet], codes);
        Assert.True(channels.IsQuiet);
        Assert.NotEmpty(EconomyCheckpoint.Capture(component));
    }

    [Fact]
    public void AResumedEconomyContinuesExactlyAsTheOneThatNeverStopped()
    {
        var live = new EcoHost();
        live.Tick(7);
        live.Trade("seaport", "iron", 12);
        live.Tick(3);
        var bytes = EconomyCheckpoint.Capture(live.Component);
        var tick = live.Component.Tick;
        var resumed = new EcoHost(runtime: EconomyCheckpoint.Open(live.Runtime.Set, bytes), startTick: tick, clock: new TickEconomyClock(1, live.Runtime.Step - tick));
        for (var i = 0; i < 40; i++)
        {
            if (i == 5) { live.Trade("hillfort", "iron", -30); resumed.Trade("hillfort", "iron", -30); }
            live.Tick(); resumed.Tick();
            Assert.Equal(live.Runtime.StateDigest(), resumed.Runtime.StateDigest());
        }
    }

    // ---- faults and arguments --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AComponentWhoseRegistryIsNotRunningOnItsSessionFailsItsFirstUpdate()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(50, 8), reporter);
        new EconomyChannels().Install(session);
        var facts = Samples.Host();
        var elsewhere = new EconomyChannels();
        var other = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        elsewhere.Install(other);
        other.Init();
        session.AddComponent(new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, elsewhere), 10);
        session.Init();
        session.Update(20_000);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Contains(reporter.Reports, r => r.StartsWith("RUNTIME_FAULT", StringComparison.Ordinal) && r.Contains("does not run on this session", StringComparison.Ordinal));
    }

    [Fact]
    public void ABadArgumentIsRefusedAtTheConstructorAndTheClocks()
    {
        var channels = new EconomyChannels();
        var facts = Samples.Host();
        Assert.Throws<ArgumentException>(() => new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels, maxEvents: 0));
        Assert.Throws<ArgumentException>(() => new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels, maxEvents: 10, maxOwed: 9));
        Assert.Throws<ArgumentNullException>(() => new EconomyComponent(null!, new TickEconomyClock(1), () => facts, channels));
        Assert.Throws<ArgumentNullException>(() => new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), null!, () => facts, channels));
        Assert.Throws<ArgumentNullException>(() => new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), null!, channels));
        Assert.Throws<ArgumentNullException>(() => new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, null!));
        _ = new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => facts, channels, maxEvents: 10, maxOwed: 10);      // exactly one Update's worth is enough
        Assert.Equal(EconomyCodes.Range, Samples.Code(() => new TickEconomyClock(0)));
        Assert.Equal(EconomyCodes.StepOrder, Samples.Code(() => new TickEconomyClock(1, -1)));
        Assert.Equal(10 + 7 / 3, new TickEconomyClock(3, 10).StepAt(7));
    }

    [Fact]
    public void TheFingerprintNamesTheTickTheStepTheStateAndWhatIsWaiting()
    {
        var host = new EcoHost();
        host.Trade("riverton", "grain", 1);
        host.Tick(3);
        var fingerprint = host.Component.Fingerprint();
        Assert.StartsWith("economy tick=3 step=3 state=" + host.Runtime.StateDigest()[..16], fingerprint, StringComparison.Ordinal);
        Assert.EndsWith(" answered=1 pending=0 owed=" + host.Component.Owed, fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameRequestsGiveTheSameTranscriptAndOtherRequestsAnother()
    {
        string Run(long quantity)
        {
            var host = new EcoHost();
            host.Tick(2);
            host.Trade("riverton", "grain", quantity);
            host.Tick(20);
            return host.Transcript.Hash;
        }
        Assert.Equal(Run(30), Run(30));
        Assert.NotEqual(Run(30), Run(31));
    }
}
