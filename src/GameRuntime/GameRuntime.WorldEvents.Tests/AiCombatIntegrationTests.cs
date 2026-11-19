using AIEngine;
using CombatEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The AI engine and the combat rules with the world: an agent knows the weather and the danger only as percepts it was told, and a fight's terrain is the combat rules' own, chosen by a table from what the world says of the region.</summary>
public sealed class AiCombatIntegrationTests
{
    private const string Farmer = """
    { "format": 1, "id": "world.farmer",
      "keys": [ { "id": "k_danger", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" }, { "id": "k_visibility", "type": "int", "initial": 1000, "min": 0, "max": 1000, "source": "agent" } ],
      "percepts": [ { "kind": "weather", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "weather", "type": "string", "unknown": "" }, { "id": "season", "type": "string", "unknown": "" },
          { "id": "visibility", "type": "int", "min": 0, "max": 1000, "unknown": 1000 }, { "id": "mobility", "type": "int", "min": 0, "max": 1000, "unknown": 1000 },
          { "id": "danger", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "sickness", "type": "int", "min": 0, "max": 1000, "unknown": 0 } ] } ],
      "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
      "behaviors": [ { "id": "farmer", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "see-danger", "kind": "let", "key": "k_danger", "value": { "known": { "from": "weather", "id": "sky", "attr": "danger" } } },
          { "id": "see-visibility", "kind": "let", "key": "k_visibility", "value": { "known": { "from": "weather", "id": "sky", "attr": "visibility" } } },
          { "id": "choose", "kind": "utility", "momentum": 0, "options": [
            { "id": "shelter", "weight": 1000, "considerations": [ { "input": { "key": "k_danger" }, "min": 0, "max": 600, "curve": { "kind": "linear" } } ], "child": { "id": "do-shelter", "kind": "act", "intent": "do", "args": { "what": "shelter" } } },
            { "id": "work", "weight": 800, "considerations": [ { "input": { "key": "k_visibility" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-work", "kind": "act", "intent": "do", "args": { "what": "work" } } } ] } ] } } ] }
    """;

    private static WorldSet Calm() => Samples.Calm();

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public WorldComponent World { get; }
        public WorldAiPercepts Bridge { get; }
        public List<string> Posts { get; } = new();
        public List<string> Intents { get; } = new();
        public List<string> AiRefused { get; } = new();

        public Rig(string document = Farmer, string kind = "weather", string region = "seaport", string visibility = "visibility", string mobility = "mobility")
        {
            var ai = AiLoader.Load(document);
            AiEventCatalog.Declare(Session);
            var channels = new WorldChannels();
            channels.Install(Session);
            var set = Calm();
            var host = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
            World = new WorldComponent(WorldRuntime.Create(set), new TickWorldClock(set, ticksPerStep: 1_000_000), () => host, channels);
            Bridge = new WorldAiPercepts("world-ai", World, region, visibility, mobility, "ai", ai, kind, "sky", "farmer");
            Session.AddComponent(World, 10);
            Session.AddComponent(Bridge, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(ai, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add(e.Args));
            Session.Subscribe<AiPerceptPosted>(e => Posts.Add(e.Attrs));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Init();
            Session.Submit(new AiAgentAddRequested("ai", "agent", "farmer", "farmer"));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a, string b, string c) => Session.Submit(new WorldRequested("t" + Posts.Count + kind + a, kind, a, b, c));

        public string Latest => Intents.Count == 0 ? "" : Intents[^1];
    }

