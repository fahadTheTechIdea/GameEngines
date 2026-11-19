using EngineCommon;

namespace SpatialEngine;

/// <summary>Every code the spatial engine refuses or reports with: the map, the calls, the queries and the checkpoint each have their own.</summary>
public static class SpatialCodes
{
    // Map document.
    public const string Format = "SPT_FORMAT";
    public const string UnknownField = "SPT_UNKNOWN_FIELD";
    public const string MissingField = "SPT_MISSING_FIELD";
    public const string Range = "SPT_RANGE";
    public const string Duplicate = "SPT_DUPLICATE";
    public const string Reference = "SPT_REFERENCE";
    public const string Ragged = "SPT_RAGGED_ROWS";
    public const string Legend = "SPT_LEGEND";
    public const string TooBig = "SPT_TOO_BIG";

    // Calls.
    public const string OutOfBounds = "SPT_OUT_OF_BOUNDS";
    public const string UnknownUnit = "SPT_UNKNOWN_UNIT";
    public const string DuplicateUnit = "SPT_DUPLICATE_UNIT";
    public const string BadValue = "SPT_BAD_VALUE";

    // Query outcomes (reported in a result, not thrown).
    public const string Found = "SPT_FOUND";
    public const string Blocked = "SPT_BLOCKED";
    public const string Unreachable = "SPT_UNREACHABLE";
    public const string Budget = "SPT_BUDGET";

    // Checkpoint.
    public const string SaveFormat = "SPT_SAVE_FORMAT";
    public const string SaveDigest = "SPT_SAVE_DIGEST";
    public const string SaveMap = "SPT_SAVE_MAP";
    public const string SaveState = "SPT_SAVE_STATE";

    public static readonly RefusalCodes ForMap = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}