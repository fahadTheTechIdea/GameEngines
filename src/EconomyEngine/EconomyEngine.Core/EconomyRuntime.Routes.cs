namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    private int TariffRate(MarketDef market, GoodDef good, PolicyKind kind)
    {
        var rate = 0;
        for (var slot = 0; slot < market.Policies.Count; slot++) // indexed, not foreach: the enumerator of an interface list is an allocation, and this is called twice per route and good in the landed field
        {
            var policy = Set.Policies[market.Policies[slot]];
            if (policy.Kind == kind && Matches(policy.Tags, good)) rate += policy.Rate;
        }
        return Math.Min(rate, 1000);
    }

    /// <summary>Adds to every accrual of the market whose policy is of this kind and covers the good: the policy's rate of the value.</summary>
    private void Accrue(MarketDef market, PolicyKind kind, GoodDef good, long value)
    {
        var state = Markets[market.Index];
        for (var slot = 0; slot < market.Policies.Count; slot++)
        {
            var policy = Set.Policies[market.Policies[slot]];
            if (policy.Kind == kind && Matches(policy.Tags, good)) state.Accrued[slot] += EconomyMath.Nearest(value, policy.Rate, EconomyMath.Permille);
        }
    }

    /// <summary>Shipments whose time has come join the stock of their destination (what does not fit its warehouse is lost) and pay the import tariff on their value.</summary>
    private void Arrive(long period, EconomyChanges changes)
    {
        if (_shipments.Count == 0) return;
        var kept = 0;
        for (var i = 0; i < _shipments.Count; i++)
        {
            var shipment = _shipments[i];
            if (shipment.Arrives > period)
            {
                _shipments[kept++] = shipment;
                continue;
            }
            var route = Set.Routes[shipment.Route];
            var good = Set.Goods[shipment.Good];
            var market = Set.Markets[route.To];
            var slot = Set.PairOf[route.To][good.Index];
            var pairState = Markets[route.To].Pairs[slot];
            pairState.Stock = Math.Min(market.Pairs[slot].Capacity, pairState.Stock + shipment.Quantity);
            Accrue(market, PolicyKind.Import, good, EconomyMath.Nearest(shipment.Quantity, pairState.Price, EconomyMath.Permille));
            changes.Arrivals.Add(new ShipmentArrived(shipment.Route, shipment.Good, shipment.Quantity));
        }
        _shipments.RemoveRange(kept, _shipments.Count - kept);
    }

    private long RouteCapacity(RouteDef route, long period)
    {
        var product = EconomyMath.Permille;
        foreach (var instance in _shocks)
        {
            if (instance.Start >= period || instance.Market != -1 && instance.Market != route.From) continue;
            foreach (var effect in Set.Shocks[instance.Shock].Effects)
                if (effect.Kind == EffectKind.Route && (effect.Route == -1 || effect.Route == route.Index))
                    product = Math.Max(0, EconomyMath.Trunc(product, EconomyMath.Permille + EconomyMath.Trunc(effect.Amount, instance.Scale, EconomyMath.Permille), EconomyMath.Permille));
        }
        return Scale(route.Capacity, product);
    }

    /// <summary>What a unit of the good pays to go on the route, in the origin's money: the freight, the export tariff on the origin price and the import tariff on the destination price (given in the origin's money).</summary>
    private long RouteCost(RouteDef route, GoodDef good, MarketDef from, MarketDef to, long fromPrice, long priceToInFromMoney) =>
        RouteCost(route, good, fromPrice, priceToInFromMoney, TariffRate(from, good, PolicyKind.Export), TariffRate(to, good, PolicyKind.Import));

    /// <summary>The same cost with the two tariff rates already known (the landed field finds each market's rates once per good, not once per route).</summary>
    private static long RouteCost(RouteDef route, GoodDef good, long fromPrice, long priceToInFromMoney, int exportRate, int importRate) =>
        checked(good.Weight * route.CostPerWeight) + EconomyMath.Trunc(fromPrice, exportRate, EconomyMath.Permille)
        + EconomyMath.Trunc(priceToInFromMoney, importRate, EconomyMath.Permille);
}
