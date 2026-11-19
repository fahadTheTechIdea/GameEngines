using System.Text.Json.Nodes;
using DiplomacyEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The economy and the diplomacy: shocks go through the economy's own shock path and payments through its fund path, addressed to the right markets; the documents are checked against each other; the economy never reads the diplomacy.</summary>
public sealed class EconomyIntegrationTests
{
    private static EconomySet Isles(Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(Samples.Read("isles.economy.json"))!.AsObject();
        edit?.Invoke(root);
        return EconomyLoader.Load(root.ToJsonString());
    }

    /// <summary>The isles economy without the fifth market and the routes that touch it.</summary>
    private static EconomySet WithoutEmber() => Isles(r =>
    {
        r["markets"]!.AsArray().RemoveAt(4);
        var routes = r["routes"]!.AsArray();
        for (var i = routes.Count - 1; i >= 0; i--)
            if (routes[i]!["from"]!.GetValue<string>() == "ember-market" || routes[i]!["to"]!.GetValue<string>() == "ember-market") routes.RemoveAt(i);
    });

    private static readonly Dictionary<string, FactValue> EconomyHost = new(StringComparer.Ordinal) { ["cal.season"] = FactValue.From("spring"), ["story.plague"] = FactValue.From(0L) };

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public DiplomacyComponent Diplomacy { get; }
        public EconomyComponent Economy { get; }
        public DiplomacyEconomy Bridge { get; }
        public List<EconomyShockRequested> Shocks { get; } = new();
        public List<EconomyShockChanged> Changed { get; } = new();
        public List<EconomyFundRequested> Funds { get; } = new();
        public List<EconomyRefused> Refused { get; } = new();
        public List<DiplomacyObligationChanged> Obligations { get; } = new();

        public Rig(DipSet? diplomacy = null, EconomySet? economy = null, string diplomacyChannel = "", string economyChannel = "", string bridgeDiplomacyChannel = "", string bridgeEconomyChannel = "", bool bridge = true)
        {
            var dip = diplomacy ?? Samples.Calm();
            var eco = economy ?? Isles();
            var diplomacyChannels = new DiplomacyChannels();
            diplomacyChannels.Install(Session);
            var economyChannels = new EconomyChannels();
            economyChannels.Install(Session);
            var runtime = DipRuntime.Create(dip);
            Diplomacy = new DiplomacyComponent(runtime, new TickDiplomacyClock(dip), () => Samples.Crisis(), diplomacyChannels, channel: diplomacyChannel);
            Economy = new EconomyComponent(EconomyRuntime.Create(eco), new TickEconomyClock(1_000_000), () => EconomyHost, economyChannels, channel: economyChannel);
            Bridge = new DiplomacyEconomy("dip-econ", dip, eco, bridgeDiplomacyChannel, bridgeEconomyChannel);
            Session.AddComponent(Diplomacy, 10);
            if (bridge) Session.AddComponent(Bridge, 15);
            Session.AddComponent(Economy, 30);
            Session.Subscribe<EconomyShockRequested>(Shocks.Add);
            Session.Subscribe<EconomyShockChanged>(Changed.Add);
            Session.Subscribe<EconomyFundRequested>(Funds.Add);
            Session.Subscribe<EconomyRefused>(Refused.Add);
            Session.Subscribe<DiplomacyObligationChanged>(Obligations.Add);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }

        public void Ask(string kind, string a = "", string b = "", string c = "", long number = 0, long number2 = 0, string channel = "") =>
            Session.Submit(new DiplomacyRequested("t" + Shocks.Count + kind + a + b, kind, a, b, c, number, number2, channel));

