using System.Runtime.InteropServices;

namespace EconomyEngine;

/// <summary>What became of one good on a route in a period.</summary>
public enum LineOutcome
{
    /// <summary>Loaded all it asked for.</summary>
    Shipped,
    /// <summary>Loaded part of what it asked for: the capacity ran out during it.</summary>
    Trimmed,
    /// <summary>Profitable, but the capacity was spent on goods that pay more per unit.</summary>
    NoCapacity,
    /// <summary>Profitable and given capacity, but the shipments on their way are at the document's limit.</summary>
    Blocked,
    /// <summary>Profitable, but the gain over the origin price is under the route's margin.</summary>
    BelowMargin,
    /// <summary>Profitable and over the margin, but the origin holds no surplus over a period's use (or the share of it rounds to nothing).</summary>
    NoSurplus,
    /// <summary>Not worth carrying: the destination pays no more than the origin price and the costs of the way.</summary>
    NoGain,
}

/// <summary>
/// One good of a route, as the loading rule saw it: its rank among the profitable ones (1 first; 0 when it was not profitable enough to rank), the gain a unit would make (the value at the destination less the origin
/// price and the costs of the way, in the origin's money) and that gain per mille of the origin price, the value at the destination that was used and the first onward route it counted on (-1: the destination's own
/// price), the surplus at the origin, what the rule asked for, what it was given, and why.
/// </summary>
public readonly record struct RouteLine(int Good, int Rank, long Gain, long Ratio, long Landed, int Via, long Surplus, long Wanted, long Shipped, LineOutcome Outcome);

public sealed partial class EconomyRuntime
{
    private readonly List<Candidate> _candidates = new();

    private static readonly Comparison<Candidate> ByGain = (a, b) =>
    {
        var order = b.Gain.CompareTo(a.Gain);
        if (order != 0) return order;
        order = b.Ratio.CompareTo(a.Ratio);
        return order != 0 ? order : a.GoodRank.CompareTo(b.GoodRank);
    };

    private struct Candidate
    {
        public int Good;
        public int GoodRank;
        public int Pair;
        public long Gain;
        public long Ratio;
        public long Landed;
        public int Via;
        public long Surplus;
        public long Wanted;
    }

    /// <summary>Merchants load what pays, dearest first: see <see cref="Allocate"/>.</summary>
    private void Launch(RouteDef route, long period, EconomyChanges changes) => Allocate(route, period, changes, null);

