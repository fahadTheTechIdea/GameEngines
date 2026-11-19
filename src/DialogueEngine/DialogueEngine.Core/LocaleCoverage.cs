using EngineCommon;

namespace DialogueEngine;

/// <summary>
/// Checks locale tables against a dialogue document: every key the document uses has a message, the table has no message the document does not use, and no message uses a placeholder its line does not
/// give. The rule is <see cref="CoverageRules"/>, shared with every engine that has words; this class only says which keys and placeholders a dialogue document has. <see cref="Report"/> lists every
/// problem; <see cref="Check"/> run when content is loaded refuses at the first, so a missing translation is found before a player does.
/// </summary>
public static class LocaleCoverage
{
    public static CoverageReport Report(DialogueSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        return CoverageRules.Report(set.Keys, table, _ => "the line does not give");
    }

    public static void Check(DialogueSet set, LocaleTable table) => CoverageRules.Check(Report(set, table));
}
