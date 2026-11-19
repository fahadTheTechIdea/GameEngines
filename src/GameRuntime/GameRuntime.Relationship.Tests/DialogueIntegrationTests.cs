using DialogueEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The dialogue engine and the relationships (R15): relationship facts are external facts for dialogue conditions, and what a conversation did is written back as deferred requests.</summary>
public sealed class DialogueIntegrationTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "npc";
    }

    private const string Talk = """
    { "format": 3, "id": "rel.talk", "variables": [],
      "externalFacts": [ { "id": "edge.state.friendship", "type": "string" }, { "id": "actor.mood_state", "type": "string" } ],
      "speakers": [ { "id": "npc", "portraits": [ "calm" ] } ],
      "quests": [ { "id": "errand", "title": "quest.errand.title", "start": "go", "stages": [ { "id": "go", "journal": "quest.errand.go", "advance": [] } ] } ],
      "conversations": [ { "id": "talk", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [
            { "when": { "fact": "ext.edge.state.friendship", "op": "eq", "value": "friend" }, "next": "friendly" },
            { "when": { "fact": "ext.actor.mood_state", "op": "eq", "value": "miserable" }, "next": "gloomy" } ], "otherwise": "cold" },
        { "id": "friendly", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.friendly", "next": "ask" },
        { "id": "gloomy", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.gloomy", "next": "ask" },
        { "id": "cold", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.cold", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "options": [
            { "id": "gift", "line": "talk.gift", "effects": [ { "signal": "gave-flowers", "args": [ { "text": "player" }, { "text": "npc" } ] } ], "next": "bye" },
            { "id": "help", "line": "talk.help", "effects": [ { "startQuest": "errand" }, { "finishQuest": "errand", "status": "completed" } ], "next": "bye" } ] },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public RelationshipComponent Relationships { get; }
        public RelationshipDialogueFacts Facts { get; }
        public RelationshipDialogueOutcomes Outcomes { get; }
        public List<string> Lines { get; } = new();
        public List<(long Tick, string Name)> Signals { get; } = new();
        public List<RelationshipRefused> Refused { get; } = new();

        public Rig(IEnumerable<DialogueOutcome>? rules = null, string document = Talk)
        {
            var relationshipChannels = new RelationshipChannels();
            relationshipChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            var host = Samples.Host();
            Relationships = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(50), () => host, relationshipChannels);
            Facts = new RelationshipDialogueFacts(Relationships, "npc", "player");
            var cast = new Cast();
            var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(document, cast), 1), cast, Facts.Facts, dialogueChannels);
            Outcomes = new RelationshipDialogueOutcomes("outcomes", rules ?? Rules);
            Session.AddComponent(Relationships, 30);
            Session.AddComponent(dialogue, 40);
            Session.AddComponent(Outcomes, 50);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Subscribe<DialogueSignalRaised>(e => Signals.Add((e.Tick, e.Name)));
            Session.Subscribe<RelationshipRefused>(Refused.Add);
            Session.Init();
            Session.Submit(new RelationshipActorAddRequested("add-npc", "npc", "kind"));
            Session.Submit(new RelationshipActorAddRequested("add-player", "player", ""));
            Tick();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        public long Tickno => Session.Clock.Tick;
    }

    private static readonly DialogueOutcome[] Rules =
    [
        new("signal:gave-flowers", DialogueOutcomeKind.Interact, "{0}", "{1}", "give-gift", "flowers"),
        new("quest:errand:completed", DialogueOutcomeKind.Grant, "npc", "", "festival-joy", "", 800),
        new("quest:errand:active", DialogueOutcomeKind.Remember, "npc", "player", "shared-feast"),
    ];

    private static string FirstLine(Action<Rig>? prepare = null)
    {
        var rig = new Rig();
        prepare?.Invoke(rig);
        rig.Session.Submit(new DialogueStartRequested("talk", "talk"));
        rig.Tick(2);
        return Assert.Single(rig.Lines);
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADialogueConditionReadsTheNpcsOpinionOfThePlayerAndItsMood()
    {
        Assert.Equal("talk.cold", FirstLine());
        Assert.Equal("talk.friendly", FirstLine(r => { r.Session.Submit(new RelationshipAxisRequested("friend", "npc", "player", "affinity", 400)); r.Tick(); }));
        Assert.Equal("talk.gloomy", FirstLine(r => { r.Session.Submit(new RelationshipNeedRequested("n1", "npc", "hunger", -800)); r.Session.Submit(new RelationshipNeedRequested("n2", "npc", "energy", -900)); r.Session.Submit(new RelationshipNeedRequested("n3", "npc", "social", -700)); r.Tick(); }));
        Assert.Equal("talk.friendly", FirstLine(r =>
        {
            r.Session.Submit(new RelationshipAxisRequested("friend", "npc", "player", "affinity", 400));
            r.Session.Submit(new RelationshipNeedRequested("n1", "npc", "hunger", -800));
            r.Session.Submit(new RelationshipNeedRequested("n2", "npc", "energy", -900));
            r.Tick();
        }));   // the first matching case wins: a friend greets as a friend even in a bad mood
    }

    [Fact]
    public void TheFactsAreThePairsAndCanBePointedAtAnotherPair()
    {
        var rig = new Rig();
        rig.Session.Submit(new RelationshipAxisRequested("a", "player", "npc", "affinity", 400));
        rig.Tick();
        var facts = rig.Facts.Facts();
        Assert.Equal("stranger", facts["ext.edge.state.friendship"].Text);
        Assert.Equal("friend", facts["ext.reverse.state.friendship"].Text);
        rig.Facts.Between("player", "npc");
        Assert.Equal("friend", rig.Facts.Facts()["ext.edge.state.friendship"].Text);
        Assert.Equal(("player", "npc"), (rig.Facts.Speaker, rig.Facts.Listener));
        Assert.All(rig.Facts.Facts().Keys, k => Assert.StartsWith("ext.", k, StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownSpeakerIsRefusedWhenTheFactsAreRead()
    {
        var rig = new Rig();
        rig.Facts.Between("nobody", "player");
        Assert.Equal(RelationshipCodes.UnknownActor, Fixture.Code(() => rig.Facts.Facts()));
    }

    // ---- the documents fit each other -----------------------------------------------------------------------------------------------

    [Fact]
    public void ADialogueDocumentThatReadsWhatTheRelationshipsOfferIsAcceptedAndOneThatDoesNotIsRefused()
    {
        var cast = new Cast();
        RelationshipDialogueFacts.Check(DialogueLoader.Load(Talk, cast), Samples.Village);
        void Mismatch(string declaration, string fragment)
        {
            var set = DialogueLoader.Load(Talk.Replace("\"externalFacts\": [", "\"externalFacts\": [ " + declaration + ",", StringComparison.Ordinal), cast);
            var refusal = Assert.Throws<EngineRefusedException>(() => RelationshipDialogueFacts.Check(set, Samples.Village));
            Assert.Equal(RelationshipDialogueFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Mismatch("{ \"id\": \"actor.mood_states\", \"type\": \"string\" }", "'actor.mood_states'");
        Mismatch("{ \"id\": \"target.mood\", \"type\": \"bool\" }", "Boolean in the dialogue document");
        var ranged = Talk.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"actor.mood\", \"type\": \"int\", \"min\": -10, \"max\": 10 },", StringComparison.Ordinal);
        var refused = Assert.Throws<EngineRefusedException>(() => RelationshipDialogueFacts.Check(DialogueLoader.Load(ranged, cast), Samples.Village));
        Assert.Contains("outside the -10 to 10", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclarationWhoseRangeIsExactlyTheRelationshipsRangeIsAccepted()
    {
        var exact = Talk.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"actor.mood\", \"type\": \"int\", \"min\": -1000, \"max\": 1000 },", StringComparison.Ordinal);
        RelationshipDialogueFacts.Check(DialogueLoader.Load(exact, new Cast()), Samples.Village);
        var wider = Talk.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"actor.mood\", \"type\": \"int\", \"min\": -2000, \"max\": 2000 },", StringComparison.Ordinal);
        RelationshipDialogueFacts.Check(DialogueLoader.Load(wider, new Cast()), Samples.Village);
    }

    [Fact]
    public void ADialogueFactThatIsNotInTheRelationshipNamespaceIsNotCheckedHere()
    {
        var cast = new Cast();
        var other = Talk.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"squads\", \"type\": \"int\", \"min\": 0, \"max\": 9 },", StringComparison.Ordinal);
        RelationshipDialogueFacts.Check(DialogueLoader.Load(other, cast), Samples.Village);
    }

    // ---- outcomes, written back as deferred requests ----------------------------------------------------------------------------------

    [Fact]
    public void ASignalOfAConversationBecomesAGiftTheRelationshipsAnswerOneTickLater()
    {
        var rig = new Rig();
        rig.Session.Submit(new DialogueStartRequested("talk", "talk"));
        rig.Tick();
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick();
        rig.Session.Submit(new DialogueChooseRequested("c", "gift"));
        rig.Tick();
        var signal = Assert.Single(rig.Signals);
        Assert.Equal("gave-flowers", signal.Name);
        var runtime = rig.Relationships.Runtime;
        Assert.Equal(1, rig.Outcomes.Requested);
        Assert.Equal(0, runtime.Opinion("npc", "player", 0));         // the signal came in this tick, after the relationship component had run: nothing has happened yet
        rig.Tick();
        // The npc has no taste for flowers (neutral, 250 permille): the memory gives 120 x 0.25 = 30, the axis effect 80 x 0.25 = 20 and the npc is kind, which scales a gain by 1.2: 24.
        Assert.Equal(54, runtime.Opinion("npc", "player", 0));
        Assert.Equal(1, runtime.MemoryCount("npc", "player", runtime.Set.MemoryById["was-gifted"].Index));
        Assert.Empty(rig.Refused);
        Assert.Equal(0, rig.Outcomes.Unmatched);
    }

    [Fact]
    public void AQuestThatEndsAndOneThatStartsAreRulesToo()
    {
        var rig = new Rig();
        rig.Session.Submit(new DialogueStartRequested("talk", "talk"));
        rig.Tick();
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick();
        rig.Session.Submit(new DialogueChooseRequested("c", "help"));
        rig.Tick(3);
        var runtime = rig.Relationships.Runtime;
        Assert.Equal(["festival-joy"], runtime.Actor("npc").Moodlets.Select(m => runtime.Set.Moodlets[m.Moodlet].Id));
        Assert.Equal(1, runtime.MemoryCount("npc", "player", runtime.Set.MemoryById["shared-feast"].Index));
        Assert.Equal(2, rig.Outcomes.Requested);
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void ATriggerWithNoRuleIsCountedAndAnOutcomeWithBadIdsIsRefusedByTheRelationshipsNotByTheBridge()
    {
        var rig = new Rig([new DialogueOutcome("signal:gave-flowers", DialogueOutcomeKind.Interact, "{1}", "{1}", "give-gift", "flowers")]);
        rig.Session.Submit(new DialogueStartRequested("talk", "talk"));
        rig.Tick();
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick();
        rig.Session.Submit(new DialogueChooseRequested("c", "help"));      // the quest events have no rule here
        rig.Tick(2);
        Assert.Equal(2, rig.Outcomes.Unmatched);
        Assert.Equal(0, rig.Outcomes.Requested);
    }

    [Fact]
    public void AnOutcomeThatTheRelationshipsRefuseIsAnEventOnTheirChannelAndNothingChanges()
    {
        var rig = new Rig([new DialogueOutcome("signal:gave-flowers", DialogueOutcomeKind.Interact, "{1}", "{1}", "give-gift", "flowers")]);
        var digest = rig.Relationships.Runtime.StateDigest();
        rig.Session.Submit(new DialogueStartRequested("talk", "talk"));
        rig.Tick();
        rig.Session.Submit(new DialogueAdvanceRequested("a"));
        rig.Tick();
        rig.Session.Submit(new DialogueChooseRequested("c", "gift"));
        rig.Tick(2);
        Assert.Equal(RelationshipCodes.Self, Assert.Single(rig.Refused).Code);          // the rule made the npc give the gift to itself
        Assert.Equal(digest, rig.Relationships.Runtime.StateDigest());
    }

    [Fact]
    public void RulesAreCheckedWhenTheGameIsAssembled()
    {
        Assert.Equal(RelationshipDialogueOutcomes.BadRule, Fixture.Code(() => new RelationshipDialogueOutcomes("o", [new DialogueOutcome("nonsense", DialogueOutcomeKind.Grant, "a", "", "festival-joy")])));
        Assert.Equal(RelationshipDialogueOutcomes.BadRule, Fixture.Code(() => new RelationshipDialogueOutcomes("o",
            [new DialogueOutcome("signal:x", DialogueOutcomeKind.Grant, "a", "", "festival-joy"), new DialogueOutcome("signal:x", DialogueOutcomeKind.Grant, "b", "", "festival-joy")])));
        Assert.Throws<ArgumentNullException>(() => new RelationshipDialogueOutcomes("o", null!));
    }

}
