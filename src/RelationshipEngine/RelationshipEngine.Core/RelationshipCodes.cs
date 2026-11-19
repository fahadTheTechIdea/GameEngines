using EngineCommon;

namespace RelationshipEngine;

/// <summary>Every code the relationship engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the <see cref="LocaleCodes"/> of the shared locale machinery.</summary>
public static class RelationshipCodes
{
    // Document.
    public const string Format = "REL_FORMAT";
    public const string UnknownField = "REL_UNKNOWN_FIELD";
    public const string MissingField = "REL_MISSING_FIELD";
    public const string Range = "REL_RANGE";
    public const string Duplicate = "REL_DUPLICATE";
    public const string Reference = "REL_REFERENCE";
    public const string Curve = "REL_CURVE";
    public const string Ladder = "REL_LADDER";
    public const string Decay = "REL_DECAY";
    public const string Stack = "REL_STACK";
    public const string Effect = "REL_EFFECT";
    public const string Reserved = "REL_RESERVED_NAME";
    public const string OldFormat = "REL_FORMAT_OLD";
    public const string Social = "REL_SOCIAL";
    public const string FormatTwo = "REL_FORMAT_2";

    // Calls.
    public const string UnknownActor = "REL_UNKNOWN_ACTOR";
    public const string DuplicateActor = "REL_DUPLICATE_ACTOR";
    public const string UnknownInteraction = "REL_UNKNOWN_INTERACTION";
    public const string UnknownGift = "REL_UNKNOWN_GIFT";
    public const string UnknownNeed = "REL_UNKNOWN_NEED";
    public const string UnknownMoodlet = "REL_UNKNOWN_MOODLET";
    public const string UnknownMemory = "REL_UNKNOWN_MEMORY";
    public const string UnknownAxis = "REL_UNKNOWN_AXIS";
    public const string UnknownBond = "REL_UNKNOWN_BOND";
    public const string Self = "REL_SELF";
    public const string Precondition = "REL_PRECONDITION";
    public const string Cooldown = "REL_COOLDOWN";
    public const string GiftRequired = "REL_GIFT_REQUIRED";
    public const string GiftUnexpected = "REL_GIFT_UNEXPECTED";
    public const string Witness = "REL_WITNESS";
    public const string Taste = "REL_TASTE";
    public const string FactMissing = "REL_FACT_MISSING";
    public const string FactInvalid = "REL_FACT_INVALID";
    public const string Capacity = "REL_CAPACITY";
    public const string StepOrder = "REL_STEP_ORDER";
    public const string UnknownName = "REL_UNKNOWN_NAME";

    // Save.
    public const string SaveFormat = "REL_SAVE_FORMAT";
    public const string SaveOldFormat = "REL_SAVE_FORMAT_OLD";
    public const string SaveDigest = "REL_SAVE_DIGEST";
    public const string SaveDocument = "REL_SAVE_DOCUMENT";
    public const string SaveState = "REL_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
