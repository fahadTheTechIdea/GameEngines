using System.Text.Json.Nodes;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Invented calendars go through the same code: four months of 28 days with weeks that restart, a ten-day week with festival days outside the weeks and a leap day, and a day that is not 24 hours.</summary>
public sealed class FictionalCalendarTests
{
    private static CalendarSystem Valley => Fixture.Valley.System;
    private static CalendarSystem Realms => Fixture.Realms.System;

    [Fact]
    public void TheValleyYearIs112DaysAndEveryMonthStartsOnTheFirstWeekday()
    {
        Assert.Equal(112, Valley.CommonYearDays);
        Assert.Equal(0, Valley.LeapExtraDays);
        for (var month = 0; month < 4; month++)
        {
            var day = Valley.DayOf(Valley.DayNumber(new CalendarDate(1, month, 1)));
            Assert.Equal(0, day.Weekday);
            Assert.Equal(6, Valley.DayOf(Valley.DayNumber(new CalendarDate(1, month, 28))).Weekday);
        }
        Assert.Equal(new CalendarDate(2, 0, 1), Valley.DayOf(112).Date);
        Assert.Equal(new CalendarDate(0, 3, 28), Valley.DayOf(-1).Date);
    }

    [Fact]
    public void EveryDayOfTwentyValleyYearsRoundTrips()
    {
        for (var day = -112L * 10; day < 112L * 10; day++)
        {
            var info = Valley.DayOf(day);
            Assert.Equal(day, Valley.DayNumber(info.Date));
            Assert.Equal((info.Day - 1) % 7, info.Weekday);
        }
    }

