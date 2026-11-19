namespace GameContent.Catalog;

/// <summary>
/// Turns a validated pack into SimEngine content: a content directory (manifest and documents) whose transformations are the pack's
/// batch recipes, run by one transformer entity per station (and one for recipes with no station) in a resource-only world. SimEngine's
/// own readers validate the result when it is loaded. A recipe SimEngine cannot express is refused with a code; resource-level features
/// it has no model for (spoilage, grades, stack limits) are reduced with a note. Nothing is dropped without a note.
/// </summary>
public static partial class CatalogToSim
{
    public const string WorldModuleId = "gf.catalog-world";
    public const string HandEntity = "catalog.hand";

    public static SimConversion Convert(CatalogSet set) => Convert(set, null);

    /// <summary>Converts the recipes the ownership declaration gives to SimEngine; a recipe it gives to the Economy, or does not mention, is refused with a code (a recipe runs in one layer only). With no declaration every recipe SimEngine can run is converted.</summary>
    public static SimConversion Convert(CatalogSet set, RecipeOwnership? ownership)
    {
        ArgumentNullException.ThrowIfNull(set);
        var report = new ConversionReport();
        var resources = set.Resources.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        NoteResourceReductions(resources, report);

        var recipes = new List<CatalogRecipe>();
        foreach (var recipe in set.Recipes.OrderBy(r => r.Id, StringComparer.Ordinal))
            if (Owned(ownership, recipe, report) && Admit(set, recipe, report)) recipes.Add(recipe);

        var hosts = Hosts(recipes, report);
        var stations = hosts.Select(h => h.Id).ToList();
        var vehicles = Vehicles(set, report);
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manifest.json"] = Manifest(vehicles.Count > 0),
            ["open-access.json"] = """{ "open_content_access": [ { "id": "catalog.open" } ] }""",
            ["resources.json"] = ResourcesDocument(resources),
            ["recipes.json"] = RecipesDocument(set, recipes),
            ["world.json"] = WorldDocument(recipes, hosts, vehicles),
        };
        if (vehicles.Count > 0) files["navigation.json"] = Json(NavigationDocument(vehicles));
        return new SimConversion(files, recipes.Select(r => r.Id).ToList(), stations, report, vehicles.Select(v => v.Id).ToList());
    }


    private static bool Owned(RecipeOwnership? ownership, CatalogRecipe recipe, ConversionReport report)
    {
        if (ownership is null) return true;
        switch (ownership.LayerOf(recipe.Id))
        {
            case RecipeLayer.Sim: return true;
            case RecipeLayer.Economy: report.Refuse(SimConversionCodes.OwnedByEconomy, recipe.Id, "the ownership declaration gives this recipe to the Economy; it runs there, not as a transformation"); return false;
            case null: report.Refuse(SimConversionCodes.NoOwner, recipe.Id, "the ownership declaration does not say which layer runs this recipe"); return false;
        }
        return false;
    }

    private static void NoteResourceReductions(List<CatalogResource> resources, ConversionReport report)
    {
        foreach (var resource in resources)
        {
            if (resource.Perishable is not null) report.Reduce(SimConversionCodes.Perishable, resource.Id, "SimEngine has no spoilage: the resource never decays");
            if (resource.Grade is not null) report.Reduce(SimConversionCodes.Grade, resource.Id, "grade becomes a quality axis 0.." + (resource.Grade.Levels.Count - 1) + "; the level names are not kept");
        }
        if (resources.Count > 0)
        {
            report.Reduce(SimConversionCodes.Stack, "pack", "stack sizes are not modelled; capacity is one generous policy for every entity");
            report.Reduce(SimConversionCodes.DiscreteUnits, "pack", "count units are generated continuous so every resource can be measured in every dimension; whole-number amounts are guaranteed by the catalog validator");
        }
    }

    /// <summary>Whether SimEngine can run the recipe as a batch transformation; if not, why not.</summary>
    private static bool Admit(CatalogSet set, CatalogRecipe recipe, ConversionReport report)
    {
        string? code = null;
        string detail = "";
        if (recipe.Drops.Count > 0) (code, detail) = (SimConversionCodes.Drops, "chance-based drops: a transformation's outputs are fixed");
        else if (recipe.Outputs.Concat(recipe.Byproducts).Any(o => o.Min != o.Max)) (code, detail) = (SimConversionCodes.Range, "output ranges: a transformation's outputs are fixed");
        else if (recipe.Skill is not null) (code, detail) = (SimConversionCodes.Skill, "SimEngine has no skill levels");
        else if (recipe.Environment.Count > 0) (code, detail) = (SimConversionCodes.Environment, "environment tags have no equivalent in a transformation");
        else if (QualityRefusal(set, recipe) is { } quality) (code, detail) = (SimConversionCodes.Quality, quality);
        else if (recipe.Outputs.Count + recipe.Byproducts.Count == 0) (code, detail) = (SimConversionCodes.NoOutputs, "a transformation must declare what it makes (SimEngine refuses a transformation with no result)");
        else if (recipe.Duration.Value == 0) (code, detail) = (SimConversionCodes.Instant, "a batch transformation takes time");
        else if (!Exact(recipe.Duration.Value) || recipe.Inputs.Concat(recipe.Catalysts).Any(i => !Exact(Converted(set, i.Resource, i.Amount)))
                 || recipe.Outputs.Concat(recipe.Byproducts).Any(o => !Exact(Converted(set, o.Resource, o.Max))))
            (code, detail) = (SimConversionCodes.Precision, "an amount needs more than 9 decimal places once converted to the base unit");
        if (code is not null) { report.Refuse(code, recipe.Id, detail); return false; }
        if (recipe.Reverse is not null) report.Reduce(SimConversionCodes.ReverseLink, recipe.Id, "the reverse link is not modelled");
        return true;
    }

    private static decimal Converted(CatalogSet set, string resource, decimal amount) =>
        SimUnits.ToBase(set.TryResource(resource, out var found) ? found.Unit : throw new InvalidOperationException("validated reference vanished"), amount);

    /// <summary>Whether a number has at most 9 decimal places, so it can be written as an exact fraction of a billion.</summary>
    private static bool Exact(decimal value) => value * 1_000_000_000m == decimal.Truncate(value * 1_000_000_000m);
}
