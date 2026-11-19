using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The selector fields and format placeholders the sample documents do not exercise.</summary>
public sealed class SelectorAndFormatTests
{
    private static CalendarSet WithRoutine(string when)
    {
        var text = Fixture.MutateGregorian(root =>
        {
            var routines = root["schedules"]!.AsArray()[1]!["routines"]!.AsArray();
            routines.Add(JsonNode.Parse("{ \"id\": \"special\", \"priority\": 50, \"when\": " + when + ", \"blocks\": [ { \"id\": \"away\", \"from\": \"0:00\", \"to\": \"24:00\", \"location\": \"church\", \"activity\": \"worship\" } ] }"));
        });
        return CalendarLoader.Load(text);
    }

    private static string GuardAt(CalendarSet set, long year, string month, int day) => CalendarContext.At(set, Fixture.At(set, year, month, day, 10), Fixture.Dry).WhereIs("guard").Location;

    [Fact]
    public void AMonthSelectorAppliesInItsMonthsOnly()
    {
        var set = WithRoutine("{ \"months\": [ \"april\", \"may\" ] }");
        Assert.Equal("church", GuardAt(set, 2024, "april", 3));
        Assert.Equal("church", GuardAt(set, 2024, "may", 31));
        Assert.Equal("home", GuardAt(set, 2024, "march", 31));
        Assert.Equal("home", GuardAt(set, 2024, "june", 1));
    }

    [Fact]
    public void ADateRuleSelectorAppliesOnTheDaysOfTheRule()
    {
        var set = WithRoutine("{ \"dates\": { \"freq\": \"monthly\", \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 1 }, \"bymonthday\": [ 15 ] } }");
        Assert.Equal("church", GuardAt(set, 2024, "march", 15));
        Assert.Equal("home", GuardAt(set, 2024, "march", 14));
        Assert.Equal("home", GuardAt(set, 2023, "march", 15));   // before the start of the rule
    }

    [Fact]
    public void ASelectorWithSeveralFieldsNeedsAllOfThem()
    {
        var set = WithRoutine("{ \"weekdays\": [ \"sunday\" ], \"months\": [ \"march\" ], \"seasons\": [ \"spring\" ] }");
        Assert.Equal("church", GuardAt(set, 2024, "march", 10));   // a Sunday in March
        Assert.Equal("home", GuardAt(set, 2024, "march", 11));      // a Monday
        Assert.Equal("home", GuardAt(set, 2024, "april", 7));       // a Sunday in April
        var winterMarch = WithRoutine("{ \"months\": [ \"march\" ], \"seasons\": [ \"winter\" ] }");
        Assert.Equal("home", GuardAt(winterMarch, 2024, "march", 10));
    }

    [Fact]
    public void ASelectorThatNamesNothingIsRefused()
    {
        Assert.Equal(CalendarCodes.Format, Fixture.CodeOf(() => WithRoutine("{ }")));
        Assert.Equal(CalendarCodes.Reference, Fixture.CodeOf(() => WithRoutine("{ \"months\": [ \"smarch\" ] }")));
        Assert.Equal(CalendarCodes.Duplicate, Fixture.CodeOf(() => WithRoutine("{ \"months\": [ \"april\", \"april\" ] }")));
        Assert.Equal(CalendarCodes.UnknownField, Fixture.CodeOf(() => WithRoutine("{ \"hours\": [ 1 ] }")));
    }

    [Fact]
    public void EveryPlaceholderOfAFormatIsFilledFromTheMoment()
    {
        var messages = new JsonObject();
        foreach (var key in CalendarNames.RequiredKeys(Fixture.Gregorian)) messages[key] = Fixture.English.Messages[key].Text is { } text ? text : "x";
        messages["cal.format.short"] = "{day_of_year}|{month_number}|{hour}|{minute}|{season}|{holiday}|{era}|{year}|{day}|{daypart}";
        messages["cal.format.ordinal"] = "{hour_padded}h{minute_padded}";
        var table = LocaleTable.Load(new JsonObject { ["format"] = 1, ["locale"] = "en", ["messages"] = messages }.ToJsonString());
        var chain = LocaleChain.Create("en", [table], "en");
        var moment = Fixture.Gregorian.System.MomentOf(Fixture.At(Fixture.Gregorian, 2024, "december", 25, 7, 5));
        Assert.Equal("360|12|7|5|winter|Christmas|CE|2024|25|dawn", CalendarNames.Format(Fixture.Gregorian, chain, "short", moment));
        Assert.Equal("07h05", CalendarNames.Format(Fixture.Gregorian, chain, "ordinal", moment));
        var quiet = Fixture.Gregorian.System.MomentOf(Fixture.At(Fixture.Gregorian, 2024, "december", 26, 23, 59));
        Assert.Equal("361|12|23|59|winter||CE|2024|26|late night", CalendarNames.Format(Fixture.Gregorian, chain, "short", quiet));
    }
}
