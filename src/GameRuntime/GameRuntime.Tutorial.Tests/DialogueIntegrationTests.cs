using System.Text.Json.Nodes;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The dialogue engine and the tutorial: a step is spoken by a conversation and completes when that conversation ends; conversations read the tutorial's progress as external facts.</summary>
public sealed class DialogueIntegrationTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "guide";
    }

    private const string Talk = """
    { "format": 3, "id": "tut.talk", "variables": [],
      "externalFacts": [ { "id": "tutorial.progress", "type": "int", "min": 0, "max": 1000 }, { "id": "step.s2.status", "type": "string" } ],
      "speakers": [ { "id": "guide", "portraits": [ "calm" ] } ],
      "quests": [],
      "conversations": [
        { "id": "intro", "start": "check", "nodes": [
          { "id": "check", "kind": "branch", "cases": [ { "when": { "fact": "ext.step.s2.status", "op": "eq", "value": "active" }, "next": "late" } ], "otherwise": "first" },
          { "id": "first", "kind": "line", "speaker": "guide", "portrait": "calm", "line": "talk.first", "next": "bye" },
          { "id": "late", "kind": "line", "speaker": "guide", "portrait": "calm", "line": "talk.late", "next": "bye" },
          { "id": "bye", "kind": "end", "result": "done" } ] },
        { "id": "quit", "start": "q", "nodes": [
          { "id": "q", "kind": "line", "speaker": "guide", "portrait": "calm", "line": "talk.quit", "next": "e" },
          { "id": "e", "kind": "end", "result": "abandoned" } ] } ] }
    """;

    private static readonly Cast Speakers = new();

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public TutorialComponent Tutorial { get; }
        public DialogueComponent Dialogue { get; }
        public TutorialDialogueSpeaker Speaker { get; }
        public List<string> Lines { get; } = new();
        public List<DialogueEnded> Ended { get; } = new();
        public List<DialogueRefused> Refused { get; } = new();

        public Rig(IEnumerable<TutorialSpokenStep>? rules = null, string tutorialChannel = "", string dialogueChannel = "", TutorialSet? set = null, string document = Talk)
        {
            var tutorialSet = set ?? Samples.Small;
            var tutorialChannels = new TutorialChannels();
            tutorialChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            var runtime = Samples.Done(tutorialSet);
            runtime.Start();
            Tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), tutorialChannels, channel: tutorialChannel);
            var dialogueSet = DialogueLoader.Load(document, Speakers);
            var facts = new TutorialDialogueFacts(Tutorial);
            Dialogue = new DialogueComponent(new DialogueRuntime(dialogueSet, 1), Speakers, facts.Facts, dialogueChannels, channel: dialogueChannel);
            Speaker = new TutorialDialogueSpeaker("speaker", rules ?? [new TutorialSpokenStep("s2", "intro", "e3")], tutorialSet, dialogueSet, tutorialChannel, dialogueChannel);
            Session.AddComponent(Tutorial, 30);
            Session.AddComponent(Dialogue, 35);
            Session.AddComponent(Speaker, 40);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Subscribe<DialogueEnded>(Ended.Add);
            Session.Subscribe<DialogueRefused>(Refused.Add);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Observe(string evt, string channel = "") => Session.Submit(new TutorialRequested("o-" + evt, "observe", evt, 1, channel));
    }

    private static TutorialSet WithStepOnE3() => TutorialLoader.Load(Samples.SmallText.Replace("\"complete\": { \"fact\": \"event.e2.since_step\", \"op\": \"gte\", \"value\": 1 }", "\"complete\": { \"fact\": \"event.e3.since_step\", \"op\": \"gte\", \"value\": 1 }", StringComparison.Ordinal));

    [Fact]
    public void AStepBecomingActiveStartsItsConversationAndItsEndCompletesTheStep()
    {
        var set = WithStepOnE3();
        var rig = new Rig(set: set);
        rig.Tick(2);
        Assert.Empty(rig.Lines);                                           // s1 is being taught; s2 is not yet
        rig.Observe("e1");
        rig.Tick(3);
        Assert.Equal(["talk.late"], rig.Lines);                            // s2 began, the bridge asked, the dialogue spoke (and s2 was active, which the conversation read)
        Assert.Equal(1, rig.Speaker.Started);
        Assert.Equal("active", TutorialFacts.StatusName(rig.Tutorial.Runtime.StepState("s2").Status));
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick(3);
        Assert.Equal(["done"], rig.Ended.Select(e => e.Result));
        Assert.Equal(1, rig.Speaker.Finished);
        Assert.Equal("done", TutorialFacts.StatusName(rig.Tutorial.Runtime.StepState("s2").Status));
    }

    [Fact]
    public void TheConversationReadsTheTutorialsOwnFactsSoItChangesWithTheStep()
    {
        var set = WithStepOnE3();
        var rig = new Rig(set: set);
        rig.Tick();
        rig.Observe("e1");
        rig.Tick(4);
        Assert.Equal(["talk.late"], rig.Lines);                            // by the time it speaks, step s2 is active: the branch reads ext.step.s2.status
    }

    [Fact]
    public void OnlyAConversationTheBridgeStartedCompletesTheStep()
    {
        var set = WithStepOnE3();
        var rig = new Rig(set: set);
        rig.Tick(2);
        rig.Session.Submit(new DialogueStartRequested("other", "intro"));        // the same conversation, started by the game for another reason
        rig.Tick(2);
        Assert.Equal(["talk.first"], rig.Lines);                           // s2 is not active: the same conversation reads that and speaks its first line
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick(3);
        Assert.Equal(["done"], rig.Ended.Select(e => e.Result));
        Assert.Equal(0, rig.Speaker.Finished);
        Assert.Equal(0, rig.Tutorial.Runtime.EventCounts[2]);
    }

    [Fact]
    public void ARuleThatNamesAResultOnlyCountsThatResult()
    {
        var set = WithStepOnE3();
        var rig = new Rig([new TutorialSpokenStep("s2", "quit", "e3", "done")], set: set);
        rig.Tick();
        rig.Observe("e1");
        rig.Tick(4);
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick(3);
        Assert.Equal(["abandoned"], rig.Ended.Select(e => e.Result));
        Assert.Equal(0, rig.Speaker.Finished);
        Assert.Equal("active", TutorialFacts.StatusName(rig.Tutorial.Runtime.StepState("s2").Status));
    }

    [Fact]
    public void ARuleThatDoesNotFitTheDocumentsIsRefusedWhenTheGameIsAssembled()
    {
        var set = WithStepOnE3();
        var dialogue = DialogueLoader.Load(Talk, Speakers);
        void Bad(TutorialSpokenStep rule, params TutorialSpokenStep[] more) =>
            Assert.Equal(TutorialDialogueSpeaker.BadRule, Samples.Code(() => new TutorialDialogueSpeaker("s", [rule, .. more], set, dialogue)));
        Bad(new TutorialSpokenStep("nope", "intro", "e3"));
        Bad(new TutorialSpokenStep("s2", "nope", "e3"));
        Bad(new TutorialSpokenStep("s2", "intro", "nope"));
        Bad(new TutorialSpokenStep("s2", "intro", "e3"), new TutorialSpokenStep("s2", "quit", "e3"));
        Assert.Throws<ArgumentNullException>(() => new TutorialDialogueSpeaker("s", null!, set, dialogue));
    }

    [Fact]
    public void TheSpeakerObeysTheChannelsItWasGiven()
    {
        var set = WithStepOnE3();
        var rig = new Rig(set: set, tutorialChannel: "t", dialogueChannel: "d");
        rig.Tick();
        rig.Observe("e1", "t");
        rig.Tick(4);
        Assert.Equal(["talk.late"], rig.Lines);
        rig.Session.Submit(new DialogueAdvanceRequested("a", "d"));
        rig.Tick(3);
        Assert.Equal("done", TutorialFacts.StatusName(rig.Tutorial.Runtime.StepState("s2").Status));
    }

    [Fact]
    public void ADialogueDocumentIsCheckedAgainstTheTutorialDocument()
    {
        TutorialDialogueFacts.Check(DialogueLoader.Load(Talk, Speakers), Samples.Small);
        void Bad(string from, string to, string fragment)
        {
            var document = Talk.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(Talk, document);
            var refusal = Assert.Throws<EngineRefusedException>(() => TutorialDialogueFacts.Check(DialogueLoader.Load(document, Speakers), Samples.Small));
            Assert.Equal(TutorialDialogueFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad("{ \"id\": \"tutorial.progress\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", "{ \"id\": \"tutorial.nonsense\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", "does not offer");
        Bad("{ \"id\": \"tutorial.progress\", \"type\": \"int\", \"min\": 0, \"max\": 1000 }", "{ \"id\": \"tutorial.progress\", \"type\": \"string\" }", "is Integer in the tutorial document and String");
        Bad("\"min\": 0, \"max\": 1000 }, { \"id\": \"step", "\"min\": 0, \"max\": 500 }, { \"id\": \"step", "ranges 0 to 1000");
        Bad("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"event.e1.since_step\", \"type\": \"int\", \"min\": 0, \"max\": 5 }, ", "does not offer to a conversation");
    }

    [Fact]
    public void TheFactsAFacadeGivesAreTheEnginesWithThePrefix()
    {
        var rig = new Rig();
        rig.Tick();
        var facts = new TutorialDialogueFacts(rig.Tutorial, "x.").Facts();
        Assert.Equal("active", facts["x.step.s1.status"].Text);
        Assert.False(facts.ContainsKey("flag"));
        Assert.Equal("ext.", new TutorialDialogueFacts(rig.Tutorial).Prefix);
        Assert.Throws<ArgumentNullException>(() => new TutorialDialogueFacts(null!));
    }
}
