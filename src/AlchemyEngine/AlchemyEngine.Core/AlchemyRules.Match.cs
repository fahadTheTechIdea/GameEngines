namespace AlchemyEngine;

public sealed partial class AlchemyRules
{
    private readonly record struct Matched(CombineResult Result, RecipeDef? Recipe);

    /// <summary>Validates the request and finds the one recipe it fits, ignoring chance.</summary>
    private Matched Match(IReadOnlyList<string> items, IReadOnlyList<string> environment)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(environment);
        var tags = environment.Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToArray();
        if (items.Count == 0) return Refused(AlchemyCodes.CombineEmpty, items, tags);
        if (items.Count > MaxItems) return Refused(AlchemyCodes.CombineTooMany, items, tags);
        foreach (var item in items)
            if (!Content.TryElement(item, out _)) return Refused(AlchemyCodes.CombineUnknownElement, items, tags);
        foreach (var tag in tags)
            if (!IsTag(tag)) return Refused(AlchemyCodes.CombineBadEnvironment, items, tags);

        var fits = new List<RecipeDef>();
        foreach (var recipe in Content.RecipesForVessel(AlchemyContent.VesselKey(items)))
        {
            if (!recipe.Environment.All(t => tags.Contains(t, StringComparer.Ordinal))) continue;
            if (recipe.Ordered && !items.Where(i => !recipe.Catalysts.Contains(i, StringComparer.Ordinal)).SequenceEqual(recipe.Inputs, StringComparer.Ordinal)) continue;
            fits.Add(recipe);
        }
        if (fits.Count > 1) return Refused(AlchemyCodes.CombineAmbiguous, items, tags);
        if (fits.Count == 0)
            return new Matched(new CombineResult(CombineOutcome.NoReaction, AlchemyCodes.NoReaction, null, items.ToArray(), tags, Array.Empty<string>(), Array.Empty<string>()), null);
        var found = fits[0];
        return new Matched(new CombineResult(CombineOutcome.Reacted, AlchemyCodes.Reacted, found.Id, items.ToArray(), tags, found.Outputs, found.Catalysts), found);
    }

    private static Matched Refused(string code, IReadOnlyList<string> items, string[] tags) =>
        new(new CombineResult(CombineOutcome.Refused, code, null, items.ToArray(), tags, Array.Empty<string>(), Array.Empty<string>()), null);

    private static bool IsTag(string tag)
    {
        if (tag.Length is < 1 or > 64) return false;
        foreach (var c in tag)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '.' or '_' or '-')) return false;
        return true;
    }
}
