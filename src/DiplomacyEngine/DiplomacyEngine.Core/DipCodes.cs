using EngineCommon;

namespace DiplomacyEngine;

/// <summary>Every code the diplomacy engine refuses with: the document, the calls and the save each have their own. Locale-table refusals keep the <see cref="LocaleCodes"/> of the shared locale machinery.</summary>
public static class DipCodes
{
    // Document.
    public const string Format = "DIP_FORMAT";
    public const string UnknownField = "DIP_UNKNOWN_FIELD";
    public const string MissingField = "DIP_MISSING_FIELD";
    public const string Range = "DIP_RANGE";
    public const string Duplicate = "DIP_DUPLICATE";
    public const string Reference = "DIP_REFERENCE";
    public const string Reserved = "DIP_RESERVED_NAME";
    public const string Clause = "DIP_CLAUSE";
    public const string Rules = "DIP_RULES";
    public const string Politics = "DIP_POLITICS";
    public const string Espionage = "DIP_ESPIONAGE";

    // Calls.
    public const string UnknownRealm = "DIP_UNKNOWN_REALM";
    public const string UnknownType = "DIP_UNKNOWN_TYPE";
    public const string UnknownTreaty = "DIP_UNKNOWN_TREATY";
    public const string UnknownProposal = "DIP_UNKNOWN_PROPOSAL";
    public const string UnknownWar = "DIP_UNKNOWN_WAR";
    public const string UnknownCb = "DIP_UNKNOWN_CASUS_BELLI";
    public const string UnknownDeed = "DIP_UNKNOWN_DEED";
    public const string UnknownAgent = "DIP_UNKNOWN_AGENT";
    public const string UnknownMission = "DIP_UNKNOWN_MISSION";
    public const string UnknownEvent = "DIP_UNKNOWN_EVENT";
    public const string UnknownSource = "DIP_UNKNOWN_SOURCE";
    public const string SameRealm = "DIP_SAME_REALM";
    public const string Terms = "DIP_TERMS";
    public const string Blocked = "DIP_BLOCKED";
    public const string AlreadyActive = "DIP_ALREADY_ACTIVE";
    public const string AtWar = "DIP_AT_WAR";
    public const string NotAtWar = "DIP_NOT_AT_WAR";
    public const string NotPrincipal = "DIP_NOT_PRINCIPAL";
    public const string NotHost = "DIP_NOT_HOST_CONTROLLED";
    public const string NotParty = "DIP_NOT_PARTY";
    public const string NoCasusBelli = "DIP_NO_CASUS_BELLI";
    public const string CannotCancel = "DIP_CANNOT_CANCEL";
    public const string Unavailable = "DIP_UNAVAILABLE";
    public const string Revolt = "DIP_REVOLT";
    public const string Capacity = "DIP_CAPACITY";
    public const string StepOrder = "DIP_STEP_ORDER";
    public const string FactMissing = "DIP_FACT_MISSING";
    public const string FactInvalid = "DIP_FACT_INVALID";
    public const string UnknownSeason = "DIP_UNKNOWN_SEASON";
    public const string NotInWar = "DIP_NOT_IN_WAR";
    public const string NotDue = "DIP_NOT_DUE";

    // Save.
    public const string SaveFormat = "DIP_SAVE_FORMAT";
    public const string SaveDigest = "DIP_SAVE_DIGEST";
    public const string SaveDocument = "DIP_SAVE_DOCUMENT";
    public const string SaveState = "DIP_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}
