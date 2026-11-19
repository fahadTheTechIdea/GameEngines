using EngineCommon;

namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    public const int MaxShockScale = 10_000;

    /// <summary>Money growth (ppm a period) the active shocks add to a currency this period.</summary>
    private long ShockGrowth(int currency, long period)
    {
        long growth = 0;
        foreach (var instance in _shocks)
        {
            if (instance.Start >= period) continue;
            if (instance.Market != -1 && Set.Markets[instance.Market].Currency != currency) continue;
            foreach (var effect in Set.Shocks[instance.Shock].Effects)
                if (effect.Kind == EffectKind.Growth) growth += EconomyMath.Trunc(effect.Amount, instance.Scale, EconomyMath.Permille);
        }
        return growth;
    }

    /// <summary>
    /// Starts a shock (a drought, a war, a devaluation, a decree): <paramref name="marketId"/> is the market it hits, or empty for every market. Its price jumps and its rate jump happen now; its supply, demand,
    /// route and money-growth effects apply from the next period for the shock's duration, scaled by <paramref name="scalePermille"/>. This is the hook for an event system to apply shocks.
    /// </summary>
    public EconomyChanges ApplyShock(string shockId, string marketId, int scalePermille)
    {
        if (!Set.ShockById.TryGetValue(CheckId(shockId, "shock"), out var shock)) throw new EngineRefusedException(EconomyCodes.UnknownShock, "there is no shock '" + shockId + "'");
        var market = marketId.Length == 0 ? -1 : MarketIndex(marketId);
        if (scalePermille < 1 || scalePermille > MaxShockScale) throw new EngineRefusedException(EconomyCodes.Range, "a shock scale is 1 to " + MaxShockScale + " per mille, not " + scalePermille);
        if (_shocks.Count >= Set.Limits.MaxShocks) throw new EngineRefusedException(EconomyCodes.Capacity, "there are already " + Set.Limits.MaxShocks + " shocks on");
        foreach (var effect in shock.Effects)
            if (effect.Kind == EffectKind.Rate && market >= 0 && Set.Markets[market].Currency == Set.ReferenceCurrency)
                throw new EngineRefusedException(EconomyCodes.Shock, "shock '" + shock.Id + "' devalues a currency, and market '" + marketId + "' uses the reference currency, whose rate is fixed");
        var changes = new EconomyChanges();
        _shocks.Add(new ShockInstance(shock.Index, market, Period, scalePermille));
        changes.Shocks.Add(new ShockChanged(shock.Index, market, true));
        foreach (var effect in shock.Effects)
        {
            var jump = EconomyMath.Permille + EconomyMath.Trunc(effect.Amount, scalePermille, EconomyMath.Permille);
            if (effect.Kind == EffectKind.Price) JumpPrices(market, effect, jump);
            else if (effect.Kind == EffectKind.Rate) JumpRates(market, jump, changes);
        }
        return changes;
    }

    private void JumpPrices(int market, EffectDef effect, long jump)
    {
        for (var m = 0; m < Set.Markets.Count; m++)
        {
            if (market != -1 && market != m) continue;
            var def = Set.Markets[m];
            foreach (var pair in def.Pairs)
            {
                var good = Set.Goods[pair.Good];
                if (!Selects(effect.Good, effect.Tag, good)) continue;
                var reference = ReferencePrice(def.Currency, good);
                var pairState = Markets[m].Pairs[pair.Index];
                var low = Math.Max(1, EconomyMath.Trunc(reference, good.Floor, EconomyMath.Permille));
                var high = Math.Max(low, Math.Min(EconomyFacts.MaxPrice, EconomyMath.Trunc(reference, good.Ceiling, EconomyMath.Permille)));
                pairState.Price = EconomyMath.Clamp(EconomyMath.Trunc(pairState.Price, Math.Max(0, jump), EconomyMath.Permille), low, high);
                pairState.Band = BandOf(EconomyMath.Trunc(pairState.Price, EconomyMath.Permille, reference), pairState.Band);
            }
        }
    }

    private void JumpRates(int market, long jump, EconomyChanges changes)
    {
        for (var c = 0; c < Currencies.Length; c++)
        {
            if (c == Set.ReferenceCurrency) continue;
            if (market != -1 && Set.Markets[market].Currency != c) continue;
            var moved = EconomyMath.Clamp(EconomyMath.Trunc(Currencies[c].Rate, Math.Max(0, jump), EconomyMath.Permille), 1, EconomyFacts.MaxRate);
            if (moved == Currencies[c].Rate) continue;
            changes.Rates.Add(new RateChanged(c, Currencies[c].Rate, moved));
            Currencies[c].Rate = moved;
        }
    }

    /// <summary>Removes the shocks whose last period was this one.</summary>
    private void EndShocks(long period, EconomyChanges changes)
    {
        for (var i = 0; i < _shocks.Count;)
        {
            var instance = _shocks[i];
            if (period < instance.Start + Set.Shocks[instance.Shock].Duration)
            {
                i++;
                continue;
            }
            _shocks.RemoveAt(i);
            changes.Shocks.Add(new ShockChanged(instance.Shock, instance.Market, false));
        }
    }
}