    /// <summary>
    /// The loading rule of one route in one period. For each good both ends trade, the value at the destination (see <c>LandedTo</c>) less the origin price and the cost of the way is the gain of one unit; if it is
    /// positive and at least the route's margin per mille of the origin price, and the origin holds a surplus over a period's use, merchants want the route's share (or the gain ratio, if less) of that surplus.
    /// The route's capacity is units a period, so it goes to the goods that pay most per unit first (then the higher gain ratio, then the lower good id, ordinally: never the order of the document). With
    /// <paramref name="changes"/> the goods are loaded; without it nothing moves and <paramref name="lines"/> alone says what would be.
    /// </summary>
    internal void Allocate(RouteDef route, long period, EconomyChanges? changes, List<RouteLine>? lines)
    {
        var remaining = RouteCapacity(route, period);
        if (remaining <= 0 && lines is null) return;
        var from = Set.Markets[route.From];
        var to = Set.Markets[route.To];
        var fromRate = Currencies[from.Currency].Rate;
        var toRate = Currencies[to.Currency].Rate;
        var skipped = lines is null ? null : new List<RouteLine>();
        _candidates.Clear();
        foreach (var pair in from.Pairs)
        {
            var good = Set.Goods[pair.Good];
            var toSlot = Set.PairOf[route.To][good.Index];
            if (toSlot < 0) continue;
            var fromState = Markets[route.From].Pairs[pair.Index];
            var priceTo = EconomyMath.Trunc(Markets[route.To].Pairs[toSlot].Price, toRate, fromRate);
            var (landed, via) = LandedTo(route, good.Index, priceTo);
            var gain = landed - fromState.Price - RouteCost(route, good, from, to, fromState.Price, priceTo);
            var surplus = fromState.Stock - pair.Consumption;
            if (gain <= 0)
            {
                skipped?.Add(new RouteLine(good.Index, 0, gain, 0, landed, via, surplus, 0, 0, LineOutcome.NoGain));
                continue;
            }
            var ratio = EconomyMath.Trunc(gain, EconomyMath.Permille, fromState.Price);
            if (ratio < route.Margin)
            {
                skipped?.Add(new RouteLine(good.Index, 0, gain, ratio, landed, via, surplus, 0, 0, LineOutcome.BelowMargin));
                continue;
            }
            var wanted = surplus <= 0 ? 0 : EconomyMath.Trunc(surplus, Math.Min(route.Share, ratio), EconomyMath.Permille);
            if (wanted <= 0)
            {
                skipped?.Add(new RouteLine(good.Index, 0, gain, ratio, landed, via, surplus, 0, 0, LineOutcome.NoSurplus));
                continue;
            }
            _candidates.Add(new Candidate { Good = good.Index, GoodRank = Set.GoodRank[good.Index], Pair = pair.Index, Gain = gain, Ratio = ratio, Landed = landed, Via = via, Surplus = surplus, Wanted = wanted });
        }
        CollectionsMarshal.AsSpan(_candidates).Sort(ByGain);
        var inFlight = _shipments.Count;
        for (var rank = 1; rank <= _candidates.Count; rank++)
        {
            var c = _candidates[rank - 1];
            var line = new RouteLine(c.Good, rank, c.Gain, c.Ratio, c.Landed, c.Via, c.Surplus, c.Wanted, 0, LineOutcome.NoCapacity);
            if (remaining <= 0)
            {
                if (lines is null) break;
                lines.Add(line);
                continue;
            }
            if (inFlight >= Set.Limits.MaxShipments)
            {
                changes?.Blocks.Add(new RouteBlocked(route.Index, c.Good));
                lines?.Add(line with { Outcome = LineOutcome.Blocked });
                continue;
            }
            var quantity = Math.Min(remaining, c.Wanted);
            if (changes is not null)
            {
                var good = Set.Goods[c.Good];
                var fromState = Markets[route.From].Pairs[c.Pair];
                fromState.Stock -= quantity;
                Accrue(from, PolicyKind.Export, good, EconomyMath.Nearest(quantity, fromState.Price, EconomyMath.Permille));
                _shipments.Add(new Shipment(route.Index, c.Good, quantity, period + route.Delay));
                changes.Launches.Add(new ShipmentLaunched(route.Index, c.Good, quantity, period + route.Delay));
            }
            inFlight++;
            remaining -= quantity;
            lines?.Add(line with { Shipped = quantity, Outcome = quantity < c.Wanted ? LineOutcome.Trimmed : LineOutcome.Shipped });
        }
        if (lines is not null && skipped is not null)
        {
            skipped.Sort((a, b) => Set.GoodRank[a.Good].CompareTo(Set.GoodRank[b.Good]));
            lines.AddRange(skipped);
        }
    }

    /// <summary>The units the route can carry in the next period, after the shocks on it.</summary>
    public long CapacityNow(int route) => RouteCapacity(Set.Routes[route], Period + 1);

    /// <summary>
    /// What the loading rule would do on a route if the next period closed on the economy as it stands now: every good both ends trade, ranked, with what it was given and why. Nothing moves. With landed routing
    /// the field is computed afresh from the present prices first.
    /// </summary>
    public IReadOnlyList<RouteLine> PlanRoute(int route)
    {
        if (route < 0 || route >= Set.Routes.Count) throw new EngineCommon.EngineRefusedException(EconomyCodes.UnknownRoute, "there is no route " + route);
        if (Set.Routing == RoutingMode.Landed) ComputeLandedField();
        var lines = new List<RouteLine>();
        Allocate(Set.Routes[route], Period + 1, null, lines);
        return lines;
    }
}
