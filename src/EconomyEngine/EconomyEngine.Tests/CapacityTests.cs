using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// A route's capacity is units a period, and it goes to the goods that pay most per unit, whatever order the document lists them in. The tests recompute the allocation without the engine's code (a hand-worked
/// example, every ordering of the goods tried, and the same network declared in many orders).
/// </summary>
public sealed class CapacityTests
{
    private static EconomyRuntime TwoGoods(bool dearFirst, long capacity = 100, string mode = "direct", string cheap = "cheap", string dear = "dear")
    {
        var goods = new List<GoodSpec> { new(cheap, Weight: 0), new(dear, Weight: 0) };
        var pairs = new List<PairSpec> { new("m", cheap, 0, 0, 1000), new("m", dear, 0, 0, 1000), new("n", cheap), new("n", dear) };
        if (dearFirst)
        {
            goods.Reverse();
            pairs = [pairs[1], pairs[0], pairs[3], pairs[2]];
        }
        var runtime = Net.Runtime(Net.Root(mode, goods, [("m", "c"), ("n", "c")], pairs, [new RouteSpec("r", "m", "n", Capacity: capacity, Margin: 50)]));
        Net.Price(runtime, "m", cheap, 2000);
        Net.Price(runtime, "n", cheap, 3000);
        Net.Price(runtime, "m", dear, 20_000);
        Net.Price(runtime, "n", dear, 24_000);
        return runtime;
    }

    [Theory]
    [InlineData(false, "direct")]
    [InlineData(true, "direct")]
    [InlineData(false, "landed")]
    [InlineData(true, "landed")]
    public void TheCapacityGoesToTheGoodThatPaysMostAUnitEvenWhenItsRatioIsLowerAndWhateverIsDeclaredFirst(bool dearFirst, string mode)
    {
        // Cheap: 2,000 to 3,000 = 1,000 a unit (500 per mille), asks 1,000 x 500 / 1,000 = 500. Dear: 20,000 to 24,000 = 4,000 a unit (200 per mille), asks 200. The capacity is 100.
        // In document order cheap would take it all; by gain, dear does (400 against 100 of profit).
        var runtime = TwoGoods(dearFirst, mode: mode);
        var lines = runtime.PlanRoute(0);
        Assert.Equal(["dear", "cheap"], lines.Select(l => runtime.Set.Goods[l.Good].Id));
        Assert.Equal(new RouteLine(runtime.GoodIndex("dear"), 1, 4000, 200, 24_000, -1, 1000, 200, 100, LineOutcome.Trimmed), lines[0]);
        Assert.Equal(new RouteLine(runtime.GoodIndex("cheap"), 2, 1000, 500, 3000, -1, 1000, 500, 0, LineOutcome.NoCapacity), lines[1]);
        var launches = runtime.Advance(1, Fixture.NoFacts).Launches;
        var launch = Assert.Single(launches);
        Assert.Equal(runtime.GoodIndex("dear"), launch.Good);
        Assert.Equal(100, launch.Quantity);
        Assert.Equal(900, Net.Pair(runtime, "m", "dear").Stock);
        Assert.Equal(1000, Net.Pair(runtime, "m", "cheap").Stock);
    }

