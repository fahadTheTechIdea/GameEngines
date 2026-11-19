using ScenarioEngine.Contracts;

namespace EconomyEngine;

/// <summary>
/// What the economy says about a market, as facts in the shared fact model (boolean, integer, string): the same facts feed a dialogue's conditions (declared as external facts under <c>ext.</c>) and AI sensors.
/// Fixed names: <c>market.treasury</c>, <c>market.staff</c>, <c>market.currency</c>, <c>market.arrears</c> and, for each good, <c>market.price.&lt;good&gt;</c> (thousandths of a minor unit),
/// <c>market.ratio.&lt;good&gt;</c> (per mille of its reference), <c>market.stock.&lt;good&gt;</c>, <c>market.band.&lt;good&gt;</c>, <c>market.short.&lt;good&gt;</c>; and for each currency <c>currency.&lt;id&gt;.rate</c>
/// (parts per million of the reference) and <c>currency.&lt;id&gt;.level</c> (the price level). A good the market does not trade reads 0, an empty text or false. Whole numbers saturate at <see cref="FactCeiling"/>. Any other id is the host's.
/// </summary>
public static class EconomyFacts
{
    public const string Market = "market.";
    public const string Currency = "currency.";

    public const long MaxMoney = 1_000_000_000_000_000;
    public const long MaxPrice = 10_000_000_000_000;
    public const long MaxStock = 1_000_000_000_000;
    public const long MaxLevel = 10_000_000_000_000;
    public const long MaxRate = 1_000_000_000_000;
    public const long MaxRatio = 1_000_000;

    /// <summary>
    /// The largest number a fact reports. The shared fact model of the dialogue and AI engines carries whole numbers up to a billion, so a treasury, a price, a stock, a rate or a price level above it reads as
    /// a billion ("at least that much"); a condition that needs to tell larger apart reads the economy directly.
    /// </summary>
    public const long FactCeiling = 1_000_000_000;

    private static long Cap(long value) => Math.Min(value, FactCeiling);

    /// <summary>True when the id begins with one of the two prefixes the engine owns.</summary>
    public static bool IsReserved(string id) => id.StartsWith(Market, StringComparison.Ordinal) || id.StartsWith(Currency, StringComparison.Ordinal);

    /// <summary>Every fact a document with these declarations offers, each with its type and range, plus the host's own.</summary>
    internal static IReadOnlyDictionary<string, FactDefinition> Definitions(EconomySet set)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal)
        {
            [Market + "treasury"] = new FactDefinition(FactKind.Integer, 0, FactCeiling),
            [Market + "staff"] = new FactDefinition(FactKind.Integer, 0, 1_000_000),
            [Market + "currency"] = new FactDefinition(FactKind.String),
            [Market + "arrears"] = new FactDefinition(FactKind.Boolean),
        };
        foreach (var good in set.Goods)
        {
            facts[Market + "price." + good.Id] = new FactDefinition(FactKind.Integer, 0, FactCeiling);
            facts[Market + "ratio." + good.Id] = new FactDefinition(FactKind.Integer, 0, MaxRatio);
            facts[Market + "stock." + good.Id] = new FactDefinition(FactKind.Integer, 0, FactCeiling);
            facts[Market + "band." + good.Id] = new FactDefinition(FactKind.String);
            facts[Market + "short." + good.Id] = new FactDefinition(FactKind.Boolean);
        }
        foreach (var currency in set.Currencies)
        {
            facts[Currency + currency.Id + ".rate"] = new FactDefinition(FactKind.Integer, 0, FactCeiling);
            facts[Currency + currency.Id + ".level"] = new FactDefinition(FactKind.Integer, 0, FactCeiling);
        }
        foreach (var (id, definition) in set.HostFacts) facts[id] = definition;
        return facts;
    }

    /// <summary>
    /// Every fact the engine itself offers about a market and the currencies, each id prefixed by <paramref name="prefix"/> (a dialogue document declares them as external facts under <c>ext.</c>).
    /// The host's own facts are not included.
    /// </summary>
    public static Dictionary<string, FactValue> Read(EconomyRuntime runtime, string marketId, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var market = runtime.MarketIndex(marketId);
        var set = runtime.Set;
        var def = set.Markets[market];
        var state = runtime.Markets[market];
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            [prefix + Market + "treasury"] = FactValue.From(Cap(state.Treasury)),
            [prefix + Market + "staff"] = FactValue.From(def.Staff),
            [prefix + Market + "currency"] = FactValue.From(set.Currencies[def.Currency].Id),
            [prefix + Market + "arrears"] = FactValue.From(runtime.InArrears(market)),
        };
        foreach (var good in set.Goods)
        {
            var slot = set.PairOf[market][good.Index];
            var traded = slot >= 0;
            facts[prefix + Market + "price." + good.Id] = FactValue.From(traded ? Cap(state.Pairs[slot].Price) : 0);
            facts[prefix + Market + "ratio." + good.Id] = FactValue.From(traded ? runtime.Ratio(market, slot) : 0);
            facts[prefix + Market + "stock." + good.Id] = FactValue.From(traded ? Cap(state.Pairs[slot].Stock) : 0);
            facts[prefix + Market + "band." + good.Id] = FactValue.From(traded ? set.Bands[state.Pairs[slot].Band].Id : "");
            facts[prefix + Market + "short." + good.Id] = FactValue.From(traded && state.Pairs[slot].Short);
        }
        foreach (var currency in set.Currencies)
        {
            facts[prefix + Currency + currency.Id + ".rate"] = FactValue.From(Cap(runtime.Currencies[currency.Index].Rate));
            facts[prefix + Currency + currency.Id + ".level"] = FactValue.From(Cap(runtime.Currencies[currency.Index].Level));
        }
        return facts;
    }
}
