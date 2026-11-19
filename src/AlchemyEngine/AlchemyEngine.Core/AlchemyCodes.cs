namespace AlchemyEngine;

/// <summary>Every refusal code the library can give. A code names the first check that failed; nothing is repaired or guessed.</summary>
public static class AlchemyCodes
{
    // Content
    public const string ContentFormat = "ALCHEMY_FORMAT";
    public const string ContentMissingField = "ALCHEMY_MISSING_FIELD";
    public const string ContentUnknownField = "ALCHEMY_UNKNOWN_FIELD";
    public const string ContentBadId = "ALCHEMY_BAD_ID";
    public const string ContentDuplicateElement = "ALCHEMY_DUPLICATE_ELEMENT";
    public const string ContentDuplicateRecipe = "ALCHEMY_DUPLICATE_RECIPE";
    public const string ContentUnknownElement = "ALCHEMY_UNKNOWN_ELEMENT";
    public const string ContentEmptyInputs = "ALCHEMY_EMPTY_INPUTS";
    public const string ContentNoOutputs = "ALCHEMY_NO_OUTPUTS";
    public const string ContentBadChance = "ALCHEMY_BAD_CHANCE";
    public const string ContentAmbiguousRecipe = "ALCHEMY_AMBIGUOUS_RECIPE";
    public const string ContentNoStartingElement = "ALCHEMY_NO_STARTING_ELEMENT";
    public const string ContentCatalystIsInput = "ALCHEMY_CATALYST_IS_INPUT";
    public const string ContentUnreachableElement = "ALCHEMY_UNREACHABLE_ELEMENT";

    // Combine results that are not refusals
    public const string Reacted = "ALCHEMY_REACTED";
    public const string NoReaction = "ALCHEMY_NO_REACTION";
    public const string Fizzled = "ALCHEMY_FIZZLED";

    // Combine refusals
    public const string CombineEmpty = "ALCHEMY_COMBINE_EMPTY";
    public const string CombineTooMany = "ALCHEMY_COMBINE_TOO_MANY";
    public const string CombineUnknownElement = "ALCHEMY_COMBINE_UNKNOWN_ELEMENT";
    public const string CombineNotDiscovered = "ALCHEMY_COMBINE_NOT_DISCOVERED";
    public const string CombineBadEnvironment = "ALCHEMY_COMBINE_BAD_ENVIRONMENT";
    public const string CombineAmbiguous = "ALCHEMY_COMBINE_AMBIGUOUS";

    // Discovery book
    public const string BookFormat = "ALCHEMY_BOOK_FORMAT";
    public const string BookDigest = "ALCHEMY_BOOK_DIGEST";
    public const string BookContent = "ALCHEMY_BOOK_CONTENT";
    public const string BookUnknownElement = "ALCHEMY_BOOK_UNKNOWN_ELEMENT";
    public const string BookUnknownRecipe = "ALCHEMY_BOOK_UNKNOWN_RECIPE";
}
