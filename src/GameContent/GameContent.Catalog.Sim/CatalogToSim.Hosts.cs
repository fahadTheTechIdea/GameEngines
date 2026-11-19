namespace GameContent.Catalog;

public static partial class CatalogToSim
{
    /// <summary>
    /// One processor entity of the generated world. A station that recipes use at several levels becomes one entity per level in use
    /// (<c>catalog.at.&lt;station&gt;</c> for level 1, <c>.l2</c>, <c>.l3</c>...), and a higher level also runs every recipe of the lower
    /// levels, so building the level 2 entity is what a station upgrade means. Recipes with no station run on the shared hand entity.
    /// </summary>
    private sealed record Host(string Id, string? Station, int Level)
    {
        public bool Runs(CatalogRecipe recipe) =>
            recipe.Station is { } needed ? Station == needed.Station && needed.Level <= Level : Station is null;
    }

    private static List<Host> Hosts(List<CatalogRecipe> recipes, ConversionReport report)
    {
        var hosts = new List<Host>();
        if (recipes.Any(r => r.Station is null)) hosts.Add(new Host(HandEntity, null, 0));
        foreach (var group in recipes.Where(r => r.Station is not null).GroupBy(r => r.Station!.Station, StringComparer.Ordinal))
        {
            var levels = group.Select(r => r.Station!.Level).Distinct().Order().ToList();
            foreach (var level in levels) hosts.Add(new Host("catalog.at." + group.Key + (level > 1 ? ".l" + level : ""), group.Key, level));
            if (levels.Count > 1)
                report.Reduce(SimConversionCodes.StationLevelsAsEntities, group.Key, "levels " + string.Join(", ", levels) + " become separate processor entities; a higher level also runs the lower levels' recipes, and upgrading is building the next entity");
        }
        return hosts.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
    }
}
