using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// The landed-cost price field: a hand-worked chain, a brute-force enumeration of every simple path on random networks (the oracle shares no code with the field), the weights beyond the graph library's bound,
/// and the rules that keep goods from travelling in a circle.
/// </summary>
public sealed class LandedTests
{
    private static readonly GoodSpec G = new("g");

    private static string[] Ids(params string[] ids) => ids;

    /// <summary>A, B, C in a line. A and B price the good at 10,000, C at 30,000; the freight is 1,000 from A to B and 2,000 from B to C (weight 1).</summary>
    private static EconomyRuntime Chain(string mode)
    {
        var runtime = Net.Runtime(Net.Root(mode, [G], [("a", "c"), ("b", "c"), ("c", "c")],
            [new PairSpec("a", "g", 0, 100, 1000), new PairSpec("b", "g", 0, 100, 0), new PairSpec("c", "g", 0, 100, 0)],
            [new RouteSpec("ab", "a", "b", CostPerWeight: 1000), new RouteSpec("bc", "b", "c", CostPerWeight: 2000)]));
        Net.Price(runtime, "a", "g", 10_000);
        Net.Price(runtime, "b", "g", 10_000);
        Net.Price(runtime, "c", "g", 30_000);
        return runtime;
    }

    [Fact]
    public void TheBestValueFromEachMarketOfAChainIsWorkedOutByHand()
    {
        // Landed routing: C sells at 30,000. B can sell at 10,000 or send it on for 30,000 - 2,000 = 28,000. A can sell at 10,000 or send it to B for 28,000 - 1,000 = 27,000.
        var landed = Chain("landed");
        Assert.Equal(new Reachable(27_000, 0, 2), Net.Reach(landed, "a", "g"));
        Assert.Equal(new Reachable(28_000, 1, 1), Net.Reach(landed, "b", "g"));
        Assert.Equal(new Reachable(30_000, -1, 0), Net.Reach(landed, "c", "g"));
        Assert.Equal(new[] { 0, 1 }, landed.WayOn(landed.MarketIndex("a"), 0));
        // Direct routing looks one route ahead: A would net 10,000 - 1,000 at B (worse than selling at home); B nets 28,000.
        var direct = Chain("direct");
        Assert.Equal(new Reachable(10_000, -1, 0), Net.Reach(direct, "a", "g"));
        Assert.Equal(new Reachable(28_000, 1, 1), Net.Reach(direct, "b", "g"));
    }

    [Fact]
    public void AGoodCrossesAMarketThatDoesNotWantItBecauseTheFarOneWillPayAndNothingMovesWithDirectRouting()
    {
        // Period 1: A holds 1,000, uses 100: 900 left, a surplus of 800 over a period's use. B's landed value 28,000 gives A a gain of 28,000 - 10,000 - 1,000 = 17,000 a unit (1,700 per mille): share 1,000 caps the load at all 800.
        var landed = Chain("landed");
        var first = landed.Advance(1, Fixture.NoFacts);
        Assert.Equal(new ShipmentLaunched(0, 0, 800, 2), Assert.Single(first.Launches));
        // Period 2: 800 reach B, B uses 100 (700 left, surplus 600) and sends 600 on to C: gain 30,000 - 10,000 - 2,000 = 18,000.
        var second = landed.Advance(2, Fixture.NoFacts);
        Assert.Equal(new ShipmentArrived(0, 0, 800), Assert.Single(second.Arrivals));
        Assert.Equal(new ShipmentLaunched(1, 0, 600, 3), Assert.Single(second.Launches));
        // Period 3: 600 reach C. At three times its reference price C buys only the least the document allows (responseMin 200 per mille of 100 = 20), so 580 are left.
        var third = landed.Advance(3, Fixture.NoFacts);
        Assert.Equal(new ShipmentArrived(1, 0, 600), Assert.Single(third.Arrivals));
        Assert.Equal(580, landed.Markets[2].Pairs[0].Stock);
        // Direct routing: A sees only B's price, 10,000, and nothing leaves A; B has no surplus; C gets nothing.
        var direct = Chain("direct");
        Assert.Empty(Fixture.Periods(direct, 3, Fixture.NoFacts).Launches);
        Assert.Equal(0, direct.Markets[2].Pairs[0].Stock);
    }

