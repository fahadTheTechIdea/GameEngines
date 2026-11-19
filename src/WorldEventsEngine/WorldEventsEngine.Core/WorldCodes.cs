using EngineCommon;

namespace WorldEventsEngine;

/// <summary>Every code the world-events engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the <see cref="LocaleCodes"/> of the shared locale machinery.</summary>
public static class WorldCodes
{
    // Document.
    public const string Format = "WE_FORMAT";
    public const string FormatSuperseded = "WE_FORMAT_SUPERSEDED";
    public const string Nonstationary = "WE_NONSTATIONARY";
    public const string UnknownField = "WE_UNKNOWN_FIELD";
    public const string MissingField = "WE_MISSING_FIELD";
    public const string Range = "WE_RANGE";
    public const string Duplicate = "WE_DUPLICATE";
    public const string Reference = "WE_REFERENCE";
    public const string Reserved = "WE_RESERVED_NAME";
    public const string Climate = "WE_CLIMATE";
    public const string Hazard = "WE_HAZARD";
    public const string Disease = "WE_DISEASE";
    public const string Director = "WE_DIRECTOR";
    public const string Effect = "WE_EFFECT";
    public const string Links = "WE_LINKS";

    // Calls.
    public const string UnknownRegion = "WE_UNKNOWN_REGION";
    public const string UnknownHazard = "WE_UNKNOWN_HAZARD";
    public const string UnknownSeverity = "WE_UNKNOWN_SEVERITY";
    public const string UnknownDisease = "WE_UNKNOWN_DISEASE";
    public const string UnknownIntervention = "WE_UNKNOWN_INTERVENTION";
    public const string UnknownCard = "WE_UNKNOWN_CARD";
    public const string UnknownProfile = "WE_UNKNOWN_PROFILE";
    public const string UnknownSeason = "WE_UNKNOWN_SEASON";
    public const string StepOrder = "WE_STEP_ORDER";
    public const string FactMissing = "WE_FACT_MISSING";
    public const string FactInvalid = "WE_FACT_INVALID";
    public const string Capacity = "WE_CAPACITY";
    public const string NotApplicable = "WE_NOT_APPLICABLE";
    public const string AlreadyActive = "WE_ALREADY_ACTIVE";
    public const string Count = "WE_COUNT";

    // Save.
    public const string SaveFormat = "WE_SAVE_FORMAT";
    public const string SaveDigest = "WE_SAVE_DIGEST";
    public const string SaveDocument = "WE_SAVE_DOCUMENT";
    public const string SaveState = "WE_SAVE_STATE";

    // Branch.
    public const string BranchDocument = "WE_BRANCH_DOCUMENT";
    public const string ForcedOnset = "WE_FORCED_ONSET";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
