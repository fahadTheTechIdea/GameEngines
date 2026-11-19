using EngineCommon;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>D4: no literal text in documents, locale tables checked against the document, CLDR plural categories for the named locales.</summary>
public sealed class LocaleTests
{
    private const string English = """
    { "format": 1, "locale": "en", "messages": {
      "brief.hello": { "select": "count", "one": "{count} squad is ready.", "other": "{count} squads are ready." },
      "brief.accept": "Understood.", "brief.refuse": "No.", "brief.secret": "I know a way.", "brief.lucky": "Good luck.", "brief.plain": "Hold the bridge.",
      "quest.hold.title": "Hold the bridge", "quest.hold.order": "Do not let them cross.", "quest.hold.done": "The bridge held." } }
    """;

    private static LocaleTable En() => LocaleTable.Load(English);

    private static MessageArg Count(long n) => new("count", FactValue.From(n));

    [Fact]
    public void ATableThatCoversTheDocumentPassesAndRendersWithPluralsAndPlaceholders()
    {
        var set = Fixture.Load();
        var table = En();
        LocaleCoverage.Check(set, table);
        Assert.Equal("1 squad is ready.", LocaleRenderer.Render(table, "brief.hello", [Count(1)]));
        Assert.Equal("7 squads are ready.", LocaleRenderer.Render(table, "brief.hello", [Count(7)]));
        Assert.Equal("0 squads are ready.", LocaleRenderer.Render(table, "brief.hello", [Count(0)]));
        Assert.Equal("Understood.", LocaleRenderer.Render(table, "brief.accept", []));
    }

