using System.Text.Json.Nodes;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Document format 3 (routing mode; recipes are tested in ProductionTests), and what routing does to a whole economy: goods and money are conserved, landed routing equals direct where there is no way on, the plan is what the period does, a save carries nothing of the field.</summary>
public sealed class RoutingTests
{
    // ---- the format ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFormat1DocumentIsRefusedAsOldBeforeAnyFieldItNeverHadIsMissed()
    {
        var old = Edit.Doc(root =>
        {
            root["format"] = 1;
            root.Remove("routing");
            root.Remove("recipes");
        });
        var refusal = Assert.Throws<EngineCommon.EngineRefusedException>(() => EconomyLoader.Load(old));
        Assert.Equal(EconomyCodes.Format, refusal.Code);
        Assert.Contains("format 1 is no longer read", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("this reader reads format 6 only", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(6, EconomyLoader.Format);
        Assert.Equal(6, EconomySave.Format);
        // The same document with only the format changed to 3 is refused for what it lacks.
        Assert.Equal(EconomyCodes.MissingField, Fixture.Refusal(() => EconomyLoader.Load(Edit.Doc(root => { root.Remove("routing"); }))));
    }

    [Fact]
    public void TheRoutingModeOfTheDocumentIsWhatTheSetSaysAndNeitherModeIsADefault()
    {
        Assert.Equal(RoutingMode.Landed, Fixture.Town.Routing);
        Assert.Equal(RoutingMode.Direct, Mini.Set().Routing);
        Assert.Equal(RoutingMode.Direct, EconomyLoader.Load(Edit.Doc(root => root["routing"]!["mode"] = "direct")).Routing);
        Assert.Equal(EconomyCodes.MissingField, Fixture.Refusal(() => EconomyLoader.Load(Edit.Doc(root => root["routing"] = new JsonObject()))));
    }

    [Fact]
    public void AFormat2DocumentIsRefusedByItsOwnCodeAndNamesWhatFormat3Changed()
    {
        var refusal = Assert.Throws<EngineCommon.EngineRefusedException>(() => EconomyLoader.Load(Edit.Doc(root => root["format"] = 2)));
        Assert.Equal(EconomyCodes.Format2, refusal.Code);
        Assert.Contains("format 2 is no longer read", refusal.Message, StringComparison.Ordinal);
        Assert.NotEqual(EconomyCodes.Format, EconomyCodes.Format2);
    }

    [Fact]
    public void TheSamplesAreFormat4AndLoad()
    {
        Assert.Contains("\"format\": 6", Fixture.TownText, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"landed\"", Fixture.TownText, StringComparison.Ordinal);
        Assert.Equal(5, Fixture.Town.Recipes.Count);
        Assert.Equal(6, Fixture.Town.Routes.Count);
    }

    // ---- direct and landed agree where there is no way on ---------------------------------------------------------------------------

    private static void SameState(EconomyRuntime a, EconomyRuntime b)
    {
        foreach (var market in a.Set.Markets)
        {
            Assert.Equal(a.Markets[market.Index].Treasury, b.Markets[b.MarketIndex(market.Id)].Treasury);
            foreach (var pair in market.Pairs)
            {
                var good = a.Set.Goods[pair.Good].Id;
                var x = Net.Pair(a, market.Id, good);
                var y = Net.Pair(b, market.Id, good);
                Assert.Equal((x.Price, x.Stock, x.LastChange, x.Supply, x.Demand), (y.Price, y.Stock, y.LastChange, y.Supply, y.Demand));
            }
        }
        string Key(EconomyRuntime r, Shipment s) => r.Set.Routes[s.Route].Id + "/" + r.Set.Goods[s.Good].Id + "=" + s.Quantity + "@" + s.Arrives;
        Assert.Equal(a.Shipments.Select(s => Key(a, s)).Order(StringComparer.Ordinal), b.Shipments.Select(s => Key(b, s)).Order(StringComparer.Ordinal));
    }

    private static EconomyRuntime Ring(string mode, bool twoWay)
    {
        var routes = new List<RouteSpec> { new("ab", "a", "b", Capacity: 300, CostPerWeight: 150, Margin: 40) };
        if (twoWay) routes.Add(new RouteSpec("ba", "b", "a", Capacity: 300, CostPerWeight: 150, Margin: 40));
        return Net.Runtime(Net.Root(mode, [new GoodSpec("g"), new GoodSpec("h", 7000, 2)], [("a", "c"), ("b", "c")],
            [new PairSpec("a", "g", 120, 80, 900), new PairSpec("b", "g", 20, 100, 50), new PairSpec("a", "h", 10, 90, 20), new PairSpec("b", "h", 100, 40, 700)], routes, speed: 400));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithNoWayOnLandedRoutingIsDirectRoutingExactly(bool twoWay)
    {
        var direct = Ring("direct", twoWay);
        var landed = Ring("landed", twoWay);
        long units = 0;
        for (var period = 1; period <= 25; period++)
        {
            units += direct.Advance(period, Fixture.NoFacts).Launches.Sum(l => l.Quantity);
            landed.Advance(period, Fixture.NoFacts);
            SameState(direct, landed);
        }
        Assert.True(units > 500, "the routes carried only " + units + " units, so the comparison proves little");
    }

    // ---- conservation -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void RoutingCreatesAndDestroysNoGoodsAndNoMoneyOnRandomNetworksInEitherMode()
    {
        var random = new Rng(9090);
        long moved = 0;
        for (var round = 0; round < 60; round++)
        {
            var mode = round % 2 == 0 ? "landed" : "direct";
            var count = (int)random.Between(3, 8);
            var goods = Enumerable.Range(0, 3).Select(i => new GoodSpec("g" + i, 6000 + random.Between(0, 8000), random.Between(0, 3))).ToList();
            var markets = Enumerable.Range(0, count).Select(i => ("m" + i, random.Pick(3) == 0 ? "d" : "c")).ToList();
            var pairs = markets.SelectMany(m => goods.Where(_ => random.Pick(4) != 0).Select(g => new PairSpec(m.Item1, g.Id, 0, 0, random.Between(0, 1500)))).ToList();
            foreach (var m in markets)
                if (!pairs.Any(p => p.Market == m.Item1)) pairs.Add(new PairSpec(m.Item1, goods[0].Id));
            var routes = Enumerable.Range(0, (int)random.Between(count, 3 * count)).Select(i =>
            {
                var from = random.Pick(count);
                return new RouteSpec("r" + i, "m" + from, "m" + (from + 1 + random.Pick(count - 1)) % count, Capacity: random.Between(10, 600), CostPerWeight: random.Between(0, 300), Margin: (int)random.Between(0, 100), Share: (int)random.Between(100, 1001), Delay: (int)random.Between(1, 5));
            }).ToList();
            var runtime = Net.Runtime(Net.Root(mode, goods, markets, pairs, routes, speed: 500));
            long Total() => runtime.Markets.Sum(m => m.Pairs.Sum(p => p.Stock)) + runtime.Shipments.Sum(s => s.Quantity);
            var start = Total();
            long launchedTotal = 0, arrivedTotal = 0;
            for (var period = 1; period <= 30; period++)
            {
                var changes = runtime.Advance(period, Fixture.NoFacts);
                launchedTotal += changes.Launches.Sum(l => l.Quantity);
                arrivedTotal += changes.Arrivals.Sum(a => a.Quantity);
                Assert.True(Total() == start, $"round {round} period {period}: {Total()} units exist, {start} did");
                Assert.Equal(launchedTotal - arrivedTotal, runtime.Shipments.Sum(s => s.Quantity));
                Assert.All(runtime.Markets, m => Assert.All(m.Pairs, p => Assert.True(p.Stock >= 0)));
                Assert.All(runtime.Conservation(), c => Assert.Equal(0, c));
            }
            moved += launchedTotal;
        }
        Assert.True(moved > 10_000, "only " + moved + " units were shipped in all; the networks barely trade");
    }

    // ---- the plan is what the period does -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("direct")]
    [InlineData("landed")]
    public void WhatPlanRouteSaysIsWhatTheNextPeriodLoads(string mode)
    {
        var random = new Rng(mode == "direct" ? 31UL : 32UL);
        var loaded = 0;
        for (var round = 0; round < 120; round++)
        {
            var count = (int)random.Between(3, 8);
            var goods = Enumerable.Range(0, 3).Select(i => new GoodSpec("g" + i, 10_000, random.Between(0, 3))).ToList();
            var markets = Enumerable.Range(0, count).Select(i => ("m" + i, "c")).ToList();
            var pairs = markets.SelectMany(m => goods.Select(g => new PairSpec(m.Item1, g.Id, 0, 0, random.Between(0, 2000)))).ToList();
            // At most one route leaves a market, so no two routes compete for one stock and each route's plan is exact.
            var routes = new List<RouteSpec>();
            for (var m = 0; m < count; m++)
                if (random.Pick(10) < 8) routes.Add(new RouteSpec("r" + m, "m" + m, "m" + (m + 1 + random.Pick(count - 1)) % count, Capacity: random.Between(50, 900), CostPerWeight: random.Between(0, 200), Margin: (int)random.Between(0, 150), Share: (int)random.Between(100, 1001)));
            if (routes.Count == 0) continue;
            var runtime = Net.Runtime(Net.Root(mode, goods, markets, pairs, routes));
            foreach (var m in markets)
                foreach (var g in goods)
                    Net.Price(runtime, m.Item1, g.Id, random.Between(1500, 39_000));
            var plan = new List<string>();
            for (var r = 0; r < runtime.Set.Routes.Count; r++)
                foreach (var line in runtime.PlanRoute(r).Where(l => l.Shipped > 0))
                {
                    Assert.True(line.Gain > 0 && line.Ratio >= runtime.Set.Routes[r].Margin);
                    plan.Add(runtime.Set.Routes[r].Id + "/" + runtime.Set.Goods[line.Good].Id + "=" + line.Shipped);
                }
            var done = runtime.Advance(1, Fixture.NoFacts).Launches.Select(l => runtime.Set.Routes[l.Route].Id + "/" + runtime.Set.Goods[l.Good].Id + "=" + l.Quantity).ToList();
            Assert.Equal(plan.Order(StringComparer.Ordinal), done.Order(StringComparer.Ordinal));
            loaded += done.Count;
        }
        Assert.True(loaded > 100, "only " + loaded + " loads; the worlds barely trade");
    }

    // ---- the field is not state -------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASaveOfALandedEconomyCarriesNothingOfTheFieldAndTheRestoredEconomyLivesOnExactlyLikeTheOriginal()
    {
        var original = Fixture.Runtime();
        Fixture.Periods(original, 12);
        var restored = EconomySave.Open(Fixture.Town, EconomySave.Write(original));
        Assert.Equal(original.StateDigest(), restored.StateDigest());
        Fixture.Periods(original, 15);
        Fixture.Periods(restored, 15);
        Assert.Equal(original.StateDigest(), restored.StateDigest());
        // And two economies lived from the start are the same.
        var other = Fixture.Runtime();
        Fixture.Periods(other, 27);
        Assert.Equal(original.StateDigest(), other.StateDigest());
    }

    [Fact]
    public void TheSavesAreFormat6AndTheDigestTextSaysSo()
    {
        var bytes = EconomySave.Write(Tamper.Played());
        Assert.Contains("\"Format\":6", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.StartsWith("economy-state-6\n", EconomyImage.Canonical(Tamper.Image(bytes)), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTownGoodsReachFurtherThanTheNextMarketAndTheExplanationsRunOnItsState()
    {
        var runtime = Fixture.Runtime();
        Fixture.Periods(runtime, 6);
        foreach (var route in runtime.Set.Routes)
        {
            var text = EconomyExplain.Route(runtime, route.Id).Text();
            Assert.StartsWith("route " + route.Id + " (", text, StringComparison.Ordinal);
        }
        foreach (var market in runtime.Set.Markets)
            foreach (var pair in market.Pairs)
            {
                var reach = runtime.Reach(market.Index, pair.Index);
                Assert.True(reach.Value >= runtime.Markets[market.Index].Pairs[pair.Index].Price);
                Assert.True(reach.Route < 0 ? reach.Hops == 0 : reach.Hops >= 1);
                Assert.Equal(reach.Hops, runtime.WayOn(market.Index, pair.Index).Count);
                Assert.Equal(reach.Value, EconomyExplain.Reach(runtime, market.Id, runtime.Set.Goods[pair.Good].Id).Value);
            }
    }
}
