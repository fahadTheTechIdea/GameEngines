using DialogueEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The dialogue engine reads the derived social state like any other relationship fact: who the listener is in the village (standing) and whether the two share a circle (community), through the same external-fact conventions.</summary>
public sealed class SocialDialogueTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "npc";
    }

    private const string Talk = """
    { "format": 3, "id": "rel.social.talk", "variables": [],
      "externalFacts": [ { "id": "edge.same_community", "type": "bool" }, { "id": "target.standing", "type": "int", "min": 0, "max": 100000000 },
                         { "id": "actor.community_size", "type": "int", "min": 1, "max": 2000 }, { "id": "target.community", "type": "string" } ],
      "speakers": [ { "id": "npc", "portraits": [ "calm" ] } ],
      "quests": [],
      "conversations": [ { "id": "talk", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [
            { "when": { "all": [ { "fact": "ext.edge.same_community", "op": "eq", "value": true }, { "fact": "ext.actor.community_size", "op": "gte", "value": 3 } ] }, "next": "kin" },
            { "when": { "fact": "ext.target.standing", "op": "gt", "value": 2000 }, "next": "respect" } ], "otherwise": "stranger" },
        { "id": "kin", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.kin", "next": "bye" },
        { "id": "respect", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.respect", "next": "bye" },
        { "id": "stranger", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "talk.stranger", "next": "bye" },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public RelationshipComponent Relationships { get; }
        public RelationshipDialogueFacts Facts { get; }
        public List<string> Lines { get; } = new();
        private int _requests;

        public Rig()
        {
            var relationshipChannels = new RelationshipChannels();
            relationshipChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            var host = Samples.Host();
            Relationships = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(1), () => host, relationshipChannels);
            Facts = new RelationshipDialogueFacts(Relationships, "npc", "player");
            var cast = new Cast();
            RelationshipDialogueFacts.Check(DialogueLoader.Load(Talk, cast), Samples.Village);
            var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Talk, cast), 1), cast, Facts.Facts, dialogueChannels);
            Session.AddComponent(Relationships, 30);
            Session.AddComponent(dialogue, 40);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Init();
            foreach (var actor in new[] { "npc", "player", "ally", "fan1", "fan2", "fan3" }) Session.Submit(new RelationshipActorAddRequested("add-" + actor, actor, ""));
        }

        public void Opinion(string holder, string other, string axis, long delta) => Session.Submit(new RelationshipAxisRequested("op" + _requests++, holder, other, axis, delta));

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        public string Say()
        {
            Lines.Clear();
            Session.Submit(new DialogueStartRequested("talk" + _requests++, "talk"));
            Tick(2);
            return Assert.Single(Lines);
        }
    }

    [Fact]
    public void AStrangerIsGreetedAsOneUntilTheVillageHasHadADayToWorkOutWhoIsWho()
    {
        var rig = new Rig();
        foreach (var fan in new[] { "fan1", "fan2", "fan3" }) { rig.Opinion(fan, "player", "trust", 800); rig.Opinion(fan, "player", "affinity", 300); }
        rig.Tick(5);
        Assert.Equal("talk.stranger", rig.Say());                               // before the first boundary standing is the average for all
    }

    [Fact]
    public void ARespectedListenerIsGreetedWithRespect()
    {
        var rig = new Rig();
        foreach (var fan in new[] { "fan1", "fan2", "fan3" }) { rig.Opinion(fan, "player", "trust", 800); rig.Opinion(fan, "player", "affinity", 300); }
        rig.Tick(30);                                                            // past the first boundary: the player stands high (three villagers trust only the player)
        var player = rig.Relationships.Runtime.Actor("player");
        Assert.True(player.Standing > 2000, "standing " + player.Standing);
        Assert.Equal("talk.respect", rig.Say());
    }

    [Fact]
    public void TwoOfTheSameCircleAreKin()
    {
        var rig = new Rig();
        // The npc, the player and two fans like one another: a circle of four that the first boundary finds.
        foreach (var holder in new[] { "npc", "player", "fan1", "fan2" })
            foreach (var other in new[] { "npc", "player", "fan1", "fan2" }.Where(o => o != holder)) rig.Opinion(holder, other, "affinity", 400);
        rig.Tick(30);
        var facts = RelationshipFacts.Read(rig.Relationships.Runtime, "npc", "player");
        Assert.True(facts["edge.same_community"].Boolean);
        Assert.Equal(4, facts["actor.community_size"].Integer);
        Assert.Equal("talk.kin", rig.Say());
    }

    [Fact]
    public void TheDialogueDocumentMustDeclareTheSocialFactsWithRangesThatCoverTheRelationshipDocuments()
    {
        var cast = new Cast();
        var narrow = Talk.Replace("\"max\": 100000000", "\"max\": 5000", StringComparison.Ordinal);
        var refusal = Assert.Throws<EngineRefusedException>(() => RelationshipDialogueFacts.Check(DialogueLoader.Load(narrow, cast), Samples.Village));
        Assert.Equal(RelationshipDialogueFacts.Mismatch, refusal.Code);
        Assert.Contains("target.standing", refusal.Message, StringComparison.Ordinal);
        var wrong = Talk.Replace("{ \"id\": \"target.community\", \"type\": \"string\" }", "{ \"id\": \"target.community\", \"type\": \"bool\" }", StringComparison.Ordinal);
        Assert.Equal(RelationshipDialogueFacts.Mismatch, Assert.Throws<EngineRefusedException>(() => RelationshipDialogueFacts.Check(DialogueLoader.Load(wrong, cast), Samples.Village)).Code);
    }
}
