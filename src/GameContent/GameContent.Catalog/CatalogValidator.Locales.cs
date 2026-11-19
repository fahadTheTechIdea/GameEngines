namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>The ids a pack defines itself: the only things its locale documents may name.</summary>
    public static IEnumerable<string> OwnIds(CatalogPackData pack) =>
        pack.Resources.Select(r => r.Id).Concat(pack.Stations.Select(s => s.Id)).Concat(pack.Skills.Select(s => s.Id)).Concat(pack.Recipes.Select(r => r.Id))
            .Concat(pack.Eras.Select(e => e.Id)).Concat(pack.Stats.Select(s => s.Id));

    /// <summary>A locale text must name something its own pack defines; a typo would otherwise be a translation nothing ever shows.</summary>
    private static void CheckLocales(CatalogSet set, CatalogReport report)
    {
        foreach (var pack in set.Packs)
        {
            var own = OwnIds(pack).ToHashSet(StringComparer.Ordinal);
            foreach (var locale in pack.Locales)
                foreach (var id in locale.Texts.Keys.Order(StringComparer.Ordinal).Where(id => !own.Contains(id)))
                    report.Add(CatalogCodes.LocaleUnknownId, "the " + locale.Language + " text names '" + id + "', which pack '" + pack.Id + "' does not define", pack.Id + "/" + locale.Language);
        }
    }
}
