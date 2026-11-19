using DialogueEngine;
using DialogueEngine.Tests;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The dialogue component under the pressures of a running game: reload maps carried by the request, text that would break the flat event lists, player input polled from an InputState, pauses
/// of many ticks in one Update, and seeded random sessions that are saved, resumed and replayed from their journal.
/// </summary>
public sealed class DialogueHardeningComponentTests
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

    private const string Document = """
    {
     "format": 3, "id": "demo.map",
     "variables": [ { "id": "mood", "type": "int", "initial": 1, "min": 0, "max": 9 }, { "id": "motto", "type": "string", "initial": "a;b=c%d" } ],
     "externalFacts": [], "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
     "conversations": [ { "id": "main", "start": "hi", "nodes": [
       { "id": "hi", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "main.hi", "args": { "mood": "var.mood", "motto": "var.motto" }, "next": "ask" },
       { "id": "ask", "kind": "choice", "pick": "player", "options": [
          { "id": "yes", "line": "main.yes", "effects": [ { "add": "mood", "by": 1 } ], "next": "bye" },
          { "id": "no", "line": "main.no", "next": "bye" } ] },
       { "id": "bye", "kind": "end", "result": "done" } ] } ]
    }
    """;

    private sealed class Game
    {
        public Reporter Reporter { get; } = new();
        public RuntimeSession Session { get; }
        public DialogueComponent Dialogue { get; }
        public List<string> Heard { get; } = new();
        public InputState Input { get; }
        public List<string> Refusedby { get; } = new();
        private readonly RuntimeClock _clock = new(30);

        public Game(string document = Document, bool withInput = false, string channel = "")
        {
            Session = new RuntimeSession(_clock, Reporter);
            var channels = new DialogueChannels();
            channels.Install(Session);
            Input = new InputState(() => _clock.Tick + 1);
            var cast = new Cast();
            Dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels, "dialogue", channel);
            if (withInput) Session.AddComponent(new DialogueInputComponent(Input, "dialogue-input", channel), 50);
            Session.AddComponent(Dialogue, 100);
            Session.Subscribe<DialogueLineShown>(e => Heard.Add($"t{e.Tick} line {e.RequestId} {e.Node} {e.Args}"));
            Session.Subscribe<DialogueRefused>(e => Refusedby.Add(e.Channel + ":" + e.Code));
            Session.Subscribe<DialogueChoicesShown>(e => Heard.Add($"t{e.Tick} choices {e.RequestId} {e.Node}"));
            Session.Subscribe<DialogueEnded>(e => Heard.Add($"t{e.Tick} ended {e.RequestId} {e.Result}"));
            Session.Subscribe<DialogueVariableChanged>(e => Heard.Add($"t{e.Tick} var {e.RequestId} {e.Id}={e.Value}"));
            Session.Subscribe<DialogueReloaded>(e => Heard.Add($"t{e.Tick} reloaded {e.RequestId} renamed={e.RenamedVisits} +{e.AddedVariables}"));
            Session.Subscribe<DialogueRefused>(e => Heard.Add($"t{e.Tick} refused {e.RequestId} {e.Code}"));
            Session.Init();
        }

        public void Tick(int ticks = 1) => Session.Update((RuntimeClock.MicrosecondsPerSecond + _clock.TicksPerSecond - 1) / _clock.TicksPerSecond * ticks);
    }

    // ---- line arguments --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATextValueWithSeparatorsInItCannotSplitTheFlatListOfArguments()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Tick();
        Assert.Equal(["t1 line r1 hi mood=1;motto=a%3Bb%3Dc%25d"], game.Heard);
        // A host splits on ';' and '=' and decodes the value: it gets the text back whole.
        var entries = game.Heard[0].Split(' ', 5)[4].Split(';').Select(e => e.Split('=')).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(2, e.Length));
        Assert.Equal("a;b=c%d", Uri.UnescapeDataString(entries[1][1]));
        Assert.Equal("%25%3B%3D", DialogueComponent.Escaped("%;="));
        Assert.Equal("plain text, 12", DialogueComponent.Escaped("plain text, 12"));
    }

    // ---- reload with a map -----------------------------------------------------------------------------------------------------------

    private static string Renamed() => Document.Replace("\"ask\"", "\"question\"", StringComparison.Ordinal).Replace("mood", "temper", StringComparison.Ordinal);

    private const string Map = """{ "format": 1, "nodes": { "main/ask": "main/question" }, "variables": { "mood": "temper" } }""";

    [Fact]
    public void AReloadRequestCarriesAMapAndTheConversationGoesOnUnderTheNewNames()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Session.Submit(new DialogueAdvanceRequested("r2"));
        game.Tick();
        game.Heard.Clear();
        game.Session.Submit(new DialogueReloadRequested("l1", Renamed(), false, Map));
        game.Session.Submit(new DialogueChooseRequested("c1", "yes"));
        game.Tick();
        Assert.Equal(["t2 reloaded l1 renamed=1 +", "t2 var c1 temper=2", "t2 ended c1 done"], game.Heard);
        Assert.Equal(2L, game.Dialogue.Runtime.State.Variables["temper"].Integer);
        Assert.Empty(game.Reporter.Codes);
    }

    [Fact]
    public void ABadMapOrOneThatDoesNotFitIsARefusalEventAndTheSessionIsAsItWas()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Tick();
        var before = game.Dialogue.Fingerprint();
        game.Heard.Clear();
        game.Session.Submit(new DialogueReloadRequested("l1", Renamed(), false, "{ \"format\": 7 }"));
        game.Session.Submit(new DialogueReloadRequested("l2", Renamed(), false, """{ "format": 1, "variables": { "nothing": "temper" } }"""));
        game.Session.Submit(new DialogueReloadRequested("l3", Renamed(), false, """{ "format": 1, "variables": { "mood": "nothing" } }"""));
        game.Session.Submit(new DialogueReloadRequested("l4", Renamed(), false, """{ "format": 1, "variables": { "mood": "temper", "motto": "temper" } }"""));
        game.Session.Submit(new DialogueReloadRequested("l5", Renamed(), false));   // no map: the rename is refused
        game.Tick();
        Assert.Equal(["t2 refused l1 DLG_MAP_FORMAT", "t2 refused l2 DLG_MAP_UNKNOWN_SOURCE", "t2 refused l3 DLG_MAP_UNKNOWN_TARGET", "t2 refused l4 DLG_MAP_CONFLICT", "t2 refused l5 DLG_RELOAD_INCOMPATIBLE"], game.Heard);
        Assert.Equal(before[before.IndexOf("state=", StringComparison.Ordinal)..][..22], game.Dialogue.Fingerprint()[game.Dialogue.Fingerprint().IndexOf("state=", StringComparison.Ordinal)..][..22]);
        Assert.Empty(game.Reporter.Codes);
    }

    // ---- player input ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void PressesPolledFromTheInputStateBecomeRequestsAnsweredInOrderInTheSameTickAndNothingChangesBeforeThen()
    {
        var game = new Game(withInput: true);
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Tick();
        var before = game.Dialogue.Fingerprint();
        game.Input.Press(DialogueInputComponent.Advance);
        game.Input.Release(DialogueInputComponent.Advance);
        game.Input.Press(DialogueInputComponent.Choose, "yes");
        game.Input.Press("dialogue.dance");
        Assert.Equal(before, game.Dialogue.Fingerprint());   // written, not yet polled
        game.Heard.Clear();
        game.Tick();
        Assert.Equal(["t2 refused i2-2 DLG_INPUT_ACTION", "t2 choices i2-0 ask", "t2 var i2-1 mood=2", "t2 ended i2-1 done"], game.Heard);   // the input component refuses the unknown press the moment it polls; the dialogue answers the rest after it
        Assert.Empty(game.Reporter.Codes);
    }

    [Fact]
    public void ATimeoutPressAndAChoiceWithoutAnIdAreAnsweredByTheDialogueRulesNotDropped()
    {
        var game = new Game(withInput: true);
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Input.Press(DialogueInputComponent.Advance);
        game.Tick();
        game.Heard.Clear();
        game.Input.Press(DialogueInputComponent.Timeout);                    // this choice has no timeout
        game.Input.Press(DialogueInputComponent.Choose);                     // no answer named
        game.Input.Press(DialogueInputComponent.Choose, "maybe");
        game.Tick();
        Assert.Equal(["t2 refused i2-0 DLG_NO_TIMEOUT", "t2 refused i2-1 DLG_UNKNOWN_OPTION", "t2 refused i2-2 DLG_UNKNOWN_OPTION"], game.Heard);
    }

    [Fact]
    public void InputOnAChannelAddressesThatChannelAndItsRefusalsCarryIt()
    {
        var game = new Game(withInput: true, channel: "side");
        game.Session.Submit(new DialogueStartRequested("r1", "main", "side"));
        game.Tick();
        game.Heard.Clear();
        game.Input.Press(DialogueInputComponent.Advance);
        game.Input.Press("dialogue.dance");
        game.Tick();
        Assert.Equal(["t2 refused i2-1 DLG_INPUT_ACTION", "t2 choices i2-0 ask"], game.Heard);   // a component on the channel "side" answered the press of an input component on "side"
        Assert.Equal(["side:DLG_INPUT_ACTION"], game.Refusedby);
        game.Input.Press(DialogueInputComponent.Choose, "no");
        game.Tick();
        Assert.False(game.Dialogue.Runtime.InConversation);
    }

    private const string QuestDocument = """
    {
     "format": 3, "id": "demo.side",
     "variables": [], "externalFacts": [], "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ],
     "quests": [ { "id": "q", "title": "q.title", "start": "a", "stages": [ { "id": "a", "journal": "q.a", "advance": [] } ] } ],
     "conversations": [ { "id": "main", "start": "ask", "nodes": [
       { "id": "ask", "kind": "choice", "pick": "player", "options": [
          { "id": "go", "line": "main.go", "effects": [ { "startQuest": "q" }, { "signal": "s" } ], "next": "bye" } ] },
       { "id": "bye", "kind": "end", "result": "done" } ] } ]
    }
    """;

    [Fact]
    public void EverythingAChannelRaisesCarriesItsChannelSignalsQuestsReloadsAndRefusalsIncluded()
    {
        var game = new Game(QuestDocument, channel: "side");
        var seen = new List<string>();
        game.Session.Subscribe<DialogueSignalRaised>(e => seen.Add("signal:" + e.Channel));
        game.Session.Subscribe<DialogueQuestChanged>(e => seen.Add("quest:" + e.Channel));
        game.Session.Subscribe<DialogueReloaded>(e => seen.Add("reloaded:" + e.Channel));
        game.Session.Subscribe<DialogueChoicesShown>(e => seen.Add("choices:" + e.Channel));
        game.Session.Subscribe<DialogueEnded>(e => seen.Add("ended:" + e.Channel));
        game.Session.Submit(new DialogueStartRequested("r1", "main", "side"));
        game.Session.Submit(new DialogueChooseRequested("r2", "go", "side"));
        game.Session.Submit(new DialogueReloadRequested("r3", QuestDocument, false, "", "side"));
        game.Session.Submit(new DialogueQuestStartRequested("r4", "q", "side"));         // already started
        game.Session.Submit(new DialogueQuestsUpdateRequested("r5", "side"));
        game.Tick();
        Assert.Equal(["choices:side", "quest:side", "signal:side", "ended:side", "reloaded:side"], seen);
        Assert.Equal(["side:DLG_QUEST_STATE"], game.Refusedby);
    }

    private static List<string> PlayWithInput(bool replay, List<(long Tick, InputEdgeRaised Edge)> script)
    {
        var heard = new List<string>();
        var clock = new RuntimeClock(30);
        var session = new RuntimeSession(clock, new Reporter());
        var channels = new DialogueChannels();
        channels.Install(session);
        var input = new InputState(() => clock.Tick + 1);
        var cast = new Cast();
        var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels);
        if (replay) session.AddComponent(new InputStateBridge(input), 10);
        session.AddComponent(new DialogueInputComponent(input), 50);
        session.AddComponent(dialogue, 100);
        session.Subscribe<DialogueLineShown>(e => heard.Add($"t{e.Tick} line {e.RequestId} {e.Node}"));
        session.Subscribe<DialogueChoicesShown>(e => heard.Add($"t{e.Tick} choices {e.RequestId}"));
        session.Subscribe<DialogueEnded>(e => heard.Add($"t{e.Tick} ended {e.RequestId} {e.Result}"));
        session.Subscribe<DialogueRefused>(e => heard.Add($"t{e.Tick} refused {e.RequestId} {e.Code}"));
        session.Init();
        var one = (RuntimeClock.MicrosecondsPerSecond + clock.TicksPerSecond - 1) / clock.TicksPerSecond;
        session.Submit(new DialogueStartRequested("s", "main"));
        for (var tick = 1; tick <= 6; tick++)
        {
            foreach (var (at, edge) in script.Where(s => s.Tick == tick))
            {
                if (replay) session.Submit(edge);
                else if (edge.Pressed) input.Press(edge.Action, edge.Argument);
                else input.Release(edge.Action);
            }
            session.Update(one);
            heard.Add("fingerprint " + dialogue.Fingerprint());
        }
        return heard;
    }

    [Fact]
    public void InputWrittenBetweenUpdatesAndTheSameEdgesDeliveredAsEventsGiveTheSameSession()
    {
        var script = new List<(long, InputEdgeRaised)>
        {
            (2, new InputEdgeRaised("dialogue.advance", "", true)), (2, new InputEdgeRaised("dialogue.advance", "", false)),
            (4, new InputEdgeRaised("dialogue.choose", "no", true)), (5, new InputEdgeRaised("dialogue.dance", "", true)),
        };
        var live = PlayWithInput(false, script);
        var replayed = PlayWithInput(true, script);
        Assert.Equal(live, replayed);
        Assert.Contains("t2 choices i2-0", live);
        Assert.Contains("t4 ended i4-0 done", live);
        Assert.Contains("t5 refused i5-0 DLG_INPUT_ACTION", live);
    }

    // ---- a pause of many ticks ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADelayedFrameRunsAtMostTheCatchUpCapOfTicksAtOnceAndTheRestFollowWithoutChangingTheAnswers()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Tick(1);
        game.Session.Submit(new DialogueAdvanceRequested("r2"));
        var ran = 0;
        game.Session.Subscribe<TickSettled>(_ => ran++);
        game.Heard.Clear();
        game.Tick(40);   // a stall of 40 ticks delivered as one Update: the clock runs 8 of them (its catch-up cap) and keeps the rest
        Assert.Equal(8, ran);
        Assert.Equal(["t2 choices r2 ask"], game.Heard);
        for (var more = 0; more < 4; more++) game.Session.Update(0);
        Assert.Equal(40, ran);
        Assert.Equal(["t2 choices r2 ask"], game.Heard);                      // the request was answered once, in the first of the ticks
        Assert.StartsWith("dialogue tick=41", game.Dialogue.Fingerprint(), StringComparison.Ordinal);
        Assert.True(game.Dialogue.IsQuiet);
    }

    // ---- more than one conversation at a time --------------------------------------------------------------------------------------------

    private static JournalCodec ChannelCodec() => new JournalCodec()
        .Register("dialogue.start", e => new DialogueStartRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Conversation").GetString()!, e.GetProperty("Channel").GetString()!))
        .Register("dialogue.advance", e => new DialogueAdvanceRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Channel").GetString()!))
        .Register("dialogue.choose", e => new DialogueChooseRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Option").GetString()!, e.GetProperty("Channel").GetString()!));

    private sealed class Pair
    {
        public RuntimeSession Session { get; init; } = null!;
        public IGameRuntime Runtime { get; init; } = null!;
        public SessionJournal Journal { get; init; } = null!;
        public DialogueComponent Player { get; init; } = null!;
        public DialogueComponent Ambient { get; init; } = null!;
        public List<string> Heard { get; } = new();
    }

    private static Pair BuildPair(JournalCodec codec, bool record)
    {
        var journal = new SessionJournal();
        var cast = new Cast();
        var session = new RuntimeSession(new RuntimeClock(30), new Reporter());
        var channels = new DialogueChannels();
        channels.Install(session);
        var player = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 1), cast, () => new Dictionary<string, FactValue>(), channels);
        var ambient = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Document, cast), 2), cast, () => new Dictionary<string, FactValue>(), channels, "ambient", "ambient");
        session.AddComponent(player, 100);
        session.AddComponent(ambient, 110);
        session.AddComponent(new JournalComponent(journal, () => player.Fingerprint() + "|" + ambient.Fingerprint()), 900);
        var pair = new Pair { Runtime = record ? new JournalingRuntime(session, journal, codec) : session, Journal = journal, Player = player, Ambient = ambient };
        session.Subscribe<DialogueLineShown>(e => pair.Heard.Add($"[{e.Channel}] line {e.RequestId} {e.Node}"));
        session.Subscribe<DialogueChoicesShown>(e => pair.Heard.Add($"[{e.Channel}] choices {e.RequestId} {e.Node}"));
        session.Subscribe<DialogueEnded>(e => pair.Heard.Add($"[{e.Channel}] ended {e.RequestId} {e.Result}"));
        session.Subscribe<DialogueVariableChanged>(e => pair.Heard.Add($"[{e.Channel}] var {e.RequestId} {e.Id}={e.Value}"));
        session.Subscribe<DialogueRefused>(e => pair.Heard.Add($"[{e.Channel}] refused {e.RequestId} {e.Code}"));
        return pair;
    }

    [Fact]
    public void TwoConversationsRunSideBySideAndEachComponentAnswersItsOwnChannelOnly()
    {
        var pair = BuildPair(ChannelCodec(), record: false);
        pair.Runtime.Init();
        var one = (RuntimeClock.MicrosecondsPerSecond + pair.Runtime.Clock.TicksPerSecond - 1) / pair.Runtime.Clock.TicksPerSecond;
        pair.Runtime.Submit(new DialogueStartRequested("p1", "main"));
        pair.Runtime.Submit(new DialogueStartRequested("a1", "main", "ambient"));
        pair.Runtime.Submit(new DialogueAdvanceRequested("a2", "ambient"));       // the ambient one reaches its choice; the player's is still at its first line
        pair.Runtime.Update(one);
        Assert.Equal(Waiting.Line, pair.Player.Runtime.State.WaitingFor);
        Assert.Equal(Waiting.Choice, pair.Ambient.Runtime.State.WaitingFor);
        pair.Heard.Clear();
        pair.Runtime.Submit(new DialogueChooseRequested("a3", "yes", "ambient"));
        pair.Runtime.Submit(new DialogueChooseRequested("p2", "yes"));            // the player's conversation is at a line, not a choice
        pair.Runtime.Submit(new DialogueStartRequested("n1", "main", "nobody"));   // no component has this channel: the registry refuses it, last
        pair.Runtime.Update(one);
        Assert.Equal(["[] refused p2 DLG_NOT_WAITING", "[ambient] var a3 mood=2", "[ambient] ended a3 done", "[nobody] refused n1 DLG_CHANNEL_UNKNOWN"], pair.Heard);   // the player's component updates first, the registry after every component
        Assert.Equal(1L, pair.Player.Runtime.State.Variables["mood"].Integer);   // the ambient answer did not touch the player's variables
        Assert.Equal(2L, pair.Ambient.Runtime.State.Variables["mood"].Integer);
        Assert.Equal(2, pair.Player.Answered);
        Assert.Equal(3, pair.Ambient.Answered);
    }

    [Fact]
    public void RandomRequestsOnTwoChannelsNeverCrossAndTheJournalReplaysBothConversations()
    {
        var codec = ChannelCodec();
        for (var seed = 0; seed < 20; seed++)
        {
            var live = BuildPair(codec, record: true);
            live.Runtime.Init();
            var random = new Random(seed);
            for (var frame = 0; frame < 60; frame++)
            {
                for (var n = random.Next(4); n > 0; n--)
                {
                    var channel = random.Next(2) == 0 ? "" : "ambient";
                    var id = (channel.Length == 0 ? "p" : "a") + frame + "-" + n;
                    switch (random.Next(3))
                    {
                        case 0: live.Runtime.Submit(new DialogueStartRequested(id, "main", channel)); break;
                        case 1: live.Runtime.Submit(new DialogueAdvanceRequested(id, channel)); break;
                        default: live.Runtime.Submit(new DialogueChooseRequested(id, random.Next(2) == 0 ? "yes" : "no", channel)); break;
                    }
                }
                live.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + live.Runtime.Clock.TicksPerSecond - 1) / live.Runtime.Clock.TicksPerSecond);
            }
            // Every answer carries the channel its request was sent on (the ids say which that was).
            Assert.All(live.Heard, h => Assert.True(h.StartsWith("[] ", StringComparison.Ordinal) == h.Split(' ')[2].StartsWith('p'), "seed " + seed + ": " + h));
            Assert.True(live.Heard.Count > 20);
            Assert.True(JournalReplay.Verify(live.Journal, codec, () => { var again = BuildPair(codec, record: false); return (again.Runtime, again.Journal); }).Matches, $"seed {seed}: the journal did not replay");
        }
    }

    [Fact]
    public void ALineSaysWhetherItWasShownBeforeAndTheEventCarriesIt()
    {
        var game = new Game();
        var seen = new List<string>();
        game.Session.Subscribe<DialogueLineShown>(e => seen.Add(e.Node + "=" + e.Seen));
        game.Session.Submit(new DialogueStartRequested("r1", "main"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested("r2"));
        game.Session.Submit(new DialogueChooseRequested("r3", "no"));
        game.Session.Submit(new DialogueStartRequested("r4", "main"));
        game.Tick();
        Assert.Equal(["hi=False", "hi=True"], seen);
    }

    // ---- random sessions, saved, resumed and replayed ---------------------------------------------------------------------------------

    private static JournalCodec Codec() => new JournalCodec()
        .Register("dialogue.start", e => new DialogueStartRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Conversation").GetString()!))
        .Register("dialogue.advance", e => new DialogueAdvanceRequested(e.GetProperty("RequestId").GetString()!))
        .Register("dialogue.choose", e => new DialogueChooseRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Option").GetString()!))
        .Register("dialogue.timeout", e => new DialogueTimeoutRequested(e.GetProperty("RequestId").GetString()!))
        .Register("dialogue.interrupt", e => new DialogueInterruptRequested(e.GetProperty("RequestId").GetString()!, e.GetProperty("Conversation").GetString()!));

    private sealed class Fuzzed
    {
        public RuntimeSession Session { get; init; } = null!;
        public IGameRuntime Runtime { get; init; } = null!;
        public SessionJournal Journal { get; init; } = null!;
        public DialogueComponent Dialogue { get; init; } = null!;
        public List<string> Heard { get; } = new();
    }

    private static Fuzzed Build(string document, JournalCodec codec, bool record, DialogueRuntime? resume = null, long startTick = 0)
    {
        var journal = new SessionJournal();
        var cast = new Cast();
        var session = new RuntimeSession(new RuntimeClock(30, 8, startTick), new Reporter());
        var channels = new DialogueChannels();
        channels.Install(session);
        var component = new DialogueComponent(resume ?? new DialogueRuntime(DialogueLoader.Load(document, cast), 9), cast, () => new Dictionary<string, FactValue>(), channels);
        session.AddComponent(component, 100);
        session.AddComponent(new JournalComponent(journal, component.Fingerprint), 900);
        var fuzzed = new Fuzzed { Session = session, Runtime = record ? new JournalingRuntime(session, journal, codec) : session, Journal = journal, Dialogue = component };
        session.Subscribe<DialogueLineShown>(e => fuzzed.Heard.Add($"line {e.RequestId} {e.Node} {e.Key}"));
        session.Subscribe<DialogueChoicesShown>(e => fuzzed.Heard.Add($"choices {e.RequestId} {e.Node} {e.Options}"));
        session.Subscribe<DialogueEnded>(e => fuzzed.Heard.Add($"ended {e.RequestId} {e.Result}"));
        session.Subscribe<DialogueVariableChanged>(e => fuzzed.Heard.Add($"var {e.RequestId} {e.Id}={e.Value}"));
        session.Subscribe<DialogueSignalRaised>(e => fuzzed.Heard.Add($"signal {e.RequestId} {e.Name}"));
        session.Subscribe<DialogueRefused>(e => fuzzed.Heard.Add($"refused {e.RequestId} {e.Code}"));
        return fuzzed;
    }

    private sealed record Req(string Kind, string Argument);

    private sealed record FrameScript(List<(string Id, Req Request)> Requests, int Pause);

    private static void Submit(IGameRuntime runtime, string id, Req request)
    {
        switch (request.Kind)
        {
            case "start": runtime.Submit(new DialogueStartRequested(id, "talk")); break;
            case "advance": runtime.Submit(new DialogueAdvanceRequested(id)); break;
            case "choose": runtime.Submit(new DialogueChooseRequested(id, request.Argument)); break;
            case "timeout": runtime.Submit(new DialogueTimeoutRequested(id)); break;
            case "interrupt": runtime.Submit(new DialogueInterruptRequested(id, "talk")); break;
            default: throw new InvalidOperationException("unknown request " + request.Kind);
        }
    }

    /// <summary>A mostly sensible request for the state the dialogue is in, sometimes a wrong one the rules must refuse.</summary>
    private static Req Pick(Random random, DialogueRuntime state)
    {
        switch (random.Next(10))
        {
            case 0: return new Req("start", "");
            case 1: return new Req("interrupt", "");
            case 2: return new Req("timeout", "");
            case 3: return new Req("choose", "o" + random.Next(4));
        }
        if (state.State.WaitingFor == Waiting.Choice && random.Next(4) != 0)
        {
            var node = (ChoiceNode)state.Set.Conversations["talk"].Nodes[state.State.Node!];
            return new Req("choose", node.Options[random.Next(node.Options.Count)].Id);
        }
        return state.State.Conversation is null ? new Req("start", "") : new Req("advance", "");
    }

    private static void Run(Fuzzed game, FrameScript frame)
    {
        foreach (var (id, request) in frame.Requests) Submit(game.Runtime, id, request);
        game.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + game.Runtime.Clock.TicksPerSecond - 1) / game.Runtime.Clock.TicksPerSecond * frame.Pause);
    }

    [Fact]
    public void RandomSessionsWithPausesReplayFromTheirJournalHashTheSameEveryRunAndAResumedCheckpointGoesOnIdentically()
    {
        var codec = Codec();
        long frames = 0, checkpoints = 0, ticks = 0, refused = 0;
        for (var seed = 0; seed < 60; seed++)
        {
            var document = new GraphGenerator(seed).Build(10 + seed % 40).Text();
            var live = Build(document, codec, record: true);
            live.Runtime.Init();
            var random = new Random(seed);
            var script = new List<FrameScript>();
            for (var index = 0; index < 80; index++)
            {
                var requests = new List<(string, Req)>();
                for (var n = random.Next(3); n > 0; n--) requests.Add(($"f{index}-{n}", Pick(random, live.Dialogue.Runtime)));
                // A pause: usually one tick, now and then a stall of up to 25 ticks delivered as one Update.
                var frame = new FrameScript(requests, random.Next(6) == 0 ? random.Next(2, 26) : 1);
                script.Add(frame);
                Run(live, frame);
                Assert.True(live.Dialogue.IsQuiet, $"seed {seed} frame {index}: a request was still waiting after the tick");
            }
            frames += script.Count;
            ticks += live.Journal.Hashes.Count;
            refused += live.Heard.Count(h => h.StartsWith("refused", StringComparison.Ordinal));

            // The journal replays tick for tick.
            Assert.True(JournalReplay.Verify(live.Journal, codec, () => { var again = Build(document, codec, record: false); return (again.Runtime, again.Journal); }).Matches, $"seed {seed}: the journal did not replay");

            // The same script played again gives the same events and the same hash on every tick.
            var second = Build(document, codec, record: true);
            second.Runtime.Init();
            foreach (var frame in script) Run(second, frame);
            Assert.True(live.Heard.SequenceEqual(second.Heard), $"seed {seed}: the same script showed other events");
            Assert.True(live.Journal.Hashes.Select(h => h.Value).SequenceEqual(second.Journal.Hashes.Select(h => h.Value)), $"seed {seed}: the same script hashed differently");

            // A checkpoint at a random quiet moment, resumed in a new session: the rest of the script shows the same and ends in the same state.
            var cut = 1 + random.Next(script.Count - 2);
            var before = Build(document, codec, record: false);
            before.Runtime.Init();
            foreach (var frame in script.Take(cut)) Run(before, frame);
            // A stall leaves ticks owed that a new session does not owe; checkpoint at the first moment none is.
            while (before.Session.Clock.PendingTicks > 0 && cut < script.Count - 1) Run(before, script[cut++]);
            var resumed = Build(document, codec, record: false, DialogueCheckpoint.Open(before.Dialogue.Runtime.Set, DialogueCheckpoint.Capture(before.Dialogue)), before.Session.Clock.Tick);   // a resumed session goes on from the saved tick, as the history entries in the save expect
            resumed.Runtime.Init();
            var seen = before.Heard.Count;
            foreach (var frame in script.Skip(cut))
            {
                Run(before, frame);
                Run(resumed, frame);
            }
            Assert.True(before.Heard.Skip(seen).SequenceEqual(resumed.Heard), $"seed {seed}: the session resumed at frame {cut} showed other events");
            Assert.True(DialogueSave.StateDigest(before.Dialogue.Runtime.State) == DialogueSave.StateDigest(resumed.Dialogue.Runtime.State), $"seed {seed}: the resumed session ended in another state");
            Assert.True(DialogueSave.Write(before.Dialogue.Runtime).AsSpan().SequenceEqual(DialogueSave.Write(resumed.Dialogue.Runtime)), $"seed {seed}: the final saves differ");
            checkpoints++;
        }
        Assert.Equal(4_800, frames);
        Assert.Equal(60, checkpoints);
        Assert.True(ticks > 5_000, "ticks " + ticks);
        Assert.True(refused > 300, "refused " + refused);
    }
}