    [Fact]
    public void ARouteBackThroughTheOriginIsNeverCountedSoGoodsDoNotTravelInACircle()
    {
        // A <-> B and B -> C. From A the best way is A > B > C, which passes B. So B > A must not count A's value (27,000, reached through B itself): B's gain on the route to A stays at A's own price.
        var runtime = Net.Runtime(Net.Root("landed", [G], [("a", "c"), ("b", "c"), ("c", "c")],
            [new PairSpec("a", "g", 0, 0, 0), new PairSpec("b", "g", 0, 0, 5000), new PairSpec("c", "g", 0, 0, 0)],
            [new RouteSpec("ab", "a", "b", CostPerWeight: 1000), new RouteSpec("ba", "b", "a", CostPerWeight: 1000), new RouteSpec("bc", "b", "c", CostPerWeight: 2000)]));
        Net.Price(runtime, "a", "g", 10_000);
        Net.Price(runtime, "b", "g", 10_000);
        Net.Price(runtime, "c", "g", 30_000);
        Assert.Equal(new Reachable(27_000, 0, 2), Net.Reach(runtime, "a", "g"));
        var toA = Assert.Single(runtime.PlanRoute(runtime.RouteIndex("ba")));
        Assert.Equal(LineOutcome.NoGain, toA.Outcome);
        Assert.Equal(10_000, toA.Landed);
        Assert.Equal(-1, toA.Via);
        var toC = Assert.Single(runtime.PlanRoute(runtime.RouteIndex("bc")));
        Assert.Equal(LineOutcome.Shipped, toC.Outcome);
        Assert.Equal(30_000, toC.Landed);
        Assert.Equal(18_000, toC.Gain);
        var changes = runtime.Advance(1, Fixture.NoFacts);
        Assert.All(changes.Launches, l => Assert.Equal(runtime.RouteIndex("bc"), l.Route));
        Assert.NotEmpty(changes.Launches);
    }

    [Fact]
    public void ARouteThatLeadsOnPassesTheNextMarketsFieldToTheOriginAndTheFirstOnwardRouteIsNamed()
    {
        var runtime = Chain("landed");
        var line = Assert.Single(runtime.PlanRoute(runtime.RouteIndex("ab")));
        Assert.Equal(28_000, line.Landed);
        Assert.Equal(runtime.RouteIndex("bc"), line.Via);
        Assert.Equal(17_000, line.Gain);
        Assert.Equal(1700, line.Ratio);
        Assert.Equal(900, line.Surplus);
        Assert.Equal(900, line.Wanted);
    }

