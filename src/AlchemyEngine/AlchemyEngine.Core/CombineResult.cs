namespace AlchemyEngine;

public enum CombineOutcome
{
    /// <summary>A recipe matched and its chance succeeded: <see cref="CombineResult.Produced"/> appeared.</summary>
    Reacted,
    /// <summary>The items are valid but no recipe fits them.</summary>
    NoReaction,
    /// <summary>A recipe fits but its chance did not succeed this attempt; nothing appeared.</summary>
    Fizzled,
    /// <summary>The request itself is invalid; <see cref="CombineResult.Code"/> says why.</summary>
    Refused,
}

/// <summary>
/// What a combination did. <see cref="Produced"/> are the new elements; <see cref="Kept"/> are the catalysts, which stay.
/// <see cref="Items"/> and <see cref="Environment"/> echo the request in canonical form (items as given, tags sorted).
/// </summary>
public sealed record CombineResult(
    CombineOutcome Outcome,
    string Code,
    string? RecipeId,
    IReadOnlyList<string> Items,
    IReadOnlyList<string> Environment,
    IReadOnlyList<string> Produced,
    IReadOnlyList<string> Kept);
