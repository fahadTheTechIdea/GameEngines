using ScenarioEngine.Contracts;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Moon phases, daylight with twilight, and the facts the calendar offers other engines.</summary>
public sealed class SkyAndFactsTests
{
    [Fact]
    public void AMoonOfExactlyThirtyDaysShowsEachPhaseInItsOwnEighth()
    {
        var set = Fixture.Realms;
        var step = 5400L;   // 3.75 days of 1440 ticks
        for (var i = 0; i < 16; i++)
        {
            var reading = set.MoonAt(0, i * step);
            Assert.Equal((MoonPhase)(i % 8), reading.Phase);
            Assert.Equal(i % 8 * 125, reading.PositionPermille);
        }
        Assert.Equal(MoonPhase.Full, set.MoonAt(0, 15 * 1440).Phase);
        Assert.Equal(500, set.MoonAt(0, 15 * 1440).PositionPermille);
        // Before the new moon the cycle continues backwards.
        Assert.Equal(MoonPhase.Full, set.MoonAt(0, -15 * 1440).Phase);
        Assert.Equal(MoonPhase.WaningCrescent, set.MoonAt(0, -step).Phase);
        // The boundary is half way between two phase centres: 1/16 of the period.
        Assert.Equal(MoonPhase.New, set.MoonAt(0, 30 * 1440 / 16 - 1).Phase);
        Assert.Equal(MoonPhase.WaxingCrescent, set.MoonAt(0, 30 * 1440 / 16).Phase);
    }

    [Fact]
    public void TheMeanMoonOfTheGregorianSampleIsNearTheRealPhasesOfJanuary2000()
    {
        var set = Fixture.Gregorian;
        MoonPhase At(int day, int hour, int minute) => set.MoonAt(0, Fixture.At(set, 2000, "january", day, hour, minute)).Phase;
        Assert.Equal(MoonPhase.New, At(6, 18, 14));
        Assert.Equal(MoonPhase.FirstQuarter, At(14, 13, 34));
        Assert.Equal(MoonPhase.Full, At(21, 4, 40));
        Assert.Equal(MoonPhase.LastQuarter, At(28, 7, 57));
        Assert.Equal(MoonPhase.New, At(5, 12, 0));   // a day before the new moon: still the new-moon phase
        Assert.Equal(MoonPhase.New, At(7, 12, 0));
        Assert.Equal(MoonPhase.WaxingCrescent, At(10, 0, 0));
    }

    [Fact]
    public void PhaseNamesAreStableIds()
    {
        Assert.Equal(["new", "waxing-crescent", "first-quarter", "waxing-gibbous", "full", "waning-gibbous", "last-quarter", "waning-crescent"],
            Enum.GetValues<MoonPhase>().Select(CalendarSet.PhaseId).ToArray());
    }

    [Theory]
    [InlineData(5 * 60 + 29, false, 0)]
    [InlineData(5 * 60 + 30, false, 0)]
    [InlineData(5 * 60 + 45, false, 500)]
    [InlineData(6 * 60, true, 1000)]
    [InlineData(12 * 60, true, 1000)]
    [InlineData(18 * 60 + 29, true, 1000)]
    [InlineData(18 * 60 + 30, false, 1000)]
    [InlineData(18 * 60 + 45, false, 500)]
    [InlineData(19 * 60, false, 0)]
    [InlineData(23 * 60, false, 0)]
    public void SpringDaylightRampsOverTheTwilight(int minuteOfDay, bool isDay, int light)
    {
        var reading = Fixture.Gregorian.DaylightAt(0, minuteOfDay);
        Assert.Equal((isDay, light), (reading.IsDay, reading.LightPermille));
        Assert.Equal((6 * 60, 18 * 60 + 30), (reading.Sunrise, reading.Sunset));
    }

    [Fact]
    public void EachSeasonHasItsOwnSunriseAndSunset()
    {
        var set = Fixture.Gregorian;
        Assert.Equal(["spring:360:1110", "summer:300:1260", "autumn:390:1080", "winter:450:990"], set.Seasons.Select(s => s.Id + ":" + s.Sunrise + ":" + s.Sunset).ToArray());
        var winterDay = Fixture.At(set, 2024, "december", 20, 17, 0);
        Assert.False(CalendarFacts.Read(set, set.System.MomentOf(winterDay))["cal.is_day"].Boolean);
        var summerDay = Fixture.At(set, 2024, "july", 20, 17, 0);
        Assert.True(CalendarFacts.Read(set, set.System.MomentOf(summerDay))["cal.is_day"].Boolean);
    }

    [Fact]
    public void SeasonsWrapOverTheYearEnd()
    {
        var set = Fixture.Gregorian;
        string Season(long year, string month, int day) => set.Seasons[set.SeasonOf(set.System.DayOf(set.System.DayNumber(new CalendarDate(year, set.System.Months.First(m => m.Id == month).Index, day))))].Id;
        Assert.Equal("winter", Season(2024, "january", 1));
        Assert.Equal("winter", Season(2024, "february", 29));
        Assert.Equal("spring", Season(2024, "march", 1));
        Assert.Equal("spring", Season(2024, "may", 31));
        Assert.Equal("summer", Season(2024, "june", 1));
        Assert.Equal("autumn", Season(2024, "september", 1));
        Assert.Equal("winter", Season(2024, "december", 1));
    }

