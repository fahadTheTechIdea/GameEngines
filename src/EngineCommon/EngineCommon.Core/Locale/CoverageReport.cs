namespace EngineCommon;

/// <summary>A message that cannot be used with the line it is for: <paramref name="Why"/> says what is wrong with it.</summary>
public sealed record MessageProblem(string Key, string Why);

/// <summary>
/// How much of a document a locale covers. Nothing is thrown: a translator or a build step reads the lists. <see cref="Missing"/> are keys the document uses and the table lacks,
/// <see cref="Unknown"/> are messages no document uses (left behind by an edit), <see cref="Problems"/> are messages that use a placeholder their line does not give.
/// </summary>
public sealed record CoverageReport(string Locale, int Total, IReadOnlyList<string> Missing, IReadOnlyList<string> Unknown, IReadOnlyList<MessageProblem> Problems)
{
    public int Translated => Total - Missing.Count;

    /// <summary>Whole percent of the keys that have a message (100 for a document with none).</summary>
    public int Percent => Total == 0 ? 100 : Translated * 100 / Total;

    public bool IsComplete => Missing.Count == 0 && Unknown.Count == 0 && Problems.Count == 0;
}