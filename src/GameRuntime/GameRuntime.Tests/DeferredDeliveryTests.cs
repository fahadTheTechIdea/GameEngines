using GameRuntime.Headless;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The XNA / Godot model: raising an event or writing input changes nothing by itself. It is queued, and only the session's
/// next clock-driven Update processes it, in the tick that delivers it.
/// </summary>
public sealed class DeferredDeliveryTests
{
    private sealed class QuietReporter : IEngineReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(EngineNotice notice) => Codes.Add(notice.Code);
        public void Audit(AuditRecord record) { }
    }

    private sealed class SilentRuntimeReporter : IRuntimeReporter
    {
        public void Report(string code, string message, Exception? exception) { }
    }

    private static async Task<(HeadlessSession Session, List<string> Heard, SessionJournal Journal)> Game()
    {
        var environment = await HeadlessEnvironment.LoadAsync(new QuietReporter());
        var journal = new SessionJournal();
        var codec = new JournalCodec().Register<InputEdgeRaised>("input-edge", e =>
            new InputEdgeRaised(e.GetProperty("Action").GetString()!, e.GetProperty("Argument").GetString()!, e.GetProperty("Pressed").GetBoolean()));
        var session = HeadlessSession.Create(environment, 1, journal: journal, recordInputWith: codec);
        var heard = new List<string>();
        session.Runtime.Subscribe<PlayerDecided>(e => heard.Add($"t{e.Tick} player {e.Kind} {e.Detail}"));
        session.Runtime.Subscribe<MissionEvent>(e => heard.Add($"t{e.Tick} mission {e.Kind} {e.PayloadJson}"));
        session.Runtime.Subscribe<ObservationPosted>(e => heard.Add($"t{e.Tick} {e.Kind}"));
        session.Runtime.Init();
        return (session, heard, journal);
    }

    private static long OneTick(HeadlessSession session) => (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;

    [Fact]
    public async Task InputWrittenAndEventsSubmittedWithoutAnUpdateChangeNothing()
    {
        var (session, heard, _) = await Game();
        var before = session.Fingerprint();
        var receipts = session.Mission.Snapshot.Receipts.Count;

        session.Input.Press(PlayerActions.Sell, "10");
        session.Input.Release(PlayerActions.Sell);
        session.Runtime.Submit(new MissionCommandRequested("x1", HeadlessSession.Controller, TycoonMissionLayer.Sell, "{\"units\":1}"));

        Assert.Equal(before, session.Fingerprint());                 // sim hash (cash included) and mission hash unchanged
        Assert.Equal(0, session.Game.Tick);
        Assert.Equal(0, session.Mission.Snapshot.Tick);
        Assert.Equal(receipts, session.Mission.Snapshot.Receipts.Count);
        Assert.Empty(session.Mission.Snapshot.PendingCommands);
        Assert.Empty(heard);                                         // no output
        Assert.Equal(2, session.Input.PendingEdges);                 // still waiting to be polled
    }

    [Fact]
    public async Task TheEffectAppearsInTheUpdateThatDeliversItAndNotBefore()
    {
        var (session, heard, journal) = await Game();
        session.Input.Press(PlayerActions.Sell, "10");
        session.Input.Release(PlayerActions.Sell);
        session.Runtime.Submit(new MissionCommandRequested("x1", HeadlessSession.Controller, TycoonMissionLayer.Sell, "{\"units\":1}"));

        // The journal already knows which tick will deliver the input.
        Assert.Equal([1L, 1L], journal.Inputs.Select(i => i.Tick).ToArray());

        session.Runtime.Update(OneTick(session));                    // tick 1
        // The submitted command was delivered at the start of tick 1 and applied by the mission in that Update.
        Assert.Equal(CommandStatus.Applied, session.Mission.Snapshot.Receipts["x1"].Status);
        Assert.Contains(heard, h => h.StartsWith("t1 mission gf_tycoon.sold", StringComparison.Ordinal) && h.Contains("\"units\":1,"));
        // The player polled its input in the same Update and raised a command; the mission has only queued it.
        Assert.Contains(heard, h => h.StartsWith("t1 player raise sell 10 as p1-0", StringComparison.Ordinal));
        Assert.DoesNotContain(heard, h => h.Contains("\"units\":10"));
        Assert.Single(session.Mission.Snapshot.PendingCommands);

        session.Runtime.Update(OneTick(session));                    // tick 2
        Assert.Contains(heard, h => h.StartsWith("t2 mission gf_tycoon.sold", StringComparison.Ordinal) && h.Contains("\"units\":10"));
        Assert.Empty(session.Mission.Snapshot.PendingCommands);
    }

    [Fact]
    public async Task EachEdgeIsPolledOnceWhileTheHeldStateStaysUntilReleased()
    {
        var (session, heard, _) = await Game();
        session.Input.Press(PlayerActions.AutoSell, "1");
        session.Runtime.Update(OneTick(session));                    // tick 1: edge polled, key held
        session.Runtime.Update(OneTick(session));                    // tick 2: no new edge, still held
        session.Input.Release(PlayerActions.AutoSell);
        session.Runtime.Update(OneTick(session));                    // tick 3: released

        Assert.Contains(heard, h => h.StartsWith("t1 player raise sell 1 as p1-0 from held", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.StartsWith("t2 player raise sell 1 as p2-0 from held", StringComparison.Ordinal));
        Assert.DoesNotContain(heard, h => h.StartsWith("t3 player raise", StringComparison.Ordinal));
        Assert.True(session.Input.IsQuiet);
    }

    [Fact]
    public void AnInputStateKeepsHeldStateAndHandsOutEachEdgeOnce()
    {
        long next = 5;
        var written = new List<(long Tick, string Action, bool Pressed)>();
        var input = new InputState(() => next, (tick, edge) => written.Add((tick, edge.Action, edge.Pressed)));

        input.Press("fire", "3");
        Assert.True(input.IsHeld("fire", out var argument));
        Assert.Equal("3", argument);
        Assert.False(input.IsQuiet);

        var first = input.Poll();
        Assert.Single(first);
        Assert.True(first[0].Pressed);
        Assert.Empty(input.Poll());                                  // the edge is gone ...
        Assert.True(input.IsHeld("fire", out _));                    // ... the held state is not

        next = 6;
        input.Release("fire");
        Assert.False(input.IsHeld("fire", out _));
        Assert.Single(input.Poll());
        Assert.True(input.IsQuiet);

        input.Press("tap");                                          // a tap inside one poll window: an edge, but not held
        input.Release("tap");
        var tap = input.Poll();
        Assert.Equal([true, false], tap.Select(e => e.Pressed).ToArray());
        Assert.False(input.IsHeld("tap", out _));

        Assert.Equal([(5L, "fire", true), (6L, "fire", false), (6L, "tap", true), (6L, "tap", false)], written);
    }

    [Fact]
    public void AnEventRaisedInAnUpdateIsNotSeenByAComponentThatAlreadyRanThisTick()
    {
        var hostHeard = new List<long>();
        var early = new Probe("early", raises: false);
        var late = new Probe("late", raises: true, () => hostHeard.Count);
        var session = new RuntimeSession(new RuntimeClock(30), new SilentRuntimeReporter());
        session.AddComponent(early, 10);
        session.AddComponent(late, 20);
        session.Subscribe<ObservationPosted>(e => hostHeard.Add(e.Tick));
        session.Init();

        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);   // tick 1: `late` raises after `early` has updated
        Assert.Equal([0], early.SeenAtUpdate);                         // early's Update ran before the raise
        Assert.Equal([0], late.SeenAtUpdate);
        Assert.Equal(1, early.HandlerCalls);                           // the handler runs once the raising Update has returned
        Assert.Equal(0, late.HandlerCallsRightAfterEmit);              // nothing is delivered inside the call that raised it
        Assert.Equal(0, late.HostHeardRightAfterEmit);                 // and a host subscriber hears nothing during the Update

        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);   // tick 2: now early's Update sees it
        Assert.Equal([0, 1], early.SeenAtUpdate);
        Assert.Equal(1, hostHeard.Count(t => t == 1));
    }

    [Fact]
    public void TheUpdateContextCarriesTheFixedDeltaAndTheTotalGameTime()
    {
        var probe = new Probe("probe", raises: false);
        var session = new RuntimeSession(new RuntimeClock(30), new SilentRuntimeReporter());
        session.AddComponent(probe, 1);
        session.Init();
        for (var i = 0; i < 3; i++) session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);

        Assert.Equal([1L, 2L, 3L], probe.Ticks);
        Assert.Equal([33_333L, 66_666L, 100_000L], probe.Totals);   // exactly tick / 30 seconds, floored
        Assert.All(probe.Deltas, delta => Assert.Equal(33_333L, delta));
    }

    private sealed class Probe(string id, bool raises, Func<int>? hostCount = null) : IGameComponent
    {
        private IEventPort? _events;
        private int _received;

        public string Id => id;
        public List<int> SeenAtUpdate { get; } = new();
        public List<long> Ticks { get; } = new();
        public List<long> Totals { get; } = new();
        public List<long> Deltas { get; } = new();
        public int HandlerCalls => _received;
        public int HandlerCallsRightAfterEmit { get; private set; } = -1;
        public int HostHeardRightAfterEmit { get; private set; } = -1;

        public void Init(in ComponentContext context)
        {
            _events = context.Events;
            context.Events.Connect<ObservationPosted>(_ => _received++);
        }

        public void Update(in UpdateContext context)
        {
            Ticks.Add(context.Tick);
            Totals.Add(context.TotalMicroseconds);
            Deltas.Add(context.TickMicroseconds);
            SeenAtUpdate.Add(_received);
            if (raises && context.Tick == 1)
            {
                _events!.Emit(new ObservationPosted("probe.raised", "{}", context.Tick));
                HandlerCallsRightAfterEmit = _received;      // nothing was delivered inside this call
                HostHeardRightAfterEmit = hostCount!();
            }
        }
    }
}
