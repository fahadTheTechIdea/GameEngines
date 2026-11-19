namespace GameContent.Catalog;

/// <summary>A resource definition. <see cref="Unit"/> is a UCUM code from <see cref="CatalogUnits"/>; <see cref="Properties"/> values are strings, numbers (decimal) or booleans.</summary>
public sealed record CatalogResource(
    string Id,
    string Name,
    ResourceClass Class,
    string Unit,
    ResourceOrigin Origin,
    int StackMax,
    ResourceGrade? Grade,
    Perishability? Perishable,
    int? DurabilityUses,
    ResourceCurrency? Currency,
    string? Cpc,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, object> Properties);
