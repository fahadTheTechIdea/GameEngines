using EngineCommon;
using ScenarioEngine.Contracts;

namespace EconomyEngine;

/// <summary>The multipliers (per mille) the modifiers of the document give each good this period.</summary>
internal sealed class Modifiers(int goods)
{
    public long[] Supply { get; } = Enumerable.Repeat(EconomyMath.Permille, goods).ToArray();
    public long[] Demand { get; } = Enumerable.Repeat(EconomyMath.Permille, goods).ToArray();
}

public sealed partial class EconomyRuntime
{
    /// <summary>What the document's modifiers do to each good under these host facts. A fact a modifier reads must be given, of the declared type and inside the declared range, or the call is refused before anything moves.</summary>
    private Modifiers ModifiersFor(IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        var result = new Modifiers(Set.Goods.Count);
        foreach (var modifier in Set.Modifiers)
        {
            var read = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            foreach (var id in modifier.Facts)
            {
                if (!hostFacts.TryGetValue(id, out var value)) throw new EngineRefusedException(EconomyCodes.FactMissing, "modifier '" + modifier.Id + "' reads the fact '" + id + "', which the host did not give");
                var declared = Set.HostFacts[id];
                if (value.Kind != declared.Kind) throw new EngineRefusedException(EconomyCodes.FactInvalid, "fact '" + id + "' is " + value.Kind + ", the document declares " + declared.Kind);
                if (declared.Kind == FactKind.Integer && (value.Integer < declared.Minimum || value.Integer > declared.Maximum))
                    throw new EngineRefusedException(EconomyCodes.FactInvalid, "fact '" + id + "' is " + value.Integer + ", outside " + declared.Minimum + " to " + declared.Maximum);
                read[id] = value;
            }
            if (!Conditions.Evaluate(modifier.When, read)) continue;
            var target = modifier.Kind == ModifierKind.Supply ? result.Supply : result.Demand;
            foreach (var good in Set.Goods)
                if (Selects(modifier.Good, modifier.Tag, good)) target[good.Index] = Math.Max(0, EconomyMath.Trunc(target[good.Index], EconomyMath.Permille + modifier.Permille, EconomyMath.Permille));
        }
        return result;
    }

    /// <summary>True when a good id / tag selector names the good: both empty selects every good.</summary>
    private static bool Selects(int goodIndex, string tag, GoodDef good) =>
        goodIndex == -1 && tag.Length == 0 || goodIndex == good.Index || tag.Length > 0 && good.Tags.Contains(tag);

    /// <summary>Applies the money growth of every currency (its own and its shocks') to the price level and the money supply, then moves each rate toward purchasing power parity by at most its step. Returns the price levels before.</summary>
    private long[] StartCurrencies(long period, EconomyChanges changes)
    {
        var before = new long[Currencies.Length];
        for (var c = 0; c < Currencies.Length; c++)
        {
            var state = Currencies[c];
            before[c] = state.Level;
            var growth = EconomyMath.Clamp(state.Growth + ShockGrowth(c, period), -500_000, 500_000);
            if (growth == 0) continue;
            state.Level = EconomyMath.Clamp(EconomyMath.Nearest(state.Level, EconomyMath.Ppm + growth, EconomyMath.Ppm), 1, EconomyFacts.MaxLevel);
            var minted = Math.Max(-state.Money, EconomyMath.Nearest(state.Money, growth, EconomyMath.Ppm));
            state.Money += minted;
            state.World += minted;
            state.Minted += minted;
        }
        var reference = Currencies[Set.ReferenceCurrency].Level;
        for (var c = 0; c < Currencies.Length; c++)
        {
            var def = Set.Currencies[c];
            var state = Currencies[c];
            var target = EconomyMath.Nearest(def.Anchor, reference, state.Level);
            var room = EconomyMath.Trunc(state.Rate, def.MaxRateStepPpm, EconomyMath.Ppm);
            var moved = EconomyMath.Clamp(EconomyMath.Clamp(target, state.Rate - room, state.Rate + room), 1, EconomyFacts.MaxRate);
            if (moved == state.Rate) continue;
            changes.Rates.Add(new RateChanged(c, state.Rate, moved));
            state.Rate = moved;
        }
        return before;
    }
}
