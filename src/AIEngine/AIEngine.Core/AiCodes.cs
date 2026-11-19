using EngineCommon;

namespace AIEngine;

/// <summary>Every code the AI engine refuses with: the document, the calls, the step and the save each have their own.</summary>
public static class AiCodes
{
    // Document.
    public const string Format = "AI_FORMAT";
    public const string UnknownField = "AI_UNKNOWN_FIELD";
    public const string MissingField = "AI_MISSING_FIELD";
    public const string Range = "AI_RANGE";
    public const string Duplicate = "AI_DUPLICATE";
    public const string Reference = "AI_REFERENCE";
    public const string ValueType = "AI_VALUE_TYPE";
    public const string ExprType = "AI_EXPR_TYPE";
    public const string TooDeep = "AI_TOO_DEEP";
    public const string TooMany = "AI_TOO_MANY_NODES";
    public const string CannotWait = "AI_CANNOT_WAIT";
    public const string WrongWriter = "AI_WRONG_WRITER";
    public const string Overflow = "AI_OVERFLOW";
    public const string Divisor = "AI_DIVISOR";
    public const string Reserved = "AI_RESERVED_NAME";

    // Calls.
    public const string UnknownAgent = "AI_UNKNOWN_AGENT";
    public const string DuplicateAgent = "AI_DUPLICATE_AGENT";
    public const string UnknownBehavior = "AI_UNKNOWN_BEHAVIOR";
    public const string Percept = "AI_PERCEPT";
    public const string Capacity = "AI_PERCEPT_CAPACITY";
    public const string KeyWrite = "AI_KEY_WRITE";
    public const string Budget = "AI_BUDGET_TOO_SMALL";
    public const string TickOrder = "AI_TICK_ORDER";

    // Save.
    public const string SaveFormat = "AI_SAVE_FORMAT";
    public const string SaveDigest = "AI_SAVE_DIGEST";
    public const string SaveDocument = "AI_SAVE_DOCUMENT";
    public const string SaveState = "AI_SAVE_STATE";

    public static readonly RefusalCodes ForDocument = new(Format, UnknownField, MissingField, Range, Duplicate, Reference);
}