    [Fact]
    public void WhenTheGainIsTheSameTheBetterRatioGoesFirstEvenIfItsIdIsLater()
    {
        // Both make 1,000 a unit. "z" bought at 2,000 is 500 per mille, "a" bought at 5,000 is 200. The capacity (50) fits one: z, though "a" sorts first by id.
        var runtime = Net.Runtime(Net.Root("direct", [new GoodSpec("a", Weight: 0), new GoodSpec("z", Weight: 0)], [("m", "c"), ("n", "c")],
            [new PairSpec("m", "a", 0, 0, 1000), new PairSpec("m", "z", 0, 0, 1000), new PairSpec("n", "a"), new PairSpec("n", "z")], [new RouteSpec("r", "m", "n", Capacity: 50)]));
        Net.Price(runtime, "m", "a", 5000);
        Net.Price(runtime, "n", "a", 6000);
        Net.Price(runtime, "m", "z", 2000);
        Net.Price(runtime, "n", "z", 3000);
        var lines = runtime.PlanRoute(0);
        Assert.Equal(["z", "a"], lines.Select(l => runtime.Set.Goods[l.Good].Id));
        Assert.Equal([500L, 200L], lines.Select(l => l.Ratio));
        var launch = Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches);
        Assert.Equal(runtime.GoodIndex("z"), launch.Good);
    }

    [Fact]
    public void AShockOnTheRouteNarrowsTheCapacityThePlanAndTheExplanationSee()
    {
        EconomyRuntime Make() => Net.Runtime(Net.Root("direct", [new GoodSpec("g", Weight: 0)], [("m", "c"), ("n", "c")], [new PairSpec("m", "g", 0, 0, 1000), new PairSpec("n", "g")],
            [new RouteSpec("r", "m", "n", Capacity: 100)], edit: root =>
            {
                root["shocks"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "half", ["key"] = "k.h", ["duration"] = 5, ["effects"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["kind"] = "route", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = -500 } } });
                root["shocks"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "block", ["key"] = "k.b", ["duration"] = 5, ["effects"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["kind"] = "route", ["good"] = "", ["tag"] = "", ["route"] = "", ["amount"] = -1000 } } });
            }));
        var runtime = Make();
        Net.Price(runtime, "m", "g", 10_000);
        Net.Price(runtime, "n", "g", 20_000);
        Assert.Equal(100, runtime.CapacityNow(0));
        runtime.ApplyShock("half", "", 1000);
        Assert.Equal(50, runtime.CapacityNow(0));
        var half = Assert.Single(runtime.PlanRoute(0));
        Assert.Equal((50L, LineOutcome.Trimmed), (half.Shipped, half.Outcome));
        Assert.Contains("capacity 50)", EconomyExplain.Route(runtime, "r").Text(), StringComparison.Ordinal);
        var closed = Make();
        Net.Price(closed, "m", "g", 10_000);
        Net.Price(closed, "n", "g", 20_000);
        closed.ApplyShock("block", "", 1000);
        Assert.Equal(0, closed.CapacityNow(0));
        Assert.Equal(LineOutcome.NoCapacity, Assert.Single(closed.PlanRoute(0)).Outcome);
        Assert.Empty(closed.Advance(1, Fixture.NoFacts).Launches);
    }

    [Fact]
    public void AnUnknownRouteIndexIsRefusedAtEitherEnd()
    {
        var runtime = TwoGoods(false);
        Assert.Equal(EconomyCodes.UnknownRoute, Fixture.Refusal(() => runtime.PlanRoute(-1)));
        Assert.Equal(EconomyCodes.UnknownRoute, Fixture.Refusal(() => runtime.PlanRoute(runtime.Set.Routes.Count)));
        Assert.Single(runtime.Set.Routes);
        Assert.NotEmpty(runtime.PlanRoute(0));
    }

    [Fact]
    public void AGoodThatWasGivenCapacityButBlockedByTheShipmentLimitIsListedAsWaitingNotSkipped()
    {
        // One shipment may be on its way. r1 (a to b) loads in the first period and r2 (c to b), by id the next, is blocked; asked why, r2 says its rank 1 good waits because of the limit.
        var runtime = Net.Runtime(Net.Root("direct", [new GoodSpec("g", Weight: 0)], [("a", "c"), ("b", "c"), ("c", "c")],
            [new PairSpec("a", "g", 0, 0, 1000), new PairSpec("b", "g"), new PairSpec("c", "g", 0, 0, 1000)],
            [new RouteSpec("r1", "a", "b", Delay: 3), new RouteSpec("r2", "c", "b", Delay: 3)], edit: root => root["limits"]!["maxShipments"] = 1));
        Net.Price(runtime, "a", "g", 10_000);
        Net.Price(runtime, "c", "g", 10_000);
        Net.Price(runtime, "b", "g", 15_000);
        var changes = runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal("r1", runtime.Set.Routes[Assert.Single(changes.Launches).Route].Id);
        Assert.Equal(new RouteBlocked(runtime.RouteIndex("r2"), 0), Assert.Single(changes.Blocks));
        var line = Assert.Single(runtime.PlanRoute(runtime.RouteIndex("r2")));
        Assert.Equal((1, LineOutcome.Blocked, 0L), (line.Rank, line.Outcome, line.Shipped));
        var text = EconomyExplain.Route(runtime, "r2").Text();
        Assert.Contains("wait g: 0 (rank 1;", text, StringComparison.Ordinal);
        Assert.Contains("the shipments on their way are at the document's limit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNextBestGoodGetsWhatTheBestLeavesAndTheLastOneWaits()
    {
        // Capacity 300: dear takes its 200, cheap the remaining 100 of the 500 it asked for.
        var runtime = TwoGoods(false, capacity: 300);
        var lines = runtime.PlanRoute(0);
        Assert.Equal([LineOutcome.Shipped, LineOutcome.Trimmed], lines.Select(l => l.Outcome));
        Assert.Equal([200L, 100L], lines.Select(l => l.Shipped));
        var launches = runtime.Advance(1, Fixture.NoFacts).Launches;
        Assert.Equal(300, launches.Sum(l => l.Quantity));
        // Plenty of capacity: both get everything they ask for.
        var wide = TwoGoods(false, capacity: 10_000);
        Assert.Equal([LineOutcome.Shipped, LineOutcome.Shipped], wide.PlanRoute(0).Select(l => l.Outcome));
    }

    [Theory]
    [InlineData("a", "b", false)]
    [InlineData("a", "b", true)]
    [InlineData("b", "a", false)]
    [InlineData("b", "a", true)]
    public void GoodsWithTheSameGainAndRatioAreTakenByTheLowerIdNotThePlaceInTheDocument(string first, string second, bool swap)
    {
        var goods = new List<GoodSpec> { new(first, Weight: 0), new(second, Weight: 0) };
        var pairs = new List<PairSpec> { new("m", first, 0, 0, 1000), new("m", second, 0, 0, 1000), new("n", first), new("n", second) };
        if (swap) { goods.Reverse(); pairs.Reverse(); }
        var runtime = Net.Runtime(Net.Root("direct", goods, [("m", "c"), ("n", "c")], pairs, [new RouteSpec("r", "m", "n", Capacity: 50, Margin: 50)]));
        foreach (var good in new[] { first, second })
        {
            Net.Price(runtime, "m", good, 10_000);
            Net.Price(runtime, "n", good, 12_000);
        }
        var launch = Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches);
        Assert.Equal(runtime.GoodIndex("a"), launch.Good);
        Assert.Equal(50, launch.Quantity);
        Assert.Equal(["a", "b"], runtime.PlanRoute(0).Select(l => runtime.Set.Goods[l.Good].Id));
    }

    [Fact]
    public void GoodsThatAreNotWorthCarryingAreListedAfterTheRankedOnesWithTheirReasonByGoodId()
    {
        // m sells five goods to n: one pays, one is under the margin, one has no surplus, one is dearer at home, one pays but is blocked from loading by nothing (it ranks).
        var specs = new[] { "pays", "thin", "empty", "loss", "level" };
        var runtime = Net.Runtime(Net.Root("direct", specs.Reverse().Select(id => new GoodSpec(id, Weight: 0)), [("m", "c"), ("n", "c")],
            specs.Reverse().SelectMany(id => new[] { new PairSpec("m", id, 0, 100, id == "empty" ? 100 : 1000), new PairSpec("n", id) }),
            [new RouteSpec("r", "m", "n", Capacity: 10_000, Margin: 100)]));
        void Set(string good, long home, long away)
        {
            Net.Price(runtime, "m", good, home);
            Net.Price(runtime, "n", good, away);
        }
        Set("pays", 10_000, 13_000);   // 300 per mille: over the margin of 100
        Set("thin", 10_000, 10_500);   // 50 per mille: under it
        Set("empty", 10_000, 13_000);  // stock 100 = one period's use: no surplus
        Set("loss", 10_000, 9000);     // cheaper there
        Set("level", 10_000, 10_000);  // the same price
        var lines = runtime.PlanRoute(0);
        Assert.Equal(["pays", "empty", "level", "loss", "thin"], lines.Select(l => runtime.Set.Goods[l.Good].Id));
        Assert.Equal([LineOutcome.Shipped, LineOutcome.NoSurplus, LineOutcome.NoGain, LineOutcome.NoGain, LineOutcome.BelowMargin], lines.Select(l => l.Outcome));
        Assert.Equal([1, 0, 0, 0, 0], lines.Select(l => l.Rank));
        Assert.Equal(-1000, lines[3].Gain);
        Assert.Equal(500, lines[4].Gain);
        Assert.Equal(50, lines[4].Ratio);
        Assert.Equal(0, lines[1].Shipped);
        // The loading rule does what the plan says.
        var launch = Assert.Single(runtime.Advance(1, Fixture.NoFacts).Launches);
        Assert.Equal(runtime.GoodIndex("pays"), launch.Good);
    }

    // ---- every ordering of the goods ---------------------------------------------------------------------------------------------------

    private static IEnumerable<int[]> Permutations(int count)
    {
        var items = Enumerable.Range(0, count).ToArray();
        IEnumerable<int[]> Walk(int at)
        {
            if (at == items.Length) { yield return (int[])items.Clone(); yield break; }
            for (var i = at; i < items.Length; i++)
            {
                (items[at], items[i]) = (items[i], items[at]);
                foreach (var p in Walk(at + 1)) yield return p;
                (items[at], items[i]) = (items[i], items[at]);
            }
        }
        return Walk(0);
    }

    [Fact]
    public void NoOrderOfFillingTheCapacityEarnsMoreThanTheOrderTheEngineUses()
    {
        var random = new Rng(41);
        var checkedWorlds = 0;
        for (var round = 0; round < 250; round++)
        {
            var count = (int)random.Between(2, 6);
            var capacity = random.Between(20, 1500);
            var share = (int)random.Between(100, 1001);
            var home = Enumerable.Range(0, count).Select(_ => random.Between(2000, 30_000)).ToArray();
            var away = home.Select(h => Math.Max(1000, h + random.Between(-3000, 9000))).ToArray();
            var stock = Enumerable.Range(0, count).Select(_ => random.Between(0, 2000)).ToArray();
            var ids = Enumerable.Range(0, count).Select(i => "g" + i).ToArray();
            var shuffled = random.Shuffled(Enumerable.Range(0, count)).ToArray();
            var runtime = Net.Runtime(Net.Root("direct", shuffled.Select(i => new GoodSpec(ids[i], Weight: 0)), [("m", "c"), ("n", "c")],
                shuffled.SelectMany(i => new[] { new PairSpec("m", ids[i], 0, 0, stock[i]), new PairSpec("n", ids[i]) }),
                [new RouteSpec("r", "m", "n", Capacity: capacity, Share: share)]));
            for (var i = 0; i < count; i++)
            {
                Net.Price(runtime, "m", ids[i], home[i]);
                Net.Price(runtime, "n", ids[i], away[i]);
            }
            // What each good asks for and earns a unit, by the stated rule and nothing else of the engine's.
            var ask = new long[count];
            var gain = new long[count];
            for (var i = 0; i < count; i++)
            {
                gain[i] = away[i] - home[i];
                if (gain[i] <= 0) continue;
                var ratio = gain[i] * 1000 / home[i];
                ask[i] = stock[i] * Math.Min(share, ratio) / 1000;
            }
            long Profit(int[] order)
            {
                var left = capacity;
                long profit = 0;
                foreach (var i in order)
                {
                    var take = Math.Min(left, ask[i]);
                    profit += take * Math.Max(0, gain[i]);
                    left -= take;
                }
                return profit;
            }
            var best = Permutations(count).Max(Profit);
            var launches = runtime.Advance(1, Fixture.NoFacts).Launches;
            var earned = launches.Sum(l => l.Quantity * gain[int.Parse(runtime.Set.Goods[l.Good].Id[1..])]);
            Assert.True(launches.Sum(l => l.Quantity) <= capacity);
            Assert.True(earned == best, $"round {round}: the engine earns {earned}, the best of {count}! orders earns {best}");
            if (best > 0 && ask.Sum() > capacity) checkedWorlds++;
        }
        Assert.True(checkedWorlds > 60, "only " + checkedWorlds + " worlds had a capacity that bound; the test would prove little");
    }

    // ---- the same network in other orders ------------------------------------------------------------------------------------------------

    private sealed record Spec(string Mode, GoodSpec[] Goods, (string Id, string Currency)[] Markets, PairSpec[] Pairs, RouteSpec[] Routes);

    private static Spec RandomSpec(Rng random, string mode)
    {
        var goods = Enumerable.Range(0, 4).Select(i => new GoodSpec("g" + i, 6000 + random.Between(0, 8000), random.Between(0, 4))).ToArray();
        var markets = Enumerable.Range(0, 5).Select(i => ("m" + i, random.Pick(3) == 0 ? "d" : "c")).ToArray();
        var pairs = new List<PairSpec>();
        foreach (var market in markets)
            foreach (var good in goods)
                if (random.Pick(4) != 0) pairs.Add(new PairSpec(market.Item1, good.Id, random.Between(0, 150), random.Between(20, 150), random.Between(0, 1500)));
        foreach (var market in markets)
            if (!pairs.Any(p => p.Market == market.Item1)) pairs.Add(new PairSpec(market.Item1, goods[0].Id, 50, 50, 500));
        var routes = new List<RouteSpec>();
        for (var i = 0; i < 9; i++)
        {
            var from = random.Pick(5);
            var to = (from + 1 + random.Pick(4)) % 5;
            routes.Add(new RouteSpec("r" + i, "m" + from, "m" + to, Capacity: random.Between(10, 400), CostPerWeight: random.Between(0, 400), Margin: (int)random.Between(0, 200), Share: (int)random.Between(100, 1001), Delay: (int)random.Between(1, 4)));
        }
        return new Spec(mode, goods, markets, pairs.ToArray(), routes.ToArray());
    }

    private static EconomyRuntime Realise(Spec spec, Rng? shuffle)
    {
        var goods = spec.Goods.AsEnumerable();
        var markets = spec.Markets.AsEnumerable();
        var pairs = spec.Pairs.AsEnumerable();
        var routes = spec.Routes.AsEnumerable();
        if (shuffle is not null)
        {
            goods = shuffle.Shuffled(goods);
            markets = shuffle.Shuffled(markets);
            pairs = shuffle.Shuffled(pairs);
            routes = shuffle.Shuffled(routes);
        }
        return Net.Runtime(Net.Root(spec.Mode, goods, markets, pairs, routes, speed: 400));
    }

    private static List<string> Flows(EconomyRuntime runtime, EconomyChanges changes) =>
        changes.Launches.Select(l => runtime.Set.Routes[l.Route].Id + "/" + runtime.Set.Goods[l.Good].Id + "=" + l.Quantity + "@" + l.Arrives).Order(StringComparer.Ordinal).ToList();

    [Theory]
    [InlineData("direct")]
    [InlineData("landed")]
    public void TheFlowsOfAnEconomyAreTheSameWhateverOrderItsDocumentListsGoodsMarketsPairsAndRoutesIn(string mode)
    {
        var random = new Rng(mode == "direct" ? 1001UL : 2002UL);
        var flowing = 0;
        for (var round = 0; round < 40; round++)
        {
            var spec = RandomSpec(random, mode);
            var one = Realise(spec, null);
            var two = Realise(spec, new Rng((ulong)round + 5));
            for (var period = 1; period <= 6; period++)
            {
                var a = one.Advance(period, Fixture.NoFacts);
                var b = two.Advance(period, Fixture.NoFacts);
                Assert.Equal(Flows(one, a), Flows(two, b));
                flowing += a.Launches.Count;
            }
            foreach (var market in spec.Markets)
                foreach (var good in spec.Goods)
                {
                    var m1 = one.MarketIndex(market.Id);
                    var g1 = one.GoodIndex(good.Id);
                    if (one.Set.PairOf[m1][g1] < 0) continue;
                    var p1 = Net.Pair(one, market.Id, good.Id);
                    var p2 = Net.Pair(two, market.Id, good.Id);
                    Assert.Equal((p1.Price, p1.Stock), (p2.Price, p2.Stock));
                }
        }
        Assert.True(flowing > 300, "only " + flowing + " launches; the networks barely trade");
    }

    [Fact]
    public void TwoRoutesThatShareAnOriginShareItsStockInRouteIdOrderNotDocumentOrder()
    {
        EconomyRuntime Make(bool swap)
        {
            var routes = new List<RouteSpec> { new("x", "m", "a", Margin: 0), new("y", "m", "b", Margin: 0) };
            if (swap) routes.Reverse();
            var runtime = Net.Runtime(Net.Root("direct", [new GoodSpec("g", Weight: 0)], [("m", "c"), ("a", "c"), ("b", "c")],
                [new PairSpec("m", "g", 0, 0, 1000), new PairSpec("a", "g"), new PairSpec("b", "g")], routes));
            Net.Price(runtime, "m", "g", 10_000);
            Net.Price(runtime, "a", "g", 20_000);
            Net.Price(runtime, "b", "g", 20_000);
            return runtime;
        }
        foreach (var swap in new[] { false, true })
        {
            var runtime = Make(swap);
            var launches = runtime.Advance(1, Fixture.NoFacts).Launches;
            // x asks for the whole share of 1,000 (ratio 1,000 per mille), takes it all; y finds nothing left.
            Assert.Equal(["x"], launches.Select(l => runtime.Set.Routes[l.Route].Id));
            Assert.Equal(1000, launches[0].Quantity);
        }
    }

    // ---- the explanation ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheExplanationSaysWhyEachGoodGotItsCapacityInTheOrderMerchantsFillIt()
    {
        var runtime = TwoGoods(false);
        var text = EconomyExplain.Route(runtime, "r").Text();
        Assert.StartsWith("route r (m -> n) = 100 (direct routing, capacity 100)", text, StringComparison.Ordinal);
        var lines = text.Split('\n');
        Assert.Contains("load dear: 100", lines[1], StringComparison.Ordinal);
        Assert.Contains("rank 1; gain 4000 a unit (200 per mille), destination worth 24000 at its own price; surplus 1000, asks 200, capacity left 100: loaded in part, the capacity ran out", lines[1], StringComparison.Ordinal);
        Assert.Contains("wait cheap: 0", lines[2], StringComparison.Ordinal);
        Assert.Contains("rank 2; gain 1000 a unit (500 per mille), destination worth 3000 at its own price; surplus 1000, asks 500, capacity left 0: profitable but the capacity went to goods that pay more a unit", lines[2], StringComparison.Ordinal);
        Assert.Equal(3, lines.Length);
        // A refusal for an unknown route, by the named code.
        Assert.Equal(EconomyCodes.UnknownRoute, Fixture.Refusal(() => EconomyExplain.Route(runtime, "nowhere")));
        Assert.Equal(EconomyCodes.UnknownName, Fixture.Refusal(() => EconomyExplain.Route(runtime, "No Road")));
    }

    [Fact]
    public void TheExplanationOfALandedRouteNamesTheRouteItCountsOnAndTheReachExplanationWalksTheWay()
    {
        var runtime = Net.Runtime(Net.Root("landed", [new GoodSpec("g")], [("a", "c"), ("b", "c"), ("c", "c")],
            [new PairSpec("a", "g", 0, 100, 1000), new PairSpec("b", "g", 0, 100, 0), new PairSpec("c", "g", 0, 100, 0)],
            [new RouteSpec("ab", "a", "b", CostPerWeight: 1000), new RouteSpec("bc", "b", "c", CostPerWeight: 2000)]));
        Net.Price(runtime, "a", "g", 10_000);
        Net.Price(runtime, "b", "g", 10_000);
        Net.Price(runtime, "c", "g", 30_000);
        var route = EconomyExplain.Route(runtime, "ab").Text();
        Assert.Contains("destination worth 28000 landed through route bc", route, StringComparison.Ordinal);
        var reach = EconomyExplain.Reach(runtime, "a", "g");
        Assert.Equal(27_000, reach.Value);
        Assert.Equal("2 routes on, landed routing", reach.State);
        Assert.Equal(["here", "via", "via", "sells-at"], reach.Terms.Select(t => t.Kind));
        Assert.Equal(["g", "ab", "bc", "g"], reach.Terms.Select(t => t.Id));
        Assert.Equal(30_000, reach.Terms[3].Value);
        Assert.Equal("sell here", EconomyExplain.Reach(runtime, "c", "g").State);
    }
}
