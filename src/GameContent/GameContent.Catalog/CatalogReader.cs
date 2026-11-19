using System.Text.Json;

namespace GameContent.Catalog;

/// <summary>The manifest of a pack as written in <c>pack.json</c>.</summary>
public sealed record PackManifest(string Id, string Version, string Title, string Language, WorldKind World, IReadOnlyList<string> Genres,
    IReadOnlyList<PackDependency> Dependencies, PackLicense License, IReadOnlyList<PackLocaleRef> Locales, IReadOnlyList<string> Documents);

/// <summary>A locale document as written: the pack and language it claims and its texts.</summary>
public sealed record CatalogLocaleDocument(string Pack, string Language, IReadOnlyDictionary<string, string> Texts);

/// <summary>One document of a pack as written: the pack it claims and what it defines.</summary>
public sealed record CatalogDocument(string Pack, IReadOnlyList<CatalogResource> Resources, IReadOnlyList<CatalogStation> Stations,
    IReadOnlyList<CatalogSkill> Skills, IReadOnlyList<CatalogRecipe> Recipes, IReadOnlyList<CatalogEra> Eras, IReadOnlyList<CatalogAvailability> Availability,
    IReadOnlyList<CatalogStat> Stats, IReadOnlyList<CatalogItem> Items, IReadOnlyList<CatalogFormation> Units);

/// <summary>
/// Turns schema-valid JSON into the catalog model. It trusts the shape because the schema has already admitted it; what the
/// values mean (references, units, rules) is the validator's business, not the reader's.
/// </summary>
public static partial class CatalogReader
{
    public static PackManifest Manifest(JsonElement root) => new(
        root.GetProperty("id").GetString()!,
        root.GetProperty("version").GetString()!,
        root.GetProperty("title").GetString()!,
        root.GetProperty("language").GetString()!,
        Names<WorldKind>.Parse(root.GetProperty("world").GetString()!),
        Strings(root.GetProperty("genres")),
        root.GetProperty("dependencies").EnumerateArray().Select(d => new PackDependency(d.GetProperty("id").GetString()!, d.GetProperty("version").GetString()!)).ToList(),
        new PackLicense(root.GetProperty("license").GetProperty("spdx").GetString()!, root.GetProperty("license").GetProperty("provenance").GetString()!, root.GetProperty("license").GetProperty("author").GetString()!),
        root.GetProperty("locales").EnumerateArray().Select(l => new PackLocaleRef(l.GetProperty("language").GetString()!, l.GetProperty("document").GetString()!)).ToList(),
        Strings(root.GetProperty("documents")));

