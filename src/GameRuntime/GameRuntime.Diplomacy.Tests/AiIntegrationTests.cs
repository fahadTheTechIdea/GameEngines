using AIEngine;
using DiplomacyEngine;
using EngineCommon;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The AI engine and the diplomacy: an envoy knows its realm's regard of another, the other's public standing and a band for its might only as percepts it was told, and acts on what it was told.</summary>
public sealed class AiIntegrationTests
{
    private const string Envoy = """
    { "format": 1, "id": "dip.envoy",
      "keys": [ { "id": "k_regard", "type": "int", "initial": 0, "min": -1000, "max": 1000, "source": "agent" }, { "id": "k_one", "type": "int", "initial": 1000, "min": 0, "max": 1000, "source": "agent" } ],
      "percepts": [ { "kind": "court", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "regard", "type": "int", "min": -1000, "max": 1000, "unknown": 0 }, { "id": "trust", "type": "int", "min": -1000, "max": 1000, "unknown": 0 },
          { "id": "at_war", "type": "bool", "unknown": false }, { "id": "treaties", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "intel", "type": "string", "unknown": "" },
          { "id": "power_low", "type": "int", "min": 0, "max": 1000000000, "unknown": 0 }, { "id": "power_high", "type": "int", "min": 0, "max": 1000000000, "unknown": 0 },
          { "id": "own_power", "type": "int", "min": 0, "max": 1000000000, "unknown": 0 } ] } ],
      "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
      "behaviors": [ { "id": "envoy", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "see-regard", "kind": "let", "key": "k_regard", "value": { "known": { "from": "court", "id": "view", "attr": "regard" } } },
          { "id": "choose", "kind": "utility", "momentum": 0, "options": [
            { "id": "ally", "weight": 1000, "considerations": [ { "input": { "key": "k_regard" }, "min": 0, "max": 600, "curve": { "kind": "linear" } } ], "child": { "id": "do-ally", "kind": "act", "intent": "do", "args": { "what": "ally" } } },
            { "id": "wait", "weight": 300, "considerations": [ { "input": { "key": "k_one" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-wait", "kind": "act", "intent": "do", "args": { "what": "wait" } } } ] } ] } } ] }
    """;

    /// <summary>The isles with nothing fading, so what the envoy was told only changes when a test changes it.</summary>
    private static DipSet Still() => Samples.Calm(root =>
    {
        foreach (var key in new[] { "trustDecayPpm", "fearDecayPpm", "honourDecayPpm", "infamyDecayPpm" }) Samples.Rules(root, "standing")[key] = 0;
        Samples.Rules(root, "espionage")["intelDecayPpm"] = 0;
        Samples.Rules(root, "espionage")["epochDays"] = 36500;
        foreach (var key in new[] { "inertiaPpm", "driftPpm", "legitimacyDriftPpm" }) Samples.Rules(root, "politics")[key] = 0;
    });

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public DiplomacyComponent Diplomacy { get; }
        public DiplomacyAiPercepts Bridge { get; }
        public List<string> Posts { get; } = new();
        public List<string> Intents { get; } = new();
        public List<string> AiRefused { get; } = new();

