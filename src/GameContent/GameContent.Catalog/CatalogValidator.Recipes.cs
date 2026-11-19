namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    private static void CheckRecipes(CatalogSet set, CatalogReport report)
    {
        foreach (var pack in set.Packs)
            foreach (var recipe in pack.Recipes)
            {
                CheckRecipeReferences(set, pack, recipe, report);
                CheckRecipeIngredients(set, recipe, report);
                CheckRecipeShape(set, recipe, report);
            }
    }

    private static void CheckRecipeReferences(CatalogSet set, CatalogPackData pack, CatalogRecipe recipe, CatalogReport report)
    {
        var resources = recipe.Inputs.Select(i => i.Resource).Concat(recipe.Catalysts.Select(c => c.Resource)).Concat(recipe.Tools.Select(t => t.Resource))
            .Concat(recipe.Outputs.Select(o => o.Resource)).Concat(recipe.Byproducts.Select(o => o.Resource)).Concat(recipe.Drops.Select(d => d.Resource));
        foreach (var id in resources) Reference(pack, id, "resource", recipe.Id, set.TryResource(id, out _), report);
        if (recipe.Station is { } station) Reference(pack, station.Station, "station", recipe.Id, set.TryStation(station.Station, out _), report);
        if (recipe.Skill is { } skill) Reference(pack, skill.Skill, "skill", recipe.Id, set.TrySkill(skill.Skill, out _), report);
        if (recipe.Reverse is { } reverse) Reference(pack, reverse, "reverse recipe", recipe.Id, set.TryRecipe(reverse, out _), report);
    }

    private static void CheckRecipeIngredients(CatalogSet set, CatalogRecipe recipe, CatalogReport report)
    {
        Distinct(recipe.Inputs.Select(i => i.Resource), "inputs", recipe, report);
        Distinct(recipe.Catalysts.Select(i => i.Resource), "catalysts", recipe, report);
        Distinct(recipe.Tools.Select(i => i.Resource), "tools", recipe, report);
        Distinct(recipe.Outputs.Select(i => i.Resource), "outputs", recipe, report);
        Distinct(recipe.Byproducts.Select(i => i.Resource), "byproducts", recipe, report);
        Distinct(recipe.Drops.Select(i => i.Resource), "drops", recipe, report);

        var inputs = recipe.Inputs.Select(i => i.Resource).ToHashSet(StringComparer.Ordinal);
        foreach (var catalyst in recipe.Catalysts)
            if (inputs.Contains(catalyst.Resource)) report.Add(CatalogCodes.ToolConflict, "'" + catalyst.Resource + "' is both consumed and kept as a catalyst", recipe.Id);
        foreach (var tool in recipe.Tools)
        {
            if (inputs.Contains(tool.Resource)) report.Add(CatalogCodes.ToolConflict, "'" + tool.Resource + "' is both consumed and held as a tool", recipe.Id);
            if (recipe.Catalysts.Any(c => c.Resource == tool.Resource)) report.Add(CatalogCodes.ToolConflict, "'" + tool.Resource + "' is both a catalyst and a tool", recipe.Id);
        }

        foreach (var line in recipe.Inputs.Concat(recipe.Catalysts)) WholeIfDiscrete(set, line.Resource, line.Amount, recipe, report);
        foreach (var line in recipe.Outputs.Concat(recipe.Byproducts))
        {
            if (line.Min > line.Max) report.Add(CatalogCodes.BadAmount, "'" + line.Resource + "' has min " + line.Min + " above max " + line.Max, recipe.Id);
            WholeIfDiscrete(set, line.Resource, line.Min, recipe, report);
            WholeIfDiscrete(set, line.Resource, line.Max, recipe, report);
        }
        foreach (var drop in recipe.Drops)
        {
            if (drop.Min > drop.Max) report.Add(CatalogCodes.BadAmount, "'" + drop.Resource + "' has min " + drop.Min + " above max " + drop.Max, recipe.Id);
            if (drop.Numerator > drop.Denominator) report.Add(CatalogCodes.BadChance, "drop of '" + drop.Resource + "' has chance " + drop.Numerator + "/" + drop.Denominator + " above 1", recipe.Id);
            WholeIfDiscrete(set, drop.Resource, drop.Min, recipe, report);
            WholeIfDiscrete(set, drop.Resource, drop.Max, recipe, report);
        }
    }

    private static void CheckRecipeShape(CatalogSet set, CatalogRecipe recipe, CatalogReport report)
    {
        if (recipe.Inputs.Count == 0 && !GenerativeKinds.Contains(recipe.Kind))
            report.Add(CatalogCodes.NoInputs, "a " + Names<RecipeKind>.Of(recipe.Kind) + " recipe must consume something; only extract, harvest, grow and loot may not", recipe.Id);
        if (recipe.Outputs.Count + recipe.Byproducts.Count + recipe.Drops.Count == 0 && !SinkKinds.Contains(recipe.Kind))
            report.Add(CatalogCodes.NoOutputs, "a " + Names<RecipeKind>.Of(recipe.Kind) + " recipe must make something; only consume, upkeep and decay may not", recipe.Id);

        if (recipe.Station is { } station && set.TryStation(station.Station, out var found) && station.Level > found.Levels)
            report.Add(CatalogCodes.StationLevel, "needs level " + station.Level + " of '" + found.Id + "' which has " + found.Levels, recipe.Id);
        if (recipe.Skill is { } skill && set.TrySkill(skill.Skill, out var foundSkill) && skill.Level > foundSkill.Max)
            report.Add(CatalogCodes.SkillLevel, "needs level " + skill.Level + " of '" + foundSkill.Id + "' which tops out at " + foundSkill.Max, recipe.Id);

        if (recipe.Quality == QualityMode.Skill && recipe.Skill is null)
            report.Add(CatalogCodes.Quality, "quality by skill needs the recipe to name a skill", recipe.Id);
        if (recipe.Quality == QualityMode.InheritInput && !recipe.Inputs.Any(i => set.TryResource(i.Resource, out var r) && r.Grade is not null))
            report.Add(CatalogCodes.Quality, "quality inherited from an input needs a graded input", recipe.Id);

        if (recipe.Reverse is { } reverse)
        {
            if (recipe.Kind == RecipeKind.Disassemble) report.Add(CatalogCodes.Reverse, "a disassemble recipe has no reverse link; the recipe it undoes links to it", recipe.Id);
            else if (set.TryRecipe(reverse, out var target) && target.Kind != RecipeKind.Disassemble)
                report.Add(CatalogCodes.Reverse, "reverse '" + reverse + "' must be a disassemble recipe", recipe.Id);
        }
    }

    private static void Distinct(IEnumerable<string> ids, string list, CatalogRecipe recipe, CatalogReport report)
    {
        foreach (var group in ids.GroupBy(i => i, StringComparer.Ordinal).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.DuplicateIngredient, "'" + group.Key + "' appears " + group.Count() + " times in " + list + "; give one line with the total", recipe.Id);
    }

    private static void WholeIfDiscrete(CatalogSet set, string resourceId, decimal amount, CatalogRecipe recipe, CatalogReport report)
    {
        if (!set.TryResource(resourceId, out var resource) || !CatalogUnits.TryGet(resource.Unit, out var unit) || !unit.Discrete) return;
        if (amount != decimal.Truncate(amount))
            report.Add(CatalogCodes.UnitDiscrete, "'" + resourceId + "' is counted in whole " + resource.Unit + " but the amount is " + amount, recipe.Id);
    }
}
