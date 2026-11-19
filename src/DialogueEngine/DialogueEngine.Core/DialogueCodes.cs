using EngineCommon;

namespace DialogueEngine;

/// <summary>Every code the dialogue engine refuses with: the document, the call, the save and the locale tables each have their own.</summary>
public static class DialogueCodes
{
    // Document.
    public const string Format = "DLG_FORMAT";
    public const string UnknownField = "DLG_UNKNOWN_FIELD";
    public const string MissingField = "DLG_MISSING_FIELD";
    public const string Range = "DLG_RANGE";
    public const string Duplicate = "DLG_DUPLICATE";
    public const string Reference = "DLG_REFERENCE";
    public const string UnknownSpeaker = "DLG_UNKNOWN_SPEAKER";
    public const string UnknownPortrait = "DLG_UNKNOWN_PORTRAIT";
    public const string Unreachable = "DLG_UNREACHABLE";
    public const string Loop = "DLG_LOOP_WITHOUT_WAIT";
    public const string CallCycle = "DLG_CALL_CYCLE";
    public const string SilentChain = "DLG_SILENT_CHAIN";
    public const string ValueType = "DLG_VALUE_TYPE";
    public const string WarningUndeclared = "DLG_CW_UNDECLARED";
    public const string WarningFact = "DLG_CW_FACT";
    public const string WarningAlternative = "DLG_CW_ALTERNATIVE";

    // Calls.
    public const string FactMissing = "DLG_FACT_MISSING";
    public const string FactInvalid = "DLG_FACT_INVALID";
    public const string NoConversation = "DLG_NO_CONVERSATION";
    public const string Busy = "DLG_BUSY";
    public const string NotWaiting = "DLG_NOT_WAITING";
    public const string UnknownOption = "DLG_UNKNOWN_OPTION";
    public const string OptionUnavailable = "DLG_OPTION_UNAVAILABLE";
    public const string NoOption = "DLG_NO_OPTION_AVAILABLE";
    public const string StepLimit = "DLG_STEP_LIMIT";
    public const string CallDepth = "DLG_CALL_DEPTH";
    public const string NoTimeout = "DLG_NO_TIMEOUT";
    public const string QuestState = "DLG_QUEST_STATE";
    public const string QuestLoop = "DLG_QUEST_LOOP";
    public const string Priority = "DLG_PRIORITY";
    public const string NotInterruptible = "DLG_NOT_INTERRUPTIBLE";
    public const string QueueFull = "DLG_QUEUE_FULL";
    public const string AlreadyQueued = "DLG_ALREADY_QUEUED";

    // Save and reload.
    public const string SaveFormat = "DLG_SAVE_FORMAT";
    public const string SaveDigest = "DLG_SAVE_DIGEST";
    public const string SaveDocument = "DLG_SAVE_DOCUMENT";
    public const string SaveState = "DLG_SAVE_STATE";
    public const string ReloadIncompatible = "DLG_RELOAD_INCOMPATIBLE";
    public const string ImportSyntax = "DLG_IMPORT_SYNTAX";
    public const string ImportUnsupported = "DLG_IMPORT_UNSUPPORTED";
    public const string ImportInvalid = "DLG_IMPORT_INVALID";
    public const string ImportFunction = "DLG_IMPORT_FUNCTION";
    public const string MapFormat = "DLG_MAP_FORMAT";
    public const string MapSource = "DLG_MAP_UNKNOWN_SOURCE";
    public const string MapTarget = "DLG_MAP_UNKNOWN_TARGET";
    public const string MapConflict = "DLG_MAP_CONFLICT";


    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
    public static readonly RefusalCodes ForMap = new(MapFormat, MapFormat, MapFormat, MapFormat, MapFormat, MapFormat);
}