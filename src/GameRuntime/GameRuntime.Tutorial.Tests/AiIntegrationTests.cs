using AIEngine;
using EngineCommon;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The AI engine and the tutorial: a guide agent knows where the player is only when it is told, as a percept (knowledge, not truth), and decides what to do by what it was last told.</summary>
public sealed class AiIntegrationTests
{
    private const string Guide = """
    { "format": 1, "id": "tut.guide",
      "keys": [ { "id": "k_hint", "type": "int", "initial": 0, "min": 0, "max": 4, "source": "agent" }, { "id": "k_progress", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" } ],
      "percepts": [ { "kind": "tutorial", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "step", "type": "string", "unknown": "" }, { "id": "hint_level", "type": "int", "min": 0, "max": 4, "unknown": 0 },
          { "id": "progress", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "phase", "type": "string", "unknown": "" } ] } ],
      "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
      "behaviors": [ { "id": "guide", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "see-hint", "kind": "let", "key": "k_hint", "value": { "known": { "from": "tutorial", "id": "me", "attr": "hint_level" } } },
          { "id": "see-progress", "kind": "let", "key": "k_progress", "value": { "known": { "from": "tutorial", "id": "me", "attr": "progress" } } },
          { "id": "choose", "kind": "utility", "momentum": 0, "options": [
            { "id": "point", "weight": 1000, "considerations": [ { "input": { "key": "k_hint" }, "min": 0, "max": 2, "curve": { "kind": "linear" } } ], "child": { "id": "do-point", "kind": "act", "intent": "do", "args": { "what": "point" } } },
            { "id": "cheer", "weight": 900, "considerations": [ { "input": { "key": "k_progress" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-cheer", "kind": "act", "intent": "do", "args": { "what": "cheer" } } } ] } ] } } ] }
    """;

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public TutorialComponent Tutorial { get; }
        public TutorialAiBridge Bridge { get; }
        public List<(long Tick, string Args)> Intents { get; } = new();
        public List<string> Posts { get; } = new();
        public List<string> AiRefused { get; } = new();

        public Rig(string aiDocument = Guide, string kind = "tutorial", TutorialSet? set = null)
        {
            var ai = AiLoader.Load(aiDocument);
            AiEventCatalog.Declare(Session);
            var channels = new TutorialChannels();
            channels.Install(Session);
            var tutorialSet = set ?? Samples.Small;
            var runtime = Samples.Done(tutorialSet);
            runtime.Start();
            Tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), channels);
            Bridge = new TutorialAiBridge("tut-ai", Tutorial, "ai", ai, kind, "me", "guide");
            Session.AddComponent(Tutorial, 10);
            Session.AddComponent(Bridge, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(ai, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add((e.Tick, e.Args)));
            Session.Subscribe<AiPerceptPosted>(e => Posts.Add(e.Attrs));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Init();
            Session.Submit(new AiAgentAddRequested("ai", "agent", "guide", "guide"));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public string Latest => Intents.Count == 0 ? "" : Intents[^1].Args;
    }

    [Fact]
    public void TheGuideIsToldWhereThePlayerIsAndActsOnWhatItWasToldNotOnWhatIsTrue()
    {
        var rig = new Rig();
        rig.Tick(3);
        Assert.Equal("step=s1;hint_level=0;progress=0;phase=running", rig.Posts[0]);
        rig.Tick(8);                                                 // tick 11: the player has been idle 10 steps: the second hint is due; the guide hears of the first at tick 6 and the second at tick 11
        Assert.Equal(["step=s1;hint_level=0;progress=0;phase=running", "step=s1;hint_level=1;progress=0;phase=running", "step=s1;hint_level=2;progress=0;phase=running"], rig.Posts);
        rig.Tick(3);
        Assert.Equal("what=point", rig.Latest);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void ThePlayersProgressMakesTheGuideCheer()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Session.Submit(new TutorialRequested("o1", "observe", "e1"));
        rig.Tick(2);
        rig.Session.Submit(new TutorialRequested("o2", "observe", "e2"));
        rig.Tick(4);
        Assert.Contains("progress=666", rig.Posts[^1]);
        Assert.Contains("step=s3", rig.Posts[^1]);
        Assert.Equal("what=cheer", rig.Latest);
    }

    [Fact]
    public void NothingIsPostedWhileNothingHasChangedAndNotifyTellsTheGuideAgain()
    {
        var rig = new Rig();
        rig.Tick(4);
        var posted = rig.Bridge.Posted;
        rig.Tick(1);
        Assert.Equal(posted, rig.Bridge.Posted);
        rig.Bridge.Notify();
        rig.Tick();
        Assert.Equal(posted + 1, rig.Bridge.Posted);
    }

    [Fact]
    public void AFinishedTutorialTellsTheGuideThereIsNoStep()
    {
        var rig = new Rig();
        rig.Session.Submit(new TutorialRequested("s", "skip-tutorial"));
        rig.Tick(3);
        Assert.Equal("step=;hint_level=0;progress=1000;phase=skipped", rig.Posts[^1]);
    }

    [Fact]
    public void AnAiDocumentThatDoesNotDeclareTheExactPerceptIsRefusedWhenTheGameIsAssembled()
    {
        void Bad(string from, string to, string fragment)
        {
            var document = Guide.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(Guide, document);
            var refusal = Assert.Throws<EngineRefusedException>(() => new Rig(document));
            Assert.Equal(TutorialAiBridge.BadKind, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad("\"owned\": false,", "\"owned\": true, \"ownerCapacity\": 1,", "is owned");
        Bad("{ \"id\": \"step\", \"type\": \"string\", \"unknown\": \"\" }", "{ \"id\": \"step\", \"type\": \"bool\", \"unknown\": false }", "'step' as String");
        Bad("{ \"id\": \"hint_level\", \"type\": \"int\", \"min\": 0, \"max\": 4, \"unknown\": 0 }", "{ \"id\": \"hint_level\", \"type\": \"int\", \"min\": 0, \"max\": 1, \"unknown\": 0 }", "must allow 0 to 2");
        Bad("{ \"id\": \"progress\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"progress\", \"type\": \"int\", \"min\": 0, \"max\": 500, \"unknown\": 0 }", "must allow 0 to 1000");
        Assert.Equal(TutorialAiBridge.BadKind, Assert.Throws<EngineRefusedException>(() => new Rig(Guide, "other")).Code);
    }

    [Fact]
    public void AnAiDocumentWithAnExtraAttributeIsRefusedToo()
    {
        var document = Guide.Replace("{ \"id\": \"phase\"", "{ \"id\": \"extra\", \"type\": \"int\", \"min\": 0, \"max\": 1, \"unknown\": 0 }, { \"id\": \"phase\"", StringComparison.Ordinal);
        Assert.Equal(TutorialAiBridge.BadKind, Assert.Throws<EngineRefusedException>(() => new Rig(document)).Code);
        Assert.Throws<ArgumentNullException>(() => new TutorialAiBridge("x", null!, "ai", AiLoader.Load(Guide), "tutorial", "me", "guide"));
        Assert.Throws<ArgumentNullException>(() => new TutorialAiBridge("x", new Rig().Tutorial, "ai", null!, "tutorial", "me", "guide"));
    }
}