        public long Treasury(string market) => Economy.Runtime.Markets[Economy.Runtime.Set.MarketById[market].Index].Treasury;
    }

    // ---- the two documents fit each other -------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleDiplomacyFitsTheSampleEconomy() => DiplomacyEconomy.Check(Samples.Isles, Isles());

    [Fact]
    public void ADocumentThatAsksForWhatTheEconomyDoesNotHaveIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(DipSet set, EconomySet economy, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => DiplomacyEconomy.Check(set, economy));
            Assert.Equal(DiplomacyEconomy.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(Samples.Isles, Isles(r => { var shocks = r["shocks"]!.AsArray(); shocks.RemoveAt(shocks.Select((n, i) => (n, i)).First(p => p.n!["id"]!.GetValue<string>() == "sabotage").i); }), "asks for the shock 'sabotage'");
        Refused(Samples.Isles, WithoutEmber(), "realm 'ember' names the market 'ember-market'");
        DiplomacyEconomy.Check(Samples.Calm(r => Samples.Realm(r, "ember")["market"] = ""), WithoutEmber());
        Assert.Throws<ArgumentNullException>(() => new DiplomacyEconomy("x", null!, Isles()));
        Assert.Throws<ArgumentNullException>(() => new DiplomacyEconomy("x", Samples.Isles, null!));
    }

    // ---- shocks -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnEmbargoReachesTheEconomyAsAShockOnTheMarketOfTheRealmItIsImposedOn()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Ask("propose", "aurel", "brass", "embargo", 30);
        rig.Tick(4);
        var shock = Assert.Single(rig.Shocks);
        Assert.Equal(("embargo", "brass-market", 800), (shock.Shock, shock.Market, shock.ScalePermille));
        var changed = Assert.Single(rig.Changed);
        Assert.Equal(("embargo", "brass-market", true), (changed.Shock, changed.Market, changed.Started));
        Assert.Equal(1, rig.Bridge.Requested);
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void ATradeTreatyAsksABoomForBothMarketsAndABustWhenItEnds()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Ask("propose", "aurel", "dune", "trade", 30);
        rig.Tick(45);
        Assert.Equal([("trade-boom", "aurel-market"), ("trade-boom", "dune-market"), ("trade-bust", "aurel-market"), ("trade-bust", "dune-market")], rig.Shocks.Select(s => (s.Shock, s.Market)).ToArray());
        Assert.Equal([500, 500, 300, 300], rig.Shocks.Select(s => s.ScalePermille).ToArray());
        Assert.Equal(4, rig.Changed.Count(c => c.Started));
    }

    [Fact]
    public void ASabotageShockIsAskedForTheTargetsMarket()
    {
        // A sabotage can fail by chance; the first seed on which it succeeds shows the shock.
        Rig? found = null;
        for (long seed = 1; seed < 300 && found is null; seed++)
        {
            var rig = new Rig(Samples.Calm(root => root["seed"] = seed));
            rig.Tick();
            rig.Session.Submit(new DiplomacyRequested("m", "mission", "agent-aurel-1", "brass", "sabotage"));
            rig.Tick(14);
            if (rig.Shocks.Count == 1) found = rig;
        }
        var shock = Assert.Single(found!.Shocks);
        Assert.Equal(("sabotage", "brass-market", 1000), (shock.Shock, shock.Market, shock.ScalePermille));
    }

    [Fact]
    public void ARealmWithNoMarketHasNoEconomyToShock()
    {
        var rig = new Rig(Samples.Calm(r => Samples.Realm(r, "brass")["market"] = ""), Isles());
        rig.Tick();
        rig.Ask("propose", "aurel", "brass", "embargo", 30);
        rig.Tick(5);
        Assert.Empty(rig.Shocks);
        Assert.Equal(1, rig.Bridge.Skipped);
        Assert.Equal(0, rig.Bridge.Requested);
    }

    [Fact]
    public void OnlyTheDiplomacyOnTheBridgesChannelIsListenedTo()
    {
        var rig = new Rig(diplomacyChannel: "north", bridgeDiplomacyChannel: "south");
        rig.Tick();
        rig.Ask("propose", "aurel", "brass", "embargo", 30, channel: "north");
        rig.Tick(5);
        Assert.Empty(rig.Shocks);
        var listening = new Rig(diplomacyChannel: "north", bridgeDiplomacyChannel: "north");
        listening.Tick();
        listening.Ask("propose", "aurel", "brass", "embargo", 30, channel: "north");
        listening.Tick(5);
        Assert.Single(listening.Shocks);
    }

    [Fact]
    public void TheEconomyOfAnotherChannelIsAskedByTheBridgesEconomyChannel()
    {
        var rig = new Rig(economyChannel: "east", bridgeEconomyChannel: "east");
        rig.Tick();
        rig.Ask("propose", "aurel", "brass", "embargo", 30);
        rig.Tick(5);
        Assert.Equal("east", Assert.Single(rig.Shocks).Channel);
        Assert.Single(rig.Changed);
    }

    // ---- payments ---------------------------------------------------------------------------------------------------------------------

    private static void UntilDue(Rig rig)
    {
        for (var i = 0; i < 80 && !rig.Obligations.Any(); i++) rig.Tick();
    }

    [Fact]
    public void ATributeFallingDueMovesMoneyFromThePayersTreasuryToThePayeesAndIsReportedPaid()
    {
        var rig = new Rig();
        rig.Tick();
        rig.Ask("propose", "brass", "dune", "vassalage", 0, 1000);
        var before = (rig.Treasury("dune-market"), rig.Treasury("brass-market"));
        UntilDue(rig);
        rig.Tick(8);
        Assert.Equal((before.Item1 - 1000, before.Item2 + 1000), (rig.Treasury("dune-market"), rig.Treasury("brass-market")));
        Assert.Equal(["due", "paid"], rig.Obligations.Select(o => o.Change).ToArray());
        Assert.Equal(1, rig.Bridge.Paid);
        Assert.Equal(0, rig.Bridge.Failed);
        Assert.Equal([-1000L, 1000L], rig.Funds.Select(f => f.Delta).ToArray());
        Assert.Equal(["dune-market", "brass-market"], rig.Funds.Select(f => f.Market).ToArray());
        Assert.Equal(0, rig.Diplomacy.Runtime.Treaties[0].Missed);
    }

    [Fact]
    public void APayerWhoCannotPayIsReportedUnpaidAndTheTreasuriesAreAsTheyWere()
    {
        var rig = new Rig(economy: Isles(r => r["markets"]!.AsArray()[3]!["treasury"] = 300));
        rig.Tick();
        rig.Ask("propose", "brass", "dune", "vassalage", 0, 1000);
        UntilDue(rig);
        rig.Tick(8);
        Assert.Equal(300, rig.Treasury("dune-market"));
        Assert.Equal(["due", "missed"], rig.Obligations.Select(o => o.Change).ToArray());
        Assert.Equal(1, rig.Bridge.Failed);
        Assert.Equal(0, rig.Bridge.Paid);
        Assert.Equal(DiplomacyEconomyCodes.Funds, Assert.Single(rig.Refused).Code);
        Assert.Equal(1, rig.Diplomacy.Runtime.Treaties[0].Missed);
    }

    [Fact]
    public void APayeeWhoCannotTakeTheMoneyGivesItBackToThePayerAndThePaymentIsNotMade()
    {
        var rich = EconomyFacts.MaxMoney - 500;
        var rig = new Rig(economy: Isles(r => r["markets"]!.AsArray()[1]!["treasury"] = rich));
        rig.Tick();
        rig.Ask("propose", "brass", "dune", "vassalage", 0, 1000);
        var dune = rig.Treasury("dune-market");
        UntilDue(rig);
        rig.Tick(10);
        Assert.Equal(dune, rig.Treasury("dune-market"));
        Assert.Equal(rich, rig.Treasury("brass-market"));
        Assert.Equal(["due", "missed"], rig.Obligations.Select(o => o.Change).ToArray());
        Assert.Equal([-1000L, 1000L, 1000L], rig.Funds.Select(f => f.Delta).ToArray());
        Assert.Equal(1, rig.Bridge.Failed);
    }

    [Fact]
    public void APaymentBetweenRealmsWithoutMarketsIsLeftForTheHostAndMissedWhenNotSettled()
    {
        var rig = new Rig(Samples.Calm(r => Samples.Realm(r, "dune")["market"] = ""), Isles());
        rig.Tick();
        rig.Ask("propose", "brass", "dune", "vassalage", 0, 1000);
        UntilDue(rig);
        rig.Tick(10);
        Assert.Empty(rig.Funds);
        Assert.Equal(1, rig.Bridge.Skipped);
        Assert.Contains("missed", rig.Obligations.Select(o => o.Change));
    }

    [Fact]
    public void TheBridgeNeedsNoCallIntoTheEconomyOrTheDiplomacy()
    {
        var rig = new Rig(bridge: false);
        rig.Tick();
        rig.Ask("propose", "aurel", "brass", "embargo", 30);
        rig.Tick(5);
        Assert.Empty(rig.Shocks);
        Assert.Empty(rig.Changed);
    }
}

/// <summary>The economy's own code for a treasury that cannot take a movement, which the payment test names.</summary>
internal static class DiplomacyEconomyCodes
{
    public const string Funds = EconomyCodes.Funds;
}
