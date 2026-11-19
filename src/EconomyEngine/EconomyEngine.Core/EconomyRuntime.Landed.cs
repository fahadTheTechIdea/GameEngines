using EngineCommon.Graph;

namespace EconomyEngine;

/// <summary>What a market can get for one unit of a good by selling it where it is or sending it on through the routes: the value (in the market's own money), the first route of the best way on (-1 when selling here is best) and how many routes that way has.</summary>
public readonly record struct Reachable(long Value, int Route, int Hops);

/// <summary>
/// The landed-cost price field. For every good, the best value a unit can fetch starting from each market: sell it there, or pay a route's freight and tariffs and have it fetch what it can at the next market.
/// That is a longest-path question with non-negative costs, so it is answered by one Dijkstra run per good on the reversed routes, from a sink that every market that trades the good is joined to by an edge of
/// (the dearest price anywhere less its own price); the distance to a market is then (the dearest price less the best value it can reach), exactly. A cycle costs and never pays, so every best way is a
/// simple path. The field is not state: it is computed again from prices, rates and policies each time a period closes (and when an explanation or a percept finds none), so a save, a checkpoint and a replay
/// need hold nothing of it.
/// </summary>
/// <remarks>
/// Values are in thousandths of a minor unit of the reference currency (a price is converted by the currency's rate, truncating). The graph library bounds a weight at 10^9; when a good's dearest price or costliest
/// route passes that, every weight of that good is divided by the smallest power of two that brings it under, so the field is then correct to within that power per route (never in the ordinary case, where the
/// power is 1). Markets are numbered by ordinal id and a route is told apart by the ordinal rank of its id, so the answer does not depend on the order of the document.
/// </remarks>
public sealed partial class EconomyRuntime
{
    private readonly GraphScratch _graphScratch = new();
    private readonly ShortestPaths _paths = new();
    private readonly List<long> _landedEdges = new();
    private long[] _reachValue = [];
    private int[] _reachRoute = [];
    private int[]? _routeByRank;
    private const int LandedShapeLimit = 8;
    private readonly List<LandedShape> _landedShapes = new();
    private int _landedShapeNext;
    private bool[] _trades = [];
    private int[] _exportRate = [];
    private int[] _importRate = [];

    /// <summary>A price of a market's currency in thousandths of a minor unit of the reference currency.</summary>
    private long ToReference(long value, int currency) => EconomyMath.Trunc(value, Currencies[currency].Rate, EconomyMath.Ppm);

    private long FromReference(long value, int currency) => EconomyMath.Trunc(value, EconomyMath.Ppm, Currencies[currency].Rate);

    private int[] RouteByRank()
    {
        if (_routeByRank is not null) return _routeByRank;
        var byRank = new int[Set.Routes.Count];
        foreach (var route in Set.Routes) byRank[Set.RouteRank[route.Index]] = route.Index;
        _routeByRank = byRank;
        return byRank;
    }

    /// <summary>Computes the field for every good from the prices, rates and policies as they stand.</summary>
    internal void ComputeLandedField()
    {
        if (_reachValue.Length != Set.PairCount)
        {
            _reachValue = new long[Set.PairCount];
            _reachRoute = new int[Set.PairCount];
        }
        for (var good = 0; good < Set.Goods.Count; good++) ComputeLandedGood(good);
    }

    private void EnsureLandedField()
    {
        if (_reachValue.Length != Set.PairCount) ComputeLandedField();
    }

