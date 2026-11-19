using AIEngine;
using EngineCommon;
using RelationshipEngine;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The AI engine and the relationships (R15): an agent chooses what to do by the urgency of its needs, but only knows them when it is told, as a percept (knowledge, not truth).</summary>
public sealed class AiIntegrationTests
{
    private const string Villager = """
    { "format": 1, "id": "rel.ai",
      "keys": [ { "id": "u_hunger", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" }, { "id": "u_energy", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" },
                { "id": "u_social", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" }, { "id": "u_fun", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" } ],
      "percepts": [ { "kind": "self", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "mood", "type": "int", "min": -1000, "max": 1000, "unknown": 0 }, { "id": "mood_state", "type": "string", "unknown": "" },
          { "id": "urgency_hunger", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "urgency_energy", "type": "int", "min": 0, "max": 1000, "unknown": 0 },
          { "id": "urgency_social", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "urgency_fun", "type": "int", "min": 0, "max": 1000, "unknown": 0 } ] } ],
      "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
      "behaviors": [ { "id": "villager", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "see-hunger", "kind": "let", "key": "u_hunger", "value": { "known": { "from": "self", "id": "me", "attr": "urgency_hunger" } } },
          { "id": "see-energy", "kind": "let", "key": "u_energy", "value": { "known": { "from": "self", "id": "me", "attr": "urgency_energy" } } },
          { "id": "see-social", "kind": "let", "key": "u_social", "value": { "known": { "from": "self", "id": "me", "attr": "urgency_social" } } },
          { "id": "see-fun", "kind": "let", "key": "u_fun", "value": { "known": { "from": "self", "id": "me", "attr": "urgency_fun" } } },
          { "id": "choose", "kind": "utility", "momentum": 0, "options": [
            { "id": "eat", "weight": 1000, "considerations": [ { "input": { "key": "u_hunger" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-eat", "kind": "act", "intent": "do", "args": { "what": "eat" } } },
            { "id": "sleep", "weight": 1000, "considerations": [ { "input": { "key": "u_energy" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-sleep", "kind": "act", "intent": "do", "args": { "what": "sleep" } } },
            { "id": "talk", "weight": 1000, "considerations": [ { "input": { "key": "u_social" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-talk", "kind": "act", "intent": "do", "args": { "what": "talk" } } },
            { "id": "play", "weight": 1000, "considerations": [ { "input": { "key": "u_fun" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-play", "kind": "act", "intent": "do", "args": { "what": "play" } } } ] } ] } } ] }
    """;

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public RelationshipComponent Relationships { get; }
        public RelationshipAiBridge Bridge { get; }
        public List<string> Perceivers { get; } = new();
        public List<(long Tick, string Args)> Intents { get; } = new();
        public List<string> AiRefused { get; } = new();