    // ---- the agent --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheFarmerIsToldTheWeatherAndTheDangerAndActsOnWhatHeWasToldNotOnWhatIsTrue()
    {
        var rig = new Rig();
        rig.Tick(3);
        Assert.NotEmpty(rig.Posts);
        Assert.StartsWith("weather=", rig.Posts[0], StringComparison.Ordinal);
        Assert.Contains("season=spring", rig.Posts[0], StringComparison.Ordinal);
        Assert.Contains("danger=0;sickness=0", rig.Posts[0], StringComparison.Ordinal);
        Assert.Equal("what=work", rig.Latest);
        rig.Ask("hazard", "seaport", "storm", "tempest");
        rig.Tick(4);
        Assert.Contains("danger=1000", rig.Posts[^1], StringComparison.Ordinal);
        Assert.Equal("what=shelter", rig.Latest);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void ADiseaseInTheRegionIsPerceivedAsSickness()
    {
        var rig = new Rig();
        rig.Tick(2);
        rig.Session.Submit(new WorldRequested("o", "outbreak", "seaport", "plague", "", 2000));
        rig.Tick(3);
        Assert.Contains("sickness=100", rig.Posts[^1], StringComparison.Ordinal);          // 2,000 of 20,000: a tenth
    }

    [Fact]
    public void NothingIsPostedWhileNothingHasChangedAndNotifyTellsTheAgentAgain()
    {
        var rig = new Rig();
        rig.Tick(4);
        var posted = rig.Bridge.Posted;
        rig.Tick(3);
        Assert.Equal(posted, rig.Bridge.Posted);
        rig.Bridge.Notify();
        rig.Tick();
        Assert.Equal(posted + 1, rig.Bridge.Posted);
    }

    [Fact]
    public void AnAgentPerceivesTheDangerOfItsOwnRegionAtTheStrengthItReachedThere()
    {
        var rig = new Rig(region: "hillfort");
        rig.Tick(2);
        rig.Ask("hazard", "seaport", "quake", "great");               // Hillfort is two links from Seaport, through Riverton: with a falloff of 400 per mille a hop, a sixteenth of the strength
        rig.Tick(3);
        Assert.Contains("danger=160", rig.Posts[^1], StringComparison.Ordinal);
        var seaport = new Rig(region: "seaport");
        seaport.Tick(2);
        seaport.Ask("hazard", "seaport", "quake", "great");
        seaport.Tick(3);
        Assert.Contains("danger=1000", seaport.Posts[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheBridgeRefusesAPerceptKindThatDoesNotFitWhenTheGameIsAssembled()
    {
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(kind: "nope")));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(Farmer.Replace("\"owned\": false,", "\"owned\": true, \"ownerCapacity\": 1,", StringComparison.Ordinal))));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(Farmer.Replace("{ \"id\": \"sickness\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", "{ \"id\": \"fever\", \"type\": \"int\", \"min\": 0, \"max\": 1000, \"unknown\": 0 }", StringComparison.Ordinal))));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(Farmer.Replace("{ \"id\": \"weather\", \"type\": \"string\", \"unknown\": \"\" }", "{ \"id\": \"weather\", \"type\": \"int\", \"min\": 0, \"max\": 5, \"unknown\": 0 }", StringComparison.Ordinal))));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(Farmer.Replace("{ \"id\": \"danger\", \"type\": \"int\", \"min\": 0, \"max\": 1000", "{ \"id\": \"danger\", \"type\": \"int\", \"min\": 0, \"max\": 500", StringComparison.Ordinal))));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(region: "nowhere")));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(visibility: "nope")));
        Assert.Equal(WorldAiPercepts.BadKind, Samples.Code(() => new Rig(mobility: "nope")));
    }

    // ---- the combat rules -------------------------------------------------------------------------------------------------------------

    private static readonly CombatRules Rules = CombatRulesLoader.Load(Samples.Read("combat-rules.json"));

    private sealed class CombatRig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public WorldComponent World { get; }
        public WorldCombatTerrain Terrain { get; }

        public CombatRig(IEnumerable<WorldTerrainRule>? rules = null)
        {
            var set = Samples.Edited(root =>
            {
                root["weathers"]!.AsArray()[1]!["modifiers"] = new System.Text.Json.Nodes.JsonArray();          // no weather moves visibility: only the hazards do
                foreach (var card in root["cards"]!.AsArray()) card!["weight"] = 0;                                        // no card moves anything either
            });
            var channels = new WorldChannels();
            channels.Install(Session);
            var host = new Dictionary<string, FactValue> { ["ext.alert"] = FactValue.From(0L) };
            World = new WorldComponent(WorldRuntime.Create(set), new TickWorldClock(set), () => host, channels);
            Terrain = new WorldCombatTerrain(World, rules ?? [new WorldTerrainRule("open", "vis", 600, "forest"), new WorldTerrainRule("road", "labor", 400, "rough"), new WorldTerrainRule("urban", "vis", 600, "open")], Rules);
            Session.AddComponent(World, 10);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a, string b, string c) => Session.Submit(new WorldRequested("t" + kind + a, kind, a, b, c));
    }

    [Fact]
    public void AFogLikeHazardChangesTheTerrainTheCombatRulesAreAskedAboutAndThenItGoesBack()
    {
        var rig = new CombatRig();
        rig.Tick(3);
        Assert.Equal("open", rig.Terrain.TerrainFor("a", "open"));
        rig.Ask("hazard", "a", "h", "low");                               // visibility -500 over the onset, at the peak 500
        rig.Tick(3);
        Assert.Equal("forest", rig.Terrain.TerrainFor("a", "open"));      // an open field fights like a forest when one cannot see
        Assert.Equal("road", rig.Terrain.TerrainFor("a", "road"));        // the road rule is about labour, which is not (yet) hurt enough
        Assert.True(Rules.TerrainType("forest").AccuracyPercent < Rules.TerrainType("open").AccuracyPercent);
        Assert.Equal("open", rig.Terrain.TerrainFor("b", "open"));        // b has the hazard at half strength: visibility 750
        rig.Tick(12);
        Assert.Equal("open", rig.Terrain.TerrainFor("a", "open"));
    }

    [Fact]
    public void TheFirstRuleThatMatchesWinsAndNoRuleLeavesTheBaseTerrain()
    {
        var rig = new CombatRig(rules: [new WorldTerrainRule("open", "vis", 1000, "road"), new WorldTerrainRule("open", "vis", 1000, "urban")]);
        Assert.Equal("road", rig.Terrain.TerrainFor("a", "open"));
        Assert.Equal("forest", rig.Terrain.TerrainFor("a", "forest"));
        Assert.Equal(WorldCodes.UnknownRegion, Samples.Code(() => rig.Terrain.TerrainFor("zz", "open")));
    }

    [Fact]
    public void ATableThatNamesATerrainOrAModifierTheRulesLackIsRefusedWhenTheGameIsAssembled()
    {
        Assert.Equal(WorldCombatTerrain.BadRule, Samples.Code(() => new CombatRig([new WorldTerrainRule("open", "vis", 500, "swamp")])));
        Assert.Equal(WorldCombatTerrain.BadRule, Samples.Code(() => new CombatRig([new WorldTerrainRule("swamp", "vis", 500, "open")])));
        Assert.Equal(WorldCombatTerrain.BadRule, Samples.Code(() => new CombatRig([new WorldTerrainRule("open", "nope", 500, "forest")])));
    }

    [Fact]
    public void ARoundOrderCarriesTheTerrainTheTableChoseAndTheResolverAcceptsIt()
    {
        var rig = new CombatRig();
        rig.Tick(3);
        rig.Ask("hazard", "a", "h", "low");
        rig.Tick(3);
        var terrain = rig.Terrain.TerrainFor("a", "open");
        var order = new RoundOrder("a", "b", 100_000, "none", terrain);
        Assert.Equal("forest", order.Terrain);
        Assert.Equal(65, Rules.TerrainType(order.Terrain).AccuracyPercent);                // the combat rules' own number for forest, not a number of the world's
        Assert.Equal(100, Rules.TerrainType("open").AccuracyPercent);
    }
}
