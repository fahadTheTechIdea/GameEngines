using System.Text.Json.Nodes;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Boundaries found by mutation testing: each of these failed to fail when a comparison in the engine was changed by one.</summary>
public sealed class EdgeTests
{
    private static CalendarSet WithRules(params string[] rules)
    {
        var text = Fixture.MutateGregorian(root =>
        {
            var holidays = new JsonArray();
            for (var i = 0; i < rules.Length; i++)
                holidays.Add(new JsonObject { ["id"] = "r" + i, ["key"] = "cal.holiday.new-year", ["days"] = 1, ["tags"] = new JsonArray(), ["rule"] = JsonNode.Parse(rules[i]) });
            root["holidays"] = holidays;
            root["schedules"] = new JsonArray();
        });
        return CalendarLoader.Load(text);
    }

    private static List<DateOnly> Days(CalendarSet set, int holiday, DateOnly from, DateOnly to)
    {
        var found = new List<DateOnly>();
        var epoch = new DateOnly(1970, 1, 1);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var info = set.System.DayOf(date.DayNumber - epoch.DayNumber);
            if (set.Holidays[holiday].Rule.Matches(info, set.SeasonOf(info))) found.Add(date);
        }
        return found;
    }

    [Fact]
    public void WeeklyEveryThirdWeekCountsFromTheWeekOfTheStart()
    {
        // The start is a Wednesday in the middle of its week: the weeks count from the week it is in.
        var set = WithRules("{ \"freq\": \"weekly\", \"interval\": 3, \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 3 }, \"byday\": [ { \"weekday\": \"wednesday\" }, { \"weekday\": \"monday\" } ] }");
        var days = Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 15));
        Assert.Equal([new DateOnly(2024, 1, 3), new DateOnly(2024, 1, 22), new DateOnly(2024, 1, 24), new DateOnly(2024, 2, 12), new DateOnly(2024, 2, 14), new DateOnly(2024, 3, 4), new DateOnly(2024, 3, 6)], days);
    }

    [Fact]
    public void MonthlyEveryOtherMonthAndWithTheDayOfTheStartAsDefault()
    {
        var set = WithRules("{ \"freq\": \"monthly\", \"interval\": 2, \"start\": { \"year\": 2024, \"month\": \"february\", \"day\": 15 } }",
            "{ \"freq\": \"monthly\", \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 17 } }");
        Assert.Equal([new DateOnly(2024, 2, 15), new DateOnly(2024, 4, 15), new DateOnly(2024, 6, 15), new DateOnly(2024, 8, 15)], Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 8, 31)));
        Assert.Equal(Enumerable.Range(1, 6).Select(m => new DateOnly(2024, m, 17)).ToList(), Days(set, 1, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30)));
        Assert.Equal([new DateOnly(2025, 2, 15)], Days(set, 0, new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1)));   // 12 months on: February again
        var third = WithRules("{ \"freq\": \"monthly\", \"interval\": 3, \"start\": { \"year\": 2024, \"month\": \"february\", \"day\": 15 } }");
        Assert.Equal([new DateOnly(2024, 2, 15), new DateOnly(2024, 5, 15), new DateOnly(2024, 8, 15), new DateOnly(2024, 11, 15), new DateOnly(2025, 2, 15)], Days(third, 0, new DateOnly(2024, 1, 1), new DateOnly(2025, 3, 1)));
    }

    [Fact]
    public void ANextOccurrenceExactlyAtTheEndOfTheWindowIsFound()
    {
        var set = Fixture.Gregorian;
        var thanks = set.Holidays.First(h => h.Id == "thanksgiving").Index;
        var from = set.System.DayNumber(new CalendarDate(2024, 10, 27));
        Assert.Equal(from + 1, set.NextHoliday(thanks, from, 1));
        Assert.Null(set.NextHoliday(thanks, from, 0));
        Assert.Equal(from + 1, set.NextHoliday(thanks, from + 1, 0));
        Assert.Equal(from + 1, set.NextHoliday(thanks, from - 5, 6));
    }

    [Fact]
    public void AnInterruptIsOverTheMomentItEndsEvenBeforeAnyoneRefreshes()
    {
        var set = Fixture.Gregorian;
        var runtime = CalendarRuntime.Create(set, Fixture.At(set, 2024, "march", 6, 10), 1, 1);
        runtime.AddActor("anna", "shopkeeper");
        runtime.Interrupt("anna", "alarm", 30);
        runtime.SkipTo(runtime.Instant + 29);
        Assert.Equal("guardhouse", runtime.WhereIs(runtime.Context(Fixture.Dry), "anna").Location);
        runtime.SkipTo(runtime.Instant + 1);
        Assert.Equal("shop", runtime.WhereIs(runtime.Context(Fixture.Dry), "anna").Location);
    }

    [Fact]
    public void AnInterruptOfTheSamePriorityAsTheRoutineWinsAndOneJustBelowLoses()
    {
        // The invitation has priority 12: put the Sunday routine at 12 (a tie, the interrupt wins) and at 13 (the routine wins).
        string Sunday(int priority) => Fixture.MutateGregorian(r => r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[2]!["priority"] = priority);
        var tie = CalendarLoader.Load(Sunday(12));
        var above = CalendarLoader.Load(Sunday(13));
        var sunday = Fixture.At(tie, 2024, "march", 10, 9);
        Assert.Equal(AnswerSource.Interrupt, CalendarContext.At(tie, sunday, Fixture.Dry).WhereIs("shopkeeper", "invitation").Source);
        Assert.Equal(AnswerSource.Routine, CalendarContext.At(above, sunday, Fixture.Dry).WhereIs("shopkeeper", "invitation").Source);
    }

    [Fact]
    public void AMinuteBeyondTheLastMinuteOfTheHourIsAClockRefusal()
    {
        Assert.Equal(CalendarCodes.Clock, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => r["dayParts"]!.AsArray()[1]!["from"] = "5:60"))));
        _ = CalendarLoader.Load(Fixture.MutateGregorian(r => r["dayParts"]!.AsArray()[1]!["from"] = "5:59"));
    }

    [Fact]
    public void TwoSeasonsOnTheSameDayAndABlockOfNoLengthAreRefused()
    {
        Assert.Equal(CalendarCodes.Order, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => { r["seasons"]!.AsArray()[1]!["month"] = "march"; r["seasons"]!.AsArray()[1]!["day"] = 1; }))));
        Assert.Equal(CalendarCodes.Order, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => { var b = r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[1]!["blocks"]!.AsArray()[0]!; b["from"] = "9:00"; b["to"] = "9:00"; }))));
        Assert.Equal(CalendarCodes.Order, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => { var b = r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[1]!["blocks"]!.AsArray()[0]!; b["from"] = "10:00"; b["to"] = "9:00"; }))));
    }
}
