using EngineCommon;

/// <summary>
/// Every code the tree importer refuses or reduces with. The first six are the strict reader's (a file that is not the interchange at all); the rest name one
/// feature of a trained tree that the engine cannot carry over exactly, so a refusal says which feature and why instead of rounding or dropping it.
/// </summary>
public static class TreeCodes
{
    // The file is not the interchange.
    public const string Format = "TREE_FORMAT";
    public const string UnknownField = "TREE_UNKNOWN_FIELD";
    public const string MissingField = "TREE_MISSING_FIELD";
    public const string Range = "TREE_RANGE";
    public const string Duplicate = "TREE_DUPLICATE";
    public const string Reference = "TREE_REFERENCE";

    // Refused: the trained tree uses something the engine does not run.
    public const string Float = "TREE_FLOAT";
    public const string NotFinite = "TREE_NOT_FINITE";
    public const string Categorical = "TREE_CATEGORICAL";
    public const string MissingRouting = "TREE_MISSING_ROUTING";
    public const string FeatureSource = "TREE_FEATURE_SOURCE";
    public const string TooDeep = "TREE_TOO_DEEP";
    public const string TooBig = "TREE_TOO_BIG";
    public const string NotATree = "TREE_NOT_A_TREE";

    // Reduced: converted, with a stated loss of structure and none of meaning.
    public const string BranchUnreachable = "TREE_BRANCH_UNREACHABLE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
