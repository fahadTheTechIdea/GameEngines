namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>Every crafted resource must be makeable: some chain of recipes, starting from natural resources, produces it.</summary>
    private static void CheckReachability(CatalogSet set, CatalogReport report)
    {
        var reachable = new HashSet<string>(set.Resources.Where(r => r.Origin == ResourceOrigin.Natural).Select(r => r.Id), StringComparer.Ordinal);
        bool grew;
        do
        {
            grew = false;
            foreach (var recipe in set.Recipes)
            {
                var needed = recipe.Inputs.Select(i => i.Resource).Concat(recipe.Catalysts.Select(c => c.Resource)).Concat(recipe.Tools.Select(t => t.Resource));
                if (!needed.All(reachable.Contains)) continue;
                var made = recipe.Outputs.Select(o => o.Resource).Concat(recipe.Byproducts.Select(o => o.Resource)).Concat(recipe.Drops.Select(d => d.Resource));
                foreach (var id in made) grew |= reachable.Add(id);
            }
            foreach (var resource in set.Resources.Where(r => reachable.Contains(r.Id) && r.Perishable?.Result is not null))
                grew |= reachable.Add(resource.Perishable!.Result!);
        }
        while (grew);
        foreach (var resource in set.Resources.Where(r => r.Origin == ResourceOrigin.Crafted && !reachable.Contains(r.Id)))
            report.Add(CatalogCodes.Unreachable, "'" + resource.Id + "' is crafted but no chain of recipes from natural resources makes it", resource.Id);
    }

    /// <summary>
    /// Two combine recipes are ambiguous when the same set of ingredients, catalysts, tools and station could trigger both: the
    /// environments of one contain those of the other. Other kinds may have alternates; a combination must give one answer.
    /// </summary>
    private static void CheckAmbiguousCombines(CatalogSet set, CatalogReport report)
    {
        string Key(CatalogRecipe r) => string.Join(',', r.Inputs.Select(i => i.Resource).Order(StringComparer.Ordinal)) + "|"
            + string.Join(',', r.Catalysts.Select(c => c.Resource).Concat(r.Tools.Select(t => t.Resource)).Order(StringComparer.Ordinal)) + "|" + (r.Station?.Station ?? "");
        foreach (var group in set.Recipes.Where(r => r.Kind == RecipeKind.Combine).GroupBy(Key, StringComparer.Ordinal))
        {
            var members = group.ToList();
            for (var a = 0; a < members.Count; a++)
                for (var b = a + 1; b < members.Count; b++)
                {
                    var first = members[a].Environment;
                    var second = members[b].Environment;
                    if (first.All(second.Contains) || second.All(first.Contains))
                        report.Add(CatalogCodes.AmbiguousCombine, "'" + members[a].Id + "' and '" + members[b].Id + "' can both match the same combination", members[a].Id);
                }
        }
    }
}
