using DialogueEngine;
using EngineCommon;
using GameContent.Catalog;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The dialogue component on the game layer: requests only queue, answers come in Update in arrival order, refusals are events, facts come from the host.</summary>
public sealed class DialogueComponentTests
{
    private const string Speaker = "{SPEAKER}";

    private const string Document = """
    {
     "format": 3, "id": "demo.component",
     "variables": [ { "id": "trust", "type": "int", "initial": 2, "min": 0, "max": 5 } ],
     "externalFacts": [ { "id": "hold-status", "type": "string" }, { "id": "squads", "type": "int", "min": 0, "max": 1000 } ],
     "speakers": [ { "id": "{SPEAKER}", "portraits": ["calm"] } ],
     "quests": [ { "id": "hold", "title": "quest.hold.title", "start": "order", "stages": [
        { "id": "order", "journal": "quest.hold.order", "advance": [ { "when": { "fact": "ext.hold-status", "op": "eq", "value": "completed" }, "to": "done" } ] },
        { "id": "done", "journal": "quest.hold.done", "advance": [], "ends": "completed" } ] } ],
     "conversations": [ { "id": "briefing", "start": "hello", "nodes": [
       { "id": "hello", "kind": "line", "speaker": "{SPEAKER}", "portrait": "calm", "line": "brief.hello", "args": { "count": "ext.squads" }, "next": "ask" },
       { "id": "ask", "kind": "choice", "pick": "player", "options": [
          { "id": "accept", "line": "brief.accept", "effects": [ { "startQuest": "hold" }, { "add": "trust", "by": 1 }, { "signal": "order-accepted" } ], "next": "thanks" },
          { "id": "refuse", "line": "brief.refuse", "next": "bye" } ] },
       { "id": "thanks", "kind": "line", "speaker": "{SPEAKER}", "portrait": "calm", "line": "brief.thanks", "next": "bye" },
       { "id": "bye", "kind": "end", "result": "done" } ] } ]
    }
    """;

    private static readonly ISpeakerCatalog Speakers = CatalogSpeakers.FromCharacters(CatalogCharacters.Document(CharacterBridgeTests.Load("arms-ancient"), ["arms-ancient.iron-warband"], CharacterBridgeTests.Template()));

    private static string SpeakerId => CatalogCharacters.ArchetypeId("arms-ancient.iron-warband");

    /// <summary>The document with one piece replaced; the piece must be there, so a test cannot pass by changing nothing.</summary>
    private static string Changed(string old, string replacement)
    {
        Assert.Contains(old, Document, StringComparison.Ordinal);
        return Document.Replace(old, replacement, StringComparison.Ordinal);
    }

    private static string Text(string? document = null) => (document ?? Document).Replace(Speaker, SpeakerId, StringComparison.Ordinal);

