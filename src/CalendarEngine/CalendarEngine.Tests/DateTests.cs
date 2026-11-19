using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Instants, dates and weekdays of the proleptic Gregorian sample, checked against .NET DateOnly (an independent proleptic Gregorian implementation) over every day of years 1 to 9999.</summary>
public sealed class DateTests
{
    private static CalendarSystem System => Fixture.Gregorian.System;

    [Fact]
    public void TheEpochIsThursdayThe1stOfJanuary1970()
    {
        var moment = System.MomentOf(0);
        Assert.Equal(new CalendarDate(1970, 0, 1), moment.Day.Date);
        Assert.Equal(3, moment.Day.Weekday);   // thursday
        Assert.Equal(0, moment.MinuteOfDay);
        Assert.Equal(1, moment.Day.DayOfYear);
    }

    [Fact]
    public void EveryDayOfYears1To9999AgreesWithDateOnly()
    {
        var first = DateOnly.MinValue;
        var epoch = new DateOnly(1970, 1, 1);
        var checkedDays = 0;
        for (var date = first; date <= DateOnly.MaxValue; date = date.AddDays(1))
        {
            var dayNumber = date.DayNumber - epoch.DayNumber;
            var info = System.DayOf(dayNumber);
            if (info.Year != date.Year || info.Month + 1 != date.Month || info.Day != date.Day || info.Weekday != ((int)date.DayOfWeek + 6) % 7 || info.DayOfYear != date.DayOfYear)
                Assert.Fail($"{date}: got {info}");
            if (System.DayNumber(info.Date) != dayNumber) Assert.Fail($"{date}: round trip gave {System.DayNumber(info.Date)}");
            checkedDays++;
            if (date == DateOnly.MaxValue) break;
        }
        Assert.Equal(3_652_059, checkedDays);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(1900, false)]
    [InlineData(2100, false)]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    [InlineData(0, true)]
    [InlineData(-4, true)]
    [InlineData(-100, false)]
    [InlineData(-400, true)]
    public void LeapYearsFollowTheGregorianRulesAlsoBeforeYear1(long year, bool leap)
    {
        Assert.Equal(leap, System.IsLeapYear(year));
        Assert.Equal(leap ? 366 : 365, System.DaysInYear(year));
    }

