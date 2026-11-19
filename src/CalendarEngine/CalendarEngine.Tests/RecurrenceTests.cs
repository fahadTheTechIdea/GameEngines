using System.Text.Json.Nodes;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The RFC 5545 subset: each part of a rule, checked against an independent computation with DateOnly, and the rules the loader refuses.</summary>
public sealed class RecurrenceTests
{
    /// <summary>A Gregorian calendar whose holidays are exactly the given rules (named r0, r1, ...) and which has no schedules.</summary>
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

    private static string Rule(string body) => "{ \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 1 }, " + body + " }";

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
    public void AFirstMondayAndAFourthThursdayAreWhereDateOnlySaysTheyAre()
    {
        var set = Fixture.Gregorian;
        var labor = set.Holidays.First(h => h.Id == "labor-day").Index;
        var thanks = set.Holidays.First(h => h.Id == "thanksgiving").Index;
        for (var year = 1970; year <= 2100; year++)
        {
            var firstMonday = Enumerable.Range(1, 7).Select(d => new DateOnly(year, 9, d)).First(d => d.DayOfWeek == DayOfWeek.Monday);
            var fourthThursday = Enumerable.Range(1, 30).Select(d => new DateOnly(year, 11, d)).Where(d => d.DayOfWeek == DayOfWeek.Thursday).ElementAt(3);
            Assert.Equal([firstMonday], Days(set, labor, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31)));
            Assert.Equal([fourthThursday], Days(set, thanks, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31)));
        }
    }

    [Fact]
    public void DailyEveryThirdDayCountsFromTheStartAndNeverBeforeIt()
    {
        var set = WithRules(Rule("\"freq\": \"daily\", \"interval\": 3"));
        var days = Days(set, 0, new DateOnly(2023, 12, 25), new DateOnly(2024, 2, 10));
        Assert.Equal(Enumerable.Range(0, 14).Select(i => new DateOnly(2024, 1, 1).AddDays(3 * i)).ToList(), days);
    }

    [Fact]
    public void WeeklyOnSeveralWeekdaysEveryOtherWeek()
    {
        var set = WithRules(Rule("\"freq\": \"weekly\", \"interval\": 2, \"byday\": [ { \"weekday\": \"monday\" }, { \"weekday\": \"wednesday\" }, { \"weekday\": \"friday\" } ]"));
        var days = Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31));
        Assert.Equal([1, 3, 5, 15, 17, 19, 29, 31], days.Select(d => d.Day).ToArray());
    }

    [Fact]
    public void WeeklyWithoutADayRepeatsOnTheWeekdayOfTheStart()
    {
        var set = WithRules(Rule("\"freq\": \"weekly\""));
        Assert.All(Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 1)), d => Assert.Equal(DayOfWeek.Monday, d.DayOfWeek));
        Assert.Equal(22, Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 1)).Count);
    }

    [Fact]
    public void MonthlyOnTheLastDayAndOnTheThirtyFirstAndOnTheLastFriday()
    {
        var set = WithRules(Rule("\"freq\": \"monthly\", \"bymonthday\": [ -1 ]"), Rule("\"freq\": \"monthly\", \"bymonthday\": [ 31 ]"), Rule("\"freq\": \"monthly\", \"byday\": [ { \"weekday\": \"friday\", \"nth\": -1 } ]"));
        var year = (new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        Assert.Equal(Enumerable.Range(1, 12).Select(m => new DateOnly(2024, m, DateTime.DaysInMonth(2024, m))).ToList(), Days(set, 0, year.Item1, year.Item2));
        Assert.Equal([1, 3, 5, 7, 8, 10, 12], Days(set, 1, year.Item1, year.Item2).Select(d => d.Month).ToArray());
        var lastFridays = Enumerable.Range(1, 12).Select(m => Enumerable.Range(1, DateTime.DaysInMonth(2024, m)).Select(d => new DateOnly(2024, m, d)).Last(d => d.DayOfWeek == DayOfWeek.Friday)).ToList();
        Assert.Equal(lastFridays, Days(set, 2, year.Item1, year.Item2));
    }

    [Fact]
    public void YearlyOnTheLeapDayOnlyHappensInLeapYears()
    {
        var set = WithRules("{ \"freq\": \"yearly\", \"start\": { \"year\": 1996, \"month\": \"february\", \"day\": 29 } }");
        var days = Days(set, 0, new DateOnly(1996, 1, 1), new DateOnly(2008, 12, 31));
        Assert.Equal([1996, 2000, 2004, 2008], days.Select(d => d.Year).ToArray());
    }

    [Fact]
    public void YearlyEveryFourthYearAndUntilEndTheRule()
    {
        var set = WithRules("{ \"freq\": \"yearly\", \"interval\": 4, \"start\": { \"year\": 2000, \"month\": \"july\", \"day\": 4 } }",
            "{ \"freq\": \"yearly\", \"start\": { \"year\": 1990, \"month\": \"january\", \"day\": 1 }, \"until\": { \"year\": 2000, \"month\": \"january\", \"day\": 1 } }");
        Assert.Equal([2000, 2004, 2008], Days(set, 0, new DateOnly(1999, 1, 1), new DateOnly(2010, 12, 31)).Select(d => d.Year).ToArray());
        var days = Days(set, 1, new DateOnly(1985, 1, 1), new DateOnly(2010, 12, 31));
        Assert.Equal(Enumerable.Range(1990, 11).ToArray(), days.Select(d => d.Year).ToArray());   // until is inclusive
    }

    [Fact]
    public void AnOrdinalWeekdayInAWholeYearCountsFromTheStartOfTheYear()
    {
        var set = WithRules("{ \"freq\": \"yearly\", \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 1 }, \"byday\": [ { \"weekday\": \"monday\", \"nth\": 1 } ] }",
            "{ \"freq\": \"yearly\", \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 1 }, \"byday\": [ { \"weekday\": \"sunday\", \"nth\": -1 } ] }");
        Assert.Equal([new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 6), new DateOnly(2026, 1, 5)], Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal([new DateOnly(2024, 12, 29), new DateOnly(2025, 12, 28), new DateOnly(2026, 12, 27)], Days(set, 1, new DateOnly(2024, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void ASeasonIsALimitOnTheDaysOfTheRule()
    {
        var set = WithRules(Rule("\"freq\": \"daily\", \"byseason\": [ \"winter\" ]"));
        var days = Days(set, 0, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        Assert.Equal(31 + 29 + 31, days.Count);   // January, February (leap year) and December
        Assert.DoesNotContain(days, d => d.Month is >= 3 and <= 11);
    }

    [Fact]
    public void RulesInTheRestartWeekCalendarCountWeeksFromEachMonth()
    {
        var text = Fixture.Mutate(Fixture.ValleyText, root =>
        {
            root["holidays"] = new JsonArray(new JsonObject
            {
                ["id"] = "second-fri", ["key"] = "cal.holiday.luau", ["days"] = 1, ["tags"] = new JsonArray(),
                ["rule"] = JsonNode.Parse("{ \"freq\": \"monthly\", \"start\": { \"year\": 1, \"month\": \"spring\", \"day\": 1 }, \"byday\": [ { \"weekday\": \"fri\", \"nth\": 2 } ] }"),
            });
            root["schedules"] = new JsonArray();
        });
        var set = CalendarLoader.Load(text);
        var days = Enumerable.Range(0, 112).Select(d => set.System.DayOf(d)).Where(i => set.Holidays[0].Rule.Matches(i, set.SeasonOf(i))).ToList();
        Assert.Equal([12, 12, 12, 12], days.Select(d => d.Day).ToArray());   // friday is the 5th weekday: days 5 and 12 of a 28-day month, the second is day 12
        Assert.Equal([0, 1, 2, 3], days.Select(d => d.Month).ToArray());
    }

    [Fact]
    public void TheNextOccurrenceIsFoundWithinTheWindowAndNullBeyondIt()
    {
        var set = Fixture.Gregorian;
        var thanks = set.Holidays.First(h => h.Id == "thanksgiving").Index;
        var from = set.System.DayNumber(new CalendarDate(2024, 9, 2));
        var next = set.NextHoliday(thanks, from, 60);
        Assert.Equal(new CalendarDate(2024, 10, 28), set.System.DayOf(next!.Value).Date);
        Assert.Null(set.NextHoliday(thanks, from, 10));
        var newYear = set.Holidays.First(h => h.Id == "new-year").Index;
        Assert.Equal(new CalendarDate(2025, 0, 1), set.System.DayOf(set.NextHoliday(newYear, set.System.DayNumber(new CalendarDate(2024, 11, 26)), 10)!.Value).Date);
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => set.NextHoliday(thanks, from, 366 * 400 + 1)));
    }

    [Fact]
    public void AMultiDayHolidayIsOnForEachOfItsDays()
    {
        var set = Fixture.Gregorian;
        var harvest = set.Holidays.First(h => h.Id == "harvest-week").Index;
        foreach (var (day, on) in new[] { (30, false), (1, true), (2, true), (3, true), (4, false) })
        {
            var date = day == 30 ? new CalendarDate(2024, 8, 30) : new CalendarDate(2024, 9, day);
            Assert.Equal(on, set.HolidaysOn(set.System.DayOf(set.System.DayNumber(date))).Contains(harvest));
        }
        Assert.Equal([harvest], set.HolidaysStartingOn(set.System.DayOf(set.System.DayNumber(new CalendarDate(2024, 9, 1)))));
        Assert.Empty(set.HolidaysStartingOn(set.System.DayOf(set.System.DayNumber(new CalendarDate(2024, 9, 2)))));
    }

    [Theory]
    [InlineData("\"freq\": \"daily\", \"until\": { \"year\": 2023, \"month\": \"december\", \"day\": 31 }", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"daily\", \"byday\": [ { \"weekday\": \"monday\", \"nth\": 2 } ]", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"monthly\", \"byday\": [ { \"weekday\": \"monday\", \"nth\": 7 } ]", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"monthly\", \"bymonthday\": [ 0 ]", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"monthly\", \"bymonthday\": [ 32 ]", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"monthly\", \"byday\": [ { \"weekday\": \"someday\" } ]", CalendarCodes.Reference)]
    [InlineData("\"freq\": \"monthly\", \"bymonth\": [ \"smarch\" ]", CalendarCodes.Reference)]
    [InlineData("\"freq\": \"monthly\", \"byseason\": [ \"monsoon\" ]", CalendarCodes.Reference)]
    [InlineData("\"freq\": \"hourly\"", CalendarCodes.Recurrence)]
    [InlineData("\"freq\": \"daily\", \"interval\": 0", CalendarCodes.Range)]
    [InlineData("\"freq\": \"daily\", \"count\": 3", CalendarCodes.UnknownField)]
    [InlineData("\"freq\": \"daily\", \"bysetpos\": 1", CalendarCodes.UnknownField)]
    public void ARuleThatMeansNothingIsRefusedWithTheCodeOfItsMistake(string body, string code)
    {
        Assert.Equal(code, Fixture.CodeOf(() => WithRules(Rule(body))));
    }

    [Fact]
    public void RulesThatNeedWeeksThatRunOnAreRefusedWhereWeeksRestart()
    {
        string Valley(string rule) => Fixture.Mutate(Fixture.ValleyText, root =>
        {
            root["holidays"] = new JsonArray(new JsonObject { ["id"] = "h", ["key"] = "cal.holiday.luau", ["days"] = 1, ["tags"] = new JsonArray(), ["rule"] = JsonNode.Parse(rule) });
            root["schedules"] = new JsonArray();
        });
        const string start = "\"start\": { \"year\": 1, \"month\": \"spring\", \"day\": 1 }";
        Assert.Equal(CalendarCodes.Recurrence, Fixture.CodeOf(() => CalendarLoader.Load(Valley("{ \"freq\": \"weekly\", \"interval\": 2, " + start + " }"))));
        Assert.Equal(CalendarCodes.Recurrence, Fixture.CodeOf(() => CalendarLoader.Load(Valley("{ \"freq\": \"yearly\", " + start + ", \"byday\": [ { \"weekday\": \"fri\", \"nth\": 1 } ] }"))));
        _ = CalendarLoader.Load(Valley("{ \"freq\": \"yearly\", " + start + ", \"bymonth\": [ \"spring\" ], \"byday\": [ { \"weekday\": \"fri\", \"nth\": 1 } ] }"));
        // A weekly rule with no weekday repeats on the weekday of its start; a start outside the weeks has none.
        var realms = Fixture.Mutate(Fixture.RealmsText, root =>
        {
            root["holidays"] = new JsonArray(new JsonObject { ["id"] = "h", ["key"] = "cal.holiday.midsummer-night", ["days"] = 1, ["tags"] = new JsonArray(), ["rule"] = JsonNode.Parse("{ \"freq\": \"weekly\", \"start\": { \"year\": 1, \"month\": \"midwinter\", \"day\": 1 } }") });
            root["schedules"] = new JsonArray();
        });
        Assert.Equal(CalendarCodes.Recurrence, Fixture.CodeOf(() => CalendarLoader.Load(realms)));
    }
}
