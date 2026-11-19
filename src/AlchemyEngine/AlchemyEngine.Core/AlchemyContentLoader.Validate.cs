namespace AlchemyEngine;

public static partial class AlchemyContentLoader
{
    private static void Validate(List<ElementDef> elements, List<RecipeDef> recipes)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
            if (!known.Add(element.Id)) throw ContentJson.Refuse(AlchemyCodes.ContentDuplicateElement, "element '" + element.Id + "' is defined twice");
        if (!elements.Any(e => e.Starting)) throw ContentJson.Refuse(AlchemyCodes.ContentNoStartingElement, "at least one element must be a starting element");

        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in recipes)
        {
            if (!recipeIds.Add(recipe.Id)) throw ContentJson.Refuse(AlchemyCodes.ContentDuplicateRecipe, "recipe '" + recipe.Id + "' is defined twice");
            // A reaction needs something in the vessel: consumed inputs, or catalysts alone (a tool used on its own, e.g. a pick in a mine).
            if (recipe.Inputs.Count == 0 && recipe.Catalysts.Count == 0) throw ContentJson.Refuse(AlchemyCodes.ContentEmptyInputs, "recipe '" + recipe.Id + "' has no inputs and no catalysts");
            // Consuming inputs and making nothing is a valid reaction (burning, eating); a reaction that consumes nothing and makes nothing is not.
            if (recipe.Outputs.Count == 0 && recipe.Inputs.Count == 0) throw ContentJson.Refuse(AlchemyCodes.ContentNoOutputs, "recipe '" + recipe.Id + "' consumes nothing and makes nothing");
            foreach (var reference in recipe.Inputs.Concat(recipe.Catalysts).Concat(recipe.Outputs))
                if (!known.Contains(reference)) throw ContentJson.Refuse(AlchemyCodes.ContentUnknownElement, "recipe '" + recipe.Id + "' names unknown element '" + reference + "'");
            foreach (var catalyst in recipe.Catalysts)
                if (recipe.Inputs.Contains(catalyst, StringComparer.Ordinal))
                    throw ContentJson.Refuse(AlchemyCodes.ContentCatalystIsInput, "recipe '" + recipe.Id + "' lists '" + catalyst + "' as both input and catalyst");
            if (recipe.ChanceDenominator < 1 || recipe.ChanceDenominator > MaxDenominator || recipe.ChanceNumerator < 1 || recipe.ChanceNumerator > recipe.ChanceDenominator)
                throw ContentJson.Refuse(AlchemyCodes.ContentBadChance, "recipe '" + recipe.Id + "' chance must be numerator/denominator with 1 <= numerator <= denominator <= " + MaxDenominator);
        }

        RejectAmbiguity(recipes);
        RejectUnreachable(elements, recipes);
    }

    /// <summary>
    /// Two recipes are ambiguous when one set of items could match both: same inputs and catalysts as a multiset, an input order that
    /// fits both, and environment tags where one set contains the other (so the larger environment offers both).
    /// </summary>
    private static void RejectAmbiguity(List<RecipeDef> recipes)
    {
        foreach (var group in recipes.GroupBy(r => AlchemyContent.VesselKey(r.Inputs.Concat(r.Catalysts)), StringComparer.Ordinal))
        {
            var members = group.ToList();
            for (var a = 0; a < members.Count; a++)
                for (var b = a + 1; b < members.Count; b++)
                {
                    var first = members[a];
                    var second = members[b];
                    var orderFitsBoth = !(first.Ordered && second.Ordered) || first.Inputs.SequenceEqual(second.Inputs, StringComparer.Ordinal);
                    var environmentNested = first.Environment.All(t => second.Environment.Contains(t, StringComparer.Ordinal))
                        || second.Environment.All(t => first.Environment.Contains(t, StringComparer.Ordinal));
                    if (orderFitsBoth && environmentNested)
                        throw ContentJson.Refuse(AlchemyCodes.ContentAmbiguousRecipe, "recipes '" + first.Id + "' and '" + second.Id + "' can match the same combination");
                }
        }
    }

    /// <summary>Every element that is not a starting element must be makeable, otherwise it could never be discovered.</summary>
    private static void RejectUnreachable(List<ElementDef> elements, List<RecipeDef> recipes)
    {
        var reachable = new HashSet<string>(elements.Where(e => e.Starting).Select(e => e.Id), StringComparer.Ordinal);
        bool grew;
        do
        {
            grew = false;
            foreach (var recipe in recipes)
                if (recipe.Inputs.Concat(recipe.Catalysts).All(reachable.Contains))
                    foreach (var output in recipe.Outputs)
                        grew |= reachable.Add(output);
        }
        while (grew);
        foreach (var element in elements)
            if (!reachable.Contains(element.Id))
                throw ContentJson.Refuse(AlchemyCodes.ContentUnreachableElement, "element '" + element.Id + "' can never be made from the starting elements");
    }
}
