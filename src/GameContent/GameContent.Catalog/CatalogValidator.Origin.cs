namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>Kinds that make something: they cannot be how a "natural" resource comes to exist. Trades, extraction, harvest, loot and growth can.</summary>
    private static readonly IReadOnlySet<RecipeKind> MakingKinds = new HashSet<RecipeKind>
    {
        RecipeKind.Transform, RecipeKind.Smelt, RecipeKind.Refine, RecipeKind.Craft, RecipeKind.Assemble, RecipeKind.Disassemble, RecipeKind.Cook,
        RecipeKind.Brew, RecipeKind.Combine, RecipeKind.Upgrade, RecipeKind.Construct,
    };

    /// <summary>
    /// A natural resource is one the world provides (it is a starting element and needs no recipe). If a manufacturing recipe makes it,
    /// it is really crafted and the origin is wrong; the answer changes reachability and which resources a game starts with. Energy is
    /// exempt: it is a flow the world offers and generators convert, not an item that exists only once something has made it.
    /// </summary>
    private static void CheckOrigin(CatalogSet set, CatalogReport report)
    {
        foreach (var recipe in set.Recipes.Where(r => MakingKinds.Contains(r.Kind)))
            foreach (var line in recipe.Outputs.Concat(recipe.Byproducts))
                if (set.TryResource(line.Resource, out var resource) && resource.Origin == ResourceOrigin.Natural && resource.Class != ResourceClass.Energy)
                    report.Add(CatalogCodes.Origin, "a " + Names<RecipeKind>.Of(recipe.Kind) + " recipe makes '" + line.Resource + "', which is declared natural; declare it crafted or make it by extract, harvest, loot or trade", recipe.Id);
    }
}
