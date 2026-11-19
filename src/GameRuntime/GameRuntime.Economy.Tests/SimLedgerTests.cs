using System.Text.Json;
using System.Text.Json.Nodes;
using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using EconomyEngine;
using EngineCommon;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The SimEngine ledger and the economy: a trade is written into the inventory ledger as its own lifecycle flows, through the one writer of the stock ledger, and the ledger and the economy agree to the minor unit.</summary>
public sealed class SimLedgerTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("economy-ledger-");

    public void Dispose() => _dir.Delete(true);

    private sealed class LabModule : GameModule
    {
        public override string Id => "econ-lab";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    /// <summary>Writes the content of a small world: one inventory vocabulary generated from the economy document and two holders (entity 1 holds cash and iron, entity 2 holds nothing).</summary>
    private string WriteContent(EconomySet set, long cash, long iron, string cashResource = "econ.cash.crown")
    {
        void File(string name, JsonNode node)
        {
            var path = Path.Combine(_dir.FullName, "lab", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        File("access.json", JsonNode.Parse("""{ "open_content_access": [{ "id": "lab.open" }] }""")!);
        File("resources.json", JsonNode.Parse(EconomySimContent.Document(set))!);
        File("startup.json", JsonNode.Parse("""
            { "world_setup_profiles": [{ "id": "lab.start", "entries": ["lab.player.start", "lab.vault.start"], "includes": [] }],
              "entity_setups": [
                { "id": "lab.player.start", "after": [], "entity": "lab.player", "company": 0, "placement": { "space": "lab.tiles", "x": 1, "y": 1 }, "home": null },
                { "id": "lab.vault.start", "after": ["lab.player.start"], "entity": "lab.vault", "company": 0, "placement": { "space": "lab.tiles", "x": 3, "y": 1 }, "home": null } ] }
            """)!);
        File("world.json", JsonNode.Parse($$"""
            { "time_definitions": [{ "id": "lab.time", "quantum": { "numerator": 60, "denominator": 1, "unit": "second" }, "frame_ticks": 1, "ticks_per_second": 30, "calendar": null, "horizon": null }],
              "entity_states": [{ "id": "lab.idle", "state": "state.lab.idle", "priority": 0, "default": true }],
              "tile_spaces": [{ "id": "lab.tiles", "width": 6, "height": 4 }],
              "entities": [
                { "id": "lab.player", "kind": "structure", "components": {
                    "tile_presence": { "space": "lab.tiles", "width": 1, "height": 1, "blocks": true, "movable": false },
                    "storage": { "capacity_policy": "{{EconomySimContent.HolderStore}}", "initial_stock": { {{Stock(cashResource, cash, iron)}} } } } },
                { "id": "lab.vault", "kind": "structure", "components": {
                    "tile_presence": { "space": "lab.tiles", "width": 1, "height": 1, "blocks": true, "movable": false },
                    "storage": { "capacity_policy": "{{EconomySimContent.HolderStore}}" } } } ],
              "game_modes": [{ "id": "lab.resource-only", "name": "Resource-only simulation", "properties": {} }],
              "scenarios": [{ "id": "lab", "name": "Ledger lab", "modules": ["econ-lab", "sim.inventory", "gf.director"], "space": "lab.tiles", "game_mode": "lab.resource-only", "mode_overrides": {},
                  "settings": { "sim.world-setup": { "profile": "lab.start" } }, "seats": 1, "default_seats": 1, "time": "lab.time" }] }
            """)!);
        System.IO.File.WriteAllText(Path.Combine(_dir.FullName, "manifest.json"), """
            { "schema": 1, "modules": [
              { "id": "gf.director", "version": "1.0.0", "documents": [] },
              { "id": "sim.access", "version": "1.0.0", "documents": ["lab/access.json"] },
              { "id": "sim.world-setup", "version": "1.0.0", "documents": ["lab/startup.json"] },
              { "id": "sim.inventory", "version": "1.0.0", "documents": ["lab/resources.json"] },
              { "id": "econ-lab", "version": "1.0.0", "requires": [ { "id": "sim.access", "version": "1.0.0" }, { "id": "sim.world-setup", "version": "1.0.0" } ], "documents": ["lab/world.json"] } ] }
            """);
        return _dir.FullName;
    }

    private static string Stock(string cashResource, long cash, long iron)
    {
        var parts = new List<string>();
        if (cash > 0) parts.Add("\"" + cashResource + "\": " + cash);
        if (iron > 0) parts.Add("\"econ.good.iron\": " + iron);
        return string.Join(", ", parts);
    }

    private sealed class Rig
    {
        public Reporter Faults { get; } = new();
        public RuntimeSession Session { get; }
        public SimGame Game { get; }
        public EconomyComponent Economy { get; }
        public EconomySimLedger Ledger { get; }
        public List<(string Kind, string Resource, long Amount)> Flows { get; } = new();
        public List<EconomyTradeExecuted> Trades { get; } = new();
        public List<EconomyRefused> Refused { get; } = new();
        private int _request;

        public Rig(string content, EconomySet set, IReadOnlyDictionary<string, int>? holders = null, int linesPerTick = 40)
        {
            var outbox = new SimEventOutbox();
            var tickets = new DirectorTickets();
            var definition = new GameDefinition("economy-lab", new GameModule[] {
                new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new LabModule(),
                new DirectorBridgeModule(tickets, [new StockFlowEffect()], outbox) });
            var files = new DiskFileSource(content);
            var contentSet = ContentSet.Load(files, definition.ContentTypes);
            Game = new GameFactory(definition, contentSet).NewGame(new NewGameRequest { Scenario = "lab", Seed = 3 });
            Session = new RuntimeSession(new RuntimeClock(Game.AuthoredTicksPerSecond), Faults);
            var channels = new EconomyChannels();
            channels.Install(Session);
            var host = Samples.Host();
            Economy = new EconomyComponent(EconomyRuntime.Create(set), new TickEconomyClock(1_000_000), () => host, channels);
            Ledger = new EconomySimLedger("ledger", set, holders ?? new Dictionary<string, int> { ["player"] = 1 }, Held, linesPerTick);
            Session.AddComponent(new SimComponent(Game, null, outbox), 1);
            Session.AddComponent(new SimEffectDispatcher(Game, tickets), 2);
            Session.AddComponent(Economy, 30);
            Session.AddComponent(Ledger, 31);
            Session.Subscribe<ObservationPosted>(e =>
            {
                if (e.Kind != "sim.event.stock-changed") return;
                using var document = JsonDocument.Parse(e.PayloadJson);
                var root = document.RootElement;
                Flows.Add((root.GetProperty("kind").GetString()!, root.GetProperty("resource").GetString()!, root.GetProperty("amount").GetInt64() >> Fix.FractionalBits));
            });
            Session.Subscribe<EconomyTradeExecuted>(Trades.Add);
            Session.Subscribe<EconomyRefused>(Refused.Add);
            Session.Init();
        }

        public long Tick1 => OneTick(Session);

        private static long OneTick(RuntimeSession session) => (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;

        public void Tick(int n = 1)
        {
            for (var i = 0; i < n; i++)
                if (Session.Update(Tick1) != 1) throw new InvalidOperationException("the session did not run (state " + Session.State + "): " + string.Join("; ", Faults.Reports));
        }

        public long Held(int entity, string resource)
        {
            var facets = new List<Facet>();
            if (!Game.Seat(0).View.Inspect(new EntityId(entity), facets)) return 0;
            return facets.Where(f => f.Key == Storage.FacetStock && f.Subject == resource).Select(f => f.Value.Raw >> Fix.FractionalBits).FirstOrDefault();
        }

        public void Trade(string market, string good, long quantity, string trader = "player") => Session.Submit(new EconomyTradeRequested("t" + ++_request, trader, market, good, quantity));
    }

    private Rig Start(long cash = 5000, long iron = 40, IReadOnlyDictionary<string, int>? holders = null, int linesPerTick = 40)
    {
        var set = Samples.Fast();
        return new Rig(WriteContent(set, cash, iron), set, holders, linesPerTick);
    }

    // ---- the vocabulary ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheVocabularyIsGeneratedFromTheDocumentAGoodAndACashResourceForEachAndOneHolderStore()
    {
        var document = JsonNode.Parse(EconomySimContent.Document(Samples.Town))!.AsObject();
        var ids = document["resources"]!.AsArray().Select(r => r!["id"]!.GetValue<string>()).ToList();
        Assert.Equal(["econ.good.grain", "econ.good.cloth", "econ.good.iron", "econ.good.ale", "econ.good.flour", "econ.good.bread", "econ.good.ore", "econ.good.coal", "econ.good.steel", "econ.good.tools", "econ.cash.crown", "econ.cash.florin"], ids);
        Assert.Equal(EconomySimContent.HolderStore, document["capacity_policies"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(2, document["units"]!.AsArray().Count);
        Assert.Equal("econ.good.iron", EconomySimLedger.GoodResource("iron"));
        Assert.Equal("econ.cash.florin", EconomySimLedger.CashResource("florin"));
        Assert.Throws<ArgumentNullException>(() => EconomySimContent.Document(null!));
    }

    [Fact]
    public void TheWorldLoadsWithTheGeneratedVocabularyAndTheHoldersHoldWhatTheyWereGiven()
    {
        var rig = Start(5000, 40);
        rig.Tick(2);
        Assert.Equal(5000, rig.Held(1, "econ.cash.crown"));
        Assert.Equal(40, rig.Held(1, "econ.good.iron"));
        Assert.Equal(0, rig.Held(2, "econ.cash.crown"));
        Assert.Empty(rig.Faults.Reports);
    }

    // ---- trades become ledger flows ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABuyConsumesTheCashAndIssuesTheGoodsAsTheInventorysOwnFlowsAndTheLedgerAgreesWithTheEconomy()
    {
        var rig = Start();
        rig.Tick(2);
        rig.Flows.Clear();
        Assert.True(rig.Ledger.Affordable(rig.Economy.Runtime, "player", "riverton", "grain", 10));
        rig.Trade("riverton", "grain", 10);
        rig.Tick(6);
        var trade = Assert.Single(rig.Trades);
        Assert.Equal(5000 - trade.Total, rig.Held(1, "econ.cash.crown"));
        Assert.Equal(10, rig.Held(1, "econ.good.grain"));
        Assert.Equal(40, rig.Held(1, "econ.good.iron"));
        Assert.Equal([("Consumed", "econ.cash.crown", trade.Total), ("Initialized", "econ.good.grain", 10L)], rig.Flows);
        Assert.Equal(1, rig.Ledger.Recorded);
        Assert.True(rig.Ledger.Settled);
        Assert.Empty(rig.Faults.Reports);
    }

    [Fact]
    public void TheLedgerIsNotSettledWhileItsFlowsAreSentButNotYetApplied()
    {
        var rig = Start();
        rig.Tick(2);
        Assert.True(rig.Ledger.Settled);
        rig.Trade("riverton", "grain", 10);
        rig.Tick(1);
        Assert.Single(rig.Trades);
        Assert.False(rig.Ledger.Settled);          // sent in this tick, the world has not answered yet
        rig.Tick(5);
        Assert.True(rig.Ledger.Settled);
    }

    [Fact]
    public void ATradeWorthLessThanAMinorUnitIssuesTheGoodsAndWritesNoCashLine()
    {
        var rig = Start();
        rig.Tick(2);
        rig.Flows.Clear();
        rig.Economy.Runtime.Markets[0].Pairs[0].Price = 100;           // grain at a tenth of a minor unit: one unit is worth nothing
        rig.Trade("riverton", "grain", 1);
        rig.Tick(6);
        var trade = Assert.Single(rig.Trades);
        Assert.Equal(0, trade.Total);
        Assert.Equal([("Initialized", "econ.good.grain", 1L)], rig.Flows);
        Assert.Equal(5000, rig.Held(1, "econ.cash.crown"));
        Assert.Empty(rig.Faults.Reports);
    }

    [Fact]
    public void ASaleConsumesTheGoodsAndIssuesTheCash()
    {
        var rig = Start();
        rig.Tick(2);
        rig.Flows.Clear();
        rig.Trade("hillfort", "iron", -15);
        rig.Tick(6);
        var trade = Assert.Single(rig.Trades);
        Assert.Equal(25, rig.Held(1, "econ.good.iron"));
        Assert.Equal(5000 + trade.Total, rig.Held(1, "econ.cash.crown"));
        Assert.Equal([("Initialized", "econ.cash.crown", trade.Total), ("Consumed", "econ.good.iron", 15L)], rig.Flows);
    }

    [Fact]
    public void ATradeInAnotherCurrencyUsesThatCurrenciesCashResource()
    {
        var set = Samples.Fast();
        var rig = new Rig(WriteContent(set, 90000, 0, "econ.cash.florin"), set);
        rig.Tick(2);
        rig.Trade("seaport", "cloth", 10);
        rig.Tick(6);
        var trade = Assert.Single(rig.Trades);
        Assert.Equal("florin", trade.Currency);
        Assert.Equal(90000 - trade.Total, rig.Held(1, "econ.cash.florin"));
        Assert.Equal(10, rig.Held(1, "econ.good.cloth"));
    }

    [Fact]
    public void ATraderThatIsNotAHolderIsCountedAndNotWritten()
    {
        var rig = Start();
        rig.Tick(2);
        rig.Flows.Clear();
        rig.Trade("riverton", "grain", 5, trader: "stranger");
        rig.Tick(6);
        Assert.Single(rig.Trades);
        Assert.Equal(1, rig.Ledger.Unrecorded);
        Assert.Equal(0, rig.Ledger.Recorded);
        Assert.Empty(rig.Flows);
        Assert.Equal(5000, rig.Held(1, "econ.cash.crown"));
    }

    [Fact]
    public void WhatDoesNotFitInATickWaitsInOrderAndIsWrittenInTheNextOnes()
    {
        var rig = Start(cash: 100_000, linesPerTick: 2);
        rig.Tick(2);
        rig.Flows.Clear();
        for (var i = 0; i < 6; i++) rig.Trade("riverton", "grain", 1);
        rig.Tick(1);
        Assert.False(rig.Ledger.Settled);
        rig.Tick(30);
        Assert.True(rig.Ledger.Settled);
        Assert.Equal(6, rig.Held(1, "econ.good.grain"));
        Assert.Equal(6, rig.Trades.Count);
        Assert.Equal(100_000 - rig.Trades.Sum(t => t.Total), rig.Held(1, "econ.cash.crown"));
        Assert.Equal(12, rig.Flows.Count);
        Assert.Empty(rig.Faults.Reports);
    }

    // ---- the host asks before it trades -----------------------------------------------------------------------------------------------

    [Fact]
    public void AffordableSaysWhetherTheTradersStorageWouldTakeTheTrade()
    {
        var rig = Start(cash: 30, iron: 5);
        rig.Tick(2);
        var economy = rig.Economy.Runtime;
        Assert.True(rig.Ledger.Affordable(economy, "player", "riverton", "grain", 1));              // about 2 crowns
        Assert.False(rig.Ledger.Affordable(economy, "player", "riverton", "grain", 100));           // far more cash than 30
        Assert.True(rig.Ledger.Affordable(economy, "player", "hillfort", "iron", -5));
        Assert.False(rig.Ledger.Affordable(economy, "player", "hillfort", "iron", -6));             // sells more iron than it holds
        Assert.False(rig.Ledger.Affordable(economy, "stranger", "riverton", "grain", 1));
        Assert.False(rig.Ledger.Affordable(economy, "player", "riverton", "grain", 99_999_999));    // the market itself would refuse
        Assert.False(rig.Ledger.Affordable(economy, "player", "nowhere", "grain", 1));
        Assert.Throws<ArgumentNullException>(() => rig.Ledger.Affordable(null!, "player", "riverton", "grain", 1));
    }

    [Fact]
    public void AnAffordableTradeIsOneWhoseTotalIsExactlyTheCashTheTraderHolds()
    {
        var probe = Start();
        probe.Tick(2);
        var total = probe.Economy.Runtime.Quote("riverton", "grain", 10).Total;
        var exact = Start(cash: total);
        exact.Tick(2);
        Assert.True(exact.Ledger.Affordable(exact.Economy.Runtime, "player", "riverton", "grain", 10));
        exact.Trade("riverton", "grain", 10);
        exact.Tick(6);
        Assert.Equal(0, exact.Held(1, "econ.cash.crown"));
        Assert.Empty(exact.Faults.Reports);
        var short1 = Start(cash: total - 1);
        short1.Tick(2);
        Assert.False(short1.Ledger.Affordable(short1.Economy.Runtime, "player", "riverton", "grain", 10));
    }

    [Fact]
    public void ATradeTheLedgerRefusesFaultsTheSessionBecauseTheEconomyAndTheLedgerNowDisagree()
    {
        var rig = Start(cash: 3);
        rig.Tick(2);
        rig.Trade("riverton", "grain", 10);        // the economy makes the trade; the trader holds 3 cash, not about 22
        for (var i = 0; i < 8 && rig.Session.State != RuntimeState.Faulted; i++) rig.Session.Update(rig.Tick1);
        Assert.Equal(RuntimeState.Faulted, rig.Session.State);
        Assert.Contains(rig.Faults.Reports, r => r.Contains("the economy and the stock ledger disagree", StringComparison.Ordinal));
        Assert.Equal(3, rig.Held(1, "econ.cash.crown"));
        Assert.Equal(0, rig.Held(1, "econ.good.grain"));
    }

    [Fact]
    public void AnAmountTheLedgerCannotCountFaultsTheSessionToo()
    {
        var rig = Start();
        rig.Tick(2);
        rig.Economy.Runtime.Markets[0].Pairs[2].Price = 1_000_000_000_000;       // iron at ten billion a unit: ten units are more than the ledger counts in one flow
        rig.Trade("riverton", "iron", 10);
        for (var i = 0; i < 4 && rig.Session.State != RuntimeState.Faulted; i++) rig.Session.Update(rig.Tick1);
        Assert.Equal(RuntimeState.Faulted, rig.Session.State);
        Assert.Contains(rig.Faults.Reports, r => r.Contains("the ledger counts whole units up to", StringComparison.Ordinal));
    }

    [Fact]
    public void ConstructorArgumentsAreChecked()
    {
        var holders = new Dictionary<string, int> { ["player"] = 1 };
        Assert.Equal(EconomySimLedger.BadHolder, Samples.Code(() => new EconomySimLedger("l", Samples.Town, new Dictionary<string, int> { ["player"] = 0 }, (_, _) => 0)));
        Assert.Throws<ArgumentException>(() => new EconomySimLedger("l", Samples.Town, holders, (_, _) => 0, 0));
        Assert.Throws<ArgumentNullException>(() => new EconomySimLedger("l", null!, holders, (_, _) => 0));
        Assert.Throws<ArgumentNullException>(() => new EconomySimLedger("l", Samples.Town, null!, (_, _) => 0));
        Assert.Throws<ArgumentNullException>(() => new EconomySimLedger("l", Samples.Town, holders, null!));
    }
}