    private void ComputeLandedGood(int goodIndex)
    {
        var good = Set.Goods[goodIndex];
        if (_exportRate.Length != Set.Markets.Count)
        {
            _exportRate = new int[Set.Markets.Count];
            _importRate = new int[Set.Markets.Count];
        }
        var dearest = 0L;
        foreach (var market in Set.Markets)
        {
            var slot = Set.PairOf[market.Index][goodIndex];
            if (slot < 0) continue;
            var at = Set.PairBase[market.Index] + slot;
            _reachValue[at] = ToReference(Markets[market.Index].Pairs[slot].Price, market.Currency);
            _reachRoute[at] = -1;
            dearest = Math.Max(dearest, _reachValue[at]);
            _exportRate[market.Index] = TariffRate(market, good, PolicyKind.Export);
            _importRate[market.Index] = TariffRate(market, good, PolicyKind.Import);
        }
        _landedEdges.Clear();
        var costliest = 0L;
        foreach (var route in Set.Routes)
        {
            var fromSlot = Set.PairOf[route.From][goodIndex];
            var toSlot = Set.PairOf[route.To][goodIndex];
            if (fromSlot < 0 || toSlot < 0) continue;
            var from = Set.Markets[route.From];
            var to = Set.Markets[route.To];
            var priceTo = EconomyMath.Trunc(Markets[route.To].Pairs[toSlot].Price, Currencies[to.Currency].Rate, Currencies[from.Currency].Rate);
            var cost = ToReference(RouteCost(route, good, Markets[route.From].Pairs[fromSlot].Price, priceTo, _exportRate[route.From], _importRate[route.To]), from.Currency);
            _landedEdges.Add(cost);
            costliest = Math.Max(costliest, cost);
        }
        if (_landedEdges.Count == 0) return;

        var unit = 1L;
        while (Math.Max(dearest, costliest) / unit > GraphLimits.MaxWeight) unit *= 2;
        var shape = LandedShapeFor(goodIndex);
        var weights = shape.Weights;
        var next = 0;
        foreach (var market in Set.Markets)
        {
            var slot = Set.PairOf[market.Index][goodIndex];
            if (slot < 0) continue;
            weights[next++] = (dearest - _reachValue[Set.PairBase[market.Index] + slot]) / unit;
        }
        foreach (var cost in _landedEdges) weights[next++] = cost / unit;
        var refused = shape.Graph.Reweight(weights);
        if (refused.Length != 0) throw new InvalidOperationException("the landed-cost graph was refused: " + refused);
        var graph = shape.Graph;
        var sink = Set.Markets.Count;
        Dijkstra.Run(graph, sink, -1, _graphScratch, _paths);
        if (_paths.Code.Length != 0) throw new InvalidOperationException("the landed-cost search was refused: " + _paths.Code);
        var byRank = RouteByRank();
        foreach (var market in Set.Markets)
        {
            var slot = Set.PairOf[market.Index][goodIndex];
            if (slot < 0) continue;
            var at = Set.PairBase[market.Index] + slot;
            var node = Set.MarketRank[market.Index];
            var parent = _paths.ParentEdge(node);
            _reachValue[at] = dearest - _paths.Distance(node) * unit;
            _reachRoute[at] = graph.Source(parent) == sink ? -1 : byRank[(int)graph.Capacity(parent)];
        }
    }

    /// <summary>
    /// The graph for a good whose set of trading markets is the same as one already seen: the sink's edges and the routes between trading markets are fixed by that set alone, so goods that trade in the same markets
    /// share one topology and only the weights are replaced (<see cref="CsrGraph.Reweight"/>), which gives the graph a fresh build would give. At most <see cref="LandedShapeLimit"/> topologies are kept (the oldest is
    /// replaced); more different trading sets than that cost a build each, as before.
    /// </summary>
    private LandedShape LandedShapeFor(int goodIndex)
    {
        var markets = Set.Markets.Count;
        if (_trades.Length != markets) _trades = new bool[markets];
        foreach (var market in Set.Markets) _trades[market.Index] = Set.PairOf[market.Index][goodIndex] >= 0;
        foreach (var known in _landedShapes)
            if (known.Trades.AsSpan().SequenceEqual(_trades)) return known;

        var created = GraphBuilder.Create(markets + 1, out var builder);
        if (created.Length != 0 || builder is null) throw new InvalidOperationException("the landed-cost graph was refused: " + created);
        var sink = markets;
        var edges = 0;
        foreach (var market in Set.Markets)
        {
            if (!_trades[market.Index]) continue;
            AddEdge(builder, sink, Set.MarketRank[market.Index], 0, 0);
            edges++;
        }
        foreach (var route in Set.Routes)
        {
            if (!_trades[route.From] || !_trades[route.To]) continue;
            AddEdge(builder, Set.MarketRank[route.To], Set.MarketRank[route.From], 0, Set.RouteRank[route.Index]);
            edges++;
        }
        var built = builder.Build();
        if (!built.Ok || built.Graph is null) throw new InvalidOperationException("the landed-cost graph was refused: " + built.Code);
        var shape = new LandedShape((bool[])_trades.Clone(), built.Graph, new long[edges]);
        if (_landedShapes.Count < LandedShapeLimit) _landedShapes.Add(shape);
        else _landedShapes[_landedShapeNext++ % LandedShapeLimit] = shape;
        return shape;
    }

