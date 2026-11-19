using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The dialogue component with the pass-32 additions: a refusal for a request on a channel nobody serves, priorities between conversations (preempt, queue, resume), the history and skip requests,
/// content warnings, and signal arguments in events.
/// </summary>
public sealed class DialogueChannelsAndPriorityComponentTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "cmdr";
    }

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    private static string Line(string id, string next, string extra = "") =>
        $$"""{ "id": "{{id}}", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.{{id}}", {{extra}} "next": "{{next}}" }""";

    private const string Document = """
    {
     "format": 3, "id": "demo.channels",
     "variables": [ { "id": "motto", "type": "string", "initial": "a;b%c" } ],
     "externalFacts": [ { "id": "cw-gore", "type": "bool" } ],
     "contentWarnings": [ { "tag": "cw.gore", "fact": "cw-gore", "action": "replace" } ],
     "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
     "conversations": [
      { "id": "chat", "start": "c1", "priority": 1, "nodes": [
        { "id": "c1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.c1", "next": "c2" },
        { "id": "c2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.c2", "tags": ["cw.gore"], "alternative": "k.c2.safe", "next": "c3" },
        { "id": "c3", "kind": "effects", "effects": [ { "signal": "tell", "args": [ { "var": "motto" }, { "int": 3 }, { "bool": true } ] } ], "next": "c4" },
        { "id": "c4", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.c4", "next": "bye" },
        { "id": "bye", "kind": "end", "result": "done" } ] },
      { "id": "alarm", "start": "a1", "priority": 5, "onBusy": "preempt", "nodes": [
        { "id": "a1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.a1", "next": "aend" },
        { "id": "aend", "kind": "end", "result": "alarm-over" } ] },
      { "id": "quiet", "start": "q1", "priority": 1, "onBusy": "queue", "nodes": [
        { "id": "q1", "kind": "end", "result": "hush" } ] },
      { "id": "pick", "start": "p1", "nodes": [
        { "id": "p1", "kind": "choice", "pick": "player", "options": [
           { "id": "hit", "line": "k.hit", "tags": ["cw.gore"], "alternative": "k.hit.safe", "next": "pend" },
           { "id": "talk", "line": "k.talk", "next": "pend" } ] },
        { "id": "pend", "kind": "end", "result": "done" } ] },
      { "id": "glued", "start": "g1", "nodes": [
        { "id": "g1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.g1", "glue": true, "next": "g2" },
        { "id": "g2", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.g2", "next": "gend" },
        { "id": "gend", "kind": "end", "result": "done" } ] },
      { "id": "bark", "start": "b1", "priority": 2, "onBusy": "queue", "nodes": [
        { "id": "b1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.b1", "next": "bend" },
        { "id": "bend", "kind": "end", "result": "done" } ] }
     ]
    }
    """;

    private sealed class Game
    {
        public Reporter Reporter { get; } = new();
        public RuntimeSession Session { get; }
        public DialogueChannels Channels { get; }
        public List<DialogueComponent> Components { get; } = new();
        public List<string> Heard { get; } = new();
        public bool Gore { get; set; }
        private readonly RuntimeClock _clock;

        public Game(IEnumerable<string>? channels = null, long startTick = 0, DialogueRuntime? resume = null)
        {
            _clock = new RuntimeClock(30, 8, startTick);
            Session = new RuntimeSession(_clock, Reporter);
            var cast = new Cast();
            Channels = new DialogueChannels();
            Channels.Install(Session);
            var order = 100;
            foreach (var channel in channels ?? [""])
            {
                var runtime = resume is not null && Components.Count == 0 ? resume : new DialogueRuntime(DialogueLoader.Load(Document, cast), 1);
                var component = new DialogueComponent(runtime, cast, () => new Dictionary<string, FactValue> { ["ext.cw-gore"] = FactValue.From(Gore) }, Channels, channel.Length == 0 ? "dialogue" : "dialogue-" + channel, channel);
                Components.Add(component);
                Session.AddComponent(component, order++);
            }
            Session.Subscribe<DialogueRefused>(e => Heard.Add($"t{e.Tick} refused {e.RequestId} {e.Code} [{e.Channel}]"));
            Session.Subscribe<DialogueLineShown>(e => Heard.Add($"t{e.Tick} line {e.RequestId} {e.Conversation}/{e.Node} {e.Key} seen={e.Seen} replaced={e.Replaced} [{e.Channel}]"));
            Session.Subscribe<DialogueChoicesShown>(e => Heard.Add($"t{e.Tick} choices {e.RequestId} {e.Options}"));
            Session.Subscribe<DialogueEnded>(e => Heard.Add($"t{e.Tick} ended {e.RequestId} {e.Conversation} {e.Result}"));
            Session.Subscribe<DialogueConversationChanged>(e => Heard.Add($"t{e.Tick} {e.Change} {e.RequestId} {e.Conversation} {e.Other} {e.Position}"));
            Session.Subscribe<DialogueSignalRaised>(e => Heard.Add($"t{e.Tick} signal {e.RequestId} {e.Name} {e.Args}"));
            Session.Subscribe<DialogueSkipped>(e => Heard.Add($"t{e.Tick} skipped {e.RequestId} {e.Count}"));
            Session.Subscribe<DialogueHistoryEntryListed>(e => Heard.Add($"t{e.Tick} history {e.RequestId} #{e.Index} turn={e.Turn} shown@{e.ShownTick} {e.Kind} {e.Conversation}/{e.Node} {e.Key} {e.Args} chosen={e.Chosen} seen={e.Seen} skipped={e.Skipped} replaced={e.Replaced}"));
            Session.Subscribe<DialogueHistoryListed>(e => Heard.Add($"t{e.Tick} listed {e.RequestId} {e.Listed}/{e.Kept}"));
            Session.Init();
        }

        public DialogueComponent Dialogue => Components[0];

        public void Tick(int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) Session.Update((RuntimeClock.MicrosecondsPerSecond + _clock.TicksPerSecond - 1) / _clock.TicksPerSecond);
        }

        public long CurrentTick => _clock.Tick;
    }

    [Fact]
    public void TheCodesOfTheComponentsAreTheTextsTheTestsExpect()
    {
        Assert.Equal("DLG_CHANNEL_UNKNOWN", DialogueChannels.UnknownChannel);
        Assert.Equal("DLG_CHANNEL_DUPLICATE", DialogueChannels.DuplicateChannel);
        Assert.Equal("DLG_INPUT_ACTION", DialogueInputComponent.UnknownAction);
        Assert.Equal("DIALOGUE_NOT_QUIET", DialogueComponent.NotQuiet);
    }

    // ---- unanswered requests -------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestOnAChannelNobodyServesIsRefusedWithACodeInTheNextUpdateNotAtOnceAndNotNever()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("r1", "chat", "ghost"));
        Assert.Empty(game.Heard);   // nothing happens when the request is made
        game.Tick();
        Assert.Equal(["t1 refused r1 DLG_CHANNEL_UNKNOWN [ghost]"], game.Heard);
        Assert.False(game.Dialogue.Runtime.InConversation);
        Assert.Equal(0, game.Dialogue.Answered);   // the served channel's component was not involved
        Assert.Equal(1, game.Channels.Refused);
        Assert.Empty(game.Reporter.Codes);
    }

    [Fact]
    public void EveryKindOfRequestOnAnUnservedChannelIsRefusedNotJustTheFirst()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("start", "chat", "ghost"));
        game.Session.Submit(new DialogueAdvanceRequested("advance", "ghost"));
        game.Session.Submit(new DialogueChooseRequested("choose", "x", "ghost"));
        game.Session.Submit(new DialogueQuestStartRequested("quest", "q", "ghost"));
        game.Session.Submit(new DialogueQuestsUpdateRequested("quests", "ghost"));
        game.Session.Submit(new DialogueReloadRequested("reload", "{}", false, "", "ghost"));
        game.Session.Submit(new DialogueInterruptRequested("interrupt", "alarm", "ghost"));
        game.Session.Submit(new DialogueTimeoutRequested("timeout", "ghost"));
        game.Session.Submit(new DialogueSkipRequested("skip", "ghost"));
        game.Session.Submit(new DialogueHistoryRequested("history", 0, false, "ghost"));
        game.Tick();
        Assert.Equal((new[] { "start", "advance", "choose", "quest", "quests", "reload", "interrupt", "timeout", "skip", "history" }).Select(id => $"t1 refused {id} DLG_CHANNEL_UNKNOWN [ghost]"), game.Heard);
    }

    [Fact]
    public void AServedChannelIsAnsweredByItsComponentOnlyAndAGhostChannelBesideItIsRefused()
    {
        var game = new Game(["", "ambient"]);
        game.Session.Submit(new DialogueStartRequested("a", "chat", "ambient"));
        game.Session.Submit(new DialogueStartRequested("b", "chat", "nowhere"));
        game.Session.Submit(new DialogueStartRequested("c", "chat"));
        game.Tick();
        Assert.Equal(3, game.Heard.Count);
        Assert.Contains("t1 refused b DLG_CHANNEL_UNKNOWN [nowhere]", game.Heard);
        Assert.Contains("t1 line c chat/c1 k.c1 seen=False replaced=False []", game.Heard);
        Assert.Contains("t1 line a chat/c1 k.c1 seen=False replaced=False [ambient]", game.Heard);
        Assert.Equal(new[] { "", "ambient" }, game.Channels.Served.Keys);
        Assert.Equal(1, game.Components[0].Answered);
        Assert.Equal(1, game.Components[1].Answered);
    }

    [Fact]
    public void ARegistryNamesWhoServesEachChannelInOrdinalOrder()
    {
        var game = new Game(["zeta", "alpha", "mid"]);
        Assert.Equal(["alpha", "mid", "zeta"], game.Channels.Served.Keys);
        Assert.Equal(["dialogue-alpha", "dialogue-mid", "dialogue-zeta"], game.Channels.Served.Values);
        Assert.Equal("channels=alpha,mid,zeta refused=0 pending=0", game.Channels.Fingerprint());
    }

    [Fact]
    public void TwoComponentsOnOneChannelAreRefusedAtInitBecauseBothWouldAnswer()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(30), reporter);
        var channels = new DialogueChannels();
        channels.Install(session);
        var cast = new Cast();
        session.AddComponent(new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels, "one", "same"), 100);
        session.AddComponent(new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels, "two", "same"), 101);
        var refused = Assert.Throws<EngineRefusedException>(() => session.Init());
        Assert.Equal(DialogueChannels.DuplicateChannel, refused.Code);
        Assert.Contains("one", refused.Message, StringComparison.Ordinal);
        Assert.Contains("two", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AComponentCannotBeBuiltWithoutARegistry()
    {
        var cast = new Cast();
        var runtime = new DialogueRuntime(DialogueLoader.Load(Document, cast), 1);
        Assert.Throws<ArgumentNullException>(() => new DialogueComponent(runtime, cast, () => new Dictionary<string, FactValue>(), null!));
    }

    [Fact]
    public void ARegistryThatWasNeverInstalledCannotCarryAComponentBecauseTheDialogueEventsAreNotDeclared()
    {
        var session = new RuntimeSession(new RuntimeClock(30), new Reporter());
        var cast = new Cast();
        session.AddComponent(new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), new DialogueChannels()), 100);
        var failure = Assert.Throws<InvalidOperationException>(() => session.Init());
        Assert.Contains("not declared", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AComponentWhoseRegistryRunsOnAnotherSessionStopsItsOwnSessionAtTheFirstUpdateAndSaysWhy()
    {
        var elsewhere = new DialogueChannels();
        var other = new RuntimeSession(new RuntimeClock(30), new Reporter());
        elsewhere.Install(other);
        other.Init();
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(30), reporter);
        new DialogueChannels().Install(session);
        var cast = new Cast();
        session.AddComponent(new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), elsewhere), 100);
        session.Init();
        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Equal(["RUNTIME_FAULT"], reporter.Codes);
    }

    [Fact]
    public void OneRegistryCannotRunOnTwoSessions()
    {
        var channels = new DialogueChannels();
        var first = new RuntimeSession(new RuntimeClock(30), new Reporter());
        var second = new RuntimeSession(new RuntimeClock(30), new Reporter());
        channels.Install(first);
        channels.Install(second);
        first.Init();
        Assert.Throws<InvalidOperationException>(() => second.Init());
    }

    [Fact]
    public void TwoDialoguesRunSideBySideThroughTheRegistryAndARequestForAThirdChannelIsRefusedWhileTheyDo()
    {
        var game = new Game(["", "ambient"]);
        game.Session.Submit(new DialogueStartRequested("p1", "chat"));
        game.Session.Submit(new DialogueStartRequested("a1", "pick", "ambient"));
        game.Session.Submit(new DialogueStartRequested("x1", "chat", "ghost"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("p2"));
        game.Session.Submit(new DialogueChooseRequested("a2", "talk", "ambient"));
        game.Tick();
        Assert.Equal(
        [
            "t1 line p1 chat/c1 k.c1 seen=False replaced=False []",
            "t1 choices a1 hit|k.hit|1|cw.gore|0;talk|k.talk|1||0",
            "t1 refused x1 DLG_CHANNEL_UNKNOWN [ghost]",
            "t2 line p2 chat/c2 k.c2 seen=False replaced=False []",
            "t2 ended a2 pick done",
        ], game.Heard);
        Assert.Equal("chat", game.Components[0].Runtime.State.Conversation);
        Assert.Null(game.Components[1].Runtime.State.Conversation);
        Assert.Equal(1, game.Channels.Refused);
    }

    [Fact]
    public void ARequestRaisedByAComponentThatRunsLateIsStillAnsweredInTheSameTick()
    {
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(30), reporter);
        new DialogueChannels().Install(session);
        var heard = new List<string>();
        session.Subscribe<DialogueRefused>(e => heard.Add($"t{e.Tick} {e.RequestId} {e.Code} [{e.Channel}]"));
        session.AddComponent(new LateAsker(), 1_000_000);
        session.Init();
        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);
        Assert.Equal(["t1 late DLG_CHANNEL_UNKNOWN [nowhere]"], heard);
    }

    private sealed class LateAsker : IGameComponent
    {
        public string Id => "late-asker";
        private IEventPort? _events;
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context) => _events!.Emit(new DialogueStartRequested("late", "chat", "nowhere"));
    }
    [Fact]
    public void TheSameRequestsGiveTheSameRefusalsInTheSameOrderOnEveryRun()
    {
        List<string> Run()
        {
            var game = new Game(["", "ambient"]);
            game.Session.Submit(new DialogueStartRequested("q1", "chat", "z"));
            game.Session.Submit(new DialogueAdvanceRequested("q2", "a"));
            game.Tick(2);
            game.Session.Submit(new DialogueTimeoutRequested("q3", "z"));
            game.Tick();
            return game.Heard;
        }
        var first = Run();
        Assert.Equal(["t1 refused q1 DLG_CHANNEL_UNKNOWN [z]", "t1 refused q2 DLG_CHANNEL_UNKNOWN [a]", "t3 refused q3 DLG_CHANNEL_UNKNOWN [z]"], first);
        Assert.Equal(first, Run());
    }

    [Fact]
    public void AResumedSessionRefusesAndAnswersTheSameWayAsTheOneItWasTakenFrom()
    {
        var game = new Game(["", "ambient"]);
        game.Session.Submit(new DialogueStartRequested("r1", "chat"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("g1", "ghost"));
        game.Tick();
        Assert.True(game.Channels.IsQuiet);
        var bytes = DialogueCheckpoint.Capture(game.Dialogue);
        var resumed = new Game(["", "ambient"], startTick: game.CurrentTick, resume: DialogueCheckpoint.Open(game.Dialogue.Runtime.Set, bytes));
        game.Heard.Clear();
        foreach (var g in new[] { game, resumed })
        {
            g.Session.Submit(new DialogueAdvanceRequested("g2", "ghost"));
            g.Session.Submit(new DialogueAdvanceRequested("n1"));
            g.Tick();
        }
        Assert.Equal(["t3 line n1 chat/c2 k.c2 seen=False replaced=False []", "t3 refused g2 DLG_CHANNEL_UNKNOWN [ghost]"], game.Heard);
        Assert.Equal(game.Heard, resumed.Heard);
        Assert.Equal(DialogueSave.StateDigest(game.Dialogue.Runtime.State), DialogueSave.StateDigest(resumed.Dialogue.Runtime.State));
    }

    [Fact]
    public void ARefusalWaitingInTheRegistryBlocksACheckpointThatNamesItAndTheTickEndsWithItAnswered()
    {
        var session = new RuntimeSession(new RuntimeClock(30), new Reporter());
        var channels = new DialogueChannels();
        channels.Install(session);
        var cast = new Cast();
        var component = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels);
        var codes = new List<string>();
        session.AddComponent(new Snapshotter(component, channels, codes), 40);   // runs before the registry, which has the request but has not answered it
        session.AddComponent(component, 100);
        session.Init();
        session.Submit(new DialogueStartRequested("g", "chat", "ghost"));
        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);
        Assert.Equal([DialogueComponent.NotQuiet], codes);
        Assert.True(channels.IsQuiet);
        Assert.NotEmpty(DialogueCheckpoint.Capture(component));
    }

    private sealed class Snapshotter(DialogueComponent component, DialogueChannels channels, List<string> codes) : IGameComponent
    {
        public string Id => "snapshotter";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context)
        {
            if (!channels.IsQuiet) codes.Add(Assert.Throws<EngineRefusedException>(() => DialogueCheckpoint.Capture(component)).Code);
        }
    }

    [Fact]
    public void PlayerInputOnAnUnservedChannelIsRefusedToo()
    {
        var clock = new RuntimeClock(30);
        var input = new InputState(() => clock.Tick + 1);
        var session = new RuntimeSession(clock, new Reporter());
        new DialogueChannels().Install(session);
        session.AddComponent(new DialogueInputComponent(input, "dialogue-input", "ghost"), 60);
        var heard = new List<string>();
        session.Subscribe<DialogueRefused>(e => heard.Add($"t{e.Tick} {e.RequestId} {e.Code} [{e.Channel}]"));
        session.Init();
        input.Press(DialogueInputComponent.Advance);
        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);
        session.Update(RuntimeClock.MicrosecondsPerSecond / 30 + 1);
        Assert.Single(heard);
        Assert.EndsWith("DLG_CHANNEL_UNKNOWN [ghost]", heard[0]);
    }

    // ---- priority through the component --------------------------------------------------------------------------------------------

    [Fact]
    public void APreemptingStartIsAnnouncedAndTheOpenConversationComesBackWhenItEnds()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Tick();
        game.Heard.Clear();
        game.Session.Submit(new DialogueStartRequested("s2", "alarm"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("s3"));
        game.Tick();
        Assert.Equal(
        [
            "t2 preempted s2 chat alarm 0",
            "t2 line s2 alarm/a1 k.a1 seen=False replaced=False []",
            "t3 ended s3 alarm alarm-over 0",
            "t3 resumed s3 chat alarm 0",
            "t3 line s3 chat/c1 k.c1 seen=True replaced=False []",
        ], game.Heard);
    }

    [Fact]
    public void AQueuedStartIsAnnouncedWithItsPlaceAndRunsWhenTheOpenOneEnds()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Session.Submit(new DialogueStartRequested("s2", "bark"));
        game.Tick();
        for (var i = 0; i < 3; i++)
        {
            game.Session.Submit(new DialogueAdvanceRequested("a" + i));
            game.Tick();
        }
        game.Session.Submit(new DialogueAdvanceRequested("a3"));
        game.Tick();
        Assert.Contains("t1 queued s2 bark  1", game.Heard);
        Assert.Contains("t4 ended a2 chat done 0", game.Heard);
        Assert.Contains("t4 started a2 bark chat 0", game.Heard);
        Assert.Contains("t4 line a2 bark/b1 k.b1 seen=False replaced=False []", game.Heard);
        Assert.Contains("t5 ended a3 bark done", game.Heard);   // the end names the conversation that ended, not the one that was open when the request came
    }

    [Fact]
    public void TheEndEventNamesTheConversationThatEndedEvenWhenAQueuedOneThatSaysNothingEndedInTheSameCall()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Session.Submit(new DialogueStartRequested("s2", "quiet"));
        game.Tick();
        for (var i = 0; i < 3; i++)
        {
            game.Session.Submit(new DialogueAdvanceRequested("a" + i));
            game.Tick();
        }
        // The last advance ends the chat, which starts the quiet conversation, which ends at once: one call, two ends, and the terminal one is the quiet one's.
        Assert.Contains("t4 ended a2 chat done 0", game.Heard);
        Assert.Contains("t4 started a2 quiet chat 0", game.Heard);
        Assert.Contains("t4 ended a2 quiet hush", game.Heard);
    }

    [Fact]
    public void ARefusedPreemptionIsAnEventWithItsCode()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "alarm"));
        game.Session.Submit(new DialogueStartRequested("s2", "alarm"));
        game.Tick();
        Assert.Contains("t1 refused s2 DLG_PRIORITY []", game.Heard);
    }

    // ---- history, skip, warnings, signals ------------------------------------------------------------------------------------------

    [Fact]
    public void AHistoryRequestListsTheKeptEntriesOldestFirstWithTheTicksTheyWereShownAt()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("s2"));
        game.Tick(2);
        game.Heard.Clear();
        game.Session.Submit(new DialogueHistoryRequested("h1"));
        game.Tick();
        Assert.Equal(
        [
            "t4 history h1 #0 turn=0 shown@1 line chat/c1 k.c1  chosen= seen=False skipped=False replaced=False",
            "t4 history h1 #1 turn=1 shown@2 line chat/c2 k.c2  chosen= seen=False skipped=False replaced=False",
            "t4 listed h1 2/2",
        ], game.Heard);
    }

    [Fact]
    public void AHistoryRequestCanAskForTheNewestFewOrOnlyWhatWasNewAndNegativeIsRefused()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("s2"));
        game.Tick();
        game.Heard.Clear();
        game.Session.Submit(new DialogueHistoryRequested("h1", 1));
        game.Session.Submit(new DialogueHistoryRequested("h2", -1));
        game.Tick();
        Assert.Equal(["t3 history h1 #0 turn=1 shown@2 line chat/c2 k.c2  chosen= seen=False skipped=False replaced=False", "t3 listed h1 1/2", "t3 refused h2 DLG_RANGE []"], game.Heard);
    }

    [Fact]
    public void AHistoryRequestCanLeaveOutWhatWasAlreadySeen()
    {
        var game = new Game();
        for (var round = 0; round < 2; round++)
        {
            game.Session.Submit(new DialogueStartRequested("s" + round, "chat"));
            game.Tick();
            game.Session.Submit(new DialogueAdvanceRequested("a" + round));
            game.Tick(2);
            game.Session.Submit(new DialogueAdvanceRequested("b" + round));
            game.Tick();
            game.Session.Submit(new DialogueAdvanceRequested("c" + round));
            game.Tick();
        }
        game.Heard.Clear();
        game.Session.Submit(new DialogueHistoryRequested("h1", 0, true));
        game.Tick();
        Assert.Equal(["chat/c1", "chat/c2", "chat/c4"], game.Heard.Where(h => h.Contains(" history ", StringComparison.Ordinal)).Select(h => h.Split(' ')[7]));
        Assert.EndsWith("listed h1 3/6", game.Heard[^1]);
    }

    [Fact]
    public void ASkipRequestPassesSeenLinesAndReportsHowManyAndRefusesWhenNothingShows()
    {
        var game = new Game();
        game.Session.Submit(new DialogueSkipRequested("k0"));
        game.Tick();
        Assert.Equal(["t1 refused k0 DLG_NOT_WAITING []"], game.Heard);
        game.Heard.Clear();
        // Play chat to its end once, then again: the second time every line is seen.
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Tick();
        for (var i = 0; i < 3; i++) { game.Session.Submit(new DialogueAdvanceRequested("a" + i)); game.Tick(); }
        game.Heard.Clear();
        game.Session.Submit(new DialogueStartRequested("s2", "chat"));
        game.Tick();
        game.Session.Submit(new DialogueSkipRequested("k1"));
        game.Tick();
        Assert.Contains(game.Heard, h => h.Contains("skipped k1 ", StringComparison.Ordinal));
        Assert.Contains(game.Heard, h => h.Contains(" ended k1 chat done", StringComparison.Ordinal));
    }

    [Fact]
    public void AContentWarningPolicyFromTheHostReplacesALineAndTheEventSaysSo()
    {
        var game = new Game { Gore = true };
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Session.Submit(new DialogueAdvanceRequested("s2"));
        game.Tick();
        Assert.Contains("t1 line s2 chat/c2 k.c2.safe seen=False replaced=True []", game.Heard);
    }

    [Fact]
    public void AnAnswerThatAWarningReplacesIsMarkedInTheChoicesEventAndTheLineGlueIsCarried()
    {
        var game = new Game { Gore = true };
        game.Session.Submit(new DialogueStartRequested("s1", "pick"));
        game.Tick();
        Assert.Contains("t1 choices s1 hit|k.hit.safe|1|cw.gore|1;talk|k.talk|1||0", game.Heard);
        var glue = new List<string>();
        game.Session.Subscribe<DialogueLineShown>(e => glue.Add(e.Node + "=" + e.Glue));
        game.Session.Submit(new DialogueChooseRequested("s2", "talk"));
        game.Session.Submit(new DialogueStartRequested("s3", "glued"));
        game.Session.Submit(new DialogueAdvanceRequested("s4"));
        game.Tick();
        Assert.Equal(["g1=True", "g2=False"], glue);
    }

    [Fact]
    public void ASignalsArgumentsAreInTheEventWithTheirTypesAndTextIsEscaped()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("s1", "chat"));
        game.Session.Submit(new DialogueAdvanceRequested("s2"));
        game.Session.Submit(new DialogueAdvanceRequested("s3"));
        game.Tick();
        Assert.Contains("t1 signal s3 tell s:a%3Bb%25c;i:3;b:true", game.Heard);
    }
}