        public Rig(string aiDocument = Villager, string kind = "self")
        {
            var set = AiLoader.Load(aiDocument);
            AiEventCatalog.Declare(Session);
            var channels = new RelationshipChannels();
            channels.Install(Session);
            var host = Samples.Host();
            Relationships = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), new TickRelationshipClock(1_000_000), () => host, channels);
            Bridge = new RelationshipAiBridge("rel-ai", Relationships, "ai", set, kind, "me", () => Perceivers);
            Session.AddComponent(Relationships, 10);
            Session.AddComponent(Bridge, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add((e.Tick, e.Args)));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Init();
            Session.Submit(new RelationshipActorAddRequested("add", "mira", ""));
            Session.Submit(new AiAgentAddRequested("ai", "agent", "mira", "villager"));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        public string Latest => Intents.Count == 0 ? "" : Intents[^1].Args;
    }

    [Fact]
    public void AnAgentThatIsNotToldAnythingKnowsNothingAndOneThatIsToldChoosesByTheMostUrgentNeed()
    {
        var rig = new Rig();
        rig.Tick(3);
        rig.Session.Submit(new RelationshipNeedRequested("tired", "mira", "energy", -800));          // the truth: energy 100, urgency 800
        rig.Tick(3);
        Assert.Equal(0, rig.Bridge.Posted);
        Assert.DoesNotContain(rig.Intents, i => i.Args == "what=sleep");                                // it does not know, so it cannot act on it
        rig.Perceivers.Add("mira");
        rig.Tick(4);
        Assert.Equal(1, rig.Bridge.Posted);
        Assert.Equal("what=sleep", rig.Latest);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void WhatTheAgentIsToldIsTheUrgencyFromTheDocumentCurveNotTheLevel()
    {
        var rig = new Rig();
        var posts = new List<string>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add(e.Attrs));
        rig.Perceivers.Add("mira");
        rig.Tick(3);
        // Levels 800 / 900 / 700 / 600 give urgencies 100 / 30 / 215 / 200 on the sample curves; the mood is 590, happy.
        Assert.Equal("mood=590;mood_state=happy;urgency_hunger=100;urgency_energy=30;urgency_social=215;urgency_fun=200", posts[0]);
        Assert.Equal("what=talk", rig.Latest);
    }

    [Fact]
    public void WhenTheNeedsChangeTheAgentIsToldAgainAndChoosesAgainAndNotBefore()
    {
        var rig = new Rig();
        rig.Perceivers.Add("mira");
        rig.Tick(3);
        var told = rig.Bridge.Posted;
        rig.Tick(5);
        Assert.Equal(told, rig.Bridge.Posted);                                                       // nothing changed, nothing to tell
        rig.Session.Submit(new RelationshipNeedRequested("hungry", "mira", "hunger", -780));         // hunger 20: urgency about 980
        rig.Tick(4);
        Assert.Equal(told + 1, rig.Bridge.Posted);
        Assert.Equal("what=eat", rig.Latest);
        rig.Session.Submit(new RelationshipNeedRequested("fed", "mira", "hunger", 900));
        rig.Tick(4);
        Assert.Equal("what=talk", rig.Latest);
    }

    [Fact]
    public void AnAgentActsOnWhatItLastKnewWhenItIsNoLongerToldAndTheTruthMovesOn()
    {
        var rig = new Rig();
        rig.Perceivers.Add("mira");
        rig.Session.Submit(new RelationshipNeedRequested("hungry", "mira", "hunger", -790));
        rig.Tick(5);
        Assert.Equal("what=eat", rig.Latest);
        rig.Perceivers.Clear();                                                                      // mira stops perceiving herself (asleep, say)
        rig.Session.Submit(new RelationshipNeedRequested("fed", "mira", "hunger", 990));
        rig.Session.Submit(new RelationshipNeedRequested("tired", "mira", "energy", -890));
        rig.Tick(5);
        Assert.Equal("what=eat", rig.Latest);                                                        // it still believes it is hungry
        var posted = rig.Bridge.Posted;
        rig.Bridge.Notify();
        rig.Perceivers.Add("mira");
        rig.Tick(4);
        Assert.Equal(posted + 1, rig.Bridge.Posted);
        Assert.Equal("what=sleep", rig.Latest);
    }

    [Fact]
    public void ANameThatIsNotAnActorIsNeverToldAndTheBridgeDoesNotFail()
    {
        var rig = new Rig();
        rig.Perceivers.Add("nobody");
        rig.Tick(3);
        Assert.Equal(0, rig.Bridge.Posted);
        Assert.Empty(rig.AiRefused);
    }

    // ---- the documents fit each other -----------------------------------------------------------------------------------------------

    [Fact]
    public void AnAiDocumentThatDoesNotDeclareTheExactPerceptIsRefusedWhenTheGameIsAssembled()
    {
        void Bad(string from, string to, string fragment)
        {
            var document = Villager.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(Villager, document);
            var refusal = Assert.Throws<EngineRefusedException>(() => new Rig(document));
            Assert.Equal(RelationshipAiBridge.BadKind, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad("\"owned\": false,", "\"owned\": true, \"ownerCapacity\": 1,", "is owned");
        Bad("{ \"id\": \"mood_state\", \"type\": \"string\", \"unknown\": \"\" },", "{ \"id\": \"mood_state\", \"type\": \"bool\", \"unknown\": false },", "'mood_state' as String");
        Bad("{ \"id\": \"urgency_fun\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"urgency_fun\", \"type\": \"int\", \"min\": 0, \"max\": 500, \"unknown\": 0 }", "must allow 0 to 1000");
        Bad("{ \"id\": \"mood\", \"type\": \"int\", \"min\": -1000, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"mood\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "must allow -1000 to 1000");
        var missingKind = Assert.Throws<EngineRefusedException>(() => new Rig(Villager, "other"));
        Assert.Equal(RelationshipAiBridge.BadKind, missingKind.Code);
    }

    [Fact]
    public void AnAiDocumentWithAnExtraAttributeIsRefusedToo()
    {
        var document = Villager.Replace("{ \"id\": \"urgency_fun\"", "{ \"id\": \"extra\", \"type\": \"int\", \"min\": 0, \"max\": 1, \"unknown\": 0 }, { \"id\": \"urgency_fun\"", StringComparison.Ordinal);
        Assert.Equal(RelationshipAiBridge.BadKind, Assert.Throws<EngineRefusedException>(() => new Rig(document)).Code);
    }

    [Fact]
    public void TheAttributeNameOfANeedIsTheUrgencyPrefixAndTheNeedIdWithDotsAndHyphensAsUnderscores()
    {
        Assert.Equal("urgency_hunger", RelationshipAiBridge.UrgencyAttribute(Samples.Village.NeedById["hunger"]));
        var odd = new NeedDef(0, "deep-hunger.x", "k", 100, 50, 1, 0, 0, [new CurvePoint(0, 0), new CurvePoint(100, 0)], []);
        Assert.Equal("urgency_deep_hunger_x", RelationshipAiBridge.UrgencyAttribute(odd));
    }
}