    private static void AddEdge(GraphBuilder builder, int from, int to, long weight, long routeRank)
    {
        var code = builder.Add(from, to, weight, routeRank);
        if (code.Length != 0) throw new InvalidOperationException("the landed-cost graph refused an edge: " + code);
    }

    private sealed record LandedShape(bool[] Trades, CsrGraph Graph, long[] Weights);

    /// <summary>True when the best way on from a market, for a good, passes through the target market.</summary>
    private bool WayVisits(int market, int goodIndex, int target)
    {
        var at = market;
        for (var steps = 0; steps <= Set.Markets.Count; steps++)
        {
            if (at == target) return true;
            var route = _reachRoute[Set.PairBase[at] + Set.PairOf[at][goodIndex]];
            if (route < 0) return false;
            at = Set.Routes[route].To;
        }
        return false;
    }

    /// <summary>The routes of the best way on from a market for a good, in order; empty when selling at the market is best.</summary>
    private List<int> Way(int market, int goodIndex)
    {
        var way = new List<int>();
        var at = market;
        while (way.Count <= Set.Markets.Count)
        {
            var route = _reachRoute[Set.PairBase[at] + Set.PairOf[at][goodIndex]];
            if (route < 0) break;
            way.Add(route);
            at = Set.Routes[route].To;
        }
        return way;
    }

    /// <summary>
    /// What a route's destination is worth to a merchant leaving the origin, in the origin's money. With direct routing it is the destination's own price. With landed routing it is the best value reachable from the
    /// destination, unless that way leads back through the origin (goods do not travel in a circle: then the destination's own price is all that counts); it is never less than the destination's own price.
    /// </summary>
    private (long Value, int Via) LandedTo(RouteDef route, int goodIndex, long priceTo)
    {
        if (Set.Routing == RoutingMode.Direct) return (priceTo, -1);
        var at = Set.PairBase[route.To] + Set.PairOf[route.To][goodIndex];
        var next = _reachRoute[at];
        if (next < 0 || WayVisits(route.To, goodIndex, route.From)) return (priceTo, -1);
        var value = FromReference(_reachValue[at], Set.Markets[route.From].Currency);
        return value > priceTo ? (value, next) : (priceTo, -1);
    }

    /// <summary>
    /// The best a unit of the pair's good can fetch starting from the market, in the market's money. With landed routing this is the field as of the last period closed; with direct routing it is the dearer of
    /// the market's own price and what each route out of it nets at its next market.
    /// </summary>
    public Reachable Reach(int market, int pair)
    {
        var def = Set.Markets[market];
        var good = Set.Goods[def.Pairs[pair].Good];
        var price = Markets[market].Pairs[pair].Price;
        if (Set.Routing == RoutingMode.Landed)
        {
            EnsureLandedField();
            var at = Set.PairBase[market] + pair;
            var route = _reachRoute[at];
            var value = route < 0 ? price : FromReference(_reachValue[at], def.Currency);
            return route < 0 || value <= price ? new Reachable(price, -1, 0) : new Reachable(value, route, Way(market, good.Index).Count);
        }
        var best = new Reachable(price, -1, 0);
        foreach (var index in Set.RoutesFrom[market])
        {
            var route = Set.Routes[index];
            var toSlot = Set.PairOf[route.To][good.Index];
            if (toSlot < 0) continue;
            var to = Set.Markets[route.To];
            var priceTo = EconomyMath.Trunc(Markets[route.To].Pairs[toSlot].Price, Currencies[to.Currency].Rate, Currencies[def.Currency].Rate);
            var net = priceTo - RouteCost(route, good, def, to, price, priceTo);
            if (net > best.Value) best = new Reachable(net, index, 1);
        }
        return best;
    }

    /// <summary>The routes of the best way on from a market for a pair, in order (empty when selling there is best).</summary>
    public IReadOnlyList<int> WayOn(int market, int pair)
    {
        var reach = Reach(market, pair);
        if (reach.Route < 0) return Array.Empty<int>();
        return Set.Routing == RoutingMode.Direct ? new[] { reach.Route } : Way(market, Set.Markets[market].Pairs[pair].Good);
    }
}
