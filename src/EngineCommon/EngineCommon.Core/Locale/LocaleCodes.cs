namespace EngineCommon;

/// <summary>
/// The refusal codes of the locale machinery (tables, chains, plural rules, message rendering, coverage). The machinery is shared by every engine that has words, so it has one set of codes; the texts
/// keep the <c>DLG_</c> prefix they had when the dialogue engine was the only user, so no saved report, test or tool that names a code changed.
/// </summary>
public static class LocaleCodes
{
    public const string Format = "DLG_LOCALE_FORMAT";
    public const string Unsupported = "DLG_LOCALE_UNSUPPORTED";
    public const string MissingKey = "DLG_LOCALE_MISSING_KEY";
    public const string UnknownKey = "DLG_LOCALE_UNKNOWN_KEY";
    public const string Placeholder = "DLG_LOCALE_PLACEHOLDER";
    public const string Argument = "DLG_LOCALE_ARGUMENT";

    public static readonly RefusalCodes ForLocale = new(Format, Format, Format, Format, Format, Format);
}
