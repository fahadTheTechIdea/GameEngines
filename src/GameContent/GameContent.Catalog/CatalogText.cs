namespace GameContent.Catalog;

/// <summary>
/// The one place a display name is chosen for a language. A pack's definitions carry its names in the pack's own language; other
/// languages come from its locale documents. There is no fallback: an id with no text in the asked language gives null, and the
/// caller decides what to show (a placeholder, the source name, a request for translation).
/// </summary>
public static class CatalogText
{
    /// <summary>The name of a resource, station, skill or recipe in <paramref name="language"/>, or null if the owning pack has no text for it.</summary>
    public static string? NameOf(CatalogSet set, string id, string language)
    {
        ArgumentNullException.ThrowIfNull(set);
        var owner = set.Packs.FirstOrDefault(p => id.StartsWith(p.Id + ".", StringComparison.Ordinal));
        if (owner is null) return null;
        if (language == owner.Language) return SourceName(owner, id);
        return owner.Locales.FirstOrDefault(l => l.Language == language)?.Texts.GetValueOrDefault(id);
    }

    private static string? SourceName(CatalogPackData pack, string id) =>
        pack.Resources.FirstOrDefault(r => r.Id == id)?.Name
        ?? pack.Stations.FirstOrDefault(s => s.Id == id)?.Name
        ?? pack.Skills.FirstOrDefault(s => s.Id == id)?.Name
        ?? pack.Recipes.FirstOrDefault(r => r.Id == id)?.Name
        ?? pack.Eras.FirstOrDefault(e => e.Id == id)?.Name
        ?? pack.Stats.FirstOrDefault(s => s.Id == id)?.Name;

    /// <summary>How many of a pack's own ids have a text in each language it offers: its own language is complete by definition.</summary>
    public static IReadOnlyDictionary<string, (int Translated, int Total)> Coverage(CatalogPackData pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var total = CatalogValidator.OwnIds(pack).Count();
        var result = new SortedDictionary<string, (int, int)>(StringComparer.Ordinal) { [pack.Language] = (total, total) };
        foreach (var locale in pack.Locales) result[locale.Language] = (locale.Texts.Count, total);
        return result;
    }
}
