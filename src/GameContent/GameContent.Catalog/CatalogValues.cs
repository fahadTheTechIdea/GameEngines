namespace GameContent.Catalog;

// Small value types of the catalog model. They hold exactly what the JSON says; no rule lives here.

public sealed record PackDependency(string Id, string Version);

public sealed record PackLicense(string Spdx, string Provenance, string Author);

public sealed record ResourceGrade(GradeScheme Scheme, IReadOnlyList<string> Levels);

public sealed record Perishability(int Ticks, string? Result);

public sealed record ResourceCurrency(CurrencyKind Kind, string Code);

/// <summary>A consumed or kept quantity of a resource.</summary>
public sealed record Ingredient(string Resource, decimal Amount);

/// <summary>A tool held while the recipe runs; each run wears 1/Uses of one item.</summary>
public sealed record ToolRequirement(string Resource, int Uses);

/// <summary>A produced quantity; Min equals Max for a fixed amount.</summary>
public sealed record OutputLine(string Resource, decimal Min, decimal Max);

/// <summary>A chance-based extra output: with probability Numerator/Denominator the amount appears.</summary>
public sealed record DropLine(string Resource, decimal Min, decimal Max, int Numerator, int Denominator);

public sealed record StationRequirement(string Station, int Level);

public sealed record SkillRequirement(string Skill, int Level);

public sealed record Duration(decimal Value, TimeUnit Unit);

/// <summary>A manifest entry naming the locale document that holds a pack's names in one language.</summary>
public sealed record PackLocaleRef(string Language, string Document);

/// <summary>A pack's display names in one language, keyed by the id of the thing named. A missing id is untranslated, never guessed.</summary>
public sealed record CatalogLocale(string Language, IReadOnlyDictionary<string, string> Texts);
