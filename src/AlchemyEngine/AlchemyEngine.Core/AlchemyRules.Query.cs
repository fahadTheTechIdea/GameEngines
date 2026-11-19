namespace AlchemyEngine;

public sealed partial class AlchemyRules
{
    /// <summary>What can make this element: every recipe with it among its outputs, ordered by recipe id. Unknown elements are refused.</summary>
    public IReadOnlyList<RecipeDef> WhatMakes(string elementId)
    {
        if (!Content.TryElement(elementId, out _)) throw new AlchemyRefusedException(AlchemyCodes.CombineUnknownElement, "unknown element '" + elementId + "'");
        return Content.RecipesProducing(elementId).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>What these items would make, ignoring chance: the recipe that fits, nothing, or a refusal. It changes nothing and rolls nothing.</summary>
    public CombineResult WhatDoes(IReadOnlyList<string> items, IReadOnlyList<string> environment) => Match(items, environment).Result;
}
