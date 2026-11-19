using EngineCommon;

namespace WorldEventsEngine;

/// <summary>
/// The words of a world-events document. A document holds localisation keys only; the text comes from the shared locale tables and chains (CLDR plural and ordinal categories, fallback chains, bidi isolates). Every key
/// here is a name without a placeholder, so a message gives none. Right-to-left languages are the table's.
/// </summary>
public static class WorldNames
{
    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(WorldSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var season in set.Seasons) keys.Add(season.Key);
        foreach (var modifier in set.Modifiers) keys.Add(modifier.Key);
        foreach (var weather in set.Weathers) keys.Add(weather.Key);
        foreach (var zone in set.Zones) keys.Add(zone.Key);
        foreach (var regime in set.Regimes) keys.Add(regime.Key);
        foreach (var region in set.Regions) keys.Add(region.Key);
        foreach (var hazard in set.Hazards)
        {
            keys.Add(hazard.Key);
            foreach (var rung in hazard.Ladder) keys.Add(rung.Key);
        }
        foreach (var disease in set.Diseases) keys.Add(disease.Key);
        foreach (var intervention in set.Interventions) keys.Add(intervention.Key);
        foreach (var teller in set.Storytellers) keys.Add(teller.Key);
        foreach (var level in set.Difficulties) keys.Add(level.Key);
        foreach (var card in set.Cards) keys.Add(card.Key);
        return keys.ToList();
    }

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Season(WorldSet set, LocaleChain chain, int season) => Name(chain, set.Seasons[season].Key);
    public static string Weather(WorldSet set, LocaleChain chain, int weather) => Name(chain, set.Weathers[weather].Key);
    public static string Region(WorldSet set, LocaleChain chain, int region) => Name(chain, set.Regions[region].Key);
    public static string Hazard(WorldSet set, LocaleChain chain, int hazard) => Name(chain, set.Hazards[hazard].Key);
    public static string Rung(WorldSet set, LocaleChain chain, int hazard, int rung) => Name(chain, set.Hazards[hazard].Ladder[rung].Key);
    public static string Disease(WorldSet set, LocaleChain chain, int disease) => Name(chain, set.Diseases[disease].Key);
    public static string Intervention(WorldSet set, LocaleChain chain, int intervention) => Name(chain, set.Interventions[intervention].Key);
    public static string Card(WorldSet set, LocaleChain chain, int card) => Name(chain, set.Cards[card].Key);
    public static string Regime(WorldSet set, LocaleChain chain, int regime) => Name(chain, set.Regimes[regime].Key);
    public static string Storyteller(WorldSet set, LocaleChain chain, int teller) => Name(chain, set.Storytellers[teller].Key);
    public static string Modifier(WorldSet set, LocaleChain chain, int modifier) => Name(chain, set.Modifiers[modifier].Key);

    /// <summary>How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder (names give none).</summary>
    public static CoverageReport Coverage(WorldSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, _ => (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, _ => "a name does not give");
    }
}
