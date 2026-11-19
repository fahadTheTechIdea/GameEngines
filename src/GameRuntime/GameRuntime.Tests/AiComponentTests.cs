using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The AI component on the game layer: requests only queue, answers come in Update in arrival order, intents are events, refusals are events and the budget is strict.</summary>
public sealed class AiComponentTests
{
    private const string Document = """
    { "format": 1, "id": "demo.component",
      "keys": [ { "id": "ready", "type": "bool", "initial": false, "source": "host" }, { "id": "target", "type": "string", "initial": "", "source": "agent" } ],
      "percepts": [ { "kind": "unit", "capacity": 4, "memory": 100, "owned": false, "attrs": [
          { "id": "hp", "type": "int", "min": 0, "max": 100, "unknown": 0 }, { "id": "label", "type": "string", "unknown": "" } ] },
        { "kind": "depot", "capacity": 4, "ownerCapacity": 2, "memory": 100, "owned": true, "attrs": [ { "id": "held", "type": "int", "min": 0, "max": 1000, "unknown": 0 } ] } ],
      "intents": [ { "id": "ping", "args": [ { "id": "n", "type": "int", "min": 0, "max": 1000 } ] },
                   { "id": "report", "args": [ { "id": "unit", "type": "string" }, { "id": "label", "type": "string" } ] } ],
      "behaviors": [
        { "id": "pinger", "every": 1, "root": { "id": "p", "kind": "sequence", "resume": false, "children": [
            { "id": "is-ready", "kind": "condition", "when": { "fact": "key.ready", "op": "eq", "value": true } },
            { "id": "ping", "kind": "act", "intent": "ping", "args": { "n": { "op": "mod", "args": [ { "op": "tick" }, 1000 ] } } } ] } },
        { "id": "watcher", "every": 1, "root": { "id": "w", "kind": "sequence", "resume": false, "children": [
            { "id": "pick", "kind": "select", "from": "unit", "into": "target", "pick": { "min": "hp" } },
            { "id": "report", "kind": "act", "intent": "report", "args": { "unit": { "key": "target" }, "label": { "known": { "from": "unit", "id": { "key": "target" }, "attr": "label" } } } } ] } } ] }
    """;

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    private sealed class Host
    {
        public Reporter Reporter { get; } = new();
        public RuntimeSession Runtime { get; }
        public AiComponent Component { get; }
        public List<string> Heard { get; } = new();
        private readonly RuntimeClock _clock;

        public Host(ulong seed = 1, AiRuntime? resume = null, long budget = 100, IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, string? document = null)
        {
            _clock = new RuntimeClock(30, startTick: resume?.LastTick ?? 0);   // a resumed session continues from the tick of its checkpoint
            Runtime = new RuntimeSession(_clock, Reporter);
            AiEventCatalog.Declare(Runtime);
            Component = new AiComponent(resume ?? new AiRuntime(AiLoader.Load(document ?? Document), seed), budget);
            Runtime.AddComponent(Component, 100);
            foreach (var (component, order) in extra ?? []) Runtime.AddComponent(component, order);
            Runtime.Subscribe<AiIntentRaised>(e => Heard.Add($"{e.Tick} intent {e.Agent} {e.Node} {e.Name} {e.Args}"));
            Runtime.Subscribe<AiStepDone>(e => Heard.Add($"{e.Tick} step thought={e.Thought} spent={e.Spent} deferred={e.Deferred} wait={e.LongestWait} intents={e.Intents}"));
            Runtime.Subscribe<AiRefused>(e => Heard.Add($"{e.Tick} refused {e.RequestId} {e.Code}"));
            Runtime.Init();
        }

        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) Runtime.Update((RuntimeClock.MicrosecondsPerSecond + _clock.TicksPerSecond - 1) / _clock.TicksPerSecond);
        }
    }

    /// <summary>Emits one add request from inside a tick, as another component would.</summary>
    private sealed class Emitter : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "emitter";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context) { if (context.Tick == 1) _events!.Emit(new AiAgentAddRequested("ai", "emitted", "e", "pinger")); }
    }

    /// <summary>Tries a checkpoint after the request was delivered and before the AI component answered it.</summary>
    private sealed class Probe : IGameComponent
    {
        public AiComponent? Component { get; set; }
        public string? Code { get; private set; }
        public string Id => "probe";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (context.Tick == 1) Code = Assert.Throws<EngineRefusedException>(() => AiCheckpoint.Capture(Component!)).Code;
        }
    }

    [Fact]
    public void ARequestChangesNothingUntilTheNextUpdate()
    {
        var host = new Host();
        var before = host.Component.Fingerprint();
        host.Runtime.Submit(new AiAgentAddRequested("ai", "r1", "p1", "pinger"));
        host.Runtime.Submit(new AiKeyPosted("ai", "r2", "p1", "ready", "true"));
        Assert.Equal(before, host.Component.Fingerprint());
        Assert.Empty(host.Component.Runtime.Agents);
        Assert.Empty(host.Heard);
        host.Tick();
        Assert.Equal(["p1"], host.Component.Runtime.Agents);
        Assert.Equal(["1 intent p1 ping ping n=1", "1 step thought=1 spent=3 deferred=0 wait=0 intents=1"], host.Heard);   // the key was applied before the agent thought
        host.Tick(2);
        Assert.StartsWith("ai tick=3 state=", host.Component.Fingerprint());
        Assert.EndsWith(" steps=3 pending=0", host.Component.Fingerprint());
        Assert.Empty(host.Reporter.Codes);
    }

    [Fact]
    public void RequestsAreAppliedInTheOrderTheyArrivedAndIntentsCarryTheirArguments()
    {
        var host = new Host();
        host.Runtime.Submit(new AiAgentAddRequested("ai", "a", "w1", "watcher"));
        host.Runtime.Submit(new AiPerceptPosted("ai", "b", "w1", "unit", "u2", "", "hp=40;label=weak%3Bfoe"));
        host.Runtime.Submit(new AiPerceptPosted("ai", "c", "w1", "unit", "u1", "", "hp=70;label=a%3Db%25"));
        host.Tick();
        Assert.Equal(["1 intent w1 report report label=weak%3Bfoe;unit=u2", "1 step thought=1 spent=5 deferred=0 wait=0 intents=1"], host.Heard);
        host.Heard.Clear();
        host.Runtime.Submit(new AiPerceptLost("ai", "d", "w1", "unit", "u2"));
        host.Tick();
        Assert.Equal(["2 intent w1 report report label=a%3Db%25;unit=u1", "2 step thought=1 spent=4 deferred=0 wait=0 intents=1"], host.Heard);
    }

    [Fact]
    public void AThingOfAnOwnedKindKeepsItsOwnerAndOneWithoutAnOwnerIsRefused()
    {
        var host = new Host();
        host.Runtime.Submit(new AiAgentAddRequested("ai", "a", "w1", "watcher"));
        host.Runtime.Submit(new AiPerceptPosted("ai", "b", "w1", "depot", "d1", "north", "held=40"));
        host.Runtime.Submit(new AiPerceptPosted("ai", "c", "w1", "depot", "d2", "", "held=40"));
        host.Tick();
        var known = Assert.Single(host.Component.Runtime.Known("w1", "depot"));
        Assert.Equal(("d1", "north", 1L), (known.Id, known.Owner, known.Seen));
        Assert.Contains("1 refused c AI_PERCEPT", host.Heard);
    }

    [Fact]
    public void TheSameRequestsGiveTheSameEventsAndFingerprintEveryRun()
    {
        string Run()
        {
            var host = new Host(3);
            host.Runtime.Submit(new AiAgentAddRequested("ai", "a", "p1", "pinger"));
            host.Runtime.Submit(new AiAgentAddRequested("ai", "b", "w1", "watcher"));
            host.Tick();
            host.Runtime.Submit(new AiKeyPosted("ai", "c", "p1", "ready", "true"));
            host.Runtime.Submit(new AiPerceptPosted("ai", "d", "w1", "unit", "u1", "", "hp=5;label=x"));
            host.Tick(5);
            return string.Join('\n', host.Heard) + "\n" + host.Component.Fingerprint();
        }
        var one = Run();
        Assert.Equal(one, Run());
        Assert.Contains("ping n=5", one);
    }

    [Fact]
    public void ARefusalIsAnEventWithItsCodeAndTheAgentsAreAsTheyWereAndNothingFaults()
    {
        var host = new Host();
        host.Runtime.Submit(new AiAgentAddRequested("ai", "a", "p1", "pinger"));
        host.Tick();
        host.Heard.Clear();
        host.Runtime.Submit(new AiAgentAddRequested("ai", "r1", "p1", "pinger"));                                  // already there
        host.Runtime.Submit(new AiAgentAddRequested("ai", "r2", "p2", "nothing"));                                 // no such behaviour
        host.Runtime.Submit(new AiAgentRemoveRequested("ai", "r3", "ghost"));
        host.Runtime.Submit(new AiPerceptPosted("ai", "r4", "p1", "ghost", "x", "", ""));
        host.Runtime.Submit(new AiPerceptPosted("ai", "r5", "p1", "unit", "x", "", "hp=500;label=a"));            // above its range
        host.Runtime.Submit(new AiPerceptPosted("ai", "r6", "p1", "unit", "x", "", "hp=abc;label=a"));            // not a number
        host.Runtime.Submit(new AiPerceptPosted("ai", "r7", "p1", "unit", "x", "", "label=a"));                   // hp missing
        host.Runtime.Submit(new AiPerceptPosted("ai", "r8", "p1", "unit", "x", "", "hp=1;hp=2;label=a"));         // given twice
        host.Runtime.Submit(new AiPerceptPosted("ai", "r9", "p1", "unit", "x", "", "hp=1;label=a;wealth=3"));     // not declared
        host.Runtime.Submit(new AiKeyPosted("ai", "r10", "p1", "target", "u1"));                                   // the agent own key
        host.Runtime.Submit(new AiKeyPosted("ai", "r11", "p1", "ready", "maybe"));
        host.Runtime.Submit(new AiKeyPosted("ai", "r12", "p1", "nothing", "1"));
        host.Tick();
        Assert.Equal(
        [
            "2 refused r1 AI_DUPLICATE_AGENT", "2 refused r2 AI_UNKNOWN_BEHAVIOR", "2 refused r3 AI_UNKNOWN_AGENT", "2 refused r4 AI_PERCEPT", "2 refused r5 AI_PERCEPT",
            "2 refused r6 AI_PERCEPT", "2 refused r7 AI_PERCEPT", "2 refused r8 AI_PERCEPT", "2 refused r9 AI_PERCEPT", "2 refused r10 AI_KEY_WRITE", "2 refused r11 AI_VALUE_TYPE", "2 refused r12 AI_REFERENCE",
            "2 step thought=1 spent=2 deferred=0 wait=0 intents=0",   // the agent thought once, not ready, whatever was asked of it
        ], host.Heard);
        // Everything refused left no trace: the agents are exactly where a session that was never asked those things has them.
        var control = new Host();
        control.Runtime.Submit(new AiAgentAddRequested("ai", "a", "p1", "pinger"));
        control.Tick(2);
        Assert.Equal(AiSave.StateDigest(control.Component.Runtime), AiSave.StateDigest(host.Component.Runtime));
        Assert.Empty(host.Reporter.Codes);
        Assert.Equal(RuntimeState.Running, host.Runtime.State);
    }

    [Fact]
    public void TheBudgetIsStrictAndAnOverloadedTickReportsWhoWaitedAndForHowLong()
    {
        // A pinger costs 3 units when ready (the sequence, the condition, the act). With 7 units a tick two fit.
        var host = new Host(budget: 7);
        for (var i = 0; i < 5; i++)
        {
            host.Runtime.Submit(new AiAgentAddRequested("ai", "a" + i, "p" + i, "pinger"));
            host.Runtime.Submit(new AiKeyPosted("ai", "k" + i, "p" + i, "ready", "true"));
        }
        host.Tick();
        Assert.Equal("1 step thought=2 spent=6 deferred=3 wait=0 intents=2", host.Heard.Single(l => l.Contains(" step ", StringComparison.Ordinal)));
        host.Heard.Clear();
        host.Tick();
        // At tick 2 the three still due from tick 1 go first (two of them), then the one of the next in line.
        var step = host.Heard.Single(l => l.Contains(" step ", StringComparison.Ordinal));
        Assert.StartsWith("2 step thought=2 spent=6 deferred=", step);
        Assert.Contains("wait=1", step);
        Assert.Equal(["p2", "p3"], host.Heard.Where(l => l.Contains(" intent ", StringComparison.Ordinal)).Select(l => l.Split(' ')[2]));
    }

    private static JournalCodec Codec() => new JournalCodec()
        .Register("ai.add", e => new AiAgentAddRequested(e.GetProperty("Component").GetString()!, e.GetProperty("RequestId").GetString()!, e.GetProperty("Agent").GetString()!, e.GetProperty("Behavior").GetString()!))
        .Register("ai.key", e => new AiKeyPosted(e.GetProperty("Component").GetString()!, e.GetProperty("RequestId").GetString()!, e.GetProperty("Agent").GetString()!, e.GetProperty("Key").GetString()!, e.GetProperty("Value").GetString()!))
        .Register("ai.percept", e => new AiPerceptPosted(e.GetProperty("Component").GetString()!, e.GetProperty("RequestId").GetString()!, e.GetProperty("Agent").GetString()!, e.GetProperty("Kind").GetString()!,
            e.GetProperty("Id").GetString()!, e.GetProperty("Owner").GetString()!, e.GetProperty("Attrs").GetString()!));

    private static (IGameRuntime Runtime, SessionJournal Journal) Journaled(JournalCodec codec, bool record)
    {
        var journal = new SessionJournal();
        var session = new RuntimeSession(new RuntimeClock(30), new Reporter());
        AiEventCatalog.Declare(session);
        var component = new AiComponent(new AiRuntime(AiLoader.Load(Document), 5), 100);
        session.AddComponent(component, 100);
        session.AddComponent(new JournalComponent(journal, component.Fingerprint), 900);
        return (record ? new JournalingRuntime(session, journal, codec) : session, journal);
    }

    [Fact]
    public void ARecordedSessionReplaysTickForTickAndAChangedPerceptIsFoundAtItsTick()
    {
        var codec = Codec();
        var (runtime, journal) = Journaled(codec, record: true);
        runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + runtime.Clock.TicksPerSecond - 1) / runtime.Clock.TicksPerSecond;
        runtime.Submit(new AiAgentAddRequested("ai", "a", "w1", "watcher"));
        runtime.Submit(new AiAgentAddRequested("ai", "b", "p1", "pinger"));
        runtime.Update(oneTick);
        runtime.Submit(new AiKeyPosted("ai", "k", "p1", "ready", "true"));
        runtime.Submit(new AiPerceptPosted("ai", "u", "w1", "unit", "u1", "", "hp=9;label=x"));
        for (var i = 0; i < 4; i++) runtime.Update(oneTick);
        Assert.Equal(5, journal.Hashes.Count);
        Assert.True(JournalReplay.Verify(journal, codec, () => Journaled(codec, record: false)).Matches);
        var changed = SessionJournal.Deserialize(journal.Serialize());
        var at = changed.Inputs.FindIndex(i => i.Type == "ai.percept");
        changed.Inputs[at] = changed.Inputs[at] with { Json = changed.Inputs[at].Json.Replace("x", "y", StringComparison.Ordinal) };
        var verdict = JournalReplay.Verify(changed, codec, () => Journaled(codec, record: false));
        Assert.False(verdict.Matches);
        Assert.Equal(changed.Inputs[at].Tick, verdict.DivergedAtTick);
    }

    [Fact]
    public void ARequestIsForTheComponentItNamesAndOtherAiComponentsOfTheSessionNeverHearIt()
    {
        var other = new AiComponent(new AiRuntime(AiLoader.Load(Document), 1), 100, "second");
        var host = new Host(extra: [(other, 150)]);
        var heard = new List<string>();
        host.Runtime.Subscribe<AiIntentRaised>(e => heard.Add(e.Source + ":" + e.Agent));
        host.Runtime.Subscribe<AiRefused>(e => heard.Add(e.Source + " refused " + e.RequestId + " " + e.Code));
        host.Runtime.Submit(new AiAgentAddRequested("ai", "a", "p1", "pinger"));
        host.Runtime.Submit(new AiAgentAddRequested("second", "b", "p2", "pinger"));
        host.Runtime.Submit(new AiKeyPosted("ai", "c", "p1", "ready", "true"));
        host.Runtime.Submit(new AiKeyPosted("second", "d", "p2", "ready", "true"));
        host.Runtime.Submit(new AiKeyPosted("second", "e", "p1", "ready", "true"));          // p1 belongs to the other component
        host.Runtime.Submit(new AiKeyPosted("nobody", "f", "p1", "ready", "false"));         // no such component: heard by none
        host.Tick();
        Assert.Equal(["p1"], host.Component.Runtime.Agents);
        Assert.Equal(["p2"], other.Runtime.Agents);
        Assert.Equal(["ai:p1", "second:p2", "second refused e AI_UNKNOWN_AGENT"], heard.OrderBy(h => h.StartsWith("second refused", StringComparison.Ordinal)).ThenBy(h => h, StringComparer.Ordinal));
    }

    [Fact]
    public void AComponentCannotBeBuiltWithABudgetBelowTheLargestBehaviour()
    {
        var runtime = new AiRuntime(AiLoader.Load(Document), 1);
        Assert.Equal(AiCodes.Budget, Assert.Throws<EngineRefusedException>(() => new AiComponent(runtime, runtime.Set.MaxCost - 1)).Code);
        Assert.NotNull(new AiComponent(runtime, runtime.Set.MaxCost));
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestWaitsAndAResumedSessionContinuesTheSameWay()
    {
        var probe = new Probe();
        var host = new Host(extra: [(new Emitter(), 50), (probe, 60)]);
        probe.Component = host.Component;
        host.Tick();
        Assert.Equal(AiComponent.NotQuiet, probe.Code);                      // the request had been delivered and not yet answered
        Assert.True(host.Component.IsQuiet);
        Assert.Equal(["e"], host.Component.Runtime.Agents);

        var live = new Host();
        live.Runtime.Submit(new AiAgentAddRequested("ai", "a", "p1", "pinger"));
        live.Runtime.Submit(new AiAgentAddRequested("ai", "b", "w1", "watcher"));
        live.Runtime.Submit(new AiKeyPosted("ai", "c", "p1", "ready", "true"));
        live.Runtime.Submit(new AiPerceptPosted("ai", "d", "w1", "unit", "u1", "", "hp=9;label=l"));
        live.Tick(4);
        var bytes = AiCheckpoint.Capture(live.Component);
        var resumed = new Host(resume: AiCheckpoint.Open(live.Component.Runtime.Set, bytes));
        live.Heard.Clear();
        live.Tick(6);
        resumed.Tick(6);
        Assert.Equal(live.Heard, resumed.Heard);
        Assert.Contains(live.Heard, l => l.StartsWith("10 intent p1 ping ping n=10", StringComparison.Ordinal));
        Assert.Equal(AiSave.StateDigest(live.Component.Runtime), AiSave.StateDigest(resumed.Component.Runtime));
        Assert.Equal(64, AiSave.StateDigest(live.Component.Runtime).Length);
        bytes[bytes.Length / 2] ^= 0x20;
        Assert.StartsWith("AI_SAVE", Assert.Throws<EngineRefusedException>(() => AiCheckpoint.Open(live.Component.Runtime.Set, bytes)).Code);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("a;b")]
    [InlineData("a=b")]
    [InlineData("100%")]
    [InlineData("%3B")]
    [InlineData("%253B;=;=%")]
    [InlineData("")]
    public void TextTravelsInEventsWithoutLosingAnyCharacter(string text)
    {
        var kind = AiLoader.Load(Document).Percepts.Single(p => p.Id == "unit");
        var encoded = AiValues.Encode(new Dictionary<string, FactValue> { ["hp"] = FactValue.From(7L), ["label"] = FactValue.From(text) });
        var decoded = AiValues.Decode(kind, encoded, AiCodes.Percept);
        Assert.Equal(FactValue.From(text), decoded["label"]);
        Assert.Equal(FactValue.From(7L), decoded["hp"]);
        Assert.DoesNotContain(';', AiValues.Encode(FactValue.From(text)));
    }

    [Fact]
    public void TheComponentReadsNoClockAndNoRandomSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "GameRuntime", "GameRuntime.AI"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var sources = Directory.GetFiles(Path.Combine(directory!.FullName, "src", "GameRuntime", "GameRuntime.AI"), "*.cs").ToList();
        Assert.NotEmpty(sources);
        foreach (var file in sources)
            foreach (var forbidden in new[] { "DateTime", "Stopwatch", "Environment.TickCount", "System.Random", "new Random(", "Guid.NewGuid", "Task.Delay", "Thread.Sleep" })
                Assert.DoesNotContain(forbidden, File.ReadAllText(file), StringComparison.Ordinal);
    }
}
