namespace EngineCommon;

/// <summary>
/// The one rule for how a locale table covers a document, whichever engine the document belongs to: every key the document uses has a message, the table has no message the document does not use, and no
/// message uses a placeholder its key does not give. An engine states only which keys its document uses and which placeholders each gives.
/// </summary>
public static class CoverageRules
{
    /// <param name="keys">Every key the document uses, each with the placeholder names it gives.</param>
    /// <param name="table">The locale table checked.</param>
    /// <param name="notGiven">For a key, the words that complete "which ..." when a message uses a placeholder the key does not give.</param>
    public static CoverageReport Report(IReadOnlyDictionary<string, IReadOnlyList<string>> keys, LocaleTable table, Func<string, string> notGiven)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(notGiven);
        var missing = new List<string>();
        var problems = new List<MessageProblem>();
        foreach (var (key, given) in keys)
        {
            if (!table.Messages.TryGetValue(key, out var message)) { missing.Add(key); continue; }
            var stray = message.Placeholders().FirstOrDefault(name => !given.Contains(name));
            if (stray is not null) problems.Add(new MessageProblem(key, "uses '{" + stray + "}', which " + notGiven(key)));
        }
        var unknown = table.Messages.Keys.Where(k => !keys.ContainsKey(k)).ToList();
        return new CoverageReport(table.Locale, keys.Count, missing, unknown, problems);
    }

    /// <summary>Refuses at the first problem of the report: a missing key, then a placeholder the key does not give, then a message no document uses.</summary>
    public static void Check(CoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Missing.Count > 0) throw new EngineRefusedException(LocaleCodes.MissingKey, "locale '" + report.Locale + "' has no message for '" + report.Missing[0] + "'");
        if (report.Problems.Count > 0) throw new EngineRefusedException(LocaleCodes.Placeholder, "message '" + report.Problems[0].Key + "' in '" + report.Locale + "' " + report.Problems[0].Why);
        if (report.Unknown.Count > 0) throw new EngineRefusedException(LocaleCodes.UnknownKey, "locale '" + report.Locale + "' has a message '" + report.Unknown[0] + "' no document uses");
    }
}
