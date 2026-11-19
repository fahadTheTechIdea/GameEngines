using System.Text.Json.Nodes;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The economy and the world: what the world asks of the economy goes through the economy's own shock path, scaled and addressed to the right market; the documents are checked against each other; the economy never reads the world.</summary>
public sealed class EconomyIntegrationTests
{
    private static string Town(Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(Samples.Read("town.economy.json"))!.AsObject();
        root["time"]!["stepsPerDay"] = 1;
        root["time"]!["periodSteps"] = 1;
        edit?.Invoke(root);
        return root.ToJsonString();
    }

    /// <summary>The valley with no card drawable, so nothing but what a test asks for reaches the economy.</summary>
    private static WorldSet QuietValley()
    {
        var root = JsonNode.Parse(Samples.Read("valley.world.json"))!.AsObject();
        foreach (var card in root["cards"]!.AsArray()) card!["weight"] = 0;
        return WorldLoader.Load(root.ToJsonString());
    }

    private static readonly Dictionary<string, FactValue> TownHost = new(StringComparer.Ordinal) { ["cal.season"] = FactValue.From("spring"), ["story.plague"] = FactValue.From(0L) };

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public WorldComponent World { get; }
        public EconomyComponent Economy { get; }
        public WorldEconomyShocks Shocks { get; }
        public List<EconomyShockRequested> Requested { get; } = new();
        public List<EconomyShockChanged> Changed { get; } = new();
        public List<EconomyRefused> Refused { get; } = new();

        public Rig(WorldSet? world = null, string worldChannel = "", string economyChannel = "", string bridgeWorldChannel = "", string bridgeEconomyChannel = "", EconomySet? economy = null, bool bridge = true, long worldTicksPerStep = 1_000_000)
        {
            var worldSet = world ?? Samples.Valley;
            var economySet = economy ?? EconomyLoader.Load(Town());
            var worldChannels = new WorldChannels();
            worldChannels.Install(Session);
            var economyChannels = new EconomyChannels();
            economyChannels.Install(Session);
            World = new WorldComponent(WorldRuntime.Create(worldSet), new TickWorldClock(worldSet, ticksPerStep: worldTicksPerStep), () => Garrison, worldChannels, channel: worldChannel);
            Economy = new EconomyComponent(EconomyRuntime.Create(economySet), new TickEconomyClock(1), () => TownHost, economyChannels, channel: economyChannel);
            Shocks = new WorldEconomyShocks("world-shocks", worldSet, economySet, bridgeWorldChannel, bridgeEconomyChannel);
            Session.AddComponent(World, 10);
            if (bridge) Session.AddComponent(Shocks, 15);
            Session.AddComponent(Economy, 30);
            Session.Subscribe<EconomyShockRequested>(Requested.Add);
            Session.Subscribe<EconomyShockChanged>(Changed.Add);
            Session.Subscribe<EconomyRefused>(Refused.Add);
            Session.Init();
        }