    [Fact]
    public void RenderingIsPureSoTheSameCallGivesTheSameTextWhateverCultureTheMachineUses()
    {
        var table = En();
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "sv-SE", "tr-TR", "ar-SA", "fa-IR", "de-DE" })
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
                Assert.Equal("1234567 squads are ready.", LocaleRenderer.Render(table, "brief.hello", [Count(1234567)]));
                Assert.Equal("-5 squads are ready.", LocaleRenderer.Render(table, "brief.hello", [Count(-5)]));
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void AMissingKeyAnUnknownKeyAndAStrayPlaceholderEachHaveTheirOwnCode()
    {
        var set = Fixture.Load();
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(English.Replace("\"brief.refuse\": \"No.\",", "", StringComparison.Ordinal)))));
        Assert.Equal(LocaleCodes.UnknownKey, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(English.Replace("\"brief.refuse\": \"No.\",", "\"brief.refuse\": \"No.\", \"brief.extra\": \"Extra.\",", StringComparison.Ordinal)))));
        Assert.Equal(LocaleCodes.Placeholder, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(English.Replace("\"brief.refuse\": \"No.\"", "\"brief.refuse\": \"No, {name}.\"", StringComparison.Ordinal)))));
        // The placeholder the line gives but a translator leaves out is fine.
        LocaleCoverage.Check(set, LocaleTable.Load(English.Replace("{ \"select\": \"count\", \"one\": \"{count} squad is ready.\", \"other\": \"{count} squads are ready.\" }", "\"Ready.\"", StringComparison.Ordinal)));
        // A plural message may select only on an argument the line gives.
        Assert.Equal(LocaleCodes.Placeholder, Fixture.Refusal(() => LocaleCoverage.Check(set, LocaleTable.Load(English.Replace("\"select\": \"count\"", "\"select\": \"total\"", StringComparison.Ordinal)))));
    }

    [Theory]
    [InlineData("\"locale\": \"en\"", "\"locale\": \"xx\"", LocaleCodes.Unsupported)]
    [InlineData("\"format\": 1", "\"format\": 2", LocaleCodes.Format)]
    [InlineData("\"format\": 1,", "\"format\": 1, \"extra\": 1,", LocaleCodes.Format)]
    [InlineData("\"one\": \"{count} squad is ready.\"", "\"few\": \"{count} squad is ready.\"", LocaleCodes.Format)]
    [InlineData(", \"other\": \"{count} squads are ready.\"", "", LocaleCodes.Format)]
    [InlineData("\"select\": \"count\", ", "", LocaleCodes.Format)]
    [InlineData("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"Understood. {\"", LocaleCodes.Format)]
    [InlineData("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"Understood. }\"", LocaleCodes.Format)]
    [InlineData("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"Understood. {Name}\"", LocaleCodes.Format)]
    [InlineData("\"brief.accept\": \"Understood.\"", "\"brief.accept\": 5", LocaleCodes.Format)]
    [InlineData("\"brief.accept\": \"Understood.\",", "\"brief.accept\": \"Understood.\", \"brief.accept\": \"Again.\",", LocaleCodes.Format)]
    public void ABadLocaleTableIsRefused(string old, string replacement, string code) =>
        Assert.Equal(code, Fixture.Refusal(() => LocaleTable.Load(English.Replace(old, replacement, StringComparison.Ordinal))));

    [Fact]
    public void BracesCanBeWrittenWithTheDoubledForm()
    {
        var table = LocaleTable.Load(English.Replace("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"Use {{braces}} and {count}.\"", StringComparison.Ordinal));
        Assert.Equal("Use {braces} and 3.", LocaleRenderer.Render(table, "brief.accept", [Count(3)]));
    }

    [Fact]
    public void RenderingRefusesWhatItCannotFillInsteadOfGuessing()
    {
        var table = En();
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => LocaleRenderer.Render(table, "nothing", [])));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => LocaleRenderer.Render(table, "brief.hello", [])));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => LocaleRenderer.Render(table, "brief.hello", [new MessageArg("count", FactValue.From("seven"))])));
        var wants = LocaleTable.Load(English.Replace("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"For {who}.\"", StringComparison.Ordinal));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => LocaleRenderer.Render(wants, "brief.accept", [])));
        var shown = LocaleTable.Load(English.Replace("\"brief.accept\": \"Understood.\"", "\"brief.accept\": \"{a} {b} {c}\"", StringComparison.Ordinal));
        Assert.Equal("5 true x", LocaleRenderer.Render(shown, "brief.accept", [new("a", FactValue.From(5L)), new("b", FactValue.From(true)), new("c", FactValue.From("x"))]));
    }

    [Theory]
    // locale, number, category (the whole-number rules of CLDR for the locales the engine names)
    [InlineData("en", 0, "other")] [InlineData("en", 1, "one")] [InlineData("en", 2, "other")] [InlineData("en", -1, "one")]
    [InlineData("de", 1, "one")] [InlineData("de", 5, "other")]
    [InlineData("es", 1, "one")] [InlineData("es", 0, "other")]
    [InlineData("fr", 0, "one")] [InlineData("fr", 1, "one")] [InlineData("fr", 2, "other")] [InlineData("fr", 1000000, "many")] [InlineData("fr", 3000000, "many")] [InlineData("fr", 1000001, "other")]
    [InlineData("ru", 1, "one")] [InlineData("ru", 2, "few")] [InlineData("ru", 4, "few")] [InlineData("ru", 5, "many")] [InlineData("ru", 11, "many")] [InlineData("ru", 12, "many")]
    [InlineData("ru", 14, "many")] [InlineData("ru", 21, "one")] [InlineData("ru", 22, "few")] [InlineData("ru", 25, "many")] [InlineData("ru", 101, "one")] [InlineData("ru", 111, "many")] [InlineData("ru", 0, "many")]
    [InlineData("pl", 1, "one")] [InlineData("pl", 2, "few")] [InlineData("pl", 5, "many")] [InlineData("pl", 12, "many")] [InlineData("pl", 22, "few")] [InlineData("pl", 21, "many")] [InlineData("pl", 0, "many")]
    [InlineData("ar", 0, "zero")] [InlineData("ar", 1, "one")] [InlineData("ar", 2, "two")] [InlineData("ar", 3, "few")] [InlineData("ar", 10, "few")] [InlineData("ar", 11, "many")]
    [InlineData("ar", 99, "many")] [InlineData("ar", 100, "other")] [InlineData("ar", 102, "other")] [InlineData("ar", 103, "few")] [InlineData("ar", 111, "many")]
    [InlineData("ja", 1, "other")] [InlineData("zh", 0, "other")] [InlineData("zh-CN", 5, "other")] [InlineData("ru-RU", 2, "few")] [InlineData("EN", 1, "one")]
    public void PluralCategoriesFollowTheCldrRulesForWholeNumbers(string locale, long number, string expected) =>
        Assert.Equal(expected, PluralRules.Category(locale, number));

    [Fact]
    public void ALocaleWithoutRulesIsRefusedNotGivenTheEnglishOnes()
    {
        Assert.Equal(LocaleCodes.Unsupported, Fixture.Refusal(() => PluralRules.Category("xx", 1)));
        Assert.Equal(LocaleCodes.Unsupported, Fixture.Refusal(() => PluralRules.Categories("it")));
    }

    [Fact]
    public void EveryCategoryARuleCanReturnIsOneTheLocaleDeclares()
    {
        foreach (var locale in PluralRules.Supported)
        {
            var declared = PluralRules.Categories(locale);
            for (long n = -3; n <= 1_000_105; n += n < 300 ? 1 : 997)
                Assert.Contains(PluralRules.Category(locale, n), declared);
            Assert.Contains("other", declared);
        }
    }

    [Fact]
    public void ARussianTableUsesItsThreeFormsAndLeavesOutWhatItDoesNotNeed()
    {
        var table = LocaleTable.Load("""
        { "format": 1, "locale": "ru", "messages": {
          "brief.hello": { "select": "count", "one": "{count} Ð¾Ñ‚Ñ€ÑÐ´", "few": "{count} Ð¾Ñ‚Ñ€ÑÐ´Ð°", "many": "{count} Ð¾Ñ‚Ñ€ÑÐ´Ð¾Ð²", "other": "{count} Ð¾Ñ‚Ñ€ÑÐ´Ð°" } } }
        """);
        Assert.Equal("1 Ð¾Ñ‚Ñ€ÑÐ´", LocaleRenderer.Render(table, "brief.hello", [Count(1)]));
        Assert.Equal("3 Ð¾Ñ‚Ñ€ÑÐ´Ð°", LocaleRenderer.Render(table, "brief.hello", [Count(3)]));
        Assert.Equal("11 Ð¾Ñ‚Ñ€ÑÐ´Ð¾Ð²", LocaleRenderer.Render(table, "brief.hello", [Count(11)]));
        // "other" is the catch-all; a table that gives no form for a category falls to it.
        var small = LocaleTable.Load("""{ "format": 1, "locale": "ru", "messages": { "k": { "select": "n", "other": "{n}" } } }""");
        Assert.Equal("5", LocaleRenderer.Render(small, "k", [new MessageArg("n", FactValue.From(5L))]));
    }
}