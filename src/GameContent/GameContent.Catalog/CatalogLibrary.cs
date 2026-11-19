namespace GameContent.Catalog;

/// <summary>
/// Rules that only make sense across the packs of one library: a pack is validated with its dependencies, so it cannot see that a
/// sibling genre pack defines the same thing. Two packs that each define "Bread" (or a "Cooking" skill, or a "Kitchen") are a
/// duplicate; the shared thing belongs in a pack both depend on (usually <c>core</c>).
/// </summary>
public static class CatalogLibrary
{
    private static string Slug(string id) => id[(id.IndexOf('.') + 1)..];

    private static string Normal(string name) => string.Join(' ', name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private sealed record Entry(string Pack, string Id, string Name, string Kind);

    /// <summary>Resources, stations and skills with the same display name or the same slug in two different packs of the library.</summary>
    public static CatalogReport CheckDuplicates(IEnumerable<CatalogSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);
        var report = new CatalogReport();
        var entries = sets.Select(s => s.Root).DistinctBy(p => p.Id).OrderBy(p => p.Id, StringComparer.Ordinal)
            .SelectMany(p => p.Resources.Select(r => new Entry(p.Id, r.Id, r.Name, "resource"))
                .Concat(p.Stations.Select(s => new Entry(p.Id, s.Id, s.Name, "station")))
                .Concat(p.Skills.Select(s => new Entry(p.Id, s.Id, s.Name, "skill"))))
            .ToList();
        foreach (var group in entries.GroupBy(e => e.Kind + "|" + Normal(e.Name), StringComparer.Ordinal).Where(g => g.Select(e => e.Pack).Distinct().Count() > 1))
            report.Add(CatalogCodes.DuplicateName, "the " + group.First().Kind + " '" + Normal(group.First().Name) + "' is defined by " + string.Join(", ", group.Select(e => e.Id)) + "; move the shared one to a pack they all depend on", group.First().Id);
        foreach (var group in entries.GroupBy(e => e.Kind + "|" + Slug(e.Id), StringComparer.Ordinal).Where(g => g.Select(e => e.Pack).Distinct().Count() > 1))
            report.Add(CatalogCodes.DuplicateSlug, "the " + group.First().Kind + " slug '" + Slug(group.First().Id) + "' is used by " + string.Join(", ", group.Select(e => e.Id)), group.First().Id);
        return report;
    }
}