    [Fact]
    public void TheRealmsYearIs365DaysAnd366WhenShieldmeetComes()
    {
        Assert.Equal(365, Realms.CommonYearDays);
        Assert.Equal(1, Realms.LeapExtraDays);
        for (long year = -50; year <= 450; year++)
            Assert.Equal(year % 4 == 0 ? 366 : 365, Realms.DaysInYear(year));
        var shieldmeet = Realms.Months.First(m => m.Id == "shieldmeet").Index;
        Assert.Equal(0, Realms.DaysInMonth(1, shieldmeet));
        Assert.Equal(1, Realms.DaysInMonth(4, shieldmeet));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => Realms.DayNumber(new CalendarDate(1, shieldmeet, 1))));
        _ = Realms.DayNumber(new CalendarDate(4, shieldmeet, 1));
    }

    [Fact]
    public void FestivalDaysBelongToNoWeekAndTendaysRestartEachMonth()
    {
        var hammer = Realms.Months.First(m => m.Id == "hammer").Index;
        var midwinter = Realms.Months.First(m => m.Id == "midwinter").Index;
        Assert.Equal(0, Realms.DayOf(Realms.DayNumber(new CalendarDate(1491, hammer, 1))).Weekday);
        Assert.Equal(9, Realms.DayOf(Realms.DayNumber(new CalendarDate(1491, hammer, 10))).Weekday);
        Assert.Equal(0, Realms.DayOf(Realms.DayNumber(new CalendarDate(1491, hammer, 11))).Weekday);
        Assert.Equal(9, Realms.DayOf(Realms.DayNumber(new CalendarDate(1491, hammer, 30))).Weekday);
        var festival = Realms.DayOf(Realms.DayNumber(new CalendarDate(1491, midwinter, 1)));
        Assert.Equal(-1, festival.Weekday);
        Assert.Equal(31, festival.DayOfYear);
        Assert.Equal(festival.DayNumber, Realms.DayNumber(new CalendarDate(1491, hammer, 30)) + 1);
    }

    [Fact]
    public void MonthArithmeticSkipsMonthsThatDoNotExistThatYear()
    {
        var midsummer = Realms.Months.First(m => m.Id == "midsummer").Index;
        var shieldmeet = Realms.Months.First(m => m.Id == "shieldmeet").Index;
        var eleasis = Realms.Months.First(m => m.Id == "eleasis").Index;
        Assert.Equal(new CalendarDate(1, eleasis, 1), Realms.AddMonths(new CalendarDate(1, midsummer, 1), 1));
        Assert.Equal(new CalendarDate(4, shieldmeet, 1), Realms.AddMonths(new CalendarDate(4, midsummer, 1), 1));
        Assert.Equal(new CalendarDate(1, midsummer, 1), Realms.AddMonths(new CalendarDate(1, eleasis, 1), -1));
        Assert.Equal(new CalendarDate(5, midsummer, 1), Realms.AddYears(new CalendarDate(4, shieldmeet, 1), 1));
        var hammer = Realms.Months.First(m => m.Id == "hammer").Index;
        var midwinter = Realms.Months.First(m => m.Id == "midwinter").Index;
        Assert.Equal(new CalendarDate(1491, midwinter, 1), Realms.AddMonths(new CalendarDate(1491, hammer, 30), 1));
    }

    [Fact]
    public void EachRealmsYearStartsOnTheDayAfterThePreviousOneEnds()
    {
        var day = Realms.DayNumber(new CalendarDate(-100, 0, 1));
        for (long year = -100; year < 300; year++)
        {
            Assert.Equal(day, Realms.DayNumber(new CalendarDate(year, 0, 1)));
            day += Realms.DaysInYear(year);
        }
    }

    [Fact]
    public void ADayThatIsNotTwentyFourHoursOfSixtyMinutesWorksTheSameWay()
    {
        var text = Fixture.Mutate(Fixture.ValleyText, root =>
        {
            root["time"] = new JsonObject { ["hoursPerDay"] = 30, ["minutesPerHour"] = 100, ["ticksPerMinute"] = 5 };
            root["schedules"] = new JsonArray();
            root["dayParts"]!.AsArray()[4]!["from"] = "27:50";
        });
        var set = CalendarLoader.Load(text);
        Assert.Equal(3000, set.Time.MinutesPerDay);
        Assert.Equal(15_000, set.Time.TicksPerDay);
        var instant = set.System.InstantOf(new CalendarDate(3, 2, 14), 29, 99, 4);
        var moment = set.System.MomentOf(instant);
        Assert.Equal((29, 99, 4, new CalendarDate(3, 2, 14)), (moment.Hour, moment.Minute, moment.TickOfMinute, moment.Day.Date));
        Assert.Equal(instant + 1, set.System.MomentOf(instant + 1).Instant);
        Assert.Equal(new CalendarDate(3, 2, 15), set.System.MomentOf(instant + 1).Day.Date);
        Assert.Equal("late", set.DayParts[set.DayPartOf(2790)].Id);
        Assert.Equal("day", set.DayParts[set.DayPartOf(2789 - 1500)].Id);
    }

    [Fact]
    public void ALeapRuleOfAnotherShapeIsHonouredAndItsPeriodIsBounded()
    {
        // Julian rule: every 4th year, no exception.
        var julian = CalendarLoader.Load(Fixture.MutateGregorian(root => { root["holidays"] = new JsonArray(); root["schedules"] = new JsonArray(); root["leap"] = new JsonArray(new JsonObject { ["every"] = 4, ["offset"] = 0, ["leap"] = true }); }));
        Assert.True(julian.System.IsLeapYear(1900));
        Assert.Equal(366, julian.System.DaysInYear(1900));
        // An offset moves the pattern; the last matching rule wins.
        var offset = CalendarLoader.Load(Fixture.MutateGregorian(root => { root["holidays"] = new JsonArray(); root["schedules"] = new JsonArray(); root["leap"] = new JsonArray(new JsonObject { ["every"] = 5, ["offset"] = 2, ["leap"] = true }, new JsonObject { ["every"] = 10, ["offset"] = 2, ["leap"] = false }); }));
        Assert.True(offset.System.IsLeapYear(7));
        Assert.False(offset.System.IsLeapYear(2));
        Assert.False(offset.System.IsLeapYear(12));
        Assert.True(offset.System.IsLeapYear(-3));
        var days = Enumerable.Range(-50, 200).Sum(y => offset.System.DaysInYear(y));
        Assert.Equal(200 * 365 + Enumerable.Range(-50, 200).Count(y => (y - 2) % 5 == 0 && (y - 2) % 10 != 0), days);
    }
}

