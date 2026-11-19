using System.Security.Cryptography;
using System.Text;

namespace AlchemyEngine;

/// <summary>What one recorded combination newly taught the book.</summary>
public sealed record Discovery(IReadOnlyList<string> NewElements, string? NewRecipe, bool AlreadyTried);

/// <summary>
/// What the player knows: the elements and recipes found so far, the combinations already tried and how many attempts were made.
/// The book is plain state with no clock; its hash and saved bytes depend only on that state, so equal books are equal everywhere.
/// </summary>
public sealed partial class DiscoveryBook
{
    private readonly SortedSet<string> _elements = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _recipes = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _tried = new(StringComparer.Ordinal);

    /// <summary>A new book: only the content's starting elements are known.</summary>
    public DiscoveryBook(AlchemyContent content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        foreach (var element in content.Elements)
            if (element.Starting) _elements.Add(element.Id);
    }

    public AlchemyContent Content { get; }
    public IReadOnlyCollection<string> Elements => _elements;
    public IReadOnlyCollection<string> Recipes => _recipes;
    public IReadOnlyCollection<string> Tried => _tried;

    /// <summary>Combinations that were really tried (not refused). The caller passes it as <see cref="CombineContext.Attempt"/>.</summary>
    public long Attempts { get; private set; }

    public bool Knows(string elementId) => _elements.Contains(elementId);
    public bool HasTried(IReadOnlyList<string> items, IReadOnlyList<string> environment) => _tried.Contains(TriedKey(items, environment));

    /// <summary>Null when every item is known; otherwise <see cref="AlchemyCodes.CombineNotDiscovered"/>. A player can only use what it has found.</summary>
    public string? CheckUsable(IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            if (!_elements.Contains(item)) return AlchemyCodes.CombineNotDiscovered;
        return null;
    }

    /// <summary>Learns from a result. A refusal teaches nothing and costs no attempt.</summary>
    public Discovery Record(CombineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Outcome == CombineOutcome.Refused) return new Discovery(Array.Empty<string>(), null, false);
        Attempts++;
        // A fizzle is chance, not a conclusion about the combination, so it stays open to another try (the next attempt rolls differently).
        var alreadyTried = result.Outcome != CombineOutcome.Fizzled && !_tried.Add(TriedKey(result.Items, result.Environment));
        if (result.Outcome != CombineOutcome.Reacted) return new Discovery(Array.Empty<string>(), null, alreadyTried);
        var fresh = new List<string>();
        foreach (var produced in result.Produced)
            if (_elements.Add(produced)) fresh.Add(produced);
        var recipe = result.RecipeId is { } id && _recipes.Add(id) ? id : null;
        return new Discovery(fresh, recipe, alreadyTried);
    }

    /// <summary>
    /// Learns a recipe from outside the book (a teacher, a scroll, research done elsewhere): the recipe and its outputs become known.
    /// It is not an attempt, so it neither counts one nor marks a combination as tried. Unknown recipes are refused.
    /// </summary>
    public Discovery LearnRecipe(string recipeId)
    {
        if (!Content.TryRecipe(recipeId ?? throw new ArgumentNullException(nameof(recipeId)), out var recipe))
            throw new AlchemyRefusedException(AlchemyCodes.BookUnknownRecipe, "unknown recipe '" + recipeId + "'");
        var fresh = new List<string>();
        foreach (var produced in recipe.Outputs)
            if (_elements.Add(produced)) fresh.Add(produced);
        return new Discovery(fresh, _recipes.Add(recipe.Id) ? recipe.Id : null, false);
    }

    /// <summary>The identity of an attempt: the items in the order given and the sorted environment tags.</summary>
    public static string TriedKey(IReadOnlyList<string> items, IReadOnlyList<string> environment) =>
        string.Join('+', items) + "|" + string.Join(',', environment.Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal));

    /// <summary>SHA-256 of the whole state, for a journal's per-tick fingerprint.</summary>
    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical())));

    private string Canonical() =>
        "book|" + Content.Digest + "|" + Attempts + "|" + string.Join(',', _elements) + "|" + string.Join(',', _recipes) + "|" + string.Join(';', _tried);
}
