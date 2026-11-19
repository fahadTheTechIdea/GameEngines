namespace GameContent.Catalog;

public static partial class CatalogToSim
{
    /// <summary>The unit of every generated quality axis: whole grades. SimEngine requires an axis to share its unit with the axis it inherits from.</summary>
    public const string GradeUnit = "catalog.grade";

    public static string QualityId(string resource) => "catalog.quality." + resource;

    /// <summary>
    /// A graded resource becomes a SimEngine quality axis (<c>resource_qualities</c>) and a recipe's <c>inherit-input</c> quality becomes
    /// <c>output_qualities</c> with a <c>source</c> (the quantity-weighted grade of the consumed lots, rounded down). SimEngine has no
    /// skill-dependent grade, so quality by skill, or an inherit rule it cannot wire (no graded input, or axes of different size), is refused.
    /// </summary>
    private static string? QualityRefusal(CatalogSet set, CatalogRecipe recipe)
    {
        if (recipe.Quality is null) return null;
        if (recipe.Quality == QualityMode.Skill) return "SimEngine has no skill-dependent grade";
        var output = recipe.Outputs.Select(o => o.Resource).FirstOrDefault(r => Graded(set, r) is not null);
        if (output is null) return "inherit-input quality needs a graded output";
        var source = InheritSource(set, recipe, Graded(set, output)!);
        return source is null ? "no consumed input has a grade axis of the same size as '" + output + "'" : null;
    }

    private static ResourceGrade? Graded(CatalogSet set, string resource) => set.TryResource(resource, out var found) ? found.Grade : null;

    /// <summary>The first (by id) consumed input graded on an axis the same size as the output's, or null.</summary>
    private static string? InheritSource(CatalogSet set, CatalogRecipe recipe, ResourceGrade output) =>
        recipe.Inputs.OrderBy(i => i.Resource, StringComparer.Ordinal).Select(i => i.Resource)
            .FirstOrDefault(r => Graded(set, r) is { } grade && grade.Levels.Count == output.Levels.Count);
}
