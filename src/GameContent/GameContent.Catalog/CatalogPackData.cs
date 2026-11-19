namespace GameContent.Catalog;

public sealed record CatalogStation(string Id, string Name, int Levels);

public sealed record CatalogSkill(string Id, string Name, int Max);

/// <summary>One loaded pack: its manifest fields and everything its documents define, in document order.</summary>
public sealed record CatalogPackData(
    string Id,
    string Version,
    string Title,
    string Language,
    WorldKind World,
    IReadOnlyList<string> Genres,
    IReadOnlyList<PackDependency> Dependencies,
    PackLicense License,
    IReadOnlyList<CatalogLocale> Locales,
    IReadOnlyList<string> Documents,
    IReadOnlyList<CatalogResource> Resources,
    IReadOnlyList<CatalogStation> Stations,
    IReadOnlyList<CatalogSkill> Skills,
    IReadOnlyList<CatalogRecipe> Recipes,
    IReadOnlyList<CatalogEra> Eras,
    IReadOnlyList<CatalogAvailability> Availability,
    IReadOnlyList<CatalogStat> Stats,
    IReadOnlyList<CatalogItem> Items,
    IReadOnlyList<CatalogFormation> Units);
