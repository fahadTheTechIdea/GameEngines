using AIEngine;
using EngineCommon;
using RelationshipEngine;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>
/// The AI engine and what villagers think of each other (item 5 of the algorithm review): an agent is told, as percepts, its own opinions of the people it can see and what a village knows of them (standing, community);
/// its behaviour tree chooses, through the AI engine's own utility node, to befriend the most respected neighbour or to avoid someone from a circle it is at odds with. The agent knows only what it was told.
/// </summary>
public sealed class AiOpinionTests
{
    private const string Villager = """
    { "format": 1, "id": "rel.opinions",
      "keys": [ { "id": "ally", "type": "string", "initial": "", "source": "agent" }, { "id": "foe", "type": "string", "initial": "", "source": "agent" },
                { "id": "ally_standing", "type": "int", "initial": 0, "min": 0, "max": 3000, "source": "agent" },
                { "id": "foe_hostility", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" },
                { "id": "foe_same", "type": "bool", "initial": false, "source": "agent" } ],
      "percepts": [ { "kind": "other", "capacity": 8, "memory": 100000, "owned": false, "attrs": [
          { "id": "opinion_affinity", "type": "int", "min": -1000, "max": 1000, "unknown": 0 }, { "id": "opinion_trust", "type": "int", "min": -1000, "max": 1000, "unknown": 0 },
          { "id": "opinion_attraction", "type": "int", "min": 0, "max": 1000, "unknown": 0 },
          { "id": "state_friendship", "type": "string", "unknown": "" }, { "id": "state_reliability", "type": "string", "unknown": "" },
          { "id": "known", "type": "bool", "unknown": false },
          { "id": "standing", "type": "int", "min": 0, "max": 100000000, "unknown": 0 },
          { "id": "same_community", "type": "bool", "unknown": false },
          { "id": "community_size", "type": "int", "min": 1, "max": 2000, "unknown": 1 } ] } ],
      "intents": [ { "id": "interact", "args": [ { "id": "what", "type": "string" }, { "id": "who", "type": "string" } ] } ],
      "behaviors": [ { "id": "villager", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "pick-ally", "kind": "succeed", "child": { "id": "pick-ally-select", "kind": "select", "from": "other", "into": "ally",
              "where": { "fact": "item.opinion_affinity", "op": "gte", "value": -200 }, "pick": { "max": "standing" } } },
          { "id": "pick-foe", "kind": "succeed", "child": { "id": "pick-foe-select", "kind": "select", "from": "other", "into": "foe",
              "where": { "fact": "item.known", "op": "eq", "value": true }, "pick": { "min": "opinion_affinity" } } },
          { "id": "ally-standing", "kind": "let", "key": "ally_standing", "value": { "op": "min", "args": [ 3000, { "known": { "from": "other", "id": { "key": "ally" }, "attr": "standing" } } ] } },
          { "id": "foe-hostility", "kind": "let", "key": "foe_hostility", "value": { "op": "max", "args": [ 0, { "op": "sub", "args": [ 0, { "known": { "from": "other", "id": { "key": "foe" }, "attr": "opinion_affinity" } } ] } ] } },
          { "id": "foe-same", "kind": "let", "key": "foe_same", "value": { "known": { "from": "other", "id": { "key": "foe" }, "attr": "same_community" } } },
          { "id": "choose", "kind": "utility", "momentum": 0, "options": [
            { "id": "befriend", "weight": 1000, "considerations": [ { "input": { "key": "ally_standing" }, "min": 0, "max": 3000, "curve": { "kind": "linear" } } ],
              "child": { "id": "do-befriend", "kind": "act", "intent": "interact", "args": { "what": "befriend", "who": { "key": "ally" } } } },
            { "id": "avoid", "weight": 1000, "when": { "fact": "key.foe_same", "op": "eq", "value": false },
              "considerations": [ { "input": { "key": "foe_hostility" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ],
              "child": { "id": "do-avoid", "kind": "act", "intent": "interact", "args": { "what": "avoid", "who": { "key": "foe" } } } } ] } ] } } ] }
    """;

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public RelationshipComponent Relationships { get; }
        public RelationshipAiOpinions Opinions { get; }
        public List<(string Agent, string Other)> Perceived { get; } = new();
        public List<(long Tick, string Args)> Intents { get; } = new();
        public List<string> AiRefused { get; } = new();
        public List<AiPerceptPosted> Posts { get; } = new();
        private int _requests;

