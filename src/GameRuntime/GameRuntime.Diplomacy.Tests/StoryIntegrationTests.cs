using DialogueEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The dialogue engine and the tutorial with the diplomacy: a negotiation reads the pair's regard and war as external facts and what it settles becomes a request; a tutorial step completes when the thing was really done between the realms.</summary>
public sealed class StoryIntegrationTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "envoy";
    }

    private const string Court = """
    { "format": 3, "id": "dip.court", "variables": [],
      "externalFacts": [ { "id": "diplomacy.aurel.brass.regard", "type": "int", "min": -1000, "max": 1000 }, { "id": "diplomacy.aurel.brass.at_war", "type": "bool" } ],
      "speakers": [ { "id": "envoy", "portraits": [ "calm" ] } ],
      "quests": [ { "id": "talks", "title": "quest.talks.title", "start": "go", "stages": [ { "id": "go", "journal": "quest.talks.go", "advance": [] } ] } ],
      "conversations": [ { "id": "court", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [
            { "when": { "fact": "ext.diplomacy.aurel.brass.at_war", "op": "eq", "value": true }, "next": "war" },
            { "when": { "fact": "ext.diplomacy.aurel.brass.regard", "op": "gte", "value": 200 }, "next": "warm" } ], "otherwise": "cold" },
        { "id": "war", "kind": "line", "speaker": "envoy", "portrait": "calm", "line": "court.war", "next": "ask" },
        { "id": "warm", "kind": "line", "speaker": "envoy", "portrait": "calm", "line": "court.warm", "next": "ask" },
        { "id": "cold", "kind": "line", "speaker": "envoy", "portrait": "calm", "line": "court.cold", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "options": [
            { "id": "offer", "line": "court.offer", "effects": [ { "signal": "offer-alliance", "args": [ { "text": "aurel" }, { "text": "brass" } ] } ], "next": "bye" },
            { "id": "insult", "line": "court.insult", "effects": [ { "signal": "insult", "args": [ { "text": "aurel" }, { "text": "brass" } ] } ], "next": "bye" },
            { "id": "talks", "line": "court.talks", "effects": [ { "startQuest": "talks" } ], "next": "bye" } ] },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    private static readonly DiplomacyOutcome[] Rules =
    [
        new("signal:offer-alliance", DiplomacyOutcomeKind.Propose, "{0}", "{1}", "alliance"),
        new("signal:insult", DiplomacyOutcomeKind.Deed, "{0}", "{1}", "insult", Amount: 500),
        new("quest:talks:active", DiplomacyOutcomeKind.Grant, "brass", "aurel", "border", Days: 20),
    ];

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public DiplomacyComponent Diplomacy { get; }
        public DiplomacyDialogueFacts Facts { get; }
        public DiplomacyDialogueOutcomes Outcomes { get; }
        public List<string> Lines { get; } = new();
        public List<DiplomacyRefused> Refused { get; } = new();
        public List<DiplomacyProposalChanged> Proposals { get; } = new();

        public Rig(IEnumerable<DiplomacyOutcome>? rules = null, DipSet? set = null)
        {
            var dip = set ?? Samples.Calm();
            var diplomacyChannels = new DiplomacyChannels();
            diplomacyChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            Diplomacy = new DiplomacyComponent(DipRuntime.Create(dip), new TickDiplomacyClock(dip), () => Samples.Crisis(), diplomacyChannels);
            Facts = new DiplomacyDialogueFacts(Diplomacy, "aurel", "brass");
            var cast = new Cast();
            var dialogueSet = DialogueLoader.Load(Court, cast);
            DiplomacyDialogueFacts.Check(dialogueSet, dip);
            var dialogue = new DialogueComponent(new DialogueRuntime(dialogueSet, 1), cast, Facts.Facts, dialogueChannels);
            Outcomes = new DiplomacyDialogueOutcomes("outcomes", rules ?? Rules);
            Session.AddComponent(Diplomacy, 30);
            Session.AddComponent(dialogue, 40);
            Session.AddComponent(Outcomes, 50);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Subscribe<DiplomacyRefused>(Refused.Add);
            Session.Subscribe<DiplomacyProposalChanged>(Proposals.Add);
            Session.Init();
            Tick();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Talk(string option)
        {
            Session.Submit(new DialogueStartRequested("talk", "court"));
            Tick();
            Session.Submit(new DialogueAdvanceRequested("a"));
            Tick();
            Session.Submit(new DialogueChooseRequested("c", option));
            Tick();
        }
    }

    private static string FirstLine(Action<Rig>? prepare = null)
    {
        var rig = new Rig();
        prepare?.Invoke(rig);
        rig.Session.Submit(new DialogueStartRequested("talk", "court"));
        rig.Tick(2);
        return Assert.Single(rig.Lines);
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnEnvoysLineDependsOnTheRegardBetweenTheRealmsAndOnWhetherTheyAreAtWar()
    {
        Assert.Equal("court.cold", FirstLine());
        Assert.Equal("court.warm", FirstLine(r => r.Diplomacy.Runtime.Remember("brass", "aurel", "gift", 1000)));
        Assert.Equal("court.war", FirstLine(r => r.Diplomacy.Runtime.DeclareWar("aurel", "brass", "")));
        Assert.Equal("court.war", FirstLine(r => { r.Diplomacy.Runtime.Remember("brass", "aurel", "gift", 1000); r.Diplomacy.Runtime.DeclareWar("brass", "aurel", ""); }));   // the first matching case wins: at war whatever the regard
    }

    [Fact]
    public void TheFactsArePrefixedAndCanBePointedAtAnotherPair()
    {
        var rig = new Rig();
        var facts = rig.Facts.Facts();
        Assert.All(facts.Keys, k => Assert.StartsWith("ext.diplomacy.aurel.", k, StringComparison.Ordinal));
        Assert.Contains("ext.diplomacy.aurel.brass.regard", facts.Keys);
        Assert.Contains("ext.diplomacy.aurel.stability", facts.Keys);
        rig.Facts.Between("dune", "ember");
        Assert.Equal(("dune", "ember"), (rig.Facts.Realm, rig.Facts.Other));
        Assert.Contains("ext.diplomacy.dune.ember.regard", rig.Facts.Facts().Keys);
        rig.Facts.Between("dune", "");
        Assert.DoesNotContain("ext.diplomacy.dune.ember.regard", rig.Facts.Facts().Keys);
        rig.Facts.Between("nobody", "ember");
        Assert.Equal(DipCodes.UnknownRealm, Samples.Code(() => rig.Facts.Facts()));
        Assert.Equal("ext.", rig.Facts.Prefix);
        Assert.Throws<ArgumentNullException>(() => new DiplomacyDialogueFacts(null!, "a", "b"));
    }

    [Fact]
    public void ADialogueDocumentThatReadsWhatTheDiplomacyDoesNotOfferIsRefusedWhenTheGameIsAssembled()
    {
        var cast = new Cast();
        void Refused(string declaration, string fragment)
        {
            var dialogue = DialogueLoader.Load(Court.Replace("\"externalFacts\": [", "\"externalFacts\": [ " + declaration + ",", StringComparison.Ordinal), cast);
            var refusal = Assert.Throws<EngineRefusedException>(() => DiplomacyDialogueFacts.Check(dialogue, Samples.Isles));
            Assert.Equal(DiplomacyDialogueFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused("{ \"id\": \"diplomacy.aurel.brass.nonsense\", \"type\": \"bool\" }", "does not offer");
        Refused("{ \"id\": \"diplomacy.aurel.brass.treaties\", \"type\": \"string\" }", "is Integer in the diplomacy document and String in the dialogue document");
        Refused("{ \"id\": \"diplomacy.aurel.stability\", \"type\": \"int\", \"min\": 0, \"max\": 500 }", "outside the 0 to 500 the dialogue document declares");
        DiplomacyDialogueFacts.Check(DialogueLoader.Load(Court, cast), Samples.Isles);
        Assert.Throws<ArgumentNullException>(() => DiplomacyDialogueFacts.Check(null!, Samples.Isles));
        Assert.Throws<ArgumentNullException>(() => DiplomacyDialogueFacts.Check(DialogueLoader.Load(Court, cast), null!));
    }

    [Fact]
    public void AFactOutsideTheDiplomacyNamespaceIsNotCheckedHere()
    {
        var other = Court.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"squads\", \"type\": \"int\", \"min\": 0, \"max\": 9 },", StringComparison.Ordinal);
        DiplomacyDialogueFacts.Check(DialogueLoader.Load(other, new Cast()), Samples.Isles);
    }

    // ---- outcomes, written back as deferred requests ----------------------------------------------------------------------------------

    [Fact]
    public void AnOfferMadeInAConversationBecomesAProposalTheDiplomacyAnswersByItsScore()
    {
        var rig = new Rig();
        rig.Talk("offer");
        Assert.Equal(1, rig.Outcomes.Requested);
        rig.Tick(4);
        Assert.Equal(["made", "accepted"], rig.Proposals.Select(p => p.Change).ToArray());
        Assert.Equal(("alliance", "aurel", "brass"), (rig.Proposals[0].Type, rig.Proposals[0].From, rig.Proposals[0].To));
        Assert.Equal(["alliance"], rig.Diplomacy.Runtime.Treaties.Select(t => rig.Diplomacy.Runtime.Set.Types[t.Type].Id).ToArray());
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void AnInsultSpokenInAConversationIsRememberedByTheRealmInsulted()
    {
        var rig = new Rig();
        var before = rig.Diplomacy.Runtime.RegardOf("brass", "aurel");
        rig.Talk("insult");
        rig.Tick(2);
        Assert.InRange(before - rig.Diplomacy.Runtime.RegardOf("brass", "aurel"), 145, 155);          // the deed is worth -300 at half the scale
    }

    [Fact]
    public void AQuestThatStartsIsARuleToo()
    {
        var rig = new Rig();
        rig.Talk("talks");
        rig.Tick(3);
        Assert.True(rig.Diplomacy.Runtime.Holds("brass", "aurel", "border"));
        Assert.Equal(1, rig.Outcomes.Requested);
    }

    [Fact]
    public void ATriggerWithNoRuleIsCountedAndAnOutcomeWithBadIdsIsRefusedByTheDiplomacyNotByTheBridge()
    {
        var rig = new Rig([new DiplomacyOutcome("signal:offer-alliance", DiplomacyOutcomeKind.Propose, "{1}", "{1}", "alliance")]);
        rig.Talk("insult");
        rig.Tick(2);
        Assert.Equal(1, rig.Outcomes.Unmatched);
        Assert.Equal(0, rig.Outcomes.Requested);
        var bad = new Rig([new DiplomacyOutcome("signal:offer-alliance", DiplomacyOutcomeKind.Propose, "{1}", "{1}", "alliance")]);
        bad.Talk("offer");
        bad.Tick(2);
        Assert.Equal(DipCodes.SameRealm, Assert.Single(bad.Refused).Code);
    }

    [Fact]
    public void EveryKindOfOutcomeMakesItsRequest()
    {
        var rig = new Rig(
        [
            new DiplomacyOutcome("signal:offer-alliance", DiplomacyOutcomeKind.Declare, "{0}", "{1}", ""),
            new DiplomacyOutcome("signal:insult", DiplomacyOutcomeKind.Respond, "", "", "", Id: 7, Accept: false),
        ]);
        rig.Talk("offer");
        rig.Tick(2);
        Assert.Single(rig.Diplomacy.Runtime.Wars);
        rig.Talk("insult");
        rig.Tick(2);
        Assert.Equal(DipCodes.UnknownProposal, Assert.Single(rig.Refused).Code);
    }

    [Fact]
    public void RulesAreCheckedWhenTheGameIsAssembled()
    {
        Assert.Equal(DiplomacyDialogueOutcomes.BadRule, Samples.Code(() => new DiplomacyDialogueOutcomes("o", [new DiplomacyOutcome("nonsense", DiplomacyOutcomeKind.Deed, "a", "b", "gift")])));
        Assert.Equal(DiplomacyDialogueOutcomes.BadRule, Samples.Code(() => new DiplomacyDialogueOutcomes("o",
            [new DiplomacyOutcome("signal:x", DiplomacyOutcomeKind.Deed, "a", "b", "gift"), new DiplomacyOutcome("signal:x", DiplomacyOutcomeKind.Deed, "b", "a", "gift")])));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyDialogueOutcomes("o", null!));
    }

    // ---- the tutorial -----------------------------------------------------------------------------------------------------------------

    private const string TutorialText = """
    { "format": 1, "id": "dip.tutorial", "time": { "stepsPerDay": 100 }, "limits": { "maxActiveSteps": 1, "maxVisibleTips": 2, "tipGap": 0 },
      "facts": [],
      "events": [ { "id": "first_treaty" }, { "id": "first_peace" } ],
      "inputs": [],
      "skills": [],
      "journey": { "languages": [ "en" ], "defaultLanguage": "en", "pages": [ { "id": "p1", "key": "k.p1", "body": "k.p1b", "art": "k.p1a" } ], "entries": [ "host" ] },
      "steps": [
        { "id": "treaty", "key": "k.s1", "body": "k.s1b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [], "requiresAny": [],
          "complete": { "fact": "event.first_treaty.since_step", "op": "gte", "value": 1 }, "hints": [] },
        { "id": "peace", "key": "k.s2", "body": "k.s2b", "target": "", "motion": "none", "skippable": true, "skill": "", "requires": [ "treaty" ], "requiresAny": [],
          "complete": { "fact": "event.first_peace.since_step", "op": "gte", "value": 1 }, "hints": [] } ],
      "tips": [] }
    """;

    private sealed class TutorialRig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public DiplomacyComponent Diplomacy { get; }
        public TutorialComponent Tutorial { get; }
        public DiplomacyTutorialObserver Observer { get; }

        public TutorialRig(IEnumerable<DiplomacySeenRule>? rules = null)
        {
            var set = TutorialLoader.Load(TutorialText);
            var runtime = TutorialRuntime.Create(set);
            runtime.SelectLanguage("en");
            while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
            runtime.ChooseEntry("host");
            runtime.Start();
            var dip = Samples.Calm();
            var diplomacyChannels = new DiplomacyChannels();
            diplomacyChannels.Install(Session);
            var tutorialChannels = new TutorialChannels();
            tutorialChannels.Install(Session);
            Diplomacy = new DiplomacyComponent(DipRuntime.Create(dip), new TickDiplomacyClock(dip), () => Samples.Crisis(), diplomacyChannels);
            Tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => new Dictionary<string, FactValue>(), tutorialChannels);
            Observer = new DiplomacyTutorialObserver("seen", rules ?? [new DiplomacySeenRule("treaty-signed", "first_treaty"), new DiplomacySeenRule("peace-signed", "first_peace", "dune")], dip, set);
            Session.AddComponent(Diplomacy, 10);
            Session.AddComponent(Observer, 15);
            Session.AddComponent(Tutorial, 30);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a = "", string b = "", string c = "", long number = 0) => Session.Submit(new DiplomacyRequested("t" + kind + a + b + c, kind, a, b, c, number));

        public string Status(string step) => TutorialFacts.StatusName(Tutorial.Runtime.StepState(step).Status);
    }

    [Fact]
    public void AStepIsDoneWhenTheThingWasReallyDoneBetweenTheRealmsAndNotBefore()
    {
        var rig = new TutorialRig();
        rig.Tick(3);
        Assert.Equal("active", rig.Status("treaty"));
        Assert.Equal("pending", rig.Status("peace"));
        rig.Ask("propose", "aurel", "brass", "nap", 30);
        rig.Tick(5);
        Assert.Equal("done", rig.Status("treaty"));
        Assert.Equal("active", rig.Status("peace"));
        Assert.True(rig.Observer.Observed >= 1);
    }

    [Fact]
    public void ARuleForARealmOnlyCountsThatRealmsSignals()
    {
        var rig = new TutorialRig();
        rig.Tick(3);
        rig.Ask("propose", "aurel", "brass", "nap", 30);
        rig.Tick(5);
        rig.Ask("declare", "aurel", "brass", "");
        rig.Tick(2);
        rig.Ask("propose", "aurel", "brass", "peace", 60);
        rig.Tick(4);
        Assert.Equal("active", rig.Status("peace"));
        rig.Ask("declare", "ember", "dune", "");
        rig.Tick(2);
        rig.Ask("propose", "ember", "dune", "peace", 60);
        rig.Tick(4);
        Assert.Equal("done", rig.Status("peace"));
    }

    [Fact]
    public void ObservationsWaitForTheStepTheyCompleteAndTheRulesAreCheckedAgainstBothDocuments()
    {
        var set = TutorialLoader.Load(TutorialText);
        var dip = Samples.Isles;
        Assert.Equal(DiplomacyTutorialObserver.BadRule, Samples.Code(() => new DiplomacyTutorialObserver("o", [new DiplomacySeenRule("never-issued", "first_treaty")], dip, set)));
        Assert.Equal(DiplomacyTutorialObserver.BadRule, Samples.Code(() => new DiplomacyTutorialObserver("o", [new DiplomacySeenRule("treaty-signed", "first_nothing")], dip, set)));
        Assert.Equal(DiplomacyTutorialObserver.BadRule, Samples.Code(() => new DiplomacyTutorialObserver("o", [new DiplomacySeenRule("treaty-signed", "first_treaty", "nowhere")], dip, set)));
        var rig = new TutorialRig(rules: []);
        rig.Ask("propose", "aurel", "brass", "nap", 30);
        rig.Tick(6);
        Assert.Equal("active", rig.Status("treaty"));
        Assert.Equal(0, rig.Observer.Observed);
    }
}
