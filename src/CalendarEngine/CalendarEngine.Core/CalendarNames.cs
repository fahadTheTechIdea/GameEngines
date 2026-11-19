using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>
/// The words of a calendar. A document holds localisation keys only; the text comes from the locale tables and chains of the dialogue engine (CLDR plural and ordinal categories, fallback chains,
/// bidi isolates), so a game has one locale mechanism. A format is a locale message such as <c>{weekday}, {day} {month} {year}</c>; an ordinal day is an ordinal-select message on <c>day</c>.
/// </summary>
public static class CalendarNames
{
    /// <summary>The placeholders a date format message can use.</summary>
    public static readonly IReadOnlyList<string> FormatPlaceholders =
        ["day", "day_of_year", "month", "month_number", "year", "year_of_era", "era", "weekday", "hour", "minute", "hour_padded", "minute_padded", "daypart", "season", "holiday"];

    /// <summary>Every localisation key the document uses, each once, in ordinal order.</summary>
    public static IReadOnlyList<string> RequiredKeys(CalendarSet set)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var weekday in set.System.Weekdays) keys.Add(weekday.Key);
        foreach (var month in set.System.Months) keys.Add(month.Key);
        foreach (var era in set.System.Eras) keys.Add(era.Key);
        foreach (var part in set.DayParts) keys.Add(part.Key);
        foreach (var season in set.Seasons) keys.Add(season.Key);
        foreach (var moon in set.Moons) { keys.Add(moon.Key); foreach (var phase in moon.PhaseKeys) keys.Add(phase); }
        foreach (var holiday in set.Holidays) keys.Add(holiday.Key);
        foreach (var format in set.Formats) keys.Add(format.Key);
        foreach (var place in set.Locations) keys.Add(place.Key);
        foreach (var activity in set.Activities) keys.Add(activity.Key);
        return keys.ToList();
    }

    /// <summary>The keys that are date formats (and so may use placeholders); every other key is a name.</summary>
    public static IReadOnlySet<string> FormatKeys(CalendarSet set) => set.Formats.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

    private static string Name(LocaleChain chain, string key) => chain.Render(key, Array.Empty<MessageArg>()).Text;

    public static string Month(CalendarSet set, LocaleChain chain, int month) => Name(chain, set.System.Months[month].Key);
    public static string Weekday(CalendarSet set, LocaleChain chain, int weekday) => Name(chain, set.System.Weekdays[weekday].Key);
    public static string Season(CalendarSet set, LocaleChain chain, int season) => Name(chain, set.Seasons[season].Key);
    public static string DayPart(CalendarSet set, LocaleChain chain, int part) => Name(chain, set.DayParts[part].Key);
    public static string MoonPhase(CalendarSet set, LocaleChain chain, int moon, MoonPhase phase) => Name(chain, set.Moons[moon].PhaseKeys[(int)phase]);

    /// <summary>The date and time written in the named format, in the locale of the chain.</summary>
    public static string Format(CalendarSet set, LocaleChain chain, string format, in Moment moment)
    {
        var def = set.Formats.FirstOrDefault(f => f.Id == format);
        if (def is null) throw new EngineRefusedException(CalendarCodes.UnknownName, "there is no format '" + format + "'");
        var day = moment.Day;
        var season = set.SeasonOf(day);
        var era = set.System.EraOf(day.Year);
        var holidays = set.HolidaysOn(day);
        var args = new List<MessageArg>
        {
            new("day", FactValue.From((long)day.Day)),
            new("day_of_year", FactValue.From((long)day.DayOfYear)),
            new("month", FactValue.From(Month(set, chain, day.Month))),
            new("month_number", FactValue.From(day.Month + 1L)),
            new("year", FactValue.From(day.Year)),
            new("year_of_era", FactValue.From(era is null ? day.Year : era.Value.YearOfEra)),
            new("era", FactValue.From(era is null ? "" : Name(chain, era.Value.Era.Key))),
            new("weekday", FactValue.From(day.Weekday < 0 ? "" : Weekday(set, chain, day.Weekday))),
            new("hour", FactValue.From((long)moment.Hour)),
            new("minute", FactValue.From((long)moment.Minute)),
            new("hour_padded", FactValue.From(moment.Hour.ToString("00", CultureInfo.InvariantCulture))),
            new("minute_padded", FactValue.From(moment.Minute.ToString("00", CultureInfo.InvariantCulture))),
            new("daypart", FactValue.From(DayPart(set, chain, set.DayPartOf(moment.MinuteOfDay)))),
            new("season", FactValue.From(Season(set, chain, season))),
            new("holiday", FactValue.From(holidays.Count == 0 ? "" : Name(chain, set.Holidays[holidays[0]].Key))),
        };
        return chain.Render(def.Key, args).Text;
    }

    /// <summary>
    /// How much of the document a locale table covers: keys the document uses and the table lacks, messages the document does not use, and messages that use a placeholder their key does not give
    /// (names give none; formats give <see cref="FormatPlaceholders"/>).
    /// </summary>
    public static CoverageReport Coverage(CalendarSet set, LocaleTable table)
    {
        ArgumentNullException.ThrowIfNull(set);
        var formats = FormatKeys(set);
        var keys = RequiredKeys(set).ToDictionary(k => k, k => formats.Contains(k) ? FormatPlaceholders : (IReadOnlyList<string>)[], StringComparer.Ordinal);
        return CoverageRules.Report(keys, table, key => formats.Contains(key) ? "a date format does not give" : "a name does not give");
    }
}
