using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The dialogue engine, the tutorial and the world: a conversation reads the world as external facts and is started, and a quest begun, by what the world signals; a tutorial step completes when the world really did the thing.</summary>
public sealed class StoryIntegrationTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "farmer";
    }

    private const string Farm = """
    { "format": 3, "id": "world.farm", "variables": [],
      "externalFacts": [ { "id": "world.riverton.hazard.flood", "type": "int", "min": 0, "max": 2 }, { "id": "world.season", "type": "string" }, { "id": "world.stress", "type": "int", "min": 0, "max": 1000 } ],
      "speakers": [ { "id": "farmer", "portraits": [ "calm" ] } ],
      "quests": [ { "id": "flood-relief", "title": "quest.relief.title", "start": "go", "stages": [ { "id": "go", "journal": "quest.relief.go", "advance": [] } ] } ],
      "conversations": [ { "id": "farmer", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [ { "when": { "fact": "ext.world.riverton.hazard.flood", "op": "gte", "value": 2 }, "next": "major" }, { "when": { "fact": "ext.world.riverton.hazard.flood", "op": "gte", "value": 1 }, "next": "minor" } ], "otherwise": "calm" },
        { "id": "major", "kind": "line", "speaker": "farmer", "portrait": "calm", "line": "farmer.major", "next": "bye" },
        { "id": "minor", "kind": "line", "speaker": "farmer", "portrait": "calm", "line": "farmer.minor", "next": "bye" },
        { "id": "calm", "kind": "line", "speaker": "farmer", "portrait": "calm", "line": "farmer.calm", "next": "bye" },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    private const string TutorialText = """
    { "format": 1, "id": "world.tutorial", "time": { "stepsPerDay": 100 }, "limits": { "maxActiveSteps": 1, "maxVisibleTips": 2, "tipGap": 0 },
      "facts": [],
      "events": [ { "id": "first_storm" }, { "id": "first_flood" } ],
      "inputs": [],
      "skills": [],
      "journey": { "languages": [ "en" ], "defaultLanguage": "en", "pages": [ { "id": "p1", "key": "k.p1", "body": "k.p1b", "art": "k.p1a" } ], "entries": [ "host" ] },
      "steps": [
        { "id": "storm", "key": "k.s1", "body": "k.s1b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [], "requiresAny": [],
          "complete": { "fact": "event.first_storm.since_step", "op": "gte", "value": 1 }, "hints": [] },
        { "id": "flood", "key": "k.s2", "body": "k.s2b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [ "storm" ], "requiresAny": [],
          "complete": { "fact": "event.first_flood.since_step", "op": "gte", "value": 1 }, "hints": [] } ],
      "tips": [] }
    """;

    private static readonly WorldSet Valley = Samples.Calm();

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public WorldComponent World { get; }
        public DialogueComponent Dialogue { get; }
        public WorldDialogueStory Story { get; }
        public WorldDialogueFacts Facts { get; }
        public List<string> Lines { get; } = new();
        public List<DialogueQuestChanged> Quests { get; } = new();
        public List<DialogueRefused> Refused { get; } = new();

        public Rig(IEnumerable<WorldStoryRule>? rules = null, string region = "riverton")
        {
            var worldChannels = new WorldChannels();
            worldChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            var cast = new Cast();
            var dialogueSet = DialogueLoader.Load(Farm, cast);
            var host = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
            World = new WorldComponent(WorldRuntime.Create(Valley), new TickWorldClock(Valley, ticksPerStep: 1_000_000), () => host, worldChannels);
            Facts = new WorldDialogueFacts(World, region);
            WorldDialogueFacts.Check(dialogueSet, Valley);
            Dialogue = new DialogueComponent(new DialogueRuntime(dialogueSet, 1), cast, Facts.Facts, dialogueChannels);
            Story = new WorldDialogueStory("story", rules ?? [new WorldStoryRule("world.flood.started", Quest: "flood-relief", Region: "riverton"), new WorldStoryRule("world.flood.major", Conversation: "farmer", Region: "riverton")], Valley, dialogueSet);
            Session.AddComponent(World, 10);
            Session.AddComponent(Story, 15);
            Session.AddComponent(Dialogue, 40);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Subscribe<DialogueQuestChanged>(Quests.Add);
            Session.Subscribe<DialogueRefused>(Refused.Add);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a, string b = "", string c = "") => Session.Submit(new WorldRequested("t" + kind + a + b + c, kind, a, b, c));
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFarmersLineDependsOnWhetherTheRiverIsInFloodInTheRegionHeIsIn()
    {
        string First(Action<Rig> before)
        {
            var rig = new Rig(rules: []);
            rig.Tick();
            before(rig);
            rig.Tick(2);
            rig.Session.Submit(new DialogueStartRequested("d", "farmer"));
            rig.Tick(2);
            return Assert.Single(rig.Lines);
        }
        Assert.Equal("farmer.calm", First(_ => { }));
        Assert.Equal("farmer.minor", First(r => r.Ask("hazard", "riverton", "flood", "minor")));
        Assert.Equal("farmer.major", First(r => r.Ask("hazard", "riverton", "flood", "major")));
        Assert.Equal("farmer.major", First(r => r.Ask("hazard", "hillfort", "flood", "major")));                     // a flood in Hillfort spreads to Riverton at half strength, at the same rung
    }

    [Fact]
    public void TheFactsArePrefixedAndCanBePointedAtAnotherRegion()
    {
        var rig = new Rig(rules: []);
        rig.Tick();
        rig.Ask("hazard", "seaport", "quake", "great");
        rig.Tick(2);
        Assert.Equal(3, rig.Facts.Facts()["ext.world.riverton.hazard.quake"].Integer);                           // the quake reached Riverton from Seaport at the same rung, the third
        Assert.All(rig.Facts.Facts().Keys, k => Assert.StartsWith("ext.world.", k, StringComparison.Ordinal));
        rig.Facts.At("seaport");
        Assert.Equal("seaport", rig.Facts.Region);
        Assert.Equal(3, rig.Facts.Facts()["ext.world.seaport.hazard.quake"].Integer);
        Assert.DoesNotContain("ext.world.riverton.temp", rig.Facts.Facts().Keys);
        rig.Facts.At("");
        Assert.Equal(WorldFacts.GlobalNames.Count, rig.Facts.Facts().Count);
        rig.Facts.At("nowhere");
        Assert.Equal(WorldCodes.UnknownRegion, Samples.Code(() => rig.Facts.Facts()));
    }

    [Fact]
    public void ADialogueDocumentThatReadsWhatTheWorldDoesNotOfferIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(string fragment, string from, string to)
        {
            var cast = new Cast();
            var dialogue = DialogueLoader.Load(Farm.Replace(from, to, StringComparison.Ordinal), cast);
            var refusal = Assert.Throws<EngineRefusedException>(() => WorldDialogueFacts.Check(dialogue, Valley));
            Assert.Equal(WorldDialogueFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused("does not offer", "{ \"id\": \"world.season\", \"type\": \"string\" }", "{ \"id\": \"world.nonsense\", \"type\": \"string\" }");
        Refused("is Integer in the world document and String in the dialogue document", "{ \"id\": \"world.stress\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", "{ \"id\": \"world.stress\", \"type\": \"string\" }");
        Refused("outside the 0 to 500 the dialogue document declares", "{ \"id\": \"world.stress\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", "{ \"id\": \"world.stress\", \"type\": \"int\", \"min\": 0, \"max\": 500 }");
        WorldDialogueFacts.Check(DialogueLoader.Load(Farm, new Cast()), Valley);
    }

    // ---- story triggers ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFloodStartsTheReliefQuestAndAMajorOneSendsTheFarmerToSpeak()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Ask("hazard", "riverton", "flood", "minor");
        rig.Tick(3);
        Assert.Equal("flood-relief", Assert.Single(rig.Quests).Quest);
        Assert.Empty(rig.Lines);
        Assert.Equal(1, rig.Story.Triggered);
        var major = new Rig();
        major.Tick();
        major.Ask("hazard", "riverton", "flood", "major");
        major.Tick(4);
        Assert.Equal(["farmer.major"], major.Lines);                       // the conversation read the flood the world had just made
        Assert.Empty(major.Quests);
        Assert.Empty(major.Refused);
    }

    [Fact]
    public void ARuleIsCheckedAgainstBothDocumentsWhenTheGameIsAssembled()
    {
        var dialogue = DialogueLoader.Load(Farm, new Cast());
        void Bad(WorldStoryRule rule, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => new WorldDialogueStory("s", [rule], Valley, dialogue));
            Assert.Equal(WorldDialogueStory.BadRule, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad(new WorldStoryRule("world.never", Quest: "flood-relief"), "never issues the signal");
        Bad(new WorldStoryRule("world.flood.started"), "a quest or a conversation");
        Bad(new WorldStoryRule("world.flood.started", Quest: "flood-relief", Conversation: "farmer"), "a quest or a conversation");
        Bad(new WorldStoryRule("world.flood.started", Quest: "nope"), "no quest 'nope'");
        Bad(new WorldStoryRule("world.flood.started", Conversation: "nope"), "no conversation 'nope'");
        Assert.NotNull(new WorldDialogueStory("s", [], Valley, dialogue));
    }

    [Fact]
    public void TwoRulesOnOneSignalBothFireAndOnlyTheWorldsChannelIsListenedTo()
    {
        var dialogue = DialogueLoader.Load(Farm, new Cast());
        var rig = new Rig(rules: [new WorldStoryRule("world.flood.started", Quest: "flood-relief", Region: "riverton"), new WorldStoryRule("world.flood.started", Conversation: "farmer", Region: "riverton")]);
        rig.Tick();
        rig.Ask("hazard", "riverton", "flood", "minor");
        rig.Tick(4);
        Assert.Equal(2, rig.Story.Triggered);
        Assert.Single(rig.Quests);
        Assert.Equal(["farmer.minor"], rig.Lines);
        // A story that listens to another world's channel does nothing for this one.
        var deaf = new WorldDialogueStory("deaf", [new WorldStoryRule("world.flood.started", Quest: "flood-relief")], Valley, dialogue, worldChannel: "elsewhere");
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        new WorldChannels().Install(session);
        new DialogueChannels().Install(session);
        session.AddComponent(deaf, 15);
        session.Init();
        session.Submit(new WorldEffectIssued("world", "", 1, 1, "signal", "world.flood.started", "riverton", "riverton", 0, "hazard:flood"));
        session.Update(20_000);
        Assert.Equal(0, deaf.Triggered);
    }

    // ---- the tutorial -----------------------------------------------------------------------------------------------------------------

    private sealed class TutorialRig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public WorldComponent World { get; }
        public TutorialComponent Tutorial { get; }
        public WorldTutorialObserver Observer { get; }

        public TutorialRig(IEnumerable<WorldSeenRule>? rules = null)
        {
            var set = TutorialLoader.Load(TutorialText);
            var runtime = TutorialRuntime.Create(set);
            runtime.SelectLanguage("en");
            while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
            runtime.ChooseEntry("host");
            runtime.Start();
            var worldChannels = new WorldChannels();
            worldChannels.Install(Session);
            var tutorialChannels = new TutorialChannels();
            tutorialChannels.Install(Session);
            var host = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
            World = new WorldComponent(WorldRuntime.Create(Valley), new TickWorldClock(Valley, ticksPerStep: 1_000_000), () => host, worldChannels);
            Tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => new Dictionary<string, FactValue>(), tutorialChannels);
            Observer = new WorldTutorialObserver("seen", rules ?? [new WorldSeenRule("world.storm.landfall", "first_storm", "seaport"), new WorldSeenRule("world.flood.started", "first_flood", "hillfort")], Valley, set);
            Session.AddComponent(World, 10);
            Session.AddComponent(Observer, 15);
            Session.AddComponent(Tutorial, 30);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a, string b, string c) => Session.Submit(new WorldRequested("t" + kind + a + b, kind, a, b, c));

        public string Status(string step) => TutorialFacts.StatusName(Tutorial.Runtime.StepState(step).Status);
    }

    [Fact]
    public void AStepIsDoneWhenTheWorldReallyDidTheThingAndNotBefore()
    {
        var rig = new TutorialRig();
        rig.Tick(3);
        Assert.Equal("active", rig.Status("storm"));
        Assert.Equal("pending", rig.Status("flood"));
        rig.Ask("hazard", "hillfort", "flood", "minor");                      // a flood first: the second step is not yet being taught, so it does not count for it
        rig.Tick(3);
        Assert.Equal("active", rig.Status("storm"));
        rig.Ask("hazard", "seaport", "storm", "gale");
        rig.Tick(4);
        Assert.Equal("done", rig.Status("storm"));
        Assert.Equal(2, rig.Observer.Observed);
    }

    [Fact]
    public void AnObservationWaitsForTheStepItCompletesAndTheRulesAreCheckedAgainstBothDocuments()
    {
        var set = TutorialLoader.Load(TutorialText);
        Assert.Equal(WorldTutorialObserver.BadRule, Samples.Code(() => new WorldTutorialObserver("o", [new WorldSeenRule("world.never", "first_storm")], Valley, set)));
        Assert.Equal(WorldTutorialObserver.BadRule, Samples.Code(() => new WorldTutorialObserver("o", [new WorldSeenRule("world.storm.landfall", "first_nothing")], Valley, set)));
        var rig = new TutorialRig(rules: []);
        rig.Ask("hazard", "seaport", "storm", "gale");
        rig.Tick(4);
        Assert.Equal("active", rig.Status("storm"));
        Assert.Equal(0, rig.Observer.Observed);
    }
}