        public Rig(string document = Envoy, string kind = "court", string realm = "aurel", string other = "brass")
        {
            var ai = AiLoader.Load(document);
            AiEventCatalog.Declare(Session);
            var channels = new DiplomacyChannels();
            channels.Install(Session);
            var dip = Still();
            Diplomacy = new DiplomacyComponent(DipRuntime.Create(dip), new TickDiplomacyClock(dip), () => Samples.Crisis(), channels);
            Bridge = new DiplomacyAiPercepts("dip-ai", Diplomacy, realm, other, "ai", ai, kind, "view", "envoy");
            Session.AddComponent(Diplomacy, 10);
            Session.AddComponent(Bridge, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(ai, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add(e.Args));
            Session.Subscribe<AiPerceptPosted>(e => Posts.Add(e.Attrs));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Init();
            Session.Submit(new AiAgentAddRequested("ai", "agent", "envoy", "envoy"));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public string Latest => Intents.Count == 0 ? "" : Intents[^1];
    }

    [Fact]
    public void TheEnvoyIsToldWhatItsRealmKnowsAndActsOnWhatItWasToldNotOnWhatIsTrue()
    {
        var rig = new Rig();
        rig.Tick(3);
        Assert.Single(rig.Posts);
        Assert.StartsWith("regard=", rig.Posts[0], StringComparison.Ordinal);
        Assert.Contains("trust=0;at_war=false;treaties=0;intel=none;", rig.Posts[0], StringComparison.Ordinal);
        Assert.Contains("own_power=500000", rig.Posts[0], StringComparison.Ordinal);
        Assert.Equal("what=wait", rig.Latest);
        rig.Diplomacy.Runtime.Remember("brass", "aurel", "gift", 1000);
        rig.Tick(3);
        Assert.True(rig.Posts.Count >= 2);                           // the gift fades a little every step, so what the envoy would perceive moves with it
        Assert.Equal("what=ally", rig.Latest);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void AnEnvoyIsOnlyToldAgainWhenWhatItWouldPerceiveChanged()
    {
        var rig = new Rig();
        rig.Tick(20);
        Assert.Equal(1, rig.Bridge.Posted);
        rig.Diplomacy.Runtime.DeclareWar("aurel", "brass", "");
        rig.Tick(2);
        Assert.Equal(2, rig.Bridge.Posted);
        Assert.Contains("at_war=true", rig.Posts[^1], StringComparison.Ordinal);
        rig.Tick(10);
        Assert.Equal(2, rig.Bridge.Posted);
    }

    [Fact]
    public void WhatItKnowsOfTheOthersMightIsABandNotTheTruthAndTightensWithKnowledge()
    {
        var rig = new Rig();
        rig.Tick(2);
        var blind = rig.Posts[^1];
        Assert.Contains("intel=none", blind, StringComparison.Ordinal);
        Assert.Contains("power_low=0;", blind, StringComparison.Ordinal);
        rig.Diplomacy.Runtime.State.Intel[0 * 5 + 1] = 1_000_000;
        rig.Bridge.Notify();
        rig.Tick(2);
        var known = rig.Posts[^1];
        Assert.Contains("intel=complete", known, StringComparison.Ordinal);
        Assert.Contains("power_low=400000;power_high=400000;", known, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTreatiesAndTheOthersStandingAreInThePercept()
    {
        var rig = new Rig();
        rig.Diplomacy.Runtime.Propose("aurel", "brass", "embargo", 30, 0);
        rig.Tick(2);
        Assert.Contains("treaties=1", rig.Posts[^1], StringComparison.Ordinal);
        rig.Diplomacy.Runtime.SetMight("aurel", 123456);
        rig.Bridge.Notify();
        rig.Tick(2);
        Assert.Contains("own_power=123456", rig.Posts[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void NotifyTellsTheAgentAgainAtTheNextUpdate()
    {
        var rig = new Rig();
        rig.Tick(3);
        rig.Bridge.Notify();
        rig.Tick();
        Assert.Equal(2, rig.Bridge.Posted);
    }

    [Fact]
    public void TheBridgeCheckedAgainstBothDocumentsWhenTheGameIsAssembled()
    {
        void Bad(string document, string kind, string fragment, string realm = "aurel", string other = "brass")
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => new Rig(document, kind, realm, other));
            Assert.Equal(DiplomacyAiPercepts.BadKind, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad(Envoy, "nope", "no percept kind 'nope'");
        Bad(Envoy.Replace("\"owned\": false,", "\"owned\": true, \"ownerCapacity\": 1,", StringComparison.Ordinal), "court", "is owned");
        Bad(Envoy.Replace("{ \"id\": \"treaties\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }, ", "", StringComparison.Ordinal), "court", "must declare exactly");
        Bad(Envoy.Replace("{ \"id\": \"intel\", \"type\": \"string\", \"unknown\": \"\" }", "{ \"id\": \"intel\", \"type\": \"int\", \"min\": 0, \"max\": 4, \"unknown\": 0 }", StringComparison.Ordinal), "court", "must declare 'intel' as String");
        Bad(Envoy.Replace("\"id\": \"regard\", \"type\": \"int\", \"min\": -1000", "\"id\": \"regard\", \"type\": \"int\", \"min\": -500", StringComparison.Ordinal), "court", "attribute 'regard' of 'court' must allow -1000 to 1000");
        Bad(Envoy.Replace("{ \"id\": \"power_high\", \"type\": \"int\", \"min\": 0, \"max\": 1000000000", "{ \"id\": \"power_high\", \"type\": \"int\", \"min\": 0, \"max\": 5", StringComparison.Ordinal), "court", "attribute 'power_high' of 'court' must allow 0 to 1000000000");
        Bad(Envoy, "court", "no realm 'nobody'", other: "nobody");
        Bad(Envoy, "court", "no realm 'nobody'", realm: "nobody");
        Bad(Envoy, "court", "perceives another realm than its own", other: "aurel");
        Assert.NotNull(new Rig().Bridge);
        Assert.Throws<ArgumentNullException>(() => new DiplomacyAiPercepts("x", null!, "aurel", "brass", "ai", AiLoader.Load(Envoy), "court", "view", "envoy"));
    }

    [Fact]
    public void AnUpdateBeforeInitFails()
    {
        var dip = Still();
        var channels = new DiplomacyChannels();
        var diplomacy = new DiplomacyComponent(DipRuntime.Create(dip), new TickDiplomacyClock(dip), () => Samples.Crisis(), channels);
        var bridge = new DiplomacyAiPercepts("x", diplomacy, "aurel", "brass", "ai", AiLoader.Load(Envoy), "court", "view", "envoy");
        Assert.Throws<InvalidOperationException>(() => bridge.Update(new UpdateContext(1, 0, 0.0, 0)));
    }
}
