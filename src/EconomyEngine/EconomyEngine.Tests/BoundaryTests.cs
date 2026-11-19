using System.Numerics;
using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// Tests written from the first round of mutation checking: boundaries (a value exactly at a limit, a count exactly at its maximum), the cases that only a second market or a second schedule can tell apart,
/// the units a trader bought or sold between periods, and the order of the checks of a save. Each one was a mutant that the first suite let live.
/// </summary>
public sealed class BoundaryTests
{
    private static readonly IReadOnlyDictionary<string, FactValue> None = Fixture.NoFacts;

    // ---- the document: values exactly at a limit -----------------------------------------------------------------------------------------

    [Fact]
    public void ADocumentWithValuesExactlyAtItsLimitsLoadsAndOneStepBeyondIsRefused()
    {
        Assert.Equal(1, Mini.Set(root => root["time"]!["periodSteps"] = 1).PeriodSteps);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => Mini.Set(root => root["time"]!["periodSteps"] = 0)));
        Assert.Equal(1000, Mini.Set(root => Mini.ThePrice(root)["responseMax"] = 1000).Price.ResponseMax);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => Mini.Set(root => Mini.ThePrice(root)["responseMax"] = 999)));
        Assert.Equal(64, Mini.Set(root => root["limits"]!["history"] = 64).Limits.History);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => Mini.Set(root => root["limits"]!["history"] = 65)));
        Assert.Equal(0, Mini.Set(root => root["limits"]!["history"] = 0).Limits.History);
        Assert.Single(Mini.Set(root =>
        {
            root["limits"]!["maxLoans"] = 1;
            root["loans"]!.AsArray().Add(new JsonObject { ["id"] = "l", ["lender"] = "world", ["borrower"] = "m", ["principal"] = 10, ["ratePpm"] = 0, ["term"] = 2, ["kind"] = "bullet", ["schedule"] = "pay", ["defaultAfter"] = 1 });
        }).Loans);
    }

    [Fact]
    public void AFactWhoseMinimumEqualsItsMaximumIsFineAndOneThatIsBackwardsIsNot()
    {
        var set = Mini.Set(root => root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "flag", ["type"] = "int", ["min"] = 3, ["max"] = 3 }));
        Assert.Equal(new FactDefinition(FactKind.Integer, 3, 3), set.HostFacts["flag"]);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => Mini.Set(root => root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "flag", ["type"] = "int", ["min"] = 4, ["max"] = 3 }))));
    }

    [Fact]
    public void TwoBandsThatStartAtTheSameRatioAndASubsidyWithAScheduleAreRefused()
    {
        Assert.Equal(EconomyCodes.Band, Fixture.Refusal(() => Mini.Set(root => root["bands"]!.AsArray()[2]!["from"] = 900)));
        Assert.Equal(EconomyCodes.Policy, Fixture.Refusal(() => Mini.Set(root => root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "aid", ["kind"] = "subsidy", ["rate"] = 0, ["perUnit"] = 5, ["tags"] = new JsonArray(), ["schedule"] = "pay" }))));
    }

    [Fact]
    public void AFactAtTheTopOfItsDeclaredRangeIsAccepted()
    {
        var runtime = Mini.Runtime(root =>
        {
            Mini.TheGood(root)["speed"] = 0;
            root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "plague", ["type"] = "int", ["min"] = 0, ["max"] = 3 });
            root["modifiers"]!.AsArray().Add(new JsonObject { ["id"] = "sick", ["kind"] = "supply", ["good"] = "", ["tag"] = "", ["permille"] = -100, ["when"] = new JsonObject { ["fact"] = "plague", ["op"] = "gte", ["value"] = 3 } });
        });
        runtime.Advance(1, new Dictionary<string, FactValue> { ["plague"] = FactValue.From(3L) });
        Assert.Equal(90, runtime.Markets[0].Pairs[0].Supply);
        runtime.Advance(2, new Dictionary<string, FactValue> { ["plague"] = FactValue.From(0L) });
        Assert.Equal(100, runtime.Markets[0].Pairs[0].Supply);
    }

    // ---- a document with no price history ---------------------------------------------------------------------------------------------

    [Fact]
    public void ADocumentThatKeepsNoHistoryRunsAndHasNone()
    {
        var runtime = Mini.Runtime(root => root["limits"]!["history"] = 0);
        runtime.Advance(5, None);
        Assert.Empty(runtime.PriceHistory(0, 0));
        Assert.Empty(runtime.Markets[0].History);
        Assert.Equal(5, runtime.Period);
    }

    // ---- units traded between periods ------------------------------------------------------------------------------------------------

    /// <summary>The oracle of <see cref="PriceOracle"/> with units a trader bought (<paramref name="spotDemand"/>) or sold (<paramref name="spotSupply"/>) since the last period.</summary>
    private static (long Price, long Change) Spot(PairCase c, long price, long stock, long spotDemand, long spotSupply)
    {
        BigInteger Clamp(BigInteger v, BigInteger lo, BigInteger hi) => v < lo ? lo : v > hi ? hi : v;
        BigInteger ratio = (BigInteger)price * 1000 / 10_000;
        BigInteger sr = Clamp(1000 + (BigInteger)c.SupplyElasticity * (ratio - 1000) / 1000, 200, 3000);
        BigInteger dr = Clamp(1000 - (BigInteger)c.DemandElasticity * (ratio - 1000) / 1000, 200, 3000);
        BigInteger supply = (BigInteger)c.Production * sr / 1000;
        BigInteger demand = (BigInteger)c.Consumption * dr / 1000;
        BigInteger held = stock - (BigInteger)stock * c.Decay / 1000;
        BigInteger met = BigInteger.Min(demand, held + supply);
        BigInteger newStock = BigInteger.Min(c.Capacity, held + supply - met);
        BigInteger gap = (BigInteger)c.Cover * c.Consumption - newStock;
        BigInteger d = demand + spotDemand;
        BigInteger s = supply + spotSupply;
        BigInteger excess = d - s + gap / 4;
        BigInteger norm = BigInteger.Max(1, (d + s + 1) / 2);
        BigInteger imbalance = Clamp(excess * 1000 / norm, -1000, 1000);
        BigInteger raw = imbalance * c.Speed / 1000;
        BigInteger change = Clamp(raw * (1000 - c.Inertia) / 1000, -c.MaxStep, c.MaxStep);
        BigInteger low = BigInteger.Max(1, (BigInteger)10_000 * c.Floor / 1000);
        BigInteger high = (BigInteger)10_000 * c.Ceiling / 1000;
        return ((long)Clamp(price * (1000 + change) / 1000, low, BigInteger.Max(low, high)), (long)change);
    }

    [Fact]
    public void UnitsBoughtOrSoldBetweenPeriodsMoveThePriceAsTheOracleSays()
    {
        var random = new System.Random(404);
        for (var n = 0; n < 120; n++)
        {
            var c = new PairCase(random.Next(0, 300), random.Next(1, 300), random.Next(400, 3000), random.Next(0, 4), 5000, random.Next(0, 2000), random.Next(0, 2000), random.Next(200, 1001), random.Next(100, 400), random.Next(0, 500), random.Next(0, 100), 100, 4000);
            var runtime = Mini.Runtime(root => PriceOracle.Apply(root, c));
            var buy = random.Next(0, 300);
            var sell = random.Next(0, 300);
            if (buy > 0) runtime.Trade("m", "g", buy);
            if (sell > 0) runtime.Trade("m", "g", -sell);
            var pair = runtime.Markets[0].Pairs[0];
            var expected = Spot(c, 10_000, pair.Stock, buy, sell);
            runtime.Advance(1, None);
            Assert.True(expected.Price == pair.Price && expected.Change == pair.LastChange, "case " + n + ": expected " + expected + ", got " + pair.Price + " / " + pair.LastChange);
        }
    }

    // ---- freight, margin and the loading rule at their edges --------------------------------------------------------------------------------

    private static EconomyRuntime Route(int margin, long costPerWeight = 100, int weight = 3, int share = 1000)
    {
        return Mini.Runtime(root =>
        {
            Mini.TheGood(root)["speed"] = 0;
            Mini.TheGood(root)["weight"] = weight;
            Mini.ThePair(root)["production"] = 100; Mini.ThePair(root)["consumption"] = 100; Mini.ThePair(root)["stock"] = 1000;
            root["markets"]!.AsArray().Add(Mini.Market("n", "c", Mini.Pair("g", 100, 100, 100)));
            root["routes"]!.AsArray().Add(new JsonObject { ["id"] = "r", ["from"] = "m", ["to"] = "n", ["delay"] = 2, ["capacity"] = 5000, ["costPerWeight"] = costPerWeight, ["margin"] = margin, ["share"] = share });
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "spike", ["key"] = "k.s", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = 100 } } });
        });
    }

    [Theory]
    [InlineData(70, true)]       // price 11,000 vs 10,000, freight 3 x 100: the gain is 700, 70 per mille of 10,000: exactly the margin
    [InlineData(71, false)]
    [InlineData(0, true)]
    public void MerchantsLoadWhenTheGainIsExactlyTheMarginAndNotWhenItIsOneShortOfIt(int margin, bool loads)
    {
        var runtime = Route(margin);
        runtime.ApplyShock("spike", "n", 1000);
        Assert.Equal(11_000, runtime.Markets[1].Pairs[0].Price);
        var changes = runtime.Advance(1, None);
        Assert.Equal(loads, changes.Launches.Count == 1);
    }

    [Fact]
    public void TheFreightIsTheWeightTimesTheCostPerWeight()
    {
        // weight 3 at 250 a unit: 750; the gain is 250, 25 per mille. Weight 3 at 100: 300 and 70. A freight of weight plus cost (253) would leave 747.
        var dear = Route(25, costPerWeight: 250);
        dear.ApplyShock("spike", "n", 1000);
        Assert.Single(dear.Advance(1, None).Launches);
        var dearer = Route(26, costPerWeight: 250);
        dearer.ApplyShock("spike", "n", 1000);
        Assert.Empty(dearer.Advance(1, None).Launches);
    }

    // ---- schedules told apart ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATaxIsRemittedOnlyWhenItsOwnScheduleFiresNotWhenAnotherIsSettled()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "tax", ["kind"] = "sales", ["rate"] = 100, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "pay" });
            Mini.TheMarket(root)["policies"]!.AsArray().Add("tax");
            Mini.TheMarket(root)["staff"] = 1;
            Mini.TheMarket(root)["budget"]!.AsArray().Add(new JsonObject { ["id"] = "w", ["kind"] = "wage", ["schedule"] = "hand", ["amount"] = 10, ["perHead"] = 0, ["indexed"] = false });
        });
        runtime.Advance(2, None);
        var accrued = runtime.Markets[0].Accrued[0];
        Assert.True(accrued > 0);
        runtime.Settle("hand");
        Assert.Equal(accrued, runtime.Markets[0].Accrued[0]);        // the wage line was paid, the tax (on schedule 'pay') was not remitted
        Assert.Equal(10_000 - 10, runtime.Markets[0].Treasury);
        runtime.Advance(3, None);
        Assert.Equal(0, runtime.Markets[0].Accrued[0]);
    }

    [Fact]
    public void ALoanPaymentTheTreasuryCoversExactlyIsMade()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["markets"]!.AsArray().Add(Mini.Market("l", "c"));
            Mini.TheMarket(root)["treasury"] = 110;
            root["loans"]!.AsArray().Add(new JsonObject { ["id"] = "x", ["lender"] = "l", ["borrower"] = "m", ["principal"] = 100, ["ratePpm"] = 100_000, ["term"] = 1, ["kind"] = "bullet", ["schedule"] = "pay", ["defaultAfter"] = 2 });
        });
        var changes = Fixture.Periods(runtime, 3, None);
        Assert.Equal(new Settled(0, SettleKind.Loan, "x", 110, 110), Assert.Single(changes.Settlements, s => s.Kind == SettleKind.Loan));
        Assert.Equal(0, runtime.Markets[0].Treasury);
        Assert.Equal(LoanStatus.Closed, runtime.Loans[0].Status);
    }

    [Fact]
    public void ALoanThatIsPaidOffBeforeItsLastPeriodIsClosedAtOnce()
    {
        var runtime = Mini.Runtime(root =>
            root["loans"]!.AsArray().Add(new JsonObject { ["id"] = "x", ["lender"] = "world", ["borrower"] = "m", ["principal"] = 6, ["ratePpm"] = 0, ["term"] = 4, ["kind"] = "annuity", ["schedule"] = "pay", ["defaultAfter"] = 2 }));
        Assert.Equal(2, runtime.Loans[0].Payment);                    // 6 over 4 is 1.5, rounded to 2: three payments clear it
        Fixture.Periods(runtime, 9, None);
        Assert.Equal(0, runtime.Loans[0].Balance);
        Assert.Equal(LoanStatus.Closed, runtime.Loans[0].Status);
        Assert.Equal(1, runtime.Loans[0].Remaining);
    }

    // ---- lending: the first market, the edges --------------------------------------------------------------------------------------------

    [Fact]
    public void TheFirstMarketOfTheDocumentCanBeTheLenderAndAllItsTreasuryCanBeLent()
    {
        var runtime = Mini.Runtime(root => root["markets"]!.AsArray().Add(Mini.Market("l", "c")));
        runtime.Lend("a", "m", "l", 10_000, 0, 2, LoanKind.Bullet, "pay", 2);
        Assert.Equal(0, runtime.Markets[0].Treasury);
        Assert.Equal(20_000, runtime.Markets[1].Treasury);
        Assert.Equal(0, runtime.Currencies[0].World);
        Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void ALoanFromTheFirstMarketOfTheDocumentIsServicedIntoItsTreasuryNotTheWorldAccount()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["markets"]!.AsArray().Add(Mini.Market("l", "c"));
            root["loans"]!.AsArray().Add(new JsonObject { ["id"] = "x", ["lender"] = "m", ["borrower"] = "l", ["principal"] = 100, ["ratePpm"] = 100_000, ["term"] = 2, ["kind"] = "bullet", ["schedule"] = "pay", ["defaultAfter"] = 2 });
        });
        Fixture.Periods(runtime, 3, None);
        Assert.Equal(10_010, runtime.Markets[0].Treasury);
        Assert.Equal(9990, runtime.Markets[1].Treasury);
        Assert.Equal(0, runtime.Currencies[0].World);
    }

    [Fact]
    public void ALoanAtTheLargestTermAndDefaultLimitIsAcceptedAndABorrowerMayEndAtTheMostMoneyThereIs()
    {
        var runtime = Mini.Runtime();
        runtime.Lend("long", "world", "m", 10, 0, 1200, LoanKind.Bullet, "pay", 100);
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Lend("longer", "world", "m", 10, 0, 1201, LoanKind.Bullet, "pay", 100)));
        Assert.Equal(EconomyCodes.Range, Fixture.Refusal(() => runtime.Lend("patient", "world", "m", 10, 0, 5, LoanKind.Bullet, "pay", 101)));
        var edge = Mini.Runtime();
        edge.Fund("m", EconomyFacts.MaxMoney - 10_000 - 5);
        edge.Lend("fills", "world", "m", 5, 0, 2, LoanKind.Bullet, "pay", 2);
        Assert.Equal(EconomyFacts.MaxMoney, edge.Markets[0].Treasury);
        Assert.Equal(EconomyCodes.Funds, Fixture.Refusal(() => edge.Lend("over", "world", "m", 1, 0, 2, LoanKind.Bullet, "pay", 2)));
    }

    [Fact]
    public void GrowthAtTheEdgeOfItsRangeAndATradeOfTheLargestSizeAreRefusedForTheRightReason()
    {
        var runtime = Mini.Runtime();
        runtime.SetGrowth("c", 500_000);
        runtime.SetGrowth("c", -500_000);
        Assert.Equal(EconomyCodes.Stock, Fixture.Refusal(() => runtime.Trade("m", "g", EconomyReader.MaxQuantity)));
        Assert.Equal(EconomyCodes.Capacity, Fixture.Refusal(() => runtime.Trade("m", "g", -EconomyReader.MaxQuantity)));
    }

    // ---- shocks aimed at one market ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AMoneyGrowthShockOnOneMarketRaisesOnlyThePriceLevelOfItsCurrency()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["currencies"]!.AsArray().Add(new JsonObject { ["id"] = "d", ["key"] = "k.d", ["reference"] = false, ["anchor"] = 500_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 });
            root["markets"]!.AsArray().Add(Mini.Market("n", "d"));
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "press", ["key"] = "k.p", ["duration"] = 3, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "growth", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = 50_000 } } });
        });
        runtime.ApplyShock("press", "n", 1000);
        runtime.Advance(1, None);
        Assert.Equal(1_000_000, runtime.Currencies[0].Level);
        Assert.Equal(1_050_000, runtime.Currencies[1].Level);
    }

    [Fact]
    public void APriceShockOnOneMarketMovesOnlyThatMarketsPrice()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["markets"]!.AsArray().Add(Mini.Market("n", "c"));
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "spike", ["key"] = "k.s", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = 100 } } });
        });
        runtime.ApplyShock("spike", "n", 1000);
        Assert.Equal(10_000, runtime.Markets[0].Pairs[0].Price);
        Assert.Equal(11_000, runtime.Markets[1].Pairs[0].Price);
    }

    // ---- a subsidy the treasury cannot cover in full -------------------------------------------------------------------------------------

    [Fact]
    public void AnUnderFundedSubsidyOffersTheProportionTheTreasuryCanPayExactly()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "aid", ["kind"] = "subsidy", ["rate"] = 0, ["perUnit"] = 2000, ["tags"] = new JsonArray(), ["schedule"] = "" });
            Mini.TheMarket(root)["policies"]!.AsArray().Add("aid");
            Mini.TheMarket(root)["treasury"] = 50;
            Mini.ThePair(root)["production"] = 100;
        });
        var changes = runtime.Advance(1, None);
        // Wanted 200; the treasury holds 50, so a quarter of 2,000 is offered: 500 a unit, producer price 10,500, supply response 1050 per mille, supply 105; due 53, paid 50.
        Assert.Equal(105, runtime.Markets[0].Pairs[0].Supply);
        Assert.Equal(new Settled(0, SettleKind.Subsidy, "aid", 50, 53), Assert.Single(changes.Settlements));
    }

    [Fact]
    public void TheExplanationOfABalancedPairHasNoImbalanceAndOneWithTooMuchStockHasASurplusPressure()
    {
        var balanced = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 100; p["consumption"] = 100; p["stock"] = 200; p["cover"] = 2; });
        var terms = EconomyExplain.Price(balanced, "m", "g").Terms;
        Assert.Equal(0, terms.Single(t => t.Kind == "imbalance").Value);
        Assert.Equal(0, terms.Single(t => t.Kind == "next-change").Value);
        var heavy = Mini.Runtime(root => { var p = Mini.ThePair(root); p["production"] = 100; p["consumption"] = 100; p["stock"] = 600; p["cover"] = 2; });
        Assert.True(EconomyExplain.Price(heavy, "m", "g").Terms.Single(t => t.Kind == "imbalance").Value < 0);
    }

    // ---- a save: extremes that are valid, the order of the checks ------------------------------------------------------------------------

    [Fact]
    public void TheMostDeflatedAndMostDevaluedStatesTheEngineCanReachRoundTripAsValid()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["currencies"]!.AsArray()[0]!["growthPpm"] = -500_000;
            root["currencies"]!.AsArray()[0]!["money"] = 1_000_000_000;
            root["currencies"]!.AsArray().Add(new JsonObject { ["id"] = "d", ["key"] = "k.d", ["reference"] = false, ["anchor"] = 500_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 });
            root["markets"]!.AsArray().Add(Mini.Market("n", "d"));
            root["limits"]!["maxShocks"] = 20;
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "crash", ["key"] = "k.c", ["duration"] = 100, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "rate", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = -900 } } });
        });
        for (var period = 1; period <= 60; period++) runtime.Advance(period, None);
        for (var i = 0; i < 8; i++) runtime.ApplyShock("crash", "n", 1000);
        Assert.Equal(1, runtime.Currencies[0].Level);
        Assert.Equal(1, runtime.Currencies[1].Rate);
        var opened = EconomySave.Open(runtime.Set, EconomySave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        var growing = Mini.Runtime(root => root["currencies"]!.AsArray()[0]!["growthPpm"] = 500_000);
        growing.SetGrowth("c", 500_000);
        var forged = Tamper.Forge(EconomySave.Write(growing), image => image.Currencies[0].Growth = 500_000);
        Assert.Equal(500_000, EconomySave.Open(growing.Set, forged).Currencies[0].Growth);
    }

    [Fact]
    public void ASaveWithExactlyTheMostShipmentsTheDocumentAllowsIsAccepted()
    {
        var set = Mini.Set(root =>
        {
            root["limits"]!["maxShipments"] = 3;
            root["markets"]!.AsArray().Add(Mini.Market("n", "c"));
            root["routes"]!.AsArray().Add(new JsonObject { ["id"] = "r", ["from"] = "m", ["to"] = "n", ["delay"] = 2, ["capacity"] = 10, ["costPerWeight"] = 0, ["margin"] = 0, ["share"] = 500 });
        });
        var good = EconomySave.Write(EconomyRuntime.Create(set));
        var three = Tamper.Forge(good, i => { for (var n = 0; n < 3; n++) i.Shipments.Add(new ImageShipment { Route = 0, Good = 0, Quantity = 1, Arrives = 1 }); });
        Assert.Equal(3, EconomySave.Open(set, three).Shipments.Count);
        var four = Tamper.Forge(good, i => { for (var n = 0; n < 4; n++) i.Shipments.Add(new ImageShipment { Route = 0, Good = 0, Quantity = 1, Arrives = 1 }); });
        Assert.Equal(EconomyCodes.SaveState, Assert.Throws<EngineCommon.EngineRefusedException>(() => EconomySave.Open(set, four)).Code);
    }

    private static readonly HashSet<string> CheckedAfterTheDigest =
    [
        "money from nowhere", "money vanished", "money in a treasury from nowhere", "a price under its floor", "a price over its ceiling", "a price just over its ceiling", "a band its price does not give",
    ];

    public static IEnumerable<object[]> StaleForgeries() => SaveTests.ForgeryNames().Select((name, index) => new object[] { index, name });

    [Theory]
    [MemberData(nameof(StaleForgeries))]
    public void ARangeViolationIsRefusedAsStateEvenWithTheOldDigestButWhatNeedsTheDocumentIsOnlyCheckedAfterIt(int index, string name)
    {
        var good = EconomySave.Write(Tamper.Played());
        var stale = Tamper.Stale(good, SaveTests.ForgeryEdit(index));
        var code = Assert.Throws<EngineCommon.EngineRefusedException>(() => EconomySave.Open(Fixture.Town, stale)).Code;
        var expected = CheckedAfterTheDigest.Contains(name) ? EconomyCodes.SaveDigest : EconomyCodes.SaveState;
        Assert.True(expected == code, name + " with a stale digest was refused as " + code + ", expected " + expected);
    }
}
