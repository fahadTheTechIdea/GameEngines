using EngineCommon;

namespace DiplomacyEngine;

/// <summary>
/// The words of a diplomacy document. A document holds localisation keys only; the text comes from the shared locale tables and chains (CLDR plural and ordinal categories, fallback chains, bidi isolates). Every key here
/// is a name without a placeholder, so a message gives none. Right-to-left languages are the table's.
/// </summary>
public static class DipNames
{
    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(DipSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var season in set.Seasons) keys.Add(season.Key);
        foreach (var ideology in set.Ideologies) keys.Add(ideology.Key);
        foreach (var trait in set.Traits) keys.Add(trait.Key);
        foreach (var deed in set.Deeds) keys.Add(deed.Key);
        foreach (var cb in set.CasusBelli) keys.Add(cb.Key);
        foreach (var mission in set.Missions) keys.Add(mission.Key);
        foreach (var type in set.Types)
        {
            keys.Add(type.Key);
            foreach (var modifier in type.Modifiers) keys.Add(modifier.Key);
        }
        foreach (var realm in set.Realms)
        {
            keys.Add(realm.Key);
            foreach (var faction in realm.Factions) keys.Add(faction.Key);
        }
        foreach (var agent in set.Agents) keys.Add(agent.Key);
        return keys.ToList();
    }

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Season(DipSet set, LocaleChain chain, int season) => Name(chain, set.Seasons[season].Key);
    public static string Realm(DipSet set, LocaleChain chain, int realm) => Name(chain, set.Realms[realm].Key);
    public static string Type(DipSet set, LocaleChain chain, int type) => Name(chain, set.Types[type].Key);
    public static string CasusBelli(DipSet set, LocaleChain chain, int cb) => Name(chain, set.CasusBelli[cb].Key);
    public static string Deed(DipSet set, LocaleChain chain, int deed) => Name(chain, set.Deeds[deed].Key);
    public static string Mission(DipSet set, LocaleChain chain, int mission) => Name(chain, set.Missions[mission].Key);
    public static string Ideology(DipSet set, LocaleChain chain, int ideology) => Name(chain, set.Ideologies[ideology].Key);
    public static string Trait(DipSet set, LocaleChain chain, int trait) => Name(chain, set.Traits[trait].Key);
    public static string Faction(DipSet set, LocaleChain chain, int realm, int faction) => Name(chain, set.Realms[realm].Factions[faction].Key);

    /// <summary>How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder (names give none).</summary>
    public static CoverageReport Coverage(DipSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, _ => (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, _ => "a name does not give");
    }
}
