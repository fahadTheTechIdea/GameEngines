using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Trade routes: when merchants load, how much, what arrives and when, what limits and tariffs do.</summary>
public sealed class RouteTests
{
    private static JsonObject RouteDoc(string id = "r", string from = "m", string to = "n", int delay = 2, long capacity = 1000, long cost = 100, int margin = 80, int share = 500) => new()
    {
        ["id"] = id, ["from"] = from, ["to"] = to, ["delay"] = delay, ["capacity"] = capacity, ["costPerWeight"] = cost, ["margin"] = margin, ["share"] = share,
    };

    /// <summary>Market m has plenty (1000 in stock, makes and uses 100); market n has none and uses 100: its price climbs while m's falls.</summary>
    private static EconomyRuntime Two(Action<JsonObject>? edit = null) => Mini.Runtime(root =>
    {
        Mini.ThePair(root)["stock"] = 1000;
        var n = Mini.Market("n", "c", Mini.Pair("g", 0, 100, 0));
        root["markets"]!.AsArray().Add(n);
        root["routes"]!.AsArray().Add(RouteDoc());
        edit?.Invoke(root);
    });

    private static long Priced(long quantity, long price) => EconomyMath.Nearest(quantity, price, 1000);

    [Fact]
    public void MerchantsLoadWhenTheGainBeatsTheMarginAndMoveTheShareOfTheSurplusOrTheGainWhicheverIsLess()
    {
        var runtime = Two();
        var changes = runtime.Advance(1, Fixture.NoFacts);
        var launch = Assert.Single(changes.Launches);
        var from = runtime.Markets[0].Pairs[0];
        var to = runtime.Markets[1].Pairs[0];
        var gain = to.Price - from.Price - 1 * 100;
        var gainRatio = gain * 1000 / from.Price;
        Assert.True(gainRatio >= 80);
        var surplus = from.Stock + launch.Quantity - 100;
        Assert.Equal(surplus * Math.Min(500, gainRatio) / 1000, launch.Quantity);
        Assert.Equal(new ShipmentLaunched(0, 0, launch.Quantity, 3), launch);
        Assert.Equal(launch.Quantity, runtime.Shipments.Sum(s => s.Quantity));
    }