        public Rig(string aiDocument = Villager, string kind = "other", bool fans = true, bool miraInHalsCircle = false)
        {
            var set = AiLoader.Load(aiDocument);
            AiEventCatalog.Declare(Session);
            var channels = new RelationshipChannels();
            channels.Install(Session);
            var host = Samples.Host();
            Relationships = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(1), () => host, channels);
            Opinions = new RelationshipAiOpinions("rel-opinions", Relationships, "ai", set, kind, () => Perceived);
            Session.AddComponent(Relationships, 10);
            Session.AddComponent(Opinions, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add((e.Tick, e.Args)));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Subscribe<AiPerceptPosted>(e => Posts.Add(e));
            Session.Init();
            foreach (var name in new[] { "mira", "nia", "omar", "hal", "hex", "hid" }) Add(name);
            if (fans) foreach (var fan in new[] { "f1", "f2", "f3", "f4" }) { Add(fan); Opinion(fan, "nia", "trust", 800); Opinion(fan, "nia", "affinity", 300); }
            Opinion("mira", "nia", "affinity", 300);
            Opinion("mira", "omar", "affinity", 300);
            Opinion("mira", "hal", "affinity", -600);
            foreach (var a in new[] { "hal", "hex", "hid" }) foreach (var b in new[] { "hal", "hex", "hid" }.Where(b => b != a)) Opinion(a, b, "affinity", 500);
            if (miraInHalsCircle) foreach (var other in new[] { "hex", "hid" }) { Opinion("mira", other, "affinity", 500); Opinion(other, "mira", "affinity", 500); }
            Session.Submit(new AiAgentAddRequested("ai", "agent", "mira", "villager"));
        }

        private void Add(string actor) => Session.Submit(new RelationshipActorAddRequested("add-" + actor, actor, ""));

        public void Opinion(string holder, string other, string axis, long delta) => Session.Submit(new RelationshipAxisRequested("op" + _requests++, holder, other, axis, delta));

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        /// <summary>Lives past the first day boundary (step 24) so communities and standing exist, then lets the agent think.</summary>
        public void Settle(int ticks = 40) => Tick(ticks);

        public string Latest => Intents.Count == 0 ? "" : Intents[^1].Args;
    }

    [Fact]
    public void AnAgentIsToldItsOwnOpinionsAndWhatAVillageKnowsAndNothingAboutWhatTheOthersThinkOfIt()
    {
        var rig = new Rig();
        rig.Perceived.Add(("mira", "nia"));
        rig.Settle();
        var told = rig.Posts.Last(p => p.Id == "nia");
        Assert.Equal(("mira", "other", ""), (told.Agent, told.Kind, told.Owner));
        var attrs = told.Attrs.Split(';').Select(a => a.Split('=')[0]).ToArray();
        Assert.Equal(["opinion_affinity", "opinion_trust", "opinion_attraction", "state_friendship", "state_reliability", "known", "standing", "same_community", "community_size"], attrs);
        Assert.Contains("opinion_affinity=" + rig.Relationships.Runtime.Opinion("mira", "nia", 0), told.Attrs);   // 300, less the 4 a day it has drifted by the first boundary
        Assert.Contains("state_friendship=acquaintance", told.Attrs);
        Assert.Contains("known=true", told.Attrs);
        Assert.Contains("same_community=true", told.Attrs);               // mira likes nia and so is of her circle, and nia's circle is the fans'
        var nia = rig.Relationships.Runtime.Actor("nia");
        Assert.Contains("standing=" + nia.Standing, told.Attrs);
        Assert.True(nia.Standing > 2000, "four villagers who trust only her give nia a standing of " + nia.Standing);
        // The truth about nia's own feelings for mira is not in the percept: no attribute is a reverse opinion.
        Assert.DoesNotContain(attrs, a => a.StartsWith("reverse", StringComparison.Ordinal) || a.StartsWith("their", StringComparison.Ordinal));
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void AnAgentThatIsNotToldAnythingChoosesNothing()
    {
        var rig = new Rig();
        rig.Settle();
        Assert.Equal(0, rig.Opinions.Posted);
        Assert.Empty(rig.Intents);
    }

    [Fact]
    public void AgentBefriendsTheNeighbourWithTheHighestStandingNotTheOneItLikesMore()
    {
        var rig = new Rig();
        rig.Opinion("mira", "omar", "affinity", 200);          // mira likes omar more (500) than nia (300), but omar is nobody
        rig.Perceived.AddRange([("mira", "nia"), ("mira", "omar")]);
        rig.Settle();
        Assert.Equal("what=befriend;who=nia", rig.Latest);
        var runtime = rig.Relationships.Runtime;
        Assert.True(runtime.Actor("nia").Standing > runtime.Actor("omar").Standing * 3, "nia " + runtime.Actor("nia").Standing + ", omar " + runtime.Actor("omar").Standing);
        Assert.True(runtime.Opinion("mira", "omar", 0) > runtime.Opinion("mira", "nia", 0));
    }

    [Fact]
    public void AgentAvoidsAHostileCircleWhenThereIsNobodyWorthBefriending()
    {
        var rig = new Rig();
        rig.Perceived.AddRange([("mira", "omar"), ("mira", "hal")]);
        rig.Settle();
        Assert.Equal("what=avoid;who=hal", rig.Latest);
        Assert.Equal("hal", rig.Relationships.Runtime.Actor("hex").Community);
        Assert.Equal("hal", rig.Relationships.Runtime.Actor("hal").Community);
    }

    [Fact]
    public void WithAWorthyNeighbourInSightTheAgentPrefersBefriendingToAvoiding()
    {
        var rig = new Rig();
        rig.Perceived.AddRange([("mira", "nia"), ("mira", "omar"), ("mira", "hal")]);
        rig.Settle();
        Assert.Equal("what=befriend;who=nia", rig.Latest);
    }

    [Fact]
    public void WhenTheOpinionChangesTheAgentIsToldAgainAndChoosesDifferently()
    {
        var rig = new Rig();
        rig.Perceived.AddRange([("mira", "nia"), ("mira", "omar"), ("mira", "hal")]);
        rig.Settle();
        Assert.Equal("what=befriend;who=nia", rig.Latest);
        var posted = rig.Opinions.Posted;
        rig.Tick(5);
        Assert.Equal(posted, rig.Opinions.Posted);                                  // nothing changed, nothing to tell
        rig.Opinion("mira", "nia", "affinity", -1100);                              // mira falls out with nia (296 - 1100 = -804): now the most disliked one she knows
        rig.Tick(30);                                                                // past the next day boundary, where the communities are worked out again
        Assert.True(rig.Opinions.Posted > posted);
        Assert.Equal("what=avoid;who=nia", rig.Latest);                             // nia no longer qualifies as an ally and is not of mira's circle: she avoids the one she now dislikes most
    }

    [Fact]
    public void AnAgentInTheSameCommunityAsTheOneItDislikesDoesNotAvoidThem()
    {
        var rig = new Rig(miraInHalsCircle: true);
        rig.Perceived.AddRange([("mira", "omar"), ("mira", "hal")]);
        rig.Settle();
        var runtime = rig.Relationships.Runtime;
        Assert.Equal(runtime.Actor("hal").Community, runtime.Actor("mira").Community);   // mira is one of hal's circle although she dislikes hal
        Assert.Contains("same_community=true", rig.Posts.Last(p => p.Id == "hal").Attrs);
        Assert.Equal("what=avoid;who=hal", rig.Intents[0].Args);                       // before the first boundary everyone was a stranger to mira's circle and she avoided hal
        Assert.NotEqual("what=avoid;who=hal", rig.Latest);                              // told that hal is of her own circle, she does not
    }

    [Fact]
    public void AnAgentActsOnWhatItLastKnewWhenItIsNoLongerToldAndTheVillageMovesOn()
    {
        var rig = new Rig();
        rig.Perceived.AddRange([("mira", "nia"), ("mira", "omar")]);
        rig.Settle();
        Assert.Equal("what=befriend;who=nia", rig.Latest);
        rig.Perceived.Clear();                                                        // mira turns away
        foreach (var fan in new[] { "f1", "f2", "f3", "f4" }) rig.Opinion(fan, "nia", "trust", -1500);   // nia loses her admirers
        rig.Settle(60);
        Assert.True(rig.Relationships.Runtime.Actor("nia").Standing < 1500);
        Assert.Equal("what=befriend;who=nia", rig.Latest);                            // she still believes nia is respected
        rig.Opinions.Notify();
        rig.Perceived.AddRange([("mira", "nia"), ("mira", "omar")]);
        rig.Tick(5);
        Assert.Contains("standing=" + rig.Relationships.Runtime.Actor("nia").Standing, rig.Posts.Last(p => p.Id == "nia").Attrs);
    }

    [Fact]
    public void ANameThatIsNotAnActorOrTheAgentItselfIsNeverToldAbout()
    {
        var rig = new Rig();
        rig.Perceived.AddRange([("mira", "nobody"), ("nobody", "nia"), ("mira", "mira")]);
        rig.Settle();
        Assert.Equal(0, rig.Opinions.Posted);
        Assert.Empty(rig.AiRefused);
    }

    // ---- the documents fit each other ----------------------------------------------------------------------------------------------------

    [Fact]
    public void AnAiDocumentThatDoesNotDeclareTheExactPerceptIsRefusedWhenTheGameIsAssembled()
    {
        void Bad(string from, string to, string fragment)
        {
            var document = Villager.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(Villager, document);
            var refusal = Assert.Throws<EngineRefusedException>(() => new Rig(document));
            Assert.True(RelationshipAiBridge.BadKind == refusal.Code, refusal.Code + ": " + refusal.Message);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad("\"owned\": false,", "\"owned\": true, \"ownerCapacity\": 1,", "is owned");
        Bad("{ \"id\": \"standing\", \"type\": \"int\", \"min\": 0, \"max\": 100000000, \"unknown\": 0 }", "{ \"id\": \"standing\", \"type\": \"int\", \"min\": 0, \"max\": 5000, \"unknown\": 0 }", "must allow 0 to 100000000");
        Bad("{ \"id\": \"state_reliability\", \"type\": \"string\", \"unknown\": \"\" }", "{ \"id\": \"state_reliability\", \"type\": \"bool\", \"unknown\": false }", "'state_reliability' as String");
        Bad("{ \"id\": \"opinion_trust\", \"type\": \"int\", \"min\": -1000, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"opinion_trust\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "must allow -1000 to 1000");
        Bad("{ \"id\": \"community_size\", \"type\": \"int\", \"min\": 1, \"max\": 2000, \"unknown\": 1 }", "{ \"id\": \"community_size\", \"type\": \"int\", \"min\": 1, \"max\": 2000, \"unknown\": 1 }, { \"id\": \"their_view\", \"type\": \"int\", \"min\": 0, \"max\": 1, \"unknown\": 0 }", "must declare exactly");
        Assert.Equal(RelationshipAiBridge.BadKind, Assert.Throws<EngineRefusedException>(() => new Rig(Villager, "nothing")).Code);
    }

    [Fact]
    public void TheAttributeNamesAreTheAxisAndLadderIdsWithDotsAndHyphensAsUnderscores()
    {
        Assert.Equal("opinion_affinity", RelationshipAiOpinions.OpinionAttribute(Samples.Village.AxisById["affinity"]));
        Assert.Equal("state_friendship", RelationshipAiOpinions.StateAttribute(Samples.Village.LadderById["friendship"]));
        Assert.Equal("opinion_deep_trust_x", RelationshipAiOpinions.OpinionAttribute(new AxisDef(0, "deep-trust.x", "k", 0, 1, 0, 0)));
        Assert.Equal("state_a_b", RelationshipAiOpinions.StateAttribute(new LadderDef(0, "a-b", 0, 0, [])));
    }
}
