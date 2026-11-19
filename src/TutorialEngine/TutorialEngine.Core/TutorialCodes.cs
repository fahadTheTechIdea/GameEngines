using EngineCommon;

namespace TutorialEngine;

/// <summary>Every code the tutorial engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the <see cref="LocaleCodes"/> of the shared locale machinery.</summary>
public static class TutorialCodes
{
    // Document.
    public const string Format = "TUT_FORMAT";
    public const string UnknownField = "TUT_UNKNOWN_FIELD";
    public const string MissingField = "TUT_MISSING_FIELD";
    public const string Range = "TUT_RANGE";
    public const string Duplicate = "TUT_DUPLICATE";
    public const string Reference = "TUT_REFERENCE";
    public const string Reserved = "TUT_RESERVED_NAME";
    public const string Prereq = "TUT_PREREQUISITE";
    public const string Hint = "TUT_HINT";
    public const string Scope = "TUT_SCOPE";
    public const string Journey = "TUT_JOURNEY";
    public const string Skill = "TUT_SKILL";

    // Calls.
    public const string UnknownEvent = "TUT_UNKNOWN_EVENT";
    public const string UnknownStep = "TUT_UNKNOWN_STEP";
    public const string UnknownTip = "TUT_UNKNOWN_TIP";
    public const string UnknownLanguage = "TUT_UNKNOWN_LANGUAGE";
    public const string UnknownEntry = "TUT_UNKNOWN_ENTRY";
    public const string NotSkippable = "TUT_NOT_SKIPPABLE";
    public const string StepState = "TUT_STEP_STATE";
    public const string TipState = "TUT_TIP_STATE";
    public const string Phase = "TUT_PHASE";
    public const string JourneyOrder = "TUT_JOURNEY_ORDER";
    public const string FactMissing = "TUT_FACT_MISSING";
    public const string FactInvalid = "TUT_FACT_INVALID";
    public const string StepOrder = "TUT_STEP_ORDER";
    public const string Count = "TUT_COUNT";
    public const string Setting = "TUT_SETTING";
    public const string Intent = "TUT_INTENT";

    // Save.
    public const string SaveFormat = "TUT_SAVE_FORMAT";
    public const string SaveDigest = "TUT_SAVE_DIGEST";
    public const string SaveDocument = "TUT_SAVE_DOCUMENT";
    public const string SaveState = "TUT_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