    [Fact]
    public void ShipmentsArriveAfterTheRoutesDelayAndJoinTheDestinationStock()
    {
        var runtime = Two();
        var launched = Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches);
        Assert.Empty(runtime.Advance(2, Fixture.NoFacts).Arrivals);
        var stockBefore = runtime.Markets[1].Pairs[0].Stock;
        var third = runtime.Advance(3, Fixture.NoFacts);
        Assert.Equal(new ShipmentArrived(0, 0, launched.Quantity), Assert.Single(third.Arrivals));
        // The arrived goods are in the stock before the period's own demand is met from it (the destination makes nothing).
        var pair = runtime.Markets[1].Pairs[0];
        Assert.Equal(stockBefore + launched.Quantity, pair.Stock + pair.Met);
        Assert.DoesNotContain(runtime.Shipments, s => s.Arrives == 3);
    }

    [Fact]
    public void ARoutesCapacityLimitsWhatItCarriesInAPeriodAcrossAllGoods()
    {
        var runtime = Two(root =>
        {
            root["routes"]!.AsArray()[0]!["capacity"] = 30;
            root["routes"]!.AsArray()[0]!["share"] = 1000;
        });
        var changes = runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(30, Assert.Single(changes.Launches).Quantity);
        var two = Mini.Runtime(root =>
        {
            Mini.ThePair(root)["stock"] = 1000;
            root["goods"]!.AsArray().Add(Mini.Good("h"));
            Mini.TheMarket(root)["pairs"]!.AsArray().Add(Mini.Pair("h", 100, 100, 1000));
            root["markets"]!.AsArray().Add(Mini.Market("n", "c", Mini.Pair("g", 0, 100, 0), Mini.Pair("h", 0, 100, 0)));
            root["routes"]!.AsArray().Add(RouteDoc(capacity: 50, share: 1000));
        });
        var both = two.Advance(1, Fixture.NoFacts);
        Assert.Equal(50, both.Launches.Sum(l => l.Quantity));
    }

    [Theory]
    [InlineData("margin", 10000)]            // the gain never reaches the margin
    [InlineData("costPerWeight", 50000)]     // the freight eats the gain
    [InlineData("capacity", 0)]              // nothing can be carried
    public void NothingIsLoadedWhenTheMarginTheFreightOrTheCapacityForbidsIt(string field, long value)
    {
        var runtime = Two(root => root["routes"]!.AsArray()[0]![field] = value);
        var changes = Fixture.Periods(runtime, 6, Fixture.NoFacts);
        Assert.Empty(changes.Launches);
        Assert.Empty(runtime.Shipments);
    }

    [Fact]
    public void NothingIsLoadedFromAMarketWithNoSurplusOverAPeriodsUseOrWhenThePricesAreLevel()
    {
        var empty = Two(root => Mini.ThePair(root)["stock"] = 100);
        Assert.Empty(empty.Advance(1, Fixture.NoFacts).Launches);
        var level = Mini.Runtime(root =>
        {
            root["markets"]!.AsArray().Add(Mini.Market("n", "c"));
            root["routes"]!.AsArray().Add(RouteDoc());
        });
        Assert.Empty(Fixture.Periods(level, 10, Fixture.NoFacts).Launches);
    }

    private static string TwoCurrencies(Action<JsonObject>? edit = null) => Mini.Text(root =>
    {
        Mini.TheGood(root)["speed"] = 0;
        Mini.ThePair(root)["stock"] = 1000;
        root["currencies"]!.AsArray().Add(new JsonObject { ["id"] = "d", ["key"] = "k.d", ["reference"] = false, ["anchor"] = 500_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 });
        root["markets"]!.AsArray().Add(Mini.Market("n", "d", Mini.Pair("g", 0, 100, 0)));
        root["routes"]!.AsArray().Add(RouteDoc());
        root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "revalue", ["key"] = "k.r", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "rate", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = 1000 } } });
        edit?.Invoke(root);
    });

    [Fact]
    public void TheRateDecidesWhetherAPriceGapIsWorthShippingAcrossCurrencies()
    {
        // d starts at half a c, so n prices the good at twice as many milli as m, and converted back it is the same worth: no gain, nothing ships however long they trade.
        var level = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies()));
        Assert.Equal(20_000, level.Markets[1].Pairs[0].Price);
        Assert.Empty(Fixture.Periods(level, 6, Fixture.NoFacts).Launches);
        // d doubles in value against c: n's price converted into c is twice what m asks, and merchants ship.
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(TwoCurrencies()));
        runtime.ApplyShock("revalue", "n", 1000);
        Assert.Equal(1_000_000, runtime.Currencies[1].Rate);
        var changes = runtime.Advance(1, Fixture.NoFacts);
        var launch = Assert.Single(changes.Launches);
        var converted = EconomyMath.Trunc(runtime.Markets[1].Pairs[0].Price, runtime.Currencies[1].Rate, runtime.Currencies[0].Rate);
        Assert.Equal(20_000, converted);
        var gainRatio = (converted - runtime.Markets[0].Pairs[0].Price - 100) * 1000 / runtime.Markets[0].Pairs[0].Price;
        Assert.Equal((runtime.Markets[0].Pairs[0].Stock + launch.Quantity - 100) * Math.Min(500, gainRatio) / 1000, launch.Quantity);
    }

    [Fact]
    public void TheShipmentsOnTheWayAreBoundedByTheDocumentAndARouteThatCannotLoadSaysSo()
    {
        var runtime = Mini.Runtime(root =>
        {
            root["limits"]!["maxShipments"] = 1;
            root["goods"]!.AsArray().Add(Mini.Good("h"));
            Mini.ThePair(root)["stock"] = 1000;
            Mini.TheMarket(root)["pairs"]!.AsArray().Add(Mini.Pair("h", 100, 100, 1000));
            root["markets"]!.AsArray().Add(Mini.Market("n", "c", Mini.Pair("g", 0, 100, 0), Mini.Pair("h", 0, 100, 0)));
            root["routes"]!.AsArray().Add(RouteDoc(delay: 8));
        });
        var changes = runtime.Advance(1, Fixture.NoFacts);
        Assert.Single(changes.Launches);
        Assert.Equal(new RouteBlocked(0, 1), Assert.Single(changes.Blocks));
        Assert.Single(runtime.Shipments);
        var more = Fixture.Periods(runtime, 5, Fixture.NoFacts);
        Assert.Empty(more.Launches);
        Assert.True(more.Blocks.Count >= 5);
        Assert.Single(runtime.Shipments);
    }

    private static void Tariffs(JsonObject root, string schedule)
    {
        root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "out", ["kind"] = "export", ["rate"] = 100, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = schedule });
        root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "in", ["kind"] = "import", ["rate"] = 200, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = schedule });
        Edit.Obj(root, "markets", 0)["policies"]!.AsArray().Add("out");
        Edit.Obj(root, "markets", 1)["policies"]!.AsArray().Add("in");
    }

    [Fact]
    public void ExportAndImportTariffsNarrowTheGainAndTheExportDutyAccruesOnTheValueLoaded()
    {
        var runtime = Two(root => Tariffs(root, "hand"));
        var launch = Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches);
        var from = runtime.Markets[0].Pairs[0];
        var to = runtime.Markets[1].Pairs[0];
        var tariff = from.Price * 100 / 1000 + to.Price * 200 / 1000;
        var gain = to.Price - from.Price - 100 - tariff;
        Assert.True(gain * 1000 / from.Price >= 80);
        var gainRatio = gain * 1000 / from.Price;
        Assert.Equal((from.Stock + launch.Quantity - 100) * Math.Min(500, gainRatio) / 1000, launch.Quantity);
        Assert.Equal(EconomyMath.Nearest(Priced(launch.Quantity, from.Price), 100, 1000), runtime.Markets[0].Accrued[0]);
        Assert.Equal(0, runtime.Markets[1].Accrued[0]);
        var plain = Two();
        var unTaxed = Assert.Single(plain.Advance(1, Fixture.NoFacts).Launches);
        Assert.True(unTaxed.Quantity > launch.Quantity, "tariffs shrink the gain, so less is loaded");
    }

    [Fact]
    public void TariffsThatEatTheWholeGainStopTheTrade()
    {
        var runtime = Two(root =>
        {
            Tariffs(root, "hand");
            Edit.ById(root, "policies", "in")["rate"] = 1000;
        });
        Assert.Empty(Fixture.Periods(runtime, 5, Fixture.NoFacts).Launches);
    }

    [Fact]
    public void ImportTariffAccruesAtTheDestinationPriceOnArrival()
    {
        var runtime = Two(root =>
        {
            root["policies"]!.AsArray().Add(new JsonObject { ["id"] = "in", ["kind"] = "import", ["rate"] = 200, ["perUnit"] = 0, ["tags"] = new JsonArray(), ["schedule"] = "hand" });
            Edit.Obj(root, "markets", 1)["policies"]!.AsArray().Add("in");
            root["routes"]!.AsArray()[0]!["delay"] = 1;
        });
        runtime.Advance(1, Fixture.NoFacts);
        var launch = Assert.Single(runtime.Shipments);
        // Arrivals come first in a period, at the price the pair had when the period before closed.
        var priceAtArrival = runtime.Markets[1].Pairs[0].Price;
        runtime.Advance(2, Fixture.NoFacts);
        Assert.Equal(EconomyMath.Nearest(Priced(launch.Quantity, priceAtArrival), 200, 1000), runtime.Markets[1].Accrued[0]);
    }

    [Fact]
    public void ABlockadeClosesEveryRouteForItsDurationAndTheyOpenAgain()
    {
        var runtime = Two(root =>
        {
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "block", ["key"] = "k.b", ["duration"] = 3, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "route", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = -1000 } } });
        });
        runtime.ApplyShock("block", "", 1000);
        var shut = Fixture.Periods(runtime, 3, Fixture.NoFacts);
        Assert.Empty(shut.Launches);
        Assert.Empty(runtime.ActiveShocks);
        var open = Fixture.Periods(runtime, 2, Fixture.NoFacts);
        Assert.NotEmpty(open.Launches);
    }

    [Fact]
    public void AShockOnAMarketHalvesTheCapacityOfTheRoutesLeavingIt()
    {
        var runtime = Two(root =>
        {
            root["routes"]!.AsArray()[0]!["capacity"] = 40;
            root["routes"]!.AsArray()[0]!["share"] = 1000;
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "half", ["key"] = "k.h", ["duration"] = 5, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "route", ["good"] = "", ["tag"] = "", ["route"] = "r", ["amount"] = -500 } } });
        });
        runtime.ApplyShock("half", "m", 1000);
        Assert.Equal(20, Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches).Quantity);
        var other = Two(root =>
        {
            root["routes"]!.AsArray()[0]!["capacity"] = 40;
            root["routes"]!.AsArray()[0]!["share"] = 1000;
            root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "half", ["key"] = "k.h", ["duration"] = 5, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "route", ["good"] = "", ["tag"] = "", ["route"] = "r", ["amount"] = -500 } } });
        });
        other.ApplyShock("half", "n", 1000);       // a shock on the destination does not close a route that leaves the other market
        Assert.Equal(40, Assert.Single(other.Advance(1, Fixture.NoFacts).Launches).Quantity);
    }

    [Fact]
    public void GoodsThatArriveAtAFullWarehouseAreLost()
    {
        var runtime = Two(root =>
        {
            Edit.Obj(root, "markets", 1)["pairs"]!.AsArray()[0]!["capacity"] = 10;
            Edit.Obj(root, "markets", 1)["pairs"]!.AsArray()[0]!["consumption"] = 0;
            root["routes"]!.AsArray()[0]!["delay"] = 1;
        });
        runtime.Advance(1, Fixture.NoFacts);
        runtime.Advance(2, Fixture.NoFacts);
        Assert.True(runtime.Markets[1].Pairs[0].Stock <= 10);
    }

    [Fact]
    public void ArbitrageNarrowsThePriceGapBetweenTwoMarketsOverTime()
    {
        var with = Two();
        var without = Two(root => root["routes"] = new JsonArray());
        var start = Math.Abs(with.Markets[1].Pairs[0].Price - with.Markets[0].Pairs[0].Price);
        Fixture.Periods(with, 40, Fixture.NoFacts);
        Fixture.Periods(without, 40, Fixture.NoFacts);
        var gapWith = Math.Abs(with.Markets[1].Pairs[0].Price - with.Markets[0].Pairs[0].Price);
        var gapWithout = Math.Abs(without.Markets[1].Pairs[0].Price - without.Markets[0].Pairs[0].Price);
        Assert.Equal(0, start);
        Assert.True(gapWith < gapWithout, "with the route the gap is " + gapWith + ", without it " + gapWithout);
    }
}
