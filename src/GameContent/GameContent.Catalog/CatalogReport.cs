namespace GameContent.Catalog;

/// <summary>
/// How serious a finding is. An <see cref="Error"/> refuses the pack. A <see cref="Warning"/> is a balance or authoring concern the
/// pack may still ship with, but it is reported by every tool and the shipped packs are held to none.
/// </summary>
public enum CatalogSeverity { Error, Warning }

/// <summary>One finding: the rule (code), what is wrong, and where (file or entity id).</summary>
public sealed record CatalogDiagnostic(string Code, string Message, string Location, CatalogSeverity Severity = CatalogSeverity.Error);

/// <summary>Every finding of one check. A pack is acceptable when the report has no errors; warnings are listed beside them.</summary>
public sealed class CatalogReport
{
    private readonly List<CatalogDiagnostic> _items = new();

    public IReadOnlyList<CatalogDiagnostic> Items => _items;
    public IEnumerable<CatalogDiagnostic> Errors => _items.Where(d => d.Severity == CatalogSeverity.Error);
    public IEnumerable<CatalogDiagnostic> Warnings => _items.Where(d => d.Severity == CatalogSeverity.Warning);
    public bool IsValid => !Errors.Any();

    /// <summary>Adds a finding unless the very same one (rule, text and place) is already there: a rule that checks both ends of a range reports once.</summary>
    public void Add(string code, string message, string location) => Include(new CatalogDiagnostic(code, message, location));

    public void Warn(string code, string message, string location) => Include(new CatalogDiagnostic(code, message, location, CatalogSeverity.Warning));

    private void Include(CatalogDiagnostic item)
    {
        if (!_items.Contains(item)) _items.Add(item);
    }

    public void AddRange(CatalogReport other) => _items.AddRange(other._items);

    public bool Has(string code) => _items.Any(d => d.Code == code);

    public override string ToString() => string.Join("\n", _items.Select(d => (d.Severity == CatalogSeverity.Warning ? "warning " : "") + d.Code + " " + d.Location + ": " + d.Message));
}