    private sealed class NullReporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    /// <summary>Emits one start request from inside a tick, the way another component would.</summary>
    private sealed class Emitter : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "emitter";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context) { if (context.Tick == 1) _events!.Emit(new DialogueStartRequested("emitted", "briefing")); }
    }

    /// <summary>Tries a checkpoint after the request was delivered and before the dialogue component has answered it.</summary>
    private sealed class Probe : IGameComponent
    {
        public DialogueComponent? Component { get; set; }
        public string? Code { get; private set; }
        public string Id => "probe";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (context.Tick != 1) return;
            Code = Assert.Throws<EngineRefusedException>(() => DialogueCheckpoint.Capture(Component!)).Code;
        }
    }

    private sealed class Host
    {
        public NullReporter Reporter { get; } = new();
        public RuntimeSession Runtime { get; }
        public DialogueComponent Component { get; }
        public List<string> Heard { get; } = new();
        public Dictionary<string, FactValue> Facts { get; } = new() { ["ext.hold-status"] = FactValue.From("active"), ["ext.squads"] = FactValue.From(7L) };
        private readonly RuntimeClock _clock;

        public long CurrentTick => _clock.Tick;

        public Host(ulong seed = 1, DialogueRuntime? resume = null, string? document = null, IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, long startTick = 0)
        {
            _clock = new RuntimeClock(30, 8, startTick);
            Runtime = new RuntimeSession(_clock, Reporter);
            var channels = new DialogueChannels();
            channels.Install(Runtime);
            var set = DialogueLoader.Load(Text(document), Speakers);
            Component = new DialogueComponent(resume ?? new DialogueRuntime(set, seed), Speakers, () => Facts, channels);
            Runtime.AddComponent(Component, 100);
            foreach (var (component, order) in extra ?? []) Runtime.AddComponent(component, order);
            Runtime.Subscribe<DialogueLineShown>(e => Heard.Add($"{e.Tick} line {e.RequestId} {e.Conversation}/{e.Node} {e.Speaker}/{e.Portrait} {e.Key} {e.Args}"));
            Runtime.Subscribe<DialogueChoicesShown>(e => Heard.Add($"{e.Tick} choices {e.RequestId} {e.Node} {e.Options}"));
            Runtime.Subscribe<DialogueEnded>(e => Heard.Add($"{e.Tick} ended {e.RequestId} {e.Conversation} {e.Result}"));
            Runtime.Subscribe<DialogueSignalRaised>(e => Heard.Add($"{e.Tick} signal {e.RequestId} {e.Name}"));
            Runtime.Subscribe<DialogueQuestChanged>(e => Heard.Add($"{e.Tick} quest {e.RequestId} {e.Quest} {e.Status} {e.Stage}"));
            Runtime.Subscribe<DialogueVariableChanged>(e => Heard.Add($"{e.Tick} var {e.RequestId} {e.Id}={e.Value}"));
            Runtime.Subscribe<DialogueReloaded>(e => Heard.Add($"{e.Tick} reloaded {e.RequestId} +{e.AddedVariables}|{e.AddedQuests}|{e.DroppedVisits}"));
            Runtime.Subscribe<DialogueRefused>(e => Heard.Add($"{e.Tick} refused {e.RequestId} {e.Code}"));
            Runtime.Init();
        }

        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) Runtime.Update((RuntimeClock.MicrosecondsPerSecond + _clock.TicksPerSecond - 1) / _clock.TicksPerSecond);
        }
    }

    [Fact]
    public void ARequestChangesNothingUntilTheNextUpdate()
    {
        var host = new Host();
        var before = host.Component.Fingerprint();
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        Assert.Equal(before, host.Component.Fingerprint());
        Assert.False(host.Component.Runtime.InConversation);
        Assert.Empty(host.Heard);
        host.Tick();
        Assert.True(host.Component.Runtime.InConversation);
        Assert.Equal(["1 line r1 briefing/hello " + SpeakerId + "/calm brief.hello count=7"], host.Heard);
        Assert.StartsWith("dialogue tick=1 state=", host.Component.Fingerprint());
        Assert.EndsWith(" answered=1 pending=0", host.Component.Fingerprint());
        Assert.Empty(host.Reporter.Codes);
    }

    [Fact]
    public void AConversationPlaysAsEventsInTheOrderTheRequestsArrived()
    {
        var host = new Host();
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        host.Runtime.Submit(new DialogueAdvanceRequested("r2"));
        host.Runtime.Submit(new DialogueChooseRequested("r3", "accept"));
        host.Runtime.Submit(new DialogueAdvanceRequested("r4"));
        host.Tick();   // all four arrive in one tick and are answered in order
        Assert.Equal(
        [
            "1 line r1 briefing/hello " + SpeakerId + "/calm brief.hello count=7",
            "1 choices r2 ask accept|brief.accept|1||0;refuse|brief.refuse|1||0",
            "1 quest r3 hold active order",
            "1 var r3 trust=3",
            "1 signal r3 order-accepted",
            "1 line r3 briefing/thanks " + SpeakerId + "/calm brief.thanks ",
            "1 ended r4 briefing done",
        ], host.Heard);
    }

    [Fact]
    public void TheSameRequestsGiveTheSameEventsAndFingerprintEveryRunAndOtherSeedsDoNotChangeANonRandomConversation()
    {
        string Run(ulong seed, bool withState = true)
        {
            var host = new Host(seed);
            host.Runtime.Submit(new DialogueStartRequested("a", "briefing"));
            host.Tick();
            host.Runtime.Submit(new DialogueAdvanceRequested("b"));
            host.Tick(2);
            host.Runtime.Submit(new DialogueChooseRequested("c", "refuse"));
            host.Tick();
            return string.Join('\n', host.Heard) + (withState ? "\n" + host.Component.Fingerprint() : "");
        }
        var one = Run(1);
        Assert.Equal(one, Run(1));
        // The seed is part of the state, so the fingerprints differ; the conversation itself rolls nothing, so what is heard does not.
        Assert.NotEqual(one, Run(99));
        Assert.Equal(Run(1, false), Run(99, false));
        Assert.Contains("4 ended c briefing done", one);
    }

    [Fact]
    public void ARefusalIsAnEventWithItsCodeAndTheStateIsAsItWasAndNothingFaults()
    {
        var host = new Host();
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        host.Tick();
        var before = host.Component.Fingerprint()[(host.Component.Fingerprint().IndexOf("state=", StringComparison.Ordinal))..];
        host.Heard.Clear();
        host.Runtime.Submit(new DialogueChooseRequested("r2", "accept"));      // a line is showing, not a choice
        host.Runtime.Submit(new DialogueStartRequested("r3", "briefing"));     // already open
        host.Runtime.Submit(new DialogueStartRequested("r4", "nothing"));
        host.Tick();
        Assert.Equal(["2 refused r2 DLG_NOT_WAITING", "2 refused r3 DLG_BUSY", "2 refused r4 DLG_BUSY"], host.Heard);
        Assert.Equal(before[..22], host.Component.Fingerprint()[(host.Component.Fingerprint().IndexOf("state=", StringComparison.Ordinal))..][..22]);
        Assert.Empty(host.Reporter.Codes);
        Assert.Equal(RuntimeState.Running, host.Runtime.State);
    }

    [Fact]
    public void FactsComeFromTheHostAtTheMomentOfEachAnswer()
    {
        var host = new Host();
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        host.Tick();
        host.Facts["ext.squads"] = FactValue.From(12L);
        host.Runtime.Submit(new DialogueAdvanceRequested("r2"));
        host.Runtime.Submit(new DialogueChooseRequested("r3", "refuse"));
        host.Tick();
        host.Runtime.Submit(new DialogueStartRequested("r4", "briefing"));
        host.Tick();
        Assert.Contains("1 line r1 briefing/hello " + SpeakerId + "/calm brief.hello count=7", host.Heard);
        Assert.Contains("3 line r4 briefing/hello " + SpeakerId + "/calm brief.hello count=12", host.Heard);
        // A fact the document needs and the host does not give is a refusal, never a guess.
        host.Facts.Remove("ext.squads");
        host.Runtime.Submit(new DialogueStartRequested("r5", "briefing"));
        host.Tick();
        Assert.Contains("4 refused r5 DLG_BUSY", host.Heard);   // the conversation r4 opened is still open
    }

    [Fact]
    public void AMissingFactRefusesTheStartAndLeavesNoConversationOpen()
    {
        var host = new Host();
        host.Facts.Remove("ext.squads");
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        host.Tick();
        Assert.Equal(["1 refused r1 DLG_FACT_MISSING"], host.Heard);
        Assert.False(host.Component.Runtime.InConversation);
    }

    [Fact]
    public void QuestsMoveWhenTheHostAsksAndReportTheirChange()
    {
        var host = new Host();
        host.Runtime.Submit(new DialogueQuestStartRequested("q1", "hold"));
        host.Runtime.Submit(new DialogueQuestsUpdateRequested("q2"));
        host.Tick();
        Assert.Equal(["1 quest q1 hold active order"], host.Heard);
        host.Facts["ext.hold-status"] = FactValue.From("completed");
        host.Heard.Clear();
        host.Runtime.Submit(new DialogueQuestsUpdateRequested("q3"));
        host.Tick();
        Assert.Equal(["2 quest q3 hold completed done"], host.Heard);
        host.Heard.Clear();
        host.Runtime.Submit(new DialogueQuestStartRequested("q4", "hold"));
        host.Runtime.Submit(new DialogueQuestStartRequested("q5", "nothing"));
        host.Tick();
        Assert.Equal(["3 refused q4 DLG_QUEST_STATE", "3 refused q5 DLG_REFERENCE"], host.Heard);
    }

    [Fact]
    public void AReloadIsAnsweredWithItsReportAndABadDocumentWithItsCodeAndTheOldOneStays()
    {
        var host = new Host();
        var more = Text(Document.Replace("\"variables\": [", "\"variables\": [ { \"id\": \"fame\", \"type\": \"int\", \"initial\": 1, \"min\": 0, \"max\": 9 },", StringComparison.Ordinal));
        host.Runtime.Submit(new DialogueReloadRequested("l1", more));
        host.Tick();
        Assert.Equal(["1 reloaded l1 +fame||0"], host.Heard);
        Assert.True(host.Component.Runtime.Set.Variables.ContainsKey("fame"));
        host.Heard.Clear();
        var digest = host.Component.Runtime.Set.Digest;
        host.Runtime.Submit(new DialogueReloadRequested("l2", "{ \"format\": 2 }"));
        host.Runtime.Submit(new DialogueReloadRequested("l3", Text(Changed("\"startQuest\": \"hold\"", "\"startQuest\": \"other\""))));
        host.Runtime.Submit(new DialogueReloadRequested("l4", Text().Replace(SpeakerId, "nobody", StringComparison.Ordinal)));      // not a catalog character
        host.Tick();
        Assert.Equal(["2 refused l2 DLG_MISSING_FIELD","2 refused l3 DLG_REFERENCE", "2 refused l4 DLG_UNKNOWN_SPEAKER"], host.Heard);
        Assert.Equal(digest, host.Component.Runtime.Set.Digest);
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestWaitsAndAResumedSessionContinuesTheSameWay()
    {
        // A request waits only inside a tick: after another component's event is delivered and before the dialogue component's Update. A probe in between sees that.
        var probe = new Probe();
        var host = new Host(extra: [(new Emitter(), 50), (probe, 60)]);
        probe.Component = host.Component;
        host.Tick();
        Assert.Equal(DialogueComponent.NotQuiet, probe.Code);
        Assert.True(host.Component.IsQuiet);   // and the tick ended with it answered
        Assert.Contains(host.Heard, l => l.Contains("line emitted", StringComparison.Ordinal));
        host = new Host();
        host.Runtime.Submit(new DialogueStartRequested("r1", "briefing"));
        host.Tick();
        host.Runtime.Submit(new DialogueAdvanceRequested("r2"));
        host.Tick();
        var bytes = DialogueCheckpoint.Capture(host.Component);

        var resumed = new Host(resume: DialogueCheckpoint.Open(host.Component.Runtime.Set, bytes), startTick: host.CurrentTick);   // a resumed session goes on from the saved tick
        Assert.Equal(host.Component.Fingerprint()[host.Component.Fingerprint().IndexOf("state=", StringComparison.Ordinal)..][..22],
            resumed.Component.Fingerprint()[resumed.Component.Fingerprint().IndexOf("state=", StringComparison.Ordinal)..][..22]);
        foreach (var h in new[] { host, resumed })
        {
            h.Runtime.Submit(new DialogueChooseRequested("r3", "accept"));
            h.Tick();
        }
        Assert.Equal(host.Heard.Skip(2).Select(l => l[l.IndexOf(' ')..]), resumed.Heard.Select(l => l[l.IndexOf(' ')..]));
        Assert.Equal(DialogueSave.StateDigest(host.Component.Runtime.State), DialogueSave.StateDigest(resumed.Component.Runtime.State));
        // A damaged checkpoint is refused by the dialogue save's own checks.
        bytes[bytes.Length / 2] ^= 0x20;
        Assert.StartsWith("DLG_SAVE", Assert.Throws<EngineRefusedException>(() => DialogueCheckpoint.Open(host.Component.Runtime.Set, bytes)).Code);
    }

    private static JournalCodec Codec() => new JournalCodec()
        .Register("dialogue.start", e => new DialogueStartRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Conversation").GetString()!))
        .Register("dialogue.advance", e => new DialogueAdvanceRequested(e.GetProperty("RequestId").GetString()!))
        .Register("dialogue.choose", e => new DialogueChooseRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Option").GetString()!));

    private static (IGameRuntime Runtime, SessionJournal Journal) Journaled(JournalCodec codec, bool record)
    {
        var journal = new SessionJournal();
        var facts = new Dictionary<string, FactValue> { ["ext.hold-status"] = FactValue.From("active"), ["ext.squads"] = FactValue.From(7L) };
        var session = new RuntimeSession(new RuntimeClock(30), new NullReporter());
        var channels = new DialogueChannels();
        channels.Install(session);
        var component = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Text(), Speakers), 1), Speakers, () => facts, channels);
        session.AddComponent(component, 100);
        session.AddComponent(new JournalComponent(journal, component.Fingerprint), 900);
        return (record ? new JournalingRuntime(session, journal, codec) : session, journal);
    }

    [Fact]
    public void ARecordedSessionReplaysTickForTickAndAChangedAnswerIsFoundAtItsTick()
    {
        var codec = Codec();
        var (runtime, journal) = Journaled(codec, record: true);
        runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + runtime.Clock.TicksPerSecond - 1) / runtime.Clock.TicksPerSecond;
        runtime.Submit(new DialogueStartRequested("s", "briefing"));
        runtime.Update(oneTick);
        runtime.Submit(new DialogueAdvanceRequested("a"));
        runtime.Update(oneTick);
        runtime.Update(oneTick);
        runtime.Submit(new DialogueChooseRequested("c", "accept"));
        runtime.Update(oneTick);
        runtime.Update(oneTick);
        Assert.Equal(5, journal.Hashes.Count);
        Assert.Equal(3, journal.Inputs.Count);
        Assert.True(JournalReplay.Verify(journal, codec, () => Journaled(codec, record: false)).Matches);
        // The same journal with the answer changed no longer leads to the same state: the first tick that differs is the tick the answer was taken.
        var changed = SessionJournal.Deserialize(journal.Serialize());
        var at = changed.Inputs.FindIndex(i => i.Type == "dialogue.choose");
        changed.Inputs[at] = changed.Inputs[at] with { Json = changed.Inputs[at].Json.Replace("accept", "refuse", StringComparison.Ordinal) };
        var verdict = JournalReplay.Verify(changed, codec, () => Journaled(codec, record: false));
        Assert.False(verdict.Matches);
        Assert.Equal(changed.Inputs[at].Tick, verdict.DivergedAtTick);
    }

    [Fact]
    public void SpeakersAreTheCatalogCharactersAndNobodyElse()
    {
        Assert.True(Speakers.HasSpeaker(SpeakerId));
        Assert.False(Speakers.HasSpeaker("nobody"));
        var withNarrator = CatalogSpeakers.FromCharacters("{\"character_archetypes\":[{\"id\":\"a.member\"}]}", "narrator");
        Assert.True(withNarrator.HasSpeaker("narrator"));
        Assert.True(withNarrator.HasSpeaker("a.member"));
        Assert.Equal(CatalogSpeakers.BadDocument, Assert.Throws<EngineRefusedException>(() => CatalogSpeakers.FromCharacters("{}")).Code);
        Assert.Equal(CatalogSpeakers.BadDocument, Assert.Throws<EngineRefusedException>(() => CatalogSpeakers.FromCharacters("[")).Code);
        Assert.Equal(CatalogSpeakers.BadDocument, Assert.Throws<EngineRefusedException>(() => CatalogSpeakers.FromCharacters("{\"character_archetypes\":[{}]}")).Code);
        Assert.Equal(DialogueCodes.UnknownSpeaker, Assert.Throws<EngineRefusedException>(() => DialogueLoader.Load(Text().Replace(SpeakerId, "nobody", StringComparison.Ordinal), Speakers)).Code);
    }

    [Fact]
    public void TheComponentReadsNoClockAndNoRandomSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "GameRuntime", "GameRuntime.Dialogue"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var sources = Directory.GetFiles(Path.Combine(directory!.FullName, "src", "GameRuntime", "GameRuntime.Dialogue"), "*.cs").ToList();
        Assert.NotEmpty(sources);
        foreach (var file in sources)
            foreach (var forbidden in new[] { "DateTime", "Stopwatch", "Environment.TickCount", "System.Random", "new Random", "Guid.NewGuid", "Task.Delay", "Thread.Sleep" })
                Assert.DoesNotContain(forbidden, File.ReadAllText(file), StringComparison.Ordinal);
    }
}
