using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>
/// What the calendar says about an instant, as facts in the shared fact model (boolean, integer, string) under the reserved prefix <c>cal.</c>. The same facts feed schedule selectors, dialogue
/// conditions (declare them as external facts) and AI sensors. They are a pure function of the document and the instant; they never read the world.
/// </summary>
public static class CalendarFacts
{
    public const string Prefix = "cal.";
    public const string MoonPrefix = "cal.moon.";

    /// <summary>The facts a document with this date system and these moons offers, each with its type and range.</summary>
    public static IReadOnlyDictionary<string, FactDefinition> Definitions(CalendarSystem system, IReadOnlyList<MoonDef> moons)
    {
        var time = system.Time;
        var longestMonth = system.Months.Max(m => m.Days + m.LeapDays);
        var longestYear = system.CommonYearDays + system.LeapExtraDays;
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal)
        {
            ["cal.year"] = new(FactKind.Integer, CalendarSystem.MinYear, CalendarSystem.MaxYear),
            ["cal.month_number"] = new(FactKind.Integer, 1, system.Months.Count),
            ["cal.day_of_month"] = new(FactKind.Integer, 1, longestMonth),
            ["cal.day_of_year"] = new(FactKind.Integer, 1, longestYear),
            ["cal.weekday_number"] = new(FactKind.Integer, 0, system.Weekdays.Count),
            ["cal.hour"] = new(FactKind.Integer, 0, time.HoursPerDay - 1),
            ["cal.minute"] = new(FactKind.Integer, 0, time.MinutesPerHour - 1),
            ["cal.minute_of_day"] = new(FactKind.Integer, 0, time.MinutesPerDay - 1),
            ["cal.light"] = new(FactKind.Integer, 0, 1000),
            ["cal.month"] = new(FactKind.String),
            ["cal.weekday"] = new(FactKind.String),
            ["cal.season"] = new(FactKind.String),
            ["cal.daypart"] = new(FactKind.String),
            ["cal.era"] = new(FactKind.String),
            ["cal.holiday_id"] = new(FactKind.String),
            ["cal.is_day"] = new(FactKind.Boolean),
            ["cal.is_holiday"] = new(FactKind.Boolean),
            ["cal.is_leap_year"] = new(FactKind.Boolean),
        };
        foreach (var moon in moons) facts[MoonPrefix + moon.Id] = new FactDefinition(FactKind.String);
        return facts;
    }

    /// <summary>The value of every fact at a moment. The day-level facts could be cached by the caller per day; this recomputes them, so it is correct for any moment.</summary>
    public static Dictionary<string, FactValue> Read(CalendarSet set, in Moment moment)
    {
        var day = moment.Day;
        var season = set.SeasonOf(day);
        var light = set.DaylightAt(season, moment.MinuteOfDay);
        var holidays = set.HolidaysOn(day);
        var era = set.System.EraOf(day.Year);
        var facts = new Dictionary<string, FactValue>(32, StringComparer.Ordinal)
        {
            ["cal.year"] = FactValue.From(day.Year),
            ["cal.month_number"] = FactValue.From(day.Month + 1L),
            ["cal.day_of_month"] = FactValue.From((long)day.Day),
            ["cal.day_of_year"] = FactValue.From((long)day.DayOfYear),
            ["cal.weekday_number"] = FactValue.From(day.Weekday + 1L),
            ["cal.hour"] = FactValue.From((long)moment.Hour),
            ["cal.minute"] = FactValue.From((long)moment.Minute),
            ["cal.minute_of_day"] = FactValue.From((long)moment.MinuteOfDay),
            ["cal.light"] = FactValue.From((long)light.LightPermille),
            ["cal.month"] = FactValue.From(set.System.Months[day.Month].Id),
            ["cal.weekday"] = FactValue.From(day.Weekday < 0 ? "" : set.System.Weekdays[day.Weekday].Id),
            ["cal.season"] = FactValue.From(set.Seasons[season].Id),
            ["cal.daypart"] = FactValue.From(set.DayParts[set.DayPartOf(moment.MinuteOfDay)].Id),
            ["cal.era"] = FactValue.From(era is null ? "" : era.Value.Era.Id),
            ["cal.holiday_id"] = FactValue.From(holidays.Count == 0 ? "" : set.Holidays[holidays[0]].Id),
            ["cal.is_day"] = FactValue.From(light.IsDay),
            ["cal.is_holiday"] = FactValue.From(holidays.Count > 0),
            ["cal.is_leap_year"] = FactValue.From(day.LeapYear),
        };
        foreach (var moon in set.Moons) facts[MoonPrefix + moon.Id] = FactValue.From(CalendarSet.PhaseId(set.MoonAt(moon.Index, moment.Instant).Phase));
        return facts;
    }
}
