namespace EngineCommon;

/// <summary>
/// Which way a locale is written. A right-to-left text that shows a value written the other way (a name in Latin letters, a number) must isolate it with the Unicode isolates
/// FSI and PDI (UAX 9, and what Fluent does), or the neighbouring punctuation lands on the wrong side. The renderer does that for the right-to-left locales named here.
/// </summary>
public static class TextDirection
{
    public const char FirstStrongIsolate = '\u2068';
    public const char PopDirectionalIsolate = '\u2069';

    private static readonly string[] RightToLeft = ["ar", "he"];

    public static bool IsRightToLeft(string locale) => RightToLeft.Contains(locale.Split('-', '_')[0].ToLowerInvariant());
}