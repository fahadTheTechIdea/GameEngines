using AlchemyEngine;

namespace GameRuntime;

/// <summary>
/// The alchemy view over a SimEngine game's own content: elements are its resources, recipes are its gated batch transformations.
/// <see cref="Alchemy"/> is ordinary alchemy content (so the rules, the discovery book and the queries work on it unchanged);
/// <see cref="Recipes"/> hold the engine's physical facts for each recipe; <see cref="Excluded"/> says what was left out and why.
/// </summary>
public sealed class SimRecipeView
{
    private readonly Dictionary<string, SimRecipe> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimRecipe> _byTechnology = new(StringComparer.Ordinal);

    public SimRecipeView(AlchemyContent alchemy, IReadOnlyList<SimRecipe> recipes, IReadOnlyList<SimRecipeExclusion> excluded)
    {
        Alchemy = alchemy ?? throw new ArgumentNullException(nameof(alchemy));
        Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        Excluded = excluded ?? throw new ArgumentNullException(nameof(excluded));
        foreach (var recipe in recipes)
        {
            _byId.Add(recipe.Id, recipe);
            _byTechnology.Add(recipe.TechnologyId, recipe);
        }
    }

    public AlchemyContent Alchemy { get; }
    public IReadOnlyList<SimRecipe> Recipes { get; }
    public IReadOnlyList<SimRecipeExclusion> Excluded { get; }

    public bool TryRecipe(string id, out SimRecipe recipe) => _byId.TryGetValue(id, out recipe!);
    public bool TryRecipeForTechnology(string technologyId, out SimRecipe recipe) => _byTechnology.TryGetValue(technologyId, out recipe!);
}
