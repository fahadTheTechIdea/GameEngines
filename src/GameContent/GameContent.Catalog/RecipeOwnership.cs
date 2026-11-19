using System.Text.Json;

namespace GameContent.Catalog;

/// <summary>The layer of a game that runs a recipe: SimEngine as a per-entity transformation, or the EconomyEngine as market-level production.</summary>
public enum RecipeLayer { Sim, Economy }

/// <summary>Why an ownership declaration, or a pair of conversions, is refused.</summary>
public static class OwnershipCodes
{
    public const string Format = "CATALOG_OWNER_FORMAT";
    public const string UnknownRecipe = "CATALOG_OWNER_UNKNOWN_RECIPE";
    public const string DoubleOwner = "CATALOG_OWNER_DOUBLE";
}

/// <summary>
/// Which layer runs each catalog recipe. A recipe is authored once, in the catalog, and a game runs it in exactly one layer: running it as a SimEngine transformation and as Economy production
/// would make the same goods twice. The declaration is the one place that says which; the SimEngine and Economy converters read it and leave out what the other layer owns (with a code), and
/// <see cref="CheckConversions"/> refuses two conversions that both took the same recipe. Format: <c>{ "format": 1, "owners": { "pack.recipe": "sim" | "economy" } }</c>, strict.
/// </summary>
public sealed class RecipeOwnership
{
    private readonly Dictionary<string, RecipeLayer> _owners;

    private RecipeOwnership(Dictionary<string, RecipeLayer> owners) { _owners = owners; }

    public IReadOnlyDictionary<string, RecipeLayer> Owners => _owners;

    public static RecipeOwnership Of(IEnumerable<KeyValuePair<string, RecipeLayer>> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        return new RecipeOwnership(new Dictionary<string, RecipeLayer>(owners, StringComparer.Ordinal));
    }

    /// <summary>The layer a recipe is declared to run in, or null when the declaration does not mention it.</summary>
    public RecipeLayer? LayerOf(string recipe) => _owners.TryGetValue(recipe, out var layer) ? layer : null;

    public static RecipeOwnership Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var report = new CatalogReport();
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException exception)
        {
            report.Add(OwnershipCodes.Format, "not JSON: " + exception.Message, "ownership");
            throw new CatalogRefusedException(report);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(["format", "owners"]) == false)
                report.Add(OwnershipCodes.Format, "an ownership declaration is an object with exactly 'format' and 'owners'", "ownership");
            else if (root.GetProperty("format").ValueKind != JsonValueKind.Number || !root.GetProperty("format").TryGetInt32(out var format) || format != 1)
                report.Add(OwnershipCodes.Format, "this reader reads ownership format 1 only", "ownership");
            else if (root.GetProperty("owners").ValueKind != JsonValueKind.Object)
                report.Add(OwnershipCodes.Format, "'owners' is an object from recipe id to 'sim' or 'economy'", "ownership");
            var owners = new Dictionary<string, RecipeLayer>(StringComparer.Ordinal);
            if (report.IsValid)
                foreach (var entry in root.GetProperty("owners").EnumerateObject())
                {
                    var word = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
                    if (word == "sim") owners[entry.Name] = RecipeLayer.Sim;
                    else if (word == "economy") owners[entry.Name] = RecipeLayer.Economy;
                    else report.Add(OwnershipCodes.Format, "recipe '" + entry.Name + "' is owned by 'sim' or 'economy', not " + entry.Value.GetRawText(), entry.Name);
                }
            if (!report.IsValid) throw new CatalogRefusedException(report);
            return new RecipeOwnership(owners);
        }
    }

    /// <summary>Every recipe the declaration names must be one the set has.</summary>
    public CatalogReport Check(CatalogSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var report = new CatalogReport();
        foreach (var id in _owners.Keys.Order(StringComparer.Ordinal))
            if (!set.TryRecipe(id, out _)) report.Add(OwnershipCodes.UnknownRecipe, "the declaration gives an owner to '" + id + "', which is not a recipe of the pack or its dependencies", id);
        return report;
    }

    /// <summary>The two conversions of one pack must not both run the same recipe: each recipe id they share is a double owner.</summary>
    public static CatalogReport CheckConversions(IEnumerable<string> simRecipes, IEnumerable<string> economyRecipes)
    {
        ArgumentNullException.ThrowIfNull(simRecipes);
        ArgumentNullException.ThrowIfNull(economyRecipes);
        var report = new CatalogReport();
        var economy = new HashSet<string>(economyRecipes, StringComparer.Ordinal);
        foreach (var id in simRecipes.Distinct(StringComparer.Ordinal).Where(economy.Contains).Order(StringComparer.Ordinal))
            report.Add(OwnershipCodes.DoubleOwner, "recipe '" + id + "' would run as a SimEngine transformation and as Economy production; declare one owner", id);
        return report;
    }
}
