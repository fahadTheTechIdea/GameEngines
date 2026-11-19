using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The dialogue component with the format 2 features: interruption and timeout requests, tags and voice ids in the events, and a reload that may close the open conversation.</summary>
public sealed class DialogueFeatureComponentTests
{
    private sealed class Speakers : ISpeakerCatalog
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
     "format": 3, "id": "demo.features",
     "variables": [], "externalFacts": [],
     "speakers": [ { "id": "cmdr", "portraits": ["calm"], "emotions": ["angry", "happy"] } ],
     "quests": [],
     "conversations": [
      { "id": "main", "start": "hi", "nodes": [
        { "id": "hi", "kind": "line", "speaker": "cmdr", "portrait": "calm", "emotion": "happy", "voice": "vo-hi", "tags": ["greeting", "loud"], "autoAdvanceMs": 1500, "line": "main.hi", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "timeout": { "afterMs": 5000, "option": "stay" }, "options": [
           { "id": "stay", "line": "main.stay", "tags": ["quiet"], "next": "bye" }, { "id": "leave", "line": "main.leave", "next": "bye" } ] },
        { "id": "bye", "kind": "end", "result": "done" } ] },
      { "id": "alarm", "start": "a1", "nodes": [
        { "id": "a1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "emotion": "angry", "line": "alarm.a1", "next": "back" },
        { "id": "back", "kind": "return", "result": "ok" } ] }
     ]
    }
    """;

    private sealed class Game
    {
        public RuntimeSession Session { get; }
        public DialogueComponent Dialogue { get; }
        public List<string> Heard { get; } = new();
        public Reporter Reporter { get; } = new();
        private int _request;

        public Game(string document = Document)
        {
            Session = new RuntimeSession(new RuntimeClock(30), Reporter);
            var channels = new DialogueChannels();
            channels.Install(Session);
            var speakers = new Speakers();
            Dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(document, speakers), 1), speakers, () => new Dictionary<string, FactValue>(), channels);
            Session.AddComponent(Dialogue, 100);
            Session.Subscribe<DialogueLineShown>(e => Heard.Add($"t{e.Tick} line {e.Conversation}/{e.Node} tags={e.Tags} voice={e.Voice} emotion={e.Emotion} auto={e.AutoAdvanceMs}"));
            Session.Subscribe<DialogueChoicesShown>(e => Heard.Add($"t{e.Tick} choices {e.Node} {e.Options} timeout={e.TimeoutMs}/{e.TimeoutOption}"));
            Session.Subscribe<DialogueEnded>(e => Heard.Add($"t{e.Tick} ended {e.Result}"));
            Session.Subscribe<DialogueRefused>(e => Heard.Add($"t{e.Tick} refused {e.Code}"));
            Session.Subscribe<DialogueReloaded>(e => Heard.Add($"t{e.Tick} reloaded closed={e.ClosedConversation}"));
            Session.Init();
        }

        public string Id() => "r" + _request++;

        public void Tick() => Session.Update((RuntimeClock.MicrosecondsPerSecond + Session.Clock.TicksPerSecond - 1) / Session.Clock.TicksPerSecond);
    }

    [Fact]
    public void ALineAndAChoiceCarryTheirTagsVoiceEmotionAndTimeoutInTheirEvents()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested(game.Id(), "main"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested(game.Id()));
        game.Tick();
        Assert.Equal(
            [
                "t1 line main/hi tags=greeting,loud voice=vo-hi emotion=happy auto=1500",
                "t2 choices ask stay|main.stay|1|quiet|0;leave|main.leave|1||0 timeout=5000/stay",
            ], game.Heard);
        Assert.Empty(game.Reporter.Codes);
    }

    [Fact]
    public void ATimeoutRequestTakesTheTimeoutAnswerInTheNextUpdateAndNotBefore()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested(game.Id(), "main"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested(game.Id()));
        game.Tick();
        game.Heard.Clear();
        var before = game.Dialogue.Fingerprint();
        game.Session.Submit(new DialogueTimeoutRequested(game.Id()));
        Assert.Equal(before, game.Dialogue.Fingerprint().Replace("pending=1", "pending=0"));     // queued: the state has not moved
        Assert.Empty(game.Heard);
        game.Tick();
        Assert.Equal(["t3 ended done"], game.Heard);
    }

    [Fact]
    public void AnInterruptionShowsTheInterrupterAndTheAdvanceReturnsToTheChoice()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested(game.Id(), "main"));
        game.Tick();
        game.Session.Submit(new DialogueInterruptRequested(game.Id(), "alarm"));
        game.Tick();
        game.Session.Submit(new DialogueAdvanceRequested(game.Id()));
        game.Tick();
        Assert.Equal(
            [
                "t1 line main/hi tags=greeting,loud voice=vo-hi emotion=happy auto=1500",
                "t2 line alarm/a1 tags= voice= emotion=angry auto=0",
                "t3 line main/hi tags=greeting,loud voice=vo-hi emotion=happy auto=1500",
            ], game.Heard);
    }

    [Fact]
    public void RefusalsOfTheNewRequestsAreEventsWithTheirCodesAndChangeNothing()
    {
        var game = new Game();
        game.Session.Submit(new DialogueInterruptRequested(game.Id(), "alarm"));      // nothing open
        game.Session.Submit(new DialogueTimeoutRequested(game.Id()));                  // nothing open
        game.Tick();
        game.Session.Submit(new DialogueStartRequested(game.Id(), "main"));
        game.Tick();
        game.Session.Submit(new DialogueInterruptRequested(game.Id(), "nobody"));
        game.Session.Submit(new DialogueTimeoutRequested(game.Id()));                  // at a line, not a choice
        game.Tick();
        Assert.Equal(["t1 refused DLG_NOT_WAITING", "t1 refused DLG_NOT_WAITING"], game.Heard.Where(h => h.StartsWith("t1 refused", StringComparison.Ordinal)).ToList());
        Assert.Equal(["t3 refused DLG_NO_CONVERSATION", "t3 refused DLG_NOT_WAITING"], game.Heard.Where(h => h.StartsWith("t3", StringComparison.Ordinal)).ToList());
        Assert.Equal(Waiting.Line, game.Dialogue.Runtime.State.WaitingFor);
    }

    [Fact]
    public void AReloadRequestMayAskToCloseTheConversationThatCannotGoOn()
    {
        var edited = Document.Replace("{ \"id\": \"a1\", \"kind\": \"line\", \"speaker\": \"cmdr\", \"portrait\": \"calm\", \"emotion\": \"angry\", \"line\": \"alarm.a1\", \"next\": \"back\" }",
            "{ \"id\": \"a1\", \"kind\": \"effects\", \"effects\": [ { \"signal\": \"x\" } ], \"next\": \"back\" }", StringComparison.Ordinal);
        Assert.NotEqual(Document, edited);
        var refused = new Game();
        refused.Session.Submit(new DialogueStartRequested(refused.Id(), "main"));
        refused.Session.Submit(new DialogueInterruptRequested(refused.Id(), "alarm"));
        refused.Tick();
        refused.Heard.Clear();
        refused.Session.Submit(new DialogueReloadRequested(refused.Id(), edited));
        refused.Tick();
        Assert.Equal(["t2 refused DLG_RELOAD_INCOMPATIBLE"], refused.Heard);
        Assert.True(refused.Dialogue.Runtime.InConversation);
        var restarted = new Game();
        restarted.Session.Submit(new DialogueStartRequested(restarted.Id(), "main"));
        restarted.Session.Submit(new DialogueInterruptRequested(restarted.Id(), "alarm"));
        restarted.Tick();
        restarted.Heard.Clear();
        restarted.Session.Submit(new DialogueReloadRequested(restarted.Id(), edited, restart: true));
        restarted.Tick();
        Assert.Equal(["t2 reloaded closed=alarm/a1"], restarted.Heard);
        Assert.False(restarted.Dialogue.Runtime.InConversation);
    }

    [Fact]
    public void ACheckpointInsideAnInterruptionRestoresAndContinuesTheSame()
    {
        var game = new Game();
        game.Session.Submit(new DialogueStartRequested(game.Id(), "main"));
        game.Tick();
        game.Session.Submit(new DialogueInterruptRequested(game.Id(), "alarm"));
        game.Tick();
        var bytes = DialogueCheckpoint.Capture(game.Dialogue);
        var resumed = DialogueCheckpoint.Open(game.Dialogue.Runtime.Set, bytes);
        Assert.Equal(DialogueSave.StateDigest(game.Dialogue.Runtime.State), DialogueSave.StateDigest(resumed.State));
        Assert.Equal([new DialogueFrame("main", "hi", true)], resumed.State.Frames);
        var back = resumed.Next(new Dictionary<string, FactValue>());
        Assert.Equal("hi", back.Line!.Node);
    }
}
