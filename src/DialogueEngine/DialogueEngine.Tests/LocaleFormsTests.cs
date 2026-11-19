using EngineCommon;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>Messages that choose a form: by how many, by which place, by the exact value of a text (a gender), with decimals, and in right-to-left text.</summary>
public sealed class LocaleFormsTests
{
    private static LocaleTable Table(string locale, string messages) => LocaleTable.Load("{ \"format\": 1, \"locale\": \"" + locale + "\", \"messages\": " + messages + " }");

    private static string Render(LocaleTable table, string key, params (string Name, FactValue Value)[] args) =>
        LocaleRenderer.Render(table, key, args.Select(a => new MessageArg(a.Name, a.Value)).ToList());

    [Theory]
    [InlineData(1, "1st")] [InlineData(2, "2nd")] [InlineData(3, "3rd")] [InlineData(4, "4th")] [InlineData(11, "11th")] [InlineData(12, "12th")] [InlineData(13, "13th")]
    [InlineData(21, "21st")] [InlineData(22, "22nd")] [InlineData(23, "23rd")] [InlineData(101, "101st")] [InlineData(111, "111th")]
    public void AnEnglishOrdinalMessageGivesThePlaceItsSuffix(long place, string expected)
    {
        var table = Table("en", """{ "place": { "type": "ordinal", "select": "n", "one": "{n}st", "two": "{n}nd", "few": "{n}rd", "other": "{n}th" } }""");
        Assert.Equal(expected, Render(table, "place", ("n", FactValue.From(place))));
    }

    [Fact]
    public void ADecimalGivenAsTextSelectsByItsVisibleDigits()
    {
        var table = Table("en", """{ "dist": { "select": "km", "one": "{km} kilometre", "other": "{km} kilometres" } }""");
        Assert.Equal("1 kilometre", Render(table, "dist", ("km", FactValue.From(1L))));
        Assert.Equal("1 kilometre", Render(table, "dist", ("km", FactValue.From("1"))));
        Assert.Equal("1.0 kilometres", Render(table, "dist", ("km", FactValue.From("1.0"))));      // one visible fraction digit: English says other
        Assert.Equal("1.5 kilometres", Render(table, "dist", ("km", FactValue.From("1.5"))));
        var french = Table("fr", """{ "dist": { "select": "km", "one": "{km} kilomètre", "other": "{km} kilomètres" } }""");
        Assert.Equal("1.5 kilomètre", Render(french, "dist", ("km", FactValue.From("1.5"))));      // French: 0 to 1.99 is one
        Assert.Equal("2.0 kilomètres", Render(french, "dist", ("km", FactValue.From("2.0"))));
    }

    [Fact]
    public void AGenderOrAnyOtherTextValueSelectsTheFormNamedByIt()
    {
        var table = Table("en", """{ "greet": { "type": "select", "select": "gender", "feminine": "She waves.", "masculine": "He waves.", "other": "They wave." } }""");
        Assert.Equal("She waves.", Render(table, "greet", ("gender", FactValue.From("feminine"))));
        Assert.Equal("He waves.", Render(table, "greet", ("gender", FactValue.From("masculine"))));
        Assert.Equal("They wave.", Render(table, "greet", ("gender", FactValue.From("unknown"))));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => Render(table, "greet", ("gender", FactValue.From(3L)))));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => Render(table, "greet")));
    }

    [Fact]
    public void HebrewHasTheTwoFormAndArabicSixAndTheTableRefusesAFormTheLanguageLacks()
    {
        var hebrew = Table("he", """{ "k": { "select": "n", "one": "ספר {n}", "two": "שני ספרים", "other": "{n} ספרים" } }""");
        Assert.Equal("שני ספרים", Render(hebrew, "k", ("n", FactValue.From(2L))));
        Assert.Equal("5 ספרים", Render(hebrew, "k", ("n", FactValue.From(5L))).Replace("⁨", "").Replace("⁩", ""));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => Table("he", """{ "k": { "select": "n", "few": "x", "other": "y" } }""")));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => Table("en", """{ "k": { "type": "ordinal", "select": "n", "zero": "x", "other": "y" } }""")));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => Table("en", """{ "k": { "type": "magic", "select": "n", "other": "y" } }""")));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => Table("en", """{ "k": { "type": "select", "select": "g", "Female": "x", "other": "y" } }""")));
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => Table("en", """{ "k": { "type": "select", "select": "g", "a": "x" } }""")));
    }

    [Fact]
    public void ARightToLeftTextIsolatesWhatItInsertsAndALeftToRightTextDoesNot()
    {
        var arabic = Table("ar", """{ "hi": "مرحبا {name}!" }""");
        var text = Render(arabic, "hi", ("name", FactValue.From("Ann")));
        Assert.Equal("مرحبا ⁨Ann⁩!", text);
        var english = Table("en", """{ "hi": "Hello {name}!" }""");
        Assert.Equal("Hello Ann!", Render(english, "hi", ("name", FactValue.From("Ann"))));
        Assert.True(TextDirection.IsRightToLeft("ar-EG"));
        Assert.True(TextDirection.IsRightToLeft("HE"));
        Assert.False(TextDirection.IsRightToLeft("ru"));
    }

    [Fact]
    public void ASelectOnSomethingThatIsNotANumberOrAMalformedNumberIsRefusedNotGuessed()
    {
        var table = Table("en", """{ "k": { "select": "n", "one": "one", "other": "other" } }""");
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => Render(table, "k", ("n", FactValue.From(true)))));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => Render(table, "k", ("n", FactValue.From("1e3")))));
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => Render(table, "k", ("n", FactValue.From("many")))));
    }
}