    [Fact]
    public void ADayThatIsNotInTheCalendarIsRefusedAndNotRolledOver()
    {
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.DayNumber(new CalendarDate(1900, 1, 29))));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.DayNumber(new CalendarDate(2001, 3, 31))));   // april 31
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.DayNumber(new CalendarDate(2001, 12, 1))));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.DayNumber(new CalendarDate(2001, 0, 0))));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.DayNumber(new CalendarDate(CalendarSystem.MaxYear + 1, 0, 1))));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.InstantOf(new CalendarDate(2001, 0, 1), 24, 0)));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.InstantOf(new CalendarDate(2001, 0, 1), 0, 60)));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.InstantOf(5, 1440)));
    }

    [Fact]
    public void TheTimeOfDayIsReadFromTheTicksOfTheDay()
    {
        var instant = Fixture.At(Fixture.Gregorian, 2024, "march", 10, 13, 45);
        var moment = System.MomentOf(instant);
        Assert.Equal((13, 45, 13 * 60 + 45), (moment.Hour, moment.Minute, moment.MinuteOfDay));
        Assert.Equal(instant, System.InstantOf(moment.Day.Date, 13, 45));
        Assert.Equal(instant + 7, System.InstantOf(moment.Day.DayNumber, moment.MinuteOfDay, 0) + 7);
    }

    [Fact]
    public void BeforeTheEpochCountsBackwardsAndMidnightBelongsToTheNewDay()
    {
        var moment = System.MomentOf(-1);
        Assert.Equal(new CalendarDate(1969, 11, 31), moment.Day.Date);
        Assert.Equal(1439, moment.MinuteOfDay);
        Assert.Equal(new CalendarDate(1970, 0, 1), System.MomentOf(0).Day.Date);
    }

    [Fact]
    public void TheFirstAndLastSupportedInstantsConvertAndOneBeyondIsRefused()
    {
        Assert.Equal(new CalendarDate(CalendarSystem.MinYear, 0, 1), System.MomentOf(System.MinInstant).Day.Date);
        var last = System.MomentOf(System.MaxInstant);
        Assert.Equal(new CalendarDate(CalendarSystem.MaxYear, 11, 31), last.Day.Date);
        Assert.Equal(1439, last.MinuteOfDay);
        Assert.Equal(CalendarCodes.InstantRange, Fixture.CodeOf(() => System.MomentOf(System.MaxInstant + 1)));
        Assert.Equal(CalendarCodes.InstantRange, Fixture.CodeOf(() => System.MomentOf(System.MinInstant - 1)));
    }

    [Fact]
    public void YearsBeforeYear1CountDownInTheBceEra()
    {
        Assert.Equal(("ce", 1L), (System.EraOf(1)!.Value.Era.Id, System.EraOf(1)!.Value.YearOfEra));
        Assert.Equal(("bce", 1L), (System.EraOf(0)!.Value.Era.Id, System.EraOf(0)!.Value.YearOfEra));
        Assert.Equal(("bce", 45L), (System.EraOf(-44)!.Value.Era.Id, System.EraOf(-44)!.Value.YearOfEra));
        Assert.Equal(2024, System.EraOf(2024)!.Value.YearOfEra);
    }

    [Fact]
    public void ADocumentWithoutErasHasNoEra()
    {
        Assert.Null(Fixture.Valley.System.EraOf(5));
    }

    [Fact]
    public void ArithmeticOnDaysMonthsAndYearsFollowsTheCalendar()
    {
        var jan31 = new CalendarDate(2023, 0, 31);
        Assert.Equal(new CalendarDate(2023, 1, 28), System.AddMonths(jan31, 1));
        Assert.Equal(new CalendarDate(2024, 1, 29), System.AddMonths(new CalendarDate(2024, 0, 31), 1));
        Assert.Equal(new CalendarDate(2024, 0, 31), System.AddMonths(new CalendarDate(2023, 11, 31), 1));
        Assert.Equal(new CalendarDate(2022, 11, 31), System.AddMonths(jan31, -1));
        Assert.Equal(new CalendarDate(2025, 1, 28), System.AddYears(new CalendarDate(2024, 1, 29), 1));
        Assert.Equal(new CalendarDate(2028, 1, 29), System.AddYears(new CalendarDate(2024, 1, 29), 4));
        Assert.Equal(new CalendarDate(2024, 2, 1), System.AddDays(new CalendarDate(2024, 1, 29), 1));
        Assert.Equal(new CalendarDate(2023, 2, 1), System.AddDays(new CalendarDate(2023, 1, 28), 1));
        Assert.Equal(366, System.DaysBetween(new CalendarDate(2024, 0, 1), new CalendarDate(2025, 0, 1)));
        Assert.Equal(-365, System.DaysBetween(new CalendarDate(2024, 0, 1), new CalendarDate(2023, 0, 1)));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => System.AddMonths(jan31, CalendarSystem.MaxMonthsStep + 1)));
        Assert.Equal(CalendarCodes.InstantRange, Fixture.CodeOf(() => System.AddYears(jan31, 100_000)));
        Assert.Equal(CalendarCodes.InstantRange, Fixture.CodeOf(() => System.AddDays(jan31, long.MaxValue)));
        Assert.Equal(CalendarCodes.DateInvalid, Fixture.CodeOf(() => System.AddMonths(new CalendarDate(2023, 1, 29), 1)));
    }

    [Fact]
    public void AddingMonthsOneAtATimeEqualsAddingThemAtOnceFromTheFirstOfTheMonth()
    {
        var date = new CalendarDate(1999, 0, 1);
        var stepped = date;
        for (var i = 1; i <= 500; i++)
        {
            stepped = System.AddMonths(stepped, 1);
            Assert.Equal(System.AddMonths(date, i), stepped);
        }
    }
}