        public Dictionary<string, FactValue> Garrison { get; } = new() { ["game.garrison"] = FactValue.From(30L) };

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a = "", string b = "", string c = "", string channel = "", long number = 1) => Session.Submit(new WorldRequested("t" + Requested.Count + kind + a, kind, a, b, c, number, channel));

        public long Grain(string market)
        {
            var m = Economy.Runtime.Set.MarketById[market];
            return Economy.Runtime.Ratio(m.Index, m.Pairs.First(p => Economy.Runtime.Set.Goods[p.Good].Id == "grain").Index);
        }
    }

    [Fact]
    public void ADroughtTheWorldMakesReachesTheEconomyAsAShockOnTheRegionsMarketAndRaisesThePrice()
    {
        var drought = new Rig(QuietValley());
        var control = new Rig(QuietValley());
        drought.Tick();
        control.Tick();
        drought.Ask("hazard", "riverton", "drought", "dry");
        drought.Tick(2);
        control.Tick(2);
        var riverton = Assert.Single(drought.Requested, r => r.Market == "riverton");
        Assert.Equal(("drought", 600), (riverton.Shock, riverton.ScalePermille));
        Assert.Contains(drought.Changed, c => c.Shock == "drought" && c.Market == "riverton" && c.Started);
        Assert.Contains(drought.Requested, r => r.Market == "hillfort" && r.ScalePermille == 360);       // the hazard reached the linked towns at 600 per mille: a shock of 600 times 0.6
        Assert.Contains(drought.Requested, r => r.Market == "seaport" && r.ScalePermille == 360);
        Assert.Empty(drought.Refused);
        Assert.Empty(control.Requested);
        // The drought's price jump and its cut of supply reach the linked town at once: hillfort's grain is dearer than the control's while the shock is new.
        Assert.True(drought.Grain("hillfort") > control.Grain("hillfort"), drought.Grain("hillfort") + " against " + control.Grain("hillfort"));
        drought.Tick(10);
        control.Tick(10);
        // Since pass 49 a supply shock also cuts what a recipe makes. The drought cuts the flour riverton's mill makes by 30 percent (its -500 at a scale of 600), which sinks the mill's margin into its throttle (the
        // sample's throttle is 900 to 1,200), so the mill buys less grain: by now the economy has turned the shortage into a fall in the demand for grain (a derived demand), where the control's mill runs more.
        // The price of grain after a dozen periods is therefore not a clean measure of the drought; the line is.
        LineState Mill(Rig rig)
        {
            var runtime = rig.Economy.Runtime;
            var market = runtime.MarketIndex("riverton");
            return runtime.Markets[market].Lines[runtime.Set.LineOf[market][runtime.Set.RecipeById["mill"].Index]];
        }
        // (Pass 51: the sample's mill earns more over a run than it did, with its own wage and overhead in the cost, so under the drought it is throttled but not stopped: it runs fewer times than the control's.)
        Assert.Equal(LineLimit.Margin, Mill(drought).Limit);
        Assert.True(Mill(control).LastRuns > 0, "the control's mill ran " + Mill(control).LastRuns);
        Assert.True(Mill(drought).LastRuns < Mill(control).LastRuns, "the drought's mill ran " + Mill(drought).LastRuns + " against the control's " + Mill(control).LastRuns);
        Assert.True(drought.Grain("hillfort") > control.Grain("hillfort"), "the drought's grain is still dearer in hillfort after a dozen periods: " + drought.Grain("hillfort") + " against " + control.Grain("hillfort"));
    }

    [Fact]
    public void AGlobalCardsShockIsAskedOfEveryMarket()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Ask("card", "river-blockade", "hillfort");
        rig.Tick(2);
        var request = Assert.Single(rig.Requested);
        Assert.Equal(("blockade", "hillfort", 800), (request.Shock, request.Market, request.ScalePermille));
        Assert.Contains(rig.Changed, c => c.Shock == "blockade" && c.Market == "hillfort" && c.Started);
        var plague = new Rig(QuietValley(), worldTicksPerStep: 1);
        plague.Tick();
        plague.Ask("outbreak", "seaport", "plague", number: 600);
        plague.Tick(40);
        Assert.Contains(plague.Requested, r => r.Shock == "blockade" && r.Market == "seaport" && r.ScalePermille == 500);          // the outbreak is declared when 2% are ill
    }

    [Fact]
    public void EachShockIsRequestedOnceAndTheBridgeCountsWhatItAsked()
    {
        var rig = new Rig(QuietValley());
        rig.Tick();
        rig.Ask("hazard", "riverton", "drought", "severe");
        rig.Tick(3);
        Assert.Equal(rig.Requested.Count, rig.Shocks.Requested);
        Assert.Equal(rig.Requested.Count, rig.Requested.Select(r => r.RequestId).Distinct().Count());
        Assert.Equal(3, rig.Requested.Count);
        Assert.Contains(rig.Requested, r => r.Market == "riverton" && r.ScalePermille == 1200);
    }

    [Fact]
    public void TheDocumentsAreCheckedAgainstEachOtherWhenTheGameIsAssembled()
    {
        var economy = EconomyLoader.Load(Town());
        WorldEconomyShocks.Check(Samples.Valley, economy);
        var refused = Assert.Throws<EngineRefusedException>(() => WorldEconomyShocks.Check(Samples.Tiny, economy));
        Assert.Equal(WorldEconomyShocks.Mismatch, refused.Code);
        Assert.Contains("'sh1'", refused.Message, StringComparison.Ordinal);
        var withShock = EconomyLoader.Load(Town(r =>
        {
            var copy = JsonNode.Parse(r["shocks"]!.AsArray()[0]!.ToJsonString())!.AsObject();
            copy["id"] = "sh1";
            r["shocks"]!.AsArray().Add(copy);
        }));
        var market = Assert.Throws<EngineRefusedException>(() => WorldEconomyShocks.Check(Samples.Tiny, withShock));
        Assert.Equal(WorldEconomyShocks.Mismatch, market.Code);
        Assert.Contains("market 'ma'", market.Message, StringComparison.Ordinal);
        Assert.Throws<EngineRefusedException>(() => new Rig(Samples.Tiny));
    }

    [Fact]
    public void TheBridgeListensOnlyToItsWorldsChannelAndAsksOnlyItsEconomysChannel()
    {
        var rig = new Rig(worldChannel: "w1", economyChannel: "e1", bridgeWorldChannel: "w1", bridgeEconomyChannel: "e1");
        rig.Ask("card", "river-blockade", "riverton", channel: "w1");
        rig.Tick(2);
        Assert.Equal("e1", Assert.Single(rig.Requested).Channel);
        Assert.Contains(rig.Changed, c => c.Channel == "e1" && c.Shock == "blockade");
        var elsewhere = new Rig(worldChannel: "w1", economyChannel: "e1", bridgeWorldChannel: "w2", bridgeEconomyChannel: "e1");
        elsewhere.Ask("card", "river-blockade", "riverton", channel: "w1");
        elsewhere.Tick(2);
        Assert.Empty(elsewhere.Requested);                         // the bridge listens to world "w2" and the shock came from "w1"
    }

    [Fact]
    public void WithoutTheBridgeTheEconomyNeverHearsOfTheWorld()
    {
        var rig = new Rig(QuietValley(), bridge: false);
        rig.Tick();
        rig.Ask("hazard", "riverton", "drought", "severe");
        rig.Tick(5);
        Assert.Empty(rig.Requested);
        Assert.Empty(rig.Changed);
    }

    [Fact]
    public void AShockTheEconomyCannotApplyIsRefusedByTheEconomyNotHiddenByTheBridge()
    {
        var rig = new Rig(QuietValley(), economy: EconomyLoader.Load(Town(r => r["limits"]!["maxShocks"] = 1)));
        rig.Tick();
        rig.Ask("hazard", "riverton", "drought", "severe");          // three shocks at once: riverton, hillfort, seaport
        rig.Tick(3);
        Assert.Equal(3, rig.Requested.Count);
        Assert.Equal([EconomyCodes.Capacity, EconomyCodes.Capacity], rig.Refused.Select(r => r.Code));
        Assert.Single(rig.Changed, c => c.Started);
    }
}