    [Fact]
    public void TheFactsOfAnInstantAreWhatTheCalendarSays()
    {
        var set = Fixture.Gregorian;
        var facts = CalendarFacts.Read(set, set.System.MomentOf(Fixture.At(set, 2024, "december", 25, 12, 5)));
        Assert.Equal("december", facts["cal.month"].Text);
        Assert.Equal(12, facts["cal.month_number"].Integer);
        Assert.Equal("wednesday", facts["cal.weekday"].Text);
        Assert.Equal(3, facts["cal.weekday_number"].Integer);
        Assert.Equal("winter", facts["cal.season"].Text);
        Assert.Equal("noon", facts["cal.daypart"].Text);
        Assert.Equal("ce", facts["cal.era"].Text);
        Assert.Equal("christmas", facts["cal.holiday_id"].Text);
        Assert.True(facts["cal.is_holiday"].Boolean);
        Assert.True(facts["cal.is_leap_year"].Boolean);
        Assert.Equal(360, facts["cal.day_of_year"].Integer);
        Assert.Equal((2024, 25, 12, 5, 725), (facts["cal.year"].Integer, facts["cal.day_of_month"].Integer, facts["cal.hour"].Integer, facts["cal.minute"].Integer, facts["cal.minute_of_day"].Integer));
        Assert.Equal(1000, facts["cal.light"].Integer);   // winter sunrise 7:30, sunset 16:30: noon is bright
        Assert.Equal(1000, CalendarFacts.Read(set, set.System.MomentOf(Fixture.At(set, 2024, "july", 1, 12, 0)))["cal.light"].Integer);
        Assert.Contains(facts["cal.moon.moon"].Text, Enum.GetValues<MoonPhase>().Select(CalendarSet.PhaseId));
        Assert.False(CalendarFacts.Read(set, set.System.MomentOf(Fixture.At(set, 2024, "december", 26)))["cal.is_holiday"].Boolean);
        Assert.Equal("", CalendarFacts.Read(set, set.System.MomentOf(Fixture.At(set, 2024, "december", 26)))["cal.holiday_id"].Text);
    }

    [Fact]
    public void AFestivalDayHasNoWeekdayFact()
    {
        var set = Fixture.Realms;
        var midwinter = set.System.Months.First(m => m.Id == "midwinter").Index;
        var facts = CalendarFacts.Read(set, set.System.MomentOf(set.System.InstantOf(new CalendarDate(1491, midwinter, 1), 12, 0)));
        Assert.Equal("", facts["cal.weekday"].Text);
        Assert.Equal(0, facts["cal.weekday_number"].Integer);
        Assert.Equal("midwinter-feast", facts["cal.holiday_id"].Text);
    }

    [Fact]
    public void EveryFactIsDeclaredWithItsTypeAndRangeAndNeverLeavesIt()
    {
        foreach (var set in new[] { Fixture.Gregorian, Fixture.Valley, Fixture.Realms })
        {
            var definitions = CalendarFacts.Definitions(set.System, set.Moons);
            for (var i = 0; i < 4000; i++)
            {
                var instant = set.System.MinInstant + (long)(EngineCommon.DeterministicRoll.Next(7, set.Id, i) % (ulong)(set.System.MaxInstant - set.System.MinInstant));
                var facts = CalendarFacts.Read(set, set.System.MomentOf(instant));
                Assert.Equal(definitions.Keys.OrderBy(k => k, StringComparer.Ordinal), facts.Keys.OrderBy(k => k, StringComparer.Ordinal));
                foreach (var (id, value) in facts)
                {
                    var definition = definitions[id];
                    Assert.Equal(definition.Kind, value.Kind);
                    if (definition.Kind == FactKind.Integer) Assert.InRange(value.Integer, definition.Minimum!.Value, definition.Maximum!.Value);
                }
            }
        }
    }

    [Fact]
    public void TheFactsOfTheSameInstantAreTheSameWhateverIsAskedBefore()
    {
        var set = Fixture.Gregorian;
        var instant = Fixture.At(set, 2031, "september", 1, 9, 30);
        var first = CalendarFacts.Read(set, set.System.MomentOf(instant));
        _ = CalendarFacts.Read(set, set.System.MomentOf(instant + 12345));
        var second = CalendarFacts.Read(set, set.System.MomentOf(instant));
        Assert.Equal(first.OrderBy(f => f.Key).Select(f => f.Key + "=" + f.Value), second.OrderBy(f => f.Key).Select(f => f.Key + "=" + f.Value));
        Assert.Equal("labor-day", first["cal.holiday_id"].Text);   // 2031-09-01 is the first Monday of September
    }
}
