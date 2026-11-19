using System.Text.Json;
using EngineCommon;

namespace EngineCommon;

/// <summary>
/// The CLDR plural categories, read from the CLDR plural-rules table (<c>cldr-plurals.json</c>, extracted from <c>plurals.xml</c> and <c>ordinals.xml</c> of Unicode CLDR) and evaluated by the rule
/// language of UTS 35 part 3, for the locales the table names. A locale not in the table is refused, never given the English rule. Numbers are whole or decimal (decimals as text, because
/// "1.0" and "1" differ); compact exponents (<c>1c6</c>) are not supported.
/// </summary>
public static class PluralRules
{
    private sealed record LocaleRules(IReadOnlyList<PluralRule> Cardinal, IReadOnlyList<PluralRule> Ordinal, IReadOnlyList<string> CardinalSamples, IReadOnlyList<string> OrdinalSamples);

    private static readonly Lazy<(string Source, SortedDictionary<string, LocaleRules> Locales)> Table = new(Load);

    public static IReadOnlyList<string> Supported => Table.Value.Locales.Keys.ToList();

    /// <summary>Where the table comes from and when it was taken.</summary>
    public static string Source => Table.Value.Source;

    private static (string, SortedDictionary<string, LocaleRules>) Load()
    {
        using var stream = typeof(PluralRules).Assembly.GetManifestResourceStream("cldr-plurals.json") ?? throw new InvalidOperationException("the library does not carry cldr-plurals.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var locales = new SortedDictionary<string, LocaleRules>(StringComparer.Ordinal);
        foreach (var locale in root.GetProperty("locales").EnumerateObject())
        {
            var cardinal = locale.Value.GetProperty("cardinal").EnumerateArray().ToList();
            var ordinal = locale.Value.GetProperty("ordinal").EnumerateArray().ToList();
            locales[locale.Name] = new LocaleRules(ReadRules(cardinal), ReadRules(ordinal), cardinal.Select(r => r.GetProperty("samples").GetString()!).ToList(), ordinal.Select(r => r.GetProperty("samples").GetString()!).ToList());
        }
        return (root.GetProperty("source").GetString()! + ", taken " + root.GetProperty("retrieved").GetString(), locales);
    }

    private static List<PluralRule> ReadRules(List<JsonElement> rows) => rows.Select(r => PluralRule.Parse(r.GetProperty("category").GetString()!, r.GetProperty("rule").GetString()!)).ToList();

    private static string Language(string locale) => locale.Split('-', '_')[0].ToLowerInvariant();

    private static LocaleRules For(string locale) =>
        Table.Value.Locales.TryGetValue(Language(locale), out var rules) ? rules
            : throw new EngineRefusedException(LocaleCodes.Unsupported, "no plural rules for locale '" + locale + "'; supported: " + string.Join(", ", Supported));

    /// <summary>The rules of a locale in table order, the fallback "other" last.</summary>
    public static IReadOnlyList<PluralRule> Rules(string locale, PluralKind kind) => kind == PluralKind.Cardinal ? For(locale).Cardinal : For(locale).Ordinal;

    /// <summary>The CLDR sample text of each rule (<c>@integer 1, 21 @decimal 1.0</c>), in the order of <see cref="Rules"/>, so the tests can check the rules against CLDR own examples.</summary>
    public static IReadOnlyList<string> Samples(string locale, PluralKind kind) => kind == PluralKind.Cardinal ? For(locale).CardinalSamples : For(locale).OrdinalSamples;

    /// <summary>The categories a locale uses, so a table that gives a form the language does not have is refused.</summary>
    public static IReadOnlyList<string> Categories(string locale, PluralKind kind = PluralKind.Cardinal) => Rules(locale, kind).Select(r => r.Category).ToList();

    public static string Category(string locale, long number) => Category(locale, PluralKind.Cardinal, PluralOperands.Of(number));

    public static string Category(string locale, PluralKind kind, string number) => Category(locale, kind, PluralOperands.Parse(number));

    public static string Category(string locale, PluralKind kind, PluralOperands operands)
    {
        foreach (var rule in Rules(locale, kind))
            if (rule.Category != "other" && rule.Matches(operands)) return rule.Category;
        return "other";
    }
}
