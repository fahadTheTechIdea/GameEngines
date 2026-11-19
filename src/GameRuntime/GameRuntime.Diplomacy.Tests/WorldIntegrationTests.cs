using System.Text.Json.Nodes;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The world-events engine and the diplomacy: a war and a treaty are facts the world's hazards read, and what the world signals reaches a realm's factions; neither engine reads the other's state.</summary>
public sealed class WorldIntegrationTests
{
    /// <summary>The valley with nothing happening by itself, except an earthquake that comes at once to a realm at war: its condition reads the diplomacy's fact.</summary>
    private static WorldSet Valley(Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(Samples.Read("valley.world.json"))!.AsObject();
        foreach (var hazard in root["hazards"]!.AsArray()) hazard!["ratePpm"] = 0;
        foreach (var card in root["cards"]!.AsArray()) card!["weight"] = 0;
        foreach (var disease in root["diseases"]!.AsArray()) { disease!["seedPpm"] = 0; disease["seedCases"] = 0; }
        var quake = root["hazards"]!.AsArray().Select(h => h!.AsObject()).First(h => h["id"]!.GetValue<string>() == "quake");
        quake["ratePpm"] = 1_000_000;
        quake["when"] = new JsonObject { ["fact"] = "diplomacy.at_war.aurel", ["op"] = "eq", ["value"] = true };
        root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "diplomacy.at_war.aurel", ["type"] = "bool" });
        edit?.Invoke(root);
        return WorldLoader.Load(root.ToJsonString());
    }

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public DiplomacyComponent Diplomacy { get; }
        public WorldComponent World { get; }
        public DiplomacyWorldEvents Events { get; }
        public List<WorldHazardChanged> Hazards { get; } = new();
        public List<DiplomacyRefused> Refused { get; } = new();
        public List<WorldRefused> WorldRefused { get; } = new();

        public Rig(WorldSet? world = null, IEnumerable<WorldPoliticsRule>? rules = null, string worldChannel = "", string bridgeWorldChannel = "", DipSet? diplomacy = null)
        {
            var worldSet = world ?? Valley();
            var dip = diplomacy ?? Samples.Calm(r => Samples.Rules(r, "politics")["driftPpm"] = 0);
            var dipChannels = new DiplomacyChannels();
            dipChannels.Install(Session);
            var worldChannels = new WorldChannels();
            worldChannels.Install(Session);
            Diplomacy = new DiplomacyComponent(DipRuntime.Create(dip), new TickDiplomacyClock(dip), () => Samples.Crisis(), dipChannels);
            var game = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
            World = new WorldComponent(WorldRuntime.Create(worldSet), new TickWorldClock(worldSet, ticksPerStep: 1), DiplomacyWorldFacts.HostFacts(Diplomacy.Runtime, () => game), worldChannels, channel: worldChannel);
            Events = new DiplomacyWorldEvents("dip-world", rules ?? [], worldSet, dip, bridgeWorldChannel);
            Session.AddComponent(Diplomacy, 10);
            Session.AddComponent(World, 20);
            Session.AddComponent(Events, 25);
            Session.Subscribe<WorldHazardChanged>(Hazards.Add);
            Session.Subscribe<DiplomacyRefused>(Refused.Add);
            Session.Subscribe<WorldRefused>(WorldRefused.Add);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a, string b = "", string c = "") => Session.Submit(new WorldRequested("t" + kind + a + b, kind, a, b, c));
    }

    // ---- facts the world reads --------------------------------------------------------------------------------------------------

    [Fact]
    public void AWarIsAFactTheWorldsHazardsRead()
    {
        var peace = new Rig();
        peace.Tick(30);
        Assert.Empty(peace.Hazards);
        Assert.Empty(peace.WorldRefused);
        var war = new Rig();
        war.Tick(3);
        war.Session.Submit(new DiplomacyRequested("w", "declare", "aurel", "brass", ""));
        war.Tick(10);
        Assert.NotEmpty(war.Hazards);
        Assert.All(war.Hazards, h => Assert.Equal("quake", h.Hazard));
        Assert.True(war.Hazards.First().Step >= 3, "the earthquake came with the war, not before it");
    }

    [Fact]
    public void TheFactsAreOfferedPerRealmAndPerWar()
    {
        var runtime = DipRuntime.Create(Samples.Calm());
        var offered = DiplomacyWorldFacts.Offered(runtime.Set);
        Assert.Equal(1 + 5 * 3, offered.Count);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, 16), offered["diplomacy.wars"]);
        Assert.Equal(new FactDefinition(FactKind.Boolean), offered["diplomacy.at_war.dune"]);
        Assert.Equal(new FactDefinition(FactKind.Integer, 0, runtime.Set.Limits.MaxTreaties), offered["diplomacy.treaties.dune"]);
        var facts = DiplomacyWorldFacts.Read(runtime);
        Assert.Equal(offered.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), facts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.False(facts["diplomacy.at_war.dune"].Boolean);
        runtime.DeclareWar("dune", "ember", "");
        runtime.Propose("aurel", "brass", "embargo", 30, 0);
        runtime.Remember("ember", "dune", "insult", 100);
        var after = DiplomacyWorldFacts.Read(runtime);
        Assert.Equal((1L, true, true, false, 1L, 1L), (after["diplomacy.wars"].Integer, after["diplomacy.at_war.dune"].Boolean, after["diplomacy.at_war.ember"].Boolean, after["diplomacy.at_war.aurel"].Boolean,
            after["diplomacy.treaties.aurel"].Integer, after["diplomacy.treaties.brass"].Integer));
        Assert.All(DiplomacyWorldFacts.Offered(runtime.Set).Keys, id => Assert.Equal(offered[id].Kind, after[id].Kind));
        Assert.False(after["diplomacy.unrest.dune"].Boolean);
    }

    [Fact]
    public void ARealmInUnrestIsAFact()
    {
        var runtime = DipRuntime.Create(Samples.Calm(r => Samples.Realm(r, "ember")["stability"] = 300));
        Assert.True(DiplomacyWorldFacts.Read(runtime)["diplomacy.unrest.ember"].Boolean);
        Assert.False(DiplomacyWorldFacts.Read(runtime)["diplomacy.unrest.aurel"].Boolean);
    }

    [Fact]
    public void AWorldDocumentThatReadsWhatTheDiplomacyDoesNotOfferIsRefusedWhenTheGameIsAssembled()
    {
        var dip = Samples.Calm();
        void Refused(string id, string type, string fragment, int? min = null, int? max = null)
        {
            var world = Valley(r => r["facts"]!.AsArray().Add(type == "int" ? new JsonObject { ["id"] = id, ["type"] = type, ["min"] = min, ["max"] = max } : new JsonObject { ["id"] = id, ["type"] = type }));
            var refusal = Assert.Throws<EngineRefusedException>(() => DiplomacyWorldFacts.Check(world, dip));
            Assert.Equal(DiplomacyWorldFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused("diplomacy.nonsense", "bool", "does not offer");
        Refused("diplomacy.wars", "bool", "is Integer in the diplomacy and Boolean in the world document");
        Refused("diplomacy.wars", "int", "outside the 0 to 5 the world document declares", 0, 5);
        DiplomacyWorldFacts.Check(Valley(), dip);
        DiplomacyWorldFacts.Check(Valley(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "diplomacy.wars", ["type"] = "int", ["min"] = 0, ["max"] = 16 })), dip);
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.Check(null!, dip));
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.Check(Valley(), null!));
    }

    [Fact]
    public void TheGameAndTheDiplomacyCannotBothGiveAFact()
    {
        var runtime = DipRuntime.Create(Samples.Calm());
        var facts = DiplomacyWorldFacts.HostFacts(runtime, () => new Dictionary<string, FactValue> { ["diplomacy.wars"] = FactValue.From(3L) });
        Assert.Equal(DiplomacyWorldFacts.Mismatch, Samples.Code(() => facts()));
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.HostFacts(null!, () => new Dictionary<string, FactValue>()));
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.HostFacts(runtime, null!));
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.Read(null!));
        Assert.Throws<ArgumentNullException>(() => DiplomacyWorldFacts.Offered(null!));
    }

    // ---- what the world does to a realm -------------------------------------------------------------------------------------------

    [Fact]
    public void AFloodTheWorldSignalsShakesTheFactionsThatMindADisaster()
    {
        var rig = new Rig(rules: [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 500, "riverton")]);
        rig.Tick(2);
        var commons = rig.Diplomacy.Runtime.Realms[0].Satisfaction[1];
        rig.Ask("hazard", "riverton", "flood", "minor");
        rig.Tick(4);
        Assert.Equal(1, rig.Events.Asked);
        Assert.Equal(commons - 100, rig.Diplomacy.Runtime.Realms[0].Satisfaction[1]);       // the commons mind a disaster by 200, at half the scale
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void ARuleWithARegionFiresForThatRegionsSignalsOnlyAndAnotherChannelIsNotListenedTo()
    {
        int Signals(string region)
        {
            var seen = new List<WorldEffectIssued>();
            var probe = new Rig();
            probe.Session.Subscribe<WorldEffectIssued>(seen.Add);
            probe.Tick(2);
            probe.Ask("hazard", "riverton", "flood", "minor");
            probe.Tick(4);
            return seen.Count(e => e.Kind == "signal" && e.Name == "world.flood.started" && (region.Length == 0 || e.Region == region));
        }
        var everywhere = Signals("");
        var inRiverton = Signals("riverton");
        Assert.Equal(1, inRiverton);
        Assert.True(everywhere >= inRiverton);
        var rig = new Rig(rules: [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 500, "riverton")]);
        rig.Tick(2);
        rig.Ask("hazard", "riverton", "flood", "minor");
        rig.Tick(4);
        Assert.Equal(inRiverton, rig.Events.Asked);
        var all = new Rig(rules: [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 500)]);
        all.Tick(2);
        all.Ask("hazard", "riverton", "flood", "minor");
        all.Tick(4);
        Assert.Equal(everywhere, all.Events.Asked);
        var deaf = new Rig(rules: [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 500)], bridgeWorldChannel: "elsewhere");
        deaf.Tick(2);
        deaf.Ask("hazard", "riverton", "flood", "minor");
        deaf.Tick(4);
        Assert.Equal(0, deaf.Events.Asked);
    }

    [Fact]
    public void AnyRegionIsMeantWhenNoneIsNamed()
    {
        var rig = new Rig(rules: [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 100)]);
        rig.Tick(2);
        rig.Ask("hazard", "hillfort", "flood", "minor");
        rig.Tick(4);
        Assert.True(rig.Events.Asked >= 1);
    }

    [Fact]
    public void RulesAreCheckedAgainstBothDocumentsWhenTheGameIsAssembled()
    {
        var world = Valley();
        var dip = Samples.Calm();
        void Bad(WorldPoliticsRule rule, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => new DiplomacyWorldEvents("w", [rule], world, dip));
            Assert.Equal(DiplomacyWorldEvents.BadRule, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad(new WorldPoliticsRule("world.never", "aurel", "disaster", 500), "never issues the signal");
        Bad(new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 500, "nowhere"), "no region 'nowhere'");
        Bad(new WorldPoliticsRule("world.flood.started", "nobody", "disaster", 500), "no realm 'nobody'");
        Bad(new WorldPoliticsRule("world.flood.started", "aurel", "earthquake", 500), "is no political event");
        Bad(new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 0), "1 to 1000 per mille, not 0");
        Bad(new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 1001), "1 to 1000 per mille, not 1001");
        Assert.NotNull(new DiplomacyWorldEvents("w", [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 1000)], world, dip));
        Assert.NotNull(new DiplomacyWorldEvents("w", [new WorldPoliticsRule("world.flood.started", "aurel", "disaster", 1)], world, dip));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyWorldEvents("w", null!, world, dip));
    }
}