    public static CatalogLocaleDocument Locale(JsonElement root) => new(
        root.GetProperty("pack").GetString()!,
        root.GetProperty("language").GetString()!,
        root.GetProperty("texts").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal));

    public static CatalogDocument Document(JsonElement root) => new(
        root.GetProperty("pack").GetString()!,
        List(root, "resources", Resource),
        List(root, "stations", e => new CatalogStation(e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("levels").GetInt32())),
        List(root, "skills", e => new CatalogSkill(e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("max").GetInt32())),
        List(root, "recipes", Recipe),
        List(root, "eras", Era),
        List(root, "availability", e => new CatalogAvailability(e.GetProperty("id").GetString()!, e.GetProperty("from").GetString()!, NullableString(e.GetProperty("until")))),
        List(root, "stats", Stat),
        List(root, "items", Item),
        List(root, "units", Unit));

    private static IReadOnlyList<T> List<T>(JsonElement root, string name, Func<JsonElement, T> read) =>
        root.TryGetProperty(name, out var array) ? array.EnumerateArray().Select(read).ToList() : [];

    private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

    private static CatalogResource Resource(JsonElement e)
    {
        var grade = e.GetProperty("grade");
        var perishable = e.GetProperty("perishable");
        var durability = e.GetProperty("durability");
        var currency = e.GetProperty("currency");
        var cpc = e.GetProperty("cpc");
        return new CatalogResource(
            e.GetProperty("id").GetString()!,
            e.GetProperty("name").GetString()!,
            Names<ResourceClass>.Parse(e.GetProperty("class").GetString()!),
            e.GetProperty("unit").GetString()!,
            Names<ResourceOrigin>.Parse(e.GetProperty("origin").GetString()!),
            e.GetProperty("stack").GetProperty("max").GetInt32(),
            grade.ValueKind == JsonValueKind.Null ? null : new ResourceGrade(Names<GradeScheme>.Parse(grade.GetProperty("scheme").GetString()!), Strings(grade.GetProperty("levels"))),
            perishable.ValueKind == JsonValueKind.Null ? null : new Perishability(perishable.GetProperty("ticks").GetInt32(), NullableString(perishable.GetProperty("result"))),
            durability.ValueKind == JsonValueKind.Null ? null : durability.GetProperty("uses").GetInt32(),
            currency.ValueKind == JsonValueKind.Null ? null : new ResourceCurrency(Names<CurrencyKind>.Parse(currency.GetProperty("kind").GetString()!), currency.GetProperty("code").GetString()!),
            NullableString(cpc),
            Strings(e.GetProperty("tags")),
            e.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => Property(p.Value), StringComparer.Ordinal));
    }

    private static object Property(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString()!;
        if (value.ValueKind == JsonValueKind.Number) return value.GetDecimal();
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        throw new InvalidOperationException("a property value must be a string, number or boolean");
    }

    private static string? NullableString(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetString();

    private static CatalogRecipe Recipe(JsonElement e)
    {
        var station = e.GetProperty("station");
        var skill = e.GetProperty("skill");
        var quality = e.GetProperty("quality");
        var duration = e.GetProperty("duration");
        return new CatalogRecipe(
            e.GetProperty("id").GetString()!,
            e.GetProperty("name").GetString()!,
            Names<RecipeKind>.Parse(e.GetProperty("kind").GetString()!),
            Lines(e.GetProperty("inputs")),
            Lines(e.GetProperty("catalysts")),
            e.GetProperty("tools").EnumerateArray().Select(t => new ToolRequirement(t.GetProperty("resource").GetString()!, t.GetProperty("uses").GetInt32())).ToList(),
            Outputs(e.GetProperty("outputs")),
            Outputs(e.GetProperty("byproducts")),
            e.GetProperty("drops").EnumerateArray().Select(Drop).ToList(),
            station.ValueKind == JsonValueKind.Null ? null : new StationRequirement(station.GetProperty("id").GetString()!, station.GetProperty("level").GetInt32()),
            skill.ValueKind == JsonValueKind.Null ? null : new SkillRequirement(skill.GetProperty("id").GetString()!, skill.GetProperty("level").GetInt32()),
            Strings(e.GetProperty("environment")),
            new Duration(duration.GetProperty("value").GetDecimal(), Names<TimeUnit>.Parse(duration.GetProperty("unit").GetString()!)),
            quality.ValueKind == JsonValueKind.Null ? null : Names<QualityMode>.Parse(quality.GetString()!),
            NullableString(e.GetProperty("reverse")));
    }

    private static List<Ingredient> Lines(JsonElement array) =>
        array.EnumerateArray().Select(l => new Ingredient(l.GetProperty("resource").GetString()!, l.GetProperty("amount").GetDecimal())).ToList();

    private static List<OutputLine> Outputs(JsonElement array) => array.EnumerateArray().Select(l =>
    {
        var (min, max) = Range(l.GetProperty("amount"));
        return new OutputLine(l.GetProperty("resource").GetString()!, min, max);
    }).ToList();

    private static DropLine Drop(JsonElement l)
    {
        var (min, max) = Range(l.GetProperty("amount"));
        var chance = l.GetProperty("chance").GetString()!.Split('/');
        return new DropLine(l.GetProperty("resource").GetString()!, min, max, int.Parse(chance[0]), int.Parse(chance[1]));
    }

    private static (decimal Min, decimal Max) Range(JsonElement amount) =>
        amount.ValueKind == JsonValueKind.Number ? (amount.GetDecimal(), amount.GetDecimal()) : (amount.GetProperty("min").GetDecimal(), amount.GetProperty("max").GetDecimal());
}
