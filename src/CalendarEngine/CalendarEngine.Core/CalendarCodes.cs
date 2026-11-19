using EngineCommon;

namespace CalendarEngine;

/// <summary>Every code the calendar engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the dialogue engine DLG_LOCALE codes, because the tables are its.</summary>
public static class CalendarCodes
{
    // Document.
    public const string Format = "CAL_FORMAT";
    public const string UnknownField = "CAL_UNKNOWN_FIELD";
    public const string MissingField = "CAL_MISSING_FIELD";
    public const string Range = "CAL_RANGE";
    public const string Duplicate = "CAL_DUPLICATE";
    public const string Reference = "CAL_REFERENCE";
    public const string Leap = "CAL_LEAP";
    public const string Clock = "CAL_CLOCK";
    public const string Recurrence = "CAL_RECURRENCE";
    public const string Order = "CAL_ORDER";
    public const string Weeks = "CAL_WEEKS";
    public const string Date = "CAL_DATE";
    public const string Base = "CAL_BASE";
    public const string BaseCoverage = "CAL_BASE_COVERAGE";
    public const string Overlap = "CAL_BLOCK_OVERLAP";
    public const string Tie = "CAL_PRIORITY_TIE";
    public const string Reserved = "CAL_RESERVED_NAME";

    // Calls.
    public const string DateInvalid = "CAL_DATE_INVALID";
    public const string InstantRange = "CAL_INSTANT_RANGE";
    public const string FactMissing = "CAL_FACT_MISSING";
    public const string FactInvalid = "CAL_FACT_INVALID";
    public const string UnknownSchedule = "CAL_UNKNOWN_SCHEDULE";
    public const string UnknownActor = "CAL_UNKNOWN_ACTOR";
    public const string DuplicateActor = "CAL_DUPLICATE_ACTOR";
    public const string UnknownInterrupt = "CAL_UNKNOWN_INTERRUPT";
    public const string Outranked = "CAL_INTERRUPT_OUTRANKED";
    public const string Scale = "CAL_SCALE";
    public const string Skip = "CAL_SKIP";
    public const string TickOrder = "CAL_TICK_ORDER";
    public const string Capacity = "CAL_CAPACITY";
    public const string UnknownName = "CAL_UNKNOWN_NAME";

    // Save.
    public const string SaveFormat = "CAL_SAVE_FORMAT";
    public const string SaveDigest = "CAL_SAVE_DIGEST";
    public const string SaveDocument = "CAL_SAVE_DOCUMENT";
    public const string SaveState = "CAL_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
