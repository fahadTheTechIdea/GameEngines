using EngineCommon;

namespace EconomyEngine;

/// <summary>
/// The words of an economy document. A document holds localisation keys only; the text comes from the shared locale tables and chains (CLDR plural and ordinal categories, fallback chains, bidi isolates).
/// Every key here is a name, so a message gives no placeholder.
/// </summary>
public static class EconomyNames
{
    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(EconomySet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var currency in set.Currencies) keys.Add(currency.Key);
        foreach (var good in set.Goods) keys.Add(good.Key);
        foreach (var band in set.Bands) keys.Add(band.Key);
        foreach (var schedule in set.Schedules) keys.Add(schedule.Key);
        foreach (var shock in set.Shocks) keys.Add(shock.Key);
        foreach (var market in set.Markets) keys.Add(market.Key);
        foreach (var recipe in set.Recipes) keys.Add(recipe.Key);
        return keys.ToList();
    }

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Currency(EconomySet set, LocaleChain chain, int currency) => Name(chain, set.Currencies[currency].Key);
    public static string Good(EconomySet set, LocaleChain chain, int good) => Name(chain, set.Goods[good].Key);
    public static string Band(EconomySet set, LocaleChain chain, int band) => Name(chain, set.Bands[band].Key);
    public static string Schedule(EconomySet set, LocaleChain chain, int schedule) => Name(chain, set.Schedules[schedule].Key);
    public static string Shock(EconomySet set, LocaleChain chain, int shock) => Name(chain, set.Shocks[shock].Key);
    public static string Market(EconomySet set, LocaleChain chain, int market) => Name(chain, set.Markets[market].Key);
    public static string Recipe(EconomySet set, LocaleChain chain, int recipe) => Name(chain, set.Recipes[recipe].Key);

    /// <summary>How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder (names give none).</summary>
    public static CoverageReport Coverage(EconomySet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, _ => (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, _ => "a name does not give");
    }
}