    [Fact]
    public void ARoundingTieAtTheCurrencyBoundaryIsNotAWayOnAndADirectTieKeepsTheGoodAtHome()
    {
        // e is worth two c. Selling at a (in e) for 10,000 is 20,000 c; at b (in c) a unit fetches 20,001 c, one thousandth more. Back in e that is 10,000.5, which truncates to the 10,000 it already fetches at a:
        // the way on is no better, so the answer is "sell here" (route -1, no hops) for a and for x, and the route into a does not count on it.
        var runtime = Net.Runtime(Net.Root("landed", [G], [("x", "e"), ("a", "e"), ("b", "c")],
            [new PairSpec("x", "g"), new PairSpec("a", "g"), new PairSpec("b", "g")], [new RouteSpec("xa", "x", "a"), new RouteSpec("ab", "a", "b")],
            edit: root => root["currencies"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "e", ["key"] = "k.e", ["reference"] = false, ["anchor"] = 2_000_000, ["money"] = 1000, ["growthPpm"] = 0, ["maxRateStepPpm"] = 0 })));
        Net.Price(runtime, "x", "g", 10_000);
        Net.Price(runtime, "a", "g", 10_000);
        Net.Price(runtime, "b", "g", 20_001);
        Assert.Equal(new Reachable(10_000, -1, 0), Net.Reach(runtime, "a", "g"));
        Assert.Equal(new Reachable(10_000, -1, 0), Net.Reach(runtime, "x", "g"));
        var line = Assert.Single(runtime.PlanRoute(runtime.RouteIndex("xa")));
        Assert.Equal(10_000, line.Landed);
        Assert.Equal(-1, line.Via);
        // Direct routing: a route that nets exactly what selling at home fetches is not taken; one thousandth more, and it is.
        EconomyRuntime Direct(long away)
        {
            var direct = Net.Runtime(Net.Root("direct", [G], [("a", "c"), ("b", "c")], [new PairSpec("a", "g"), new PairSpec("b", "g")], [new RouteSpec("ab", "a", "b", CostPerWeight: 1000)]));
            Net.Price(direct, "a", "g", 10_000);
            Net.Price(direct, "b", "g", away);
            return direct;
        }
        Assert.Equal(new Reachable(10_000, -1, 0), Net.Reach(Direct(11_000), "a", "g"));
        Assert.Equal(new Reachable(10_001, 0, 1), Net.Reach(Direct(11_001), "a", "g"));
    }

    // ---- brute force --------------------------------------------------------------------------------------------------------------------

    private sealed record Link(string Id, int From, int To, long Freight);

    private sealed class World
    {
        public int Markets;
        public string[] Currency = [];
        public long[] Price = [];
        public List<Link> Links = new();
        public int[] ExportRate = [];
        public int[] ImportRate = [];

        public long Rate(int market) => Currency[market] == "c" ? 1_000_000 : 500_000;

        public long ToRef(long value, int market) => value * Rate(market) / 1_000_000;

        public long FromRef(long value, int market) => value * 1_000_000 / Rate(market);

        /// <summary>What a unit pays on a link, in thousandths of the reference currency: freight, then the duties on the prices, all worked in the origin's money and converted at the end.</summary>
        public long Cost(Link link)
        {
            var priceTo = Price[link.To] * Rate(link.To) / Rate(link.From);
            var duties = Price[link.From] * Math.Min(ExportRate[link.From], 1000) / 1000 + priceTo * Math.Min(ImportRate[link.To], 1000) / 1000;
            return ToRef(link.Freight + duties, link.From);
        }

        /// <summary>The best value, in reference thousandths, a unit can fetch from a market over every simple path (sell where you stand, or pay a link and do the same from the next market without revisiting any).</summary>
        public long Best(int at, bool[] seen)
        {
            seen[at] = true;
            var best = ToRef(Price[at], at);
            foreach (var link in Links)
            {
                if (link.From != at || seen[link.To]) continue;
                best = Math.Max(best, Best(link.To, seen) - Cost(link));
            }
            seen[at] = false;
            return best;
        }
    }

    private static World RandomWorld(Rng random)
    {
        var world = new World { Markets = (int)random.Between(2, 8) };
        world.Currency = Enumerable.Range(0, world.Markets).Select(_ => random.Pick(3) == 0 ? "d" : "c").ToArray();
        world.Price = Enumerable.Range(0, world.Markets).Select(m => world.Currency[m] == "c" ? random.Between(2000, 40_000) : random.Between(4000, 80_000)).ToArray();
        world.ExportRate = Enumerable.Range(0, world.Markets).Select(_ => random.Pick(3) == 0 ? (int)random.Between(10, 300) : 0).ToArray();
        world.ImportRate = Enumerable.Range(0, world.Markets).Select(_ => random.Pick(3) == 0 ? (int)random.Between(10, 300) : 0).ToArray();
        var routes = (int)random.Between(1, 13);
        for (var i = 0; i < routes; i++)
        {
            var from = random.Pick(world.Markets);
            var to = (from + 1 + random.Pick(world.Markets - 1)) % world.Markets;
            world.Links.Add(new Link("r" + i, from, to, random.Between(0, 4000)));
        }
        return world;
    }

    private static EconomyRuntime Build(World world, Rng? shuffle = null)
    {
        var markets = Enumerable.Range(0, world.Markets).Select(m => ("m" + m, world.Currency[m])).ToList();
        var pairs = Enumerable.Range(0, world.Markets).Select(m => new PairSpec("m" + m, "g", 0, 0, 100)).ToList();
        var routes = world.Links.Select(l => new RouteSpec(l.Id, "m" + l.From, "m" + l.To, CostPerWeight: l.Freight)).ToList();
        var tariffs = new List<TariffSpec>();
        for (var m = 0; m < world.Markets; m++)
        {
            if (world.ExportRate[m] > 0) tariffs.Add(new TariffSpec("m" + m, "export", world.ExportRate[m]));
            if (world.ImportRate[m] > 0) tariffs.Add(new TariffSpec("m" + m, "import", world.ImportRate[m]));
        }
        if (shuffle is not null)
        {
            markets = shuffle.Shuffled(markets);
            routes = shuffle.Shuffled(routes);
        }
        var runtime = Net.Runtime(Net.Root("landed", [G], markets, pairs, routes, tariffs));
        for (var m = 0; m < world.Markets; m++) Net.Price(runtime, "m" + m, "g", world.Price[m]);
        return runtime;
    }

    [Fact]
    public void TheFieldEqualsTheBestOfEverySimplePathOnRandomNetworksWithTwoMoneysAndTariffs()
    {
        var random = new Rng(20261029);
        var withWay = 0;
        for (var round = 0; round < 400; round++)
        {
            var world = RandomWorld(random);
            var runtime = Build(world);
            for (var m = 0; m < world.Markets; m++)
            {
                var expected = Math.Max(world.Price[m], world.FromRef(world.Best(m, new bool[world.Markets]), m));
                var reach = Net.Reach(runtime, "m" + m, "g");
                Assert.True(expected == reach.Value, $"round {round} market {m}: the field says {reach.Value}, enumerating every path says {expected}");
                if (reach.Route < 0) continue;
                withWay++;
                // The way named must be a real simple path that earns exactly that value.
                var way = runtime.WayOn(runtime.MarketIndex("m" + m), 0).Select(i => world.Links[int.Parse(runtime.Set.Routes[i].Id[1..])]).ToList();
                Assert.Equal(reach.Hops, way.Count);
                Assert.Equal(m, way[0].From);
                for (var i = 1; i < way.Count; i++) Assert.Equal(way[i - 1].To, way[i].From);
                Assert.Equal(way.Count + 1, way.Select(l => l.From).Append(way[^1].To).Distinct().Count());
                var earned = world.ToRef(world.Price[way[^1].To], way[^1].To) - way.Sum(world.Cost);
                Assert.Equal(reach.Value, world.FromRef(earned, m));
            }
        }
        Assert.True(withWay > 400, "only " + withWay + " markets had a better way on; the networks are too sparse to test anything");
    }

    [Fact]
    public void TheFieldDoesNotDependOnTheOrderTheMarketsAndRoutesAreDeclaredIn()
    {
        var random = new Rng(77);
        for (var round = 0; round < 150; round++)
        {
            var world = RandomWorld(random);
            var one = Build(world);
            var two = Build(world, new Rng((ulong)round + 1));
            for (var m = 0; m < world.Markets; m++)
            {
                var a = Net.Reach(one, "m" + m, "g");
                var b = Net.Reach(two, "m" + m, "g");
                Assert.Equal(a.Value, b.Value);
                Assert.Equal(a.Hops, b.Hops);
                // The same first route, by id (its index changes with the order).
                Assert.Equal(a.Route < 0 ? "" : one.Set.Routes[a.Route].Id, b.Route < 0 ? "" : two.Set.Routes[b.Route].Id);
            }
        }
    }

    // ---- weights beyond the graph library's bound ----------------------------------------------------------------------------------------

    private static EconomyRuntime Dear(long a, long b, long c, long weight, long freightAb, long freightBc)
    {
        var runtime = Net.Runtime(Net.Root("landed", [new GoodSpec("g", 900_000_000, weight)], [("a", "c"), ("b", "c"), ("c", "c")],
            [new PairSpec("a", "g"), new PairSpec("b", "g"), new PairSpec("c", "g")],
            [new RouteSpec("ab", "a", "b", CostPerWeight: freightAb), new RouteSpec("bc", "b", "c", CostPerWeight: freightBc)]));
        Net.Price(runtime, "a", "g", a);
        Net.Price(runtime, "b", "g", b);
        Net.Price(runtime, "c", "g", c);
        return runtime;
    }

    [Fact]
    public void PricesAndFreightsAboveTheGraphBoundAreScaledAndAnswersThatFitAreStillExact()
    {
        // 3,000,000,000 is over the graph's 10^9 weight bound, so the weights are halved twice; every number here is a multiple of 4, so nothing is lost.
        var exact = Dear(2_000_000_000, 2_000_000_000, 3_000_000_000, 1, 100_000_000, 200_000_000);
        Assert.Equal(new Reachable(2_700_000_000, 0, 2), Net.Reach(exact, "a", "g"));
        Assert.Equal(new Reachable(2_800_000_000, 1, 1), Net.Reach(exact, "b", "g"));
        // The freight alone is over the bound (3 x 800,000,000 = 2.4 x 10^9): a good that heavy is not worth the road to C, and the field says so.
        var heavy = Dear(2_000_000_000, 2_000_000_000, 3_000_000_000, 3, 100_000_000, 800_000_000);
        Assert.Equal(new Reachable(2_000_000_000, -1, 0), Net.Reach(heavy, "b", "g"));
        Assert.Equal(new Reachable(2_000_000_000, -1, 0), Net.Reach(heavy, "a", "g"));
        // Freight dearer than any price (5 x 10^9 a route against prices of 10^8 to 3 x 10^8): the weights are scaled by the freight, nothing is refused, and nothing is worth sending.
        var freightOnly = Dear(100_000_000, 100_000_000, 300_000_000, 5, 1_000_000_000, 1_000_000_000);
        Assert.Equal(new Reachable(100_000_000, -1, 0), Net.Reach(freightOnly, "a", "g"));
        Assert.Equal(new Reachable(100_000_000, -1, 0), Net.Reach(freightOnly, "b", "g"));
        Assert.Equal(new Reachable(300_000_000, -1, 0), Net.Reach(freightOnly, "c", "g"));
        // Values that are not multiples of 4 are right to within the scaling: 4 a hop, and never off by more than that.
        var rough = Dear(2_000_000_001, 2_000_000_003, 3_000_000_007, 1, 100_000_001, 200_000_003);
        var reach = Net.Reach(rough, "a", "g");
        Assert.InRange(reach.Value, 3_000_000_007 - 200_000_003 - 100_000_001 - 12, 3_000_000_007 - 200_000_003 - 100_000_001 + 12);
        Assert.Equal(2, reach.Hops);
    }

    [Fact]
    public void ThePricesOfAPeriodMoveTheFieldAndTheLastClosedPeriodsFieldIsWhatMerchantsAreTold()
    {
        var runtime = Chain("landed");
        Assert.Equal(27_000, Net.Reach(runtime, "a", "g").Value);
        // The field is recomputed when a period closes: C's price falls to 11,000 and A's way on is worth 11,000 - 2,000 - 1,000 = 8,000 less than selling at home, so A sells at home.
        Net.Price(runtime, "c", "g", 11_000);
        runtime.Advance(1, Fixture.NoFacts);
        Assert.Equal(new Reachable(runtime.Markets[0].Pairs[0].Price, -1, 0), Net.Reach(runtime, "a", "g"));
    }

    [Theory]
    [InlineData(3, 12)]
    [InlineData(12, 14)]
    public void GoodsThatTradeInDifferentSetsOfMarketsShareOrReplaceTopologiesAndEveryFieldStillEqualsTheBestOfEverySimplePath(int distinctTradingSets, int goodCount)
    {
        // The field reuses one graph topology for goods that trade in the same markets and only replaces its weights; the oracle enumerates every simple path per good on only the markets and routes that good has.
        var random = new Rng(20261044 + (ulong)distinctTradingSets);
        var checkedMarkets = 0;
        for (var round = 0; round < 25; round++)
        {
            var shape = RandomWorld(random);
            while (shape.Markets < 4 || shape.Links.Count < 3) shape = RandomWorld(random);
            var sets = Enumerable.Range(0, distinctTradingSets).Select(_ => Enumerable.Range(0, shape.Markets).Select(_ => random.Pick(4) != 0).ToArray()).ToList();
            for (var m = 0; m < shape.Markets; m++) sets[0][m] |= sets.All(set => !set[m]); // a market with no pair at all is not a document the loader takes
            var trades = Enumerable.Range(0, goodCount).Select(g => sets[g % distinctTradingSets]).ToList();
            var goods = Enumerable.Range(0, goodCount).Select(g => new GoodSpec("g" + g)).ToList();
            var markets = Enumerable.Range(0, shape.Markets).Select(m => ("m" + m, shape.Currency[m])).ToList();
            var pairs = new List<PairSpec>();
            for (var g = 0; g < goodCount; g++)
                for (var m = 0; m < shape.Markets; m++)
                    if (trades[g][m]) pairs.Add(new PairSpec("m" + m, "g" + g, 0, 0, 100));
            var routes = shape.Links.Select(l => new RouteSpec(l.Id, "m" + l.From, "m" + l.To, CostPerWeight: l.Freight)).ToList();
            var tariffs = new List<TariffSpec>();
            for (var m = 0; m < shape.Markets; m++)
            {
                if (shape.ExportRate[m] > 0) tariffs.Add(new TariffSpec("m" + m, "export", shape.ExportRate[m]));
                if (shape.ImportRate[m] > 0) tariffs.Add(new TariffSpec("m" + m, "import", shape.ImportRate[m]));
            }
            var runtime = Net.Runtime(Net.Root("landed", goods, markets, pairs, routes, tariffs));
            for (var pass = 0; pass < 2; pass++)
            {
                // Pass 1 sets new prices on the same runtime: the same topologies, other weights.
                var worlds = new List<World>();
                for (var g = 0; g < goodCount; g++)
                {
                    var world = new World
                    {
                        Markets = shape.Markets, Currency = shape.Currency, ExportRate = shape.ExportRate, ImportRate = shape.ImportRate,
                        Links = shape.Links.Where(l => trades[g][l.From] && trades[g][l.To]).ToList(),
                        Price = Enumerable.Range(0, shape.Markets).Select(m => shape.Currency[m] == "c" ? random.Between(2000, 40_000) : random.Between(4000, 80_000)).ToArray(),
                    };
                    worlds.Add(world);
                    for (var m = 0; m < shape.Markets; m++)
                        if (trades[g][m]) Net.Price(runtime, "m" + m, "g" + g, world.Price[m]);
                }
                runtime.ComputeLandedField();
                for (var g = 0; g < goodCount; g++)
                    for (var m = 0; m < shape.Markets; m++)
                    {
                        if (!trades[g][m]) continue;
                        var world = worlds[g];
                        var expected = Math.Max(world.Price[m], world.FromRef(world.Best(m, new bool[world.Markets]), m));
                        var reach = Net.Reach(runtime, "m" + m, "g" + g);
                        Assert.True(expected == reach.Value, $"round {round} pass {pass} good {g} market {m}: the field says {reach.Value}, enumerating every path says {expected}");
                        checkedMarkets++;
                    }
            }
        }
        Assert.True(checkedMarkets > 1000, "only " + checkedMarkets + " pairs were compared");
    }

    [Fact]
    public void TheFieldOfAHundredGoodsOverTwoHundredMarketsReusesItsGraphAndAllocatesAlmostNothing()
    {
        // Before the topology was reused the field built a graph per good (about 9.7 MB a period at this size, 100 goods x 200 markets x 400 routes); now it builds one and replaces its weights.
        var runtime = EconomyRuntime.Create(EconomyLoader.Load(ScaleTests.BigText("landed")));
        runtime.ComputeLandedField();
        var before = GC.GetAllocatedBytesForCurrentThread();
        runtime.ComputeLandedField();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256 * 1024, "the field allocated " + allocated / 1024 + " KB");
    }
}
