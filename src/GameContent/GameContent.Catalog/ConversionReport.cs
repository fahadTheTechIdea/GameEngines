namespace GameContent.Catalog;

/// <summary>
/// How a converter treated a feature it could not carry over exactly. <see cref="Reduced"/> means the target models a simpler version
/// and the note says what was lost; <see cref="Refused"/> means the recipe or resource is left out entirely. Nothing is dropped without a note.
/// </summary>
public enum ConversionLevel { Reduced, Refused }

public sealed record ConversionNote(ConversionLevel Level, string Code, string Subject, string Detail);

/// <summary>Every note of one conversion, in the order they were made.</summary>
public sealed class ConversionReport
{
    private readonly List<ConversionNote> _notes = new();

    public IReadOnlyList<ConversionNote> Notes => _notes;

    public void Refuse(string code, string subject, string detail) => _notes.Add(new ConversionNote(ConversionLevel.Refused, code, subject, detail));

    public void Reduce(string code, string subject, string detail) => _notes.Add(new ConversionNote(ConversionLevel.Reduced, code, subject, detail));

    public IEnumerable<ConversionNote> Refusals => _notes.Where(n => n.Level == ConversionLevel.Refused);

    /// <summary>The subjects refused or reduced under each code, counted.</summary>
    public IReadOnlyDictionary<string, int> CountsByCode() =>
        _notes.GroupBy(n => n.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
}
