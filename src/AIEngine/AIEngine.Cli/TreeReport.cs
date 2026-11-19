public enum TreeLevel { Reduced, Refused }

public sealed record TreeNote(TreeLevel Level, string Code, string Subject, string Detail);

/// <summary>
/// The capability report of one import, in the style of the catalogue converters: what was carried over exactly (counted by construct), what was reduced with a stated loss and what
/// was refused. Nothing is dropped without a note, and a single refusal means no document is written.
/// </summary>
public sealed class TreeReport
{
    private readonly List<TreeNote> _notes = new();
    private readonly SortedDictionary<string, int> _supported = new(StringComparer.Ordinal);

    public IReadOnlyList<TreeNote> Notes => _notes;

    /// <summary>The constructs carried over exactly, by name, with how many.</summary>
    public IReadOnlyDictionary<string, int> Supported => _supported;

    public bool HasRefusals => _notes.Any(n => n.Level == TreeLevel.Refused);

    public void Refuse(string code, string subject, string detail) => _notes.Add(new TreeNote(TreeLevel.Refused, code, subject, detail));

    public void Reduce(string code, string subject, string detail) => _notes.Add(new TreeNote(TreeLevel.Reduced, code, subject, detail));

    public void Support(string construct) => _supported[construct] = _supported.GetValueOrDefault(construct) + 1;
}
