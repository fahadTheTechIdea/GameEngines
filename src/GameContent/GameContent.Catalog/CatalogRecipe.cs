namespace GameContent.Catalog;

/// <summary>
/// A recipe definition: what is consumed, kept, held and made, and under what conditions. Lists are explicit and may be empty;
/// <see cref="Station"/>, <see cref="Skill"/>, <see cref="Quality"/> and <see cref="Reverse"/> are null only because the JSON said so.
/// </summary>
public sealed record CatalogRecipe(
    string Id,
    string Name,
    RecipeKind Kind,
    IReadOnlyList<Ingredient> Inputs,
    IReadOnlyList<Ingredient> Catalysts,
    IReadOnlyList<ToolRequirement> Tools,
    IReadOnlyList<OutputLine> Outputs,
    IReadOnlyList<OutputLine> Byproducts,
    IReadOnlyList<DropLine> Drops,
    StationRequirement? Station,
    SkillRequirement? Skill,
    IReadOnlyList<string> Environment,
    Duration Duration,
    QualityMode? Quality,
    string? Reverse);
