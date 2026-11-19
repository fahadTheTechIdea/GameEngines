using System.Security.Cryptography;
using System.Text;

namespace AlchemyEngine;

/// <summary>
/// Validated, immutable alchemy content: the elements and recipes of one game. It is built only by <see cref="AlchemyContentLoader"/>,
/// so every reference in it resolves and no two recipes can both match the same combination.
/// </summary>
public sealed class AlchemyContent
{
    private readonly Dictionary<string, ElementDef> _elements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RecipeDef> _recipes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RecipeDef>> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RecipeDef>> _byOutput = new(StringComparer.Ordinal);

    internal AlchemyContent(IReadOnlyList<ElementDef> elements, IReadOnlyList<RecipeDef> recipes)
    {
        Elements = elements;
        Recipes = recipes;
        foreach (var element in elements) _elements.Add(element.Id, element);
        foreach (var recipe in recipes)
        {
            _recipes.Add(recipe.Id, recipe);
            var key = VesselKey(recipe.Inputs.Concat(recipe.Catalysts));
            if (!_byKey.TryGetValue(key, out var group)) _byKey.Add(key, group = new List<RecipeDef>());
            group.Add(recipe);
            foreach (var output in recipe.Outputs.Distinct(StringComparer.Ordinal))
            {
                if (!_byOutput.TryGetValue(output, out var makers)) _byOutput.Add(output, makers = new List<RecipeDef>());
                makers.Add(recipe);
            }
        }
        Digest = ComputeDigest();
    }

    public IReadOnlyList<ElementDef> Elements { get; }
    public IReadOnlyList<RecipeDef> Recipes { get; }

    /// <summary>SHA-256 over the canonical form of the content. A saved discovery book names it, so a book never loads against other content.</summary>
    public string Digest { get; }

    public bool TryElement(string id, out ElementDef element) => _elements.TryGetValue(id, out element!);
    public bool TryRecipe(string id, out RecipeDef recipe) => _recipes.TryGetValue(id, out recipe!);

    /// <summary>The recipes whose inputs and catalysts together are exactly the given multiset of elements.</summary>
    internal IReadOnlyList<RecipeDef> RecipesForVessel(string vesselKey) =>
        _byKey.TryGetValue(vesselKey, out var group) ? group : Array.Empty<RecipeDef>();

    internal IReadOnlyList<RecipeDef> RecipesProducing(string elementId) =>
        _byOutput.TryGetValue(elementId, out var makers) ? makers : Array.Empty<RecipeDef>();

    /// <summary>The order-free identity of a combination: its element ids sorted ordinally and joined.</summary>
    internal static string VesselKey(IEnumerable<string> items)
    {
        var sorted = items.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);
        return string.Join('+', sorted);
    }

    private string ComputeDigest()
    {
        var text = new StringBuilder("alchemy-content-1\n");
        foreach (var element in Elements.OrderBy(e => e.Id, StringComparer.Ordinal))
            text.Append("e|").Append(element.Id).Append('|').Append(element.Name).Append('|')
                .Append(string.Join(',', element.Properties.OrderBy(p => p, StringComparer.Ordinal))).Append('|').Append(element.Starting ? '1' : '0').Append('\n');
        foreach (var recipe in Recipes.OrderBy(r => r.Id, StringComparer.Ordinal))
            text.Append("r|").Append(recipe.Id).Append('|').Append(string.Join(',', recipe.Inputs)).Append('|')
                .Append(string.Join(',', recipe.Catalysts.OrderBy(c => c, StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', recipe.Environment.OrderBy(t => t, StringComparer.Ordinal))).Append('|')
                .Append(string.Join(',', recipe.Outputs)).Append('|').Append(recipe.Ordered ? '1' : '0').Append('|')
                .Append(recipe.ChanceNumerator).Append('/').Append(recipe.ChanceDenominator).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
