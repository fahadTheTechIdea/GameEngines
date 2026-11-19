using EngineCommon;

namespace TutorialEngine;

/// <summary>
/// The words of a tutorial document. A document holds localisation keys only; the text comes from the shared locale tables and chains (CLDR plural and ordinal categories, fallback chains, bidi isolates).
/// Every key here is a name or a sentence without a placeholder, so a message gives none. Right-to-left languages are the table's: the engine only supplies the keys and the language chosen.
/// </summary>
public static class TutorialNames
{
    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(TutorialSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var skill in set.Skills) keys.Add(skill.Key);
        foreach (var step in set.Steps)
        {
            keys.Add(step.Key);
            keys.Add(step.BodyKey);
            foreach (var hint in step.Hints) keys.Add(hint.Key);
        }
        foreach (var tip in set.Tips) keys.Add(tip.Key);
        foreach (var page in set.Journey.Pages)
        {
            keys.Add(page.Key);
            keys.Add(page.BodyKey);
            keys.Add(page.ArtKey);
        }
        return keys.ToList();
    }

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Step(TutorialSet set, LocaleChain chain, int step) => Name(chain, set.Steps[step].Key);
    public static string StepBody(TutorialSet set, LocaleChain chain, int step) => Name(chain, set.Steps[step].BodyKey);
    public static string Hint(TutorialSet set, LocaleChain chain, int step, int level) => Name(chain, set.Steps[step].Hints[level - 1].Key);
    public static string Tip(TutorialSet set, LocaleChain chain, int tip) => Name(chain, set.Tips[tip].Key);
    public static string Skill(TutorialSet set, LocaleChain chain, int skill) => Name(chain, set.Skills[skill].Key);
    public static string PageTitle(TutorialSet set, LocaleChain chain, int page) => Name(chain, set.Journey.Pages[page].Key);
    public static string PageBody(TutorialSet set, LocaleChain chain, int page) => Name(chain, set.Journey.Pages[page].BodyKey);

    /// <summary>How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder (names give none).</summary>
    public static CoverageReport Coverage(TutorialSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, _ => (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, _ => "a name does not give");
    }

    /// <summary>The languages of the journey that have a table in <paramref name="tables"/>, and those without: a language the player may choose but has no words for.</summary>
    public static (IReadOnlyList<string> Covered, IReadOnlyList<string> Missing) LanguageSupport(TutorialSet set, IEnumerable<LocaleTable> tables)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(tables);
        var have = tables.Select(t => t.Locale).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var covered = set.Journey.Languages.Where(have.Contains).ToList();
        var missing = set.Journey.Languages.Where(l => !have.Contains(l)).ToList();
        return (covered, missing);
    }
}