/// <summary>A calendar with more than one leap day and several leap months, checked against a plain loop over years and months.</summary>
public sealed class SeveralLeapDaysTests
{
    private const string Document = """
    { "format": 1, "id": "odd", "time": { "hoursPerDay": 10, "minutesPerHour": 10, "ticksPerMinute": 3 }, "epoch": { "year": -7, "weekday": "w3" }, "weekMode": "continuous",
      "weekdays": [ { "id": "w1", "key": "k.w1" }, { "id": "w2", "key": "k.w2" }, { "id": "w3", "key": "k.w3" }, { "id": "w4", "key": "k.w4" }, { "id": "w5", "key": "k.w5" } ],
      "months": [ { "id": "a", "key": "k.a", "days": 10, "leapDays": 2, "intercalary": false }, { "id": "b", "key": "k.b", "days": 20, "leapDays": 0, "intercalary": false },
                  { "id": "c", "key": "k.c", "days": 0, "leapDays": 4, "intercalary": true }, { "id": "d", "key": "k.d", "days": 7, "leapDays": 1, "intercalary": false } ],
      "leap": [ { "every": 3, "offset": 1, "leap": true }, { "every": 9, "offset": 1, "leap": false }, { "every": 27, "offset": 1, "leap": true } ],
      "eras": [], "dayParts": [ { "id": "all", "key": "k.all", "from": "0:00" } ],
      "seasons": [ { "id": "s", "key": "k.s", "month": "a", "day": 1, "sunrise": "2:00", "sunset": "8:00" } ], "twilightMinutes": 0,
      "moons": [], "holidays": [], "formats": [], "locations": [], "activities": [], "interrupts": [], "facts": [], "schedules": [] }
    """;

    [Fact]
    public void EveryDayOfThreeHundredYearsMatchesALoop()
    {
        var set = CalendarLoader.Load(Document);
        var system = set.System;
        Assert.Equal(37 + 0, system.CommonYearDays);
        Assert.Equal(7, system.LeapExtraDays);
        var day = system.DayNumber(new CalendarDate(-7, 0, 1));
        Assert.Equal(0, day);
        var leapYears = 0;
        for (long year = -7; year < 293; year++)
        {
            var leap = (year - 1) % 3 == 0 && ((year - 1) % 9 != 0 || (year - 1) % 27 == 0);
            if (leap) leapYears++;
            Assert.Equal(leap, system.IsLeapYear(year));
            for (var month = 0; month < 4; month++)
            {
                var length = new[] { 10, 20, 0, 7 }[month] + (leap ? new[] { 2, 0, 4, 1 }[month] : 0);
                Assert.Equal(length, system.DaysInMonth(year, month));
                for (var d = 1; d <= length; d++)
                {
                    var info = system.DayOf(day);
                    Assert.Equal((year, month, d), (info.Year, info.Month, info.Day));
                    Assert.Equal(day, system.DayNumber(new CalendarDate(year, month, d)));
                    Assert.Equal((int)((day + 2) % 5), info.Weekday);
                    day++;
                }
            }
        }
        Assert.Equal(78, leapYears);
        var moment = system.MomentOf(system.InstantOf(system.DayNumber(new CalendarDate(100, 3, 5)), 77, 2));
        Assert.Equal((7, 7, 2), (moment.Hour, moment.Minute, moment.TickOfMinute));
    }
}
