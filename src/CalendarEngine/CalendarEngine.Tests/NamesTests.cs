using EngineCommon;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The words: formats rendered through the dialogue engine locale machinery (plural categories, fallback chains) and the check that a locale table covers the document.</summary>
public sealed class NamesTests
{
    private static CalendarSet Set => Fixture.Gregorian;

    private static LocaleChain English => LocaleChain.Create("en", [Fixture.English], "en");

    private static Moment Moment(long year, string month, int day, int hour = 0, int minute = 0) => Set.System.MomentOf(Fixture.At(Set, year, month, day, hour, minute));

    [Fact]
    public void ALongDateIsWrittenWithTheWeekdayMonthAndEra()
    {
        Assert.Equal("Thursday, January 1, 1970 CE", CalendarNames.Format(Set, English, "long", Moment(1970, "january", 1)));
        Assert.Equal("Sunday, March 10, 2024 CE", CalendarNames.Format(Set, English, "long", Moment(2024, "march", 10)));
        Assert.EndsWith(", 1 BCE", CalendarNames.Format(Set, English, "long", Moment(0, "march", 1)));
        Assert.EndsWith(", 45 BCE", CalendarNames.Format(Set, English, "long", Moment(-44, "march", 15)));
        Assert.Equal("2024-3-10", CalendarNames.Format(Set, English, "short", Moment(2024, "march", 10)));
        Assert.Equal("13:05 in the afternoon", CalendarNames.Format(Set, English, "time", Moment(2024, "march", 10, 13, 5)));
    }

    [Theory]
    [InlineData(1, "the 1st of March")]
    [InlineData(2, "the 2nd of March")]
    [InlineData(3, "the 3rd of March")]
    [InlineData(4, "the 4th of March")]
    [InlineData(11, "the 11th of March")]
    [InlineData(12, "the 12th of March")]
    [InlineData(13, "the 13th of March")]
    [InlineData(21, "the 21st of March")]
    [InlineData(22, "the 22nd of March")]
    [InlineData(23, "the 23rd of March")]
    [InlineData(31, "the 31st of March")]
    public void AnOrdinalDayUsesTheCldrOrdinalCategoriesOfTheLocale(int day, string expected)
    {
        Assert.Equal(expected, CalendarNames.Format(Set, English, "ordinal", Moment(2024, "march", day)));
    }

    [Fact]
    public void AFestivalDayWithNoWeekdayLeavesThatPlaceholderEmpty()
    {
        var json = Fixture.Mutate(Fixture.RealmsText, root => root["formats"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["id"] = "long", ["key"] = "cal.format.long" }));
        var realms = CalendarLoader.Load(json);
        var messages = new System.Text.Json.Nodes.JsonObject();
        foreach (var key in CalendarNames.RequiredKeys(realms)) messages[key] = key;
        messages["cal.format.long"] = "[{weekday}] {day}";
        var table = LocaleTable.Load(new System.Text.Json.Nodes.JsonObject { ["format"] = 1, ["locale"] = "en", ["messages"] = messages }.ToJsonString());
        var midwinter = realms.System.Months.First(m => m.Id == "midwinter").Index;
        var moment = realms.System.MomentOf(realms.System.InstantOf(new CalendarDate(1491, midwinter, 1), 12, 0));
        Assert.Equal("[] 1", CalendarNames.Format(realms, LocaleChain.Create("en", [table], "en"), "long", moment));
    }

    [Fact]
    public void AMissingKeyFallsBackThroughTheChainAndIsRemembered()
    {
        var french = LocaleTable.Load("""{ "format": 1, "locale": "fr", "messages": { "cal.month.january": "janvier", "cal.weekday.thursday": "jeudi", "cal.era.ce": "apr. J.-C.", "cal.format.long": "{weekday} {day} {month} {year_of_era} {era}" } }""");
        var chain = LocaleChain.Create("fr-CA", [Fixture.English, french], "en");
        Assert.Equal("jeudi 1 janvier 1970 apr. J.-C.", CalendarNames.Format(Set, chain, "long", Moment(1970, "january", 1)));
        Assert.Equal("March", CalendarNames.Month(Set, chain, 2));   // not translated: English
        Assert.Equal("janvier", CalendarNames.Month(Set, chain, 0));
        Assert.Equal(1, chain.Fallbacks["fr:cal.month.march"]);
        Assert.Equal("winter", CalendarNames.Season(Set, chain, 3));
        Assert.Equal("morning", CalendarNames.DayPart(Set, chain, 2));
        Assert.Equal("full moon", CalendarNames.MoonPhase(Set, chain, 0, MoonPhase.Full));
    }

    [Fact]
    public void ANameThatNoLocaleOfTheChainHasIsRefusedAndAnUnknownFormatIsRefused()
    {
        var empty = LocaleTable.Load("""{ "format": 1, "locale": "en", "messages": { } }""");
        var chain = LocaleChain.Create("en", [empty], "en");
        Assert.Equal(LocaleCodes.MissingKey, Fixture.CodeOf(() => CalendarNames.Month(Set, chain, 0)));
        Assert.Equal(CalendarCodes.UnknownName, Fixture.CodeOf(() => CalendarNames.Format(Set, English, "weird", Moment(2024, "march", 10))));
    }

    [Fact]
    public void TheSampleEnglishTablesCoverTheirDocumentsCompletely()
    {
        var report = CalendarNames.Coverage(Set, Fixture.English);
        Assert.True(report.IsComplete, string.Join(";", report.Missing.Concat(report.Unknown)));
        Assert.Equal(CalendarNames.RequiredKeys(Set).Count, report.Total);
        Assert.Equal(100, report.Percent);
        Assert.True(CalendarNames.Coverage(Fixture.Valley, LocaleTable.Load(Fixture.ValleyEnglishText)).IsComplete);
    }

    [Fact]
    public void RequiredKeysAreEachListedOnceInOrdinalOrder()
    {
        var keys = CalendarNames.RequiredKeys(Set);
        Assert.Equal(keys.Distinct().Order(StringComparer.Ordinal).ToArray(), keys.ToArray());
        Assert.Contains("cal.holiday.christmas", keys);
        Assert.Contains("cal.phase.full", keys);
        Assert.Contains("cal.moon.moon", keys);
        Assert.Contains("cal.act.defend", keys);
        Assert.Contains("cal.place.guardhouse", keys);
        Assert.Contains("cal.format.ordinal", keys);
    }

    [Fact]
    public void CoverageNamesMissingUnknownAndMisusedMessages()
    {
        var table = LocaleTable.Load(Fixture.EnglishText);
        var messages = System.Text.Json.Nodes.JsonNode.Parse(Fixture.EnglishText)!.AsObject();
        messages["messages"]!.AsObject().Remove("cal.month.april");
        messages["messages"]!.AsObject()["cal.season.spring"] = "spring {year}";
        messages["messages"]!.AsObject()["cal.format.short"] = "{year}/{nonsense}";
        messages["messages"]!.AsObject()["cal.stray.key"] = "x";
        var report = CalendarNames.Coverage(Set, LocaleTable.Load(messages.ToJsonString()));
        Assert.False(report.IsComplete);
        Assert.Equal(["cal.month.april"], report.Missing.ToArray());
        Assert.Equal(["cal.stray.key"], report.Unknown.ToArray());
        Assert.Equal(["cal.format.short: uses '{nonsense}', which a date format does not give", "cal.season.spring: uses '{year}', which a name does not give"], report.Problems.Select(p => p.Key + ": " + p.Why).Order().ToArray());
        Assert.Equal(table.Locale, report.Locale);
        Assert.InRange(report.Percent, 98, 99);
    }
}
