using EngineCommon;

namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    private static bool Matches(IReadOnlyList<string> tags, GoodDef good)
    {
        if (tags.Count == 0) return true;
        for (var i = 0; i < tags.Count; i++)
            if (good.Tags.Contains(tags[i])) return true;
        return false;
    }

    /// <summary>The sales tax rate (per mille) a market charges on a good: the sum of its sales policies that cover the good, at most 1000.</summary>
    internal int SalesRate(MarketDef market, GoodDef good)
    {
        var rate = 0;
        foreach (var index in market.Policies)
        {
            var policy = Set.Policies[index];
            if (policy.Kind == PolicyKind.Sales && Matches(policy.Tags, good)) rate += policy.Rate;
        }
        return Math.Min(rate, 1000);
    }

    private long Shocked(int market, GoodDef good, EffectKind kind, long period)
    {
        var product = EconomyMath.Permille;
        foreach (var instance in _shocks)
        {
            if (instance.Start >= period || instance.Market != -1 && instance.Market != market) continue;
            foreach (var effect in Set.Shocks[instance.Shock].Effects)
                if (effect.Kind == kind && Selects(effect.Good, effect.Tag, good))
                    product = Math.Max(0, EconomyMath.Trunc(product, EconomyMath.Permille + EconomyMath.Trunc(effect.Amount, instance.Scale, EconomyMath.Permille), EconomyMath.Permille));
        }
        return product;
    }

    private long Scale(long value, long permille) => EconomyMath.Trunc(value, permille, EconomyMath.Permille);

    /// <summary>
    /// Walras's excess demand as a fraction of the traded volume, per mille (-1000 to 1000): what is wanted less what is made, plus the gap to the stock the market wants to keep spread over the settling periods.
    /// </summary>
    internal long Imbalance(long demand, long supply, long gap)
    {
        var excess = demand - supply + EconomyMath.Trunc(gap, 1, Set.Price.SettlePeriods);
        var norm = Math.Max(1, (demand + supply + 1) / 2);
        return EconomyMath.Clamp(EconomyMath.Trunc(excess, EconomyMath.Permille, norm), -EconomyMath.Permille, EconomyMath.Permille);
    }

    /// <summary>The change of price (per mille) an imbalance gives: a fraction of it (the good's speed), blended with the last change by the good's inertia, and capped by its largest step.</summary>
    internal static long PriceChange(GoodDef good, long lastChange, long imbalance)
    {
        var raw = EconomyMath.Trunc(imbalance, good.Speed, EconomyMath.Permille);
        var smoothed = EconomyMath.Trunc(lastChange, good.Inertia, EconomyMath.Permille) + EconomyMath.Trunc(raw, EconomyMath.Permille - good.Inertia, EconomyMath.Permille);
        return EconomyMath.Clamp(smoothed, -good.MaxStep, good.MaxStep);
    }

    /// <summary>
    /// Closes one period of one market. For each good: prices follow the price level; stock spoils; supply and demand respond to the price (bounded); demand is met from stock and output; sales tax accrues; the price
    /// moves toward the balance of supply, demand and the stock gap (Walras's rule: the price rises with excess demand), at a speed, smoothed by inertia and capped; the band and the history are kept.
    /// </summary>
    private void UpdateMarket(int index, long period, long[] levelBefore, Modifiers modifiers, EconomyChanges changes)
    {
        var def = Set.Markets[index];
        var state = Markets[index];
        var currency = Currencies[def.Currency];
        var rules = Set.Price;
        var penalty = InArrears(index) ? rules.ArrearsPenalty : 0;
        var subsidies = def.Policies.Where(p => Set.Policies[p].Kind == PolicyKind.Subsidy).ToArray();
        long estimated = 0;
        foreach (var pair in def.Pairs)
            foreach (var p in subsidies)
                if (Matches(Set.Policies[p].Tags, Set.Goods[pair.Good])) estimated += EconomyMath.Nearest(pair.Production, Set.Policies[p].PerUnit, EconomyMath.Permille);
        var ratio = estimated > state.Treasury ? EconomyMath.Trunc(state.Treasury, EconomyMath.Permille, estimated) : EconomyMath.Permille;
        var due = new long[subsidies.Length];
        var history = Set.Limits.History;
        foreach (var pair in def.Pairs)
        {
            var good = Set.Goods[pair.Good];
            var pairState = state.Pairs[pair.Index];
            pairState.Price = EconomyMath.Clamp(EconomyMath.Trunc(pairState.Price, currency.Level, levelBefore[def.Currency]), 1, EconomyFacts.MaxPrice);
            var reference = ReferencePrice(def.Currency, good);
            var salesRate = SalesRate(def, good);
            var uplift = 0L;
            var effective = subsidies.Length == 0 ? Array.Empty<long>() : new long[subsidies.Length];
            for (var j = 0; j < subsidies.Length; j++)
            {
                var policy = Set.Policies[subsidies[j]];
                if (!Matches(policy.Tags, good)) continue;
                effective[j] = EconomyMath.Trunc(policy.PerUnit, ratio, EconomyMath.Permille);
                uplift += effective[j];
            }
            var consumerRatio = EconomyMath.Trunc(Scale(pairState.Price, EconomyMath.Permille + salesRate), EconomyMath.Permille, reference);
            var producerRatio = EconomyMath.Trunc(pairState.Price + uplift, EconomyMath.Permille, reference);
            var supplyResponse = EconomyMath.Clamp(EconomyMath.Permille + EconomyMath.Trunc(good.SupplyElasticity, producerRatio - EconomyMath.Permille, EconomyMath.Permille), rules.ResponseMin, rules.ResponseMax);
            var demandResponse = EconomyMath.Clamp(EconomyMath.Permille - EconomyMath.Trunc(good.DemandElasticity, consumerRatio - EconomyMath.Permille, EconomyMath.Permille), rules.ResponseMin, rules.ResponseMax);
            var noise = rules.Noise == 0 ? 0 : DeterministicRoll.Below(Set.Seed, "demand", period * Set.PairCount + Set.PairBase[index] + pair.Index, 2L * rules.Noise + 1) - rules.Noise;
            var supply = Scale(Scale(Scale(Scale(pair.Production, supplyResponse), modifiers.Supply[good.Index]), Shocked(index, good, EffectKind.Supply, period)), EconomyMath.Permille - penalty);
            var demand = Scale(Scale(Scale(Scale(pair.Consumption, demandResponse), modifiers.Demand[good.Index]), Shocked(index, good, EffectKind.Demand, period)), EconomyMath.Permille + noise);
            var held = pairState.Stock - EconomyMath.Trunc(pairState.Stock, good.Decay, EconomyMath.Permille);
            var available = held + supply;
            var met = Math.Min(demand, available);
            var stock = Math.Min(pair.Capacity, available - met);
            var value = EconomyMath.Nearest(met, pairState.Price, EconomyMath.Permille);
            for (var slot = 0; slot < def.Policies.Count; slot++)
            {
                var policy = Set.Policies[def.Policies[slot]];
                if (policy.Kind == PolicyKind.Sales && Matches(policy.Tags, good)) state.Accrued[slot] += EconomyMath.Nearest(value, policy.Rate, EconomyMath.Permille);
            }
            for (var j = 0; j < subsidies.Length; j++) due[j] += EconomyMath.Nearest(supply, effective[j], EconomyMath.Permille);
            var spotDemand = pairState.SpotDemand;
            var spotSupply = pairState.SpotSupply;
            var gap = pair.Cover * pair.Consumption - stock;
            var change = PriceChange(good, pairState.LastChange, Imbalance(demand + spotDemand, supply + spotSupply, gap));
            var low = Math.Max(1, EconomyMath.Trunc(reference, good.Floor, EconomyMath.Permille));
            var high = Math.Min(EconomyFacts.MaxPrice, EconomyMath.Trunc(reference, good.Ceiling, EconomyMath.Permille));
            var price = EconomyMath.Clamp(EconomyMath.Trunc(pairState.Price, EconomyMath.Permille + change, EconomyMath.Permille), low, Math.Max(low, high));
            var shortage = demand > met;
            if (shortage != pairState.Short) changes.Shortages.Add(new ShortageChanged(index, good.Index, shortage));
            var band = BandOf(EconomyMath.Trunc(price, EconomyMath.Permille, reference), pairState.Band);
            if (band != pairState.Band) changes.Bands.Add(new BandChanged(index, good.Index, pairState.Band, band));
            pairState.Price = price;
            pairState.Stock = stock;
            pairState.LastChange = change;
            pairState.Supply = supply;
            pairState.Demand = demand;
            pairState.Met = met;
            pairState.Short = shortage;
            pairState.Band = band;
            pairState.SpotDemand = 0;
            pairState.SpotSupply = 0;
            if (history > 0) state.History[pair.Index * history + (int)(period % history)] = price;
        }
        for (var j = 0; j < subsidies.Length; j++)
        {
            if (due[j] == 0) continue;
            var paid = Math.Min(due[j], state.Treasury);
            state.Treasury -= paid;
            currency.World += paid;
            changes.Settlements.Add(new Settled(index, SettleKind.Subsidy, Set.Policies[subsidies[j]].Id, paid, due[j]));
        }
    }
}
