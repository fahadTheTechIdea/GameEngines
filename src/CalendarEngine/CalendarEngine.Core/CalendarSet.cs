using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

public enum MoonPhase { New, WaxingCrescent, FirstQuarter, WaxingGibbous, Full, WaningGibbous, LastQuarter, WaningCrescent }

/// <summary>A moon at an instant: its phase (eight, new moon first) and how far round its period it is, in thousandths.</summary>
public readonly record struct MoonReading(MoonPhase Phase, int PositionPermille);

/// <summary>Daylight at a moment: whether the sun is up, and a light level 0 to 1000 that ramps over the twilight at dawn and dusk.</summary>
public readonly record struct DaylightReading(bool IsDay, int LightPermille, int Sunrise, int Sunset);

/// <summary>
/// A loaded calendar document: the date system, the parts of the day, seasons with their daylight, moons, holidays, the places and activities, the interrupts and the schedules, plus the digest
/// of the exact text it was read from. It is immutable and every query is a pure function of it and of the instant asked about.
/// </summary>
public sealed class CalendarSet
{

    private readonly int[] _partByMinute;

    public CalendarSet(string id, string digest, CalendarSystem system, IReadOnlyList<DayPartDef> dayParts, IReadOnlyList<SeasonDef> seasons, int twilightMinutes, IReadOnlyList<MoonDef> moons,
        IReadOnlyList<HolidayDef> holidays, IReadOnlyList<FormatDef> formats, IReadOnlyList<PlaceDef> locations, IReadOnlyList<PlaceDef> activities, IReadOnlyList<InterruptDef> interrupts,
        IReadOnlyDictionary<string, FactDefinition> hostFacts, IReadOnlyList<ScheduleDef> schedules)
    {
        Id = id; Digest = digest; System = system; DayParts = dayParts; Seasons = seasons; TwilightMinutes = twilightMinutes; Moons = moons; Holidays = holidays; Formats = formats;
        Locations = locations; Activities = activities; Interrupts = interrupts; HostFacts = hostFacts; Schedules = schedules;
        _partByMinute = new int[system.Time.MinutesPerDay];
        var part = 0;
        for (var minute = 0; minute < _partByMinute.Length; minute++)
        {
            while (part + 1 < dayParts.Count && dayParts[part + 1].FromMinute <= minute) part++;
            _partByMinute[minute] = part;
        }
        ScheduleById = schedules.ToDictionary(s => s.Id, StringComparer.Ordinal);
        InterruptById = interrupts.ToDictionary(i => i.Id, StringComparer.Ordinal);
    }

    public string Id { get; }
    public string Digest { get; }
    public CalendarSystem System { get; }
    public TimeDef Time => System.Time;
    public IReadOnlyList<DayPartDef> DayParts { get; }
    public IReadOnlyList<SeasonDef> Seasons { get; }
    public int TwilightMinutes { get; }
    public IReadOnlyList<MoonDef> Moons { get; }
    public IReadOnlyList<HolidayDef> Holidays { get; }
    public IReadOnlyList<FormatDef> Formats { get; }
    public IReadOnlyList<PlaceDef> Locations { get; }
    public IReadOnlyList<PlaceDef> Activities { get; }
    public IReadOnlyList<InterruptDef> Interrupts { get; }

    /// <summary>The facts the document declares for the host to supply (weather, a story flag); the calendar own <c>cal.</c> facts are in <see cref="CalendarFacts"/>.</summary>
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }

    public IReadOnlyList<ScheduleDef> Schedules { get; }
    public IReadOnlyDictionary<string, ScheduleDef> ScheduleById { get; }
    public IReadOnlyDictionary<string, InterruptDef> InterruptById { get; }

    public int DayPartOf(int minuteOfDay) => _partByMinute[minuteOfDay];

    /// <summary>The season a day is in: the last one that has started by its month and day, or the last of the list before the first one starts (the year wraps).</summary>
    public int SeasonOf(in DayInfo day)
    {
        var found = Seasons.Count - 1;
        for (var i = 0; i < Seasons.Count; i++)
            if (Seasons[i].Month < day.Month || Seasons[i].Month == day.Month && Seasons[i].Day <= day.Day) found = i;
            else break;
        return found;
    }

    // ---- daylight --------------------------------------------------------------------------------------------------------------------

    public DaylightReading DaylightAt(int season, int minuteOfDay)
    {
        var def = Seasons[season];
        var isDay = minuteOfDay >= def.Sunrise && minuteOfDay < def.Sunset;
        int light;
        if (isDay) light = 1000;
        else if (TwilightMinutes > 0 && minuteOfDay >= def.Sunrise - TwilightMinutes && minuteOfDay < def.Sunrise) light = 1000 * (minuteOfDay - (def.Sunrise - TwilightMinutes)) / TwilightMinutes;
        else if (TwilightMinutes > 0 && minuteOfDay >= def.Sunset && minuteOfDay < def.Sunset + TwilightMinutes) light = 1000 * (def.Sunset + TwilightMinutes - minuteOfDay) / TwilightMinutes;
        else light = 0;
        return new DaylightReading(isDay, light, def.Sunrise, def.Sunset);
    }

    // ---- moons -----------------------------------------------------------------------------------------------------------------------

    public MoonReading MoonAt(int moon, long instant)
    {
        var def = Moons[moon];
        var ticksPerDay = (Int128)Time.TicksPerDay;
        // The position round the period, as the exact fraction r / b: (instant / ticksPerDay - new moon) / period.
        var a = ((Int128)instant * def.NewDenominator - (Int128)def.NewNumerator * ticksPerDay) * def.PeriodDenominator;
        var b = ticksPerDay * def.NewDenominator * def.PeriodNumerator;
        var r = ((a % b) + b) % b;
        var phase = (int)(((16 * r + b) / (2 * b)) % 8);
        return new MoonReading((MoonPhase)phase, (int)(r * 1000 / b));
    }

    public static string PhaseId(MoonPhase phase)
    {
        switch (phase)
        {
            case MoonPhase.New: return "new";
            case MoonPhase.WaxingCrescent: return "waxing-crescent";
            case MoonPhase.FirstQuarter: return "first-quarter";
            case MoonPhase.WaxingGibbous: return "waxing-gibbous";
            case MoonPhase.Full: return "full";
            case MoonPhase.WaningGibbous: return "waning-gibbous";
            case MoonPhase.LastQuarter: return "last-quarter";
            case MoonPhase.WaningCrescent: return "waning-crescent";
        }
        throw new InvalidOperationException("unknown moon phase " + phase);
    }

    // ---- holidays --------------------------------------------------------------------------------------------------------------------

    /// <summary>The holidays (by index, in document order) that start on this day.</summary>
    public IReadOnlyList<int> HolidaysStartingOn(in DayInfo day)
    {
        if (Holidays.Count == 0) return Array.Empty<int>();
        var season = SeasonOf(day);
        List<int>? found = null;
        for (var i = 0; i < Holidays.Count; i++)
            if (Holidays[i].Rule.Matches(day, season)) (found ??= new List<int>()).Add(i);
        return found ?? (IReadOnlyList<int>)Array.Empty<int>();
    }

    /// <summary>The holidays (by index) that are on during this day, including those that started on an earlier day and last several.</summary>
    public IReadOnlyList<int> HolidaysOn(in DayInfo day)
    {
        if (Holidays.Count == 0) return Array.Empty<int>();
        var season = SeasonOf(day);
        List<int>? found = null;
        for (var i = 0; i < Holidays.Count; i++)
        {
            var holiday = Holidays[i];
            var on = holiday.Rule.Matches(day, season);
            for (var back = 1; !on && back < holiday.Days; back++)
            {
                var earlier = day.DayNumber - back;
                if (earlier < System.MinDay) break;
                var info = System.DayOf(earlier);
                on = holiday.Rule.Matches(info, SeasonOf(info));
            }
            if (on) (found ??= new List<int>()).Add(i);
        }
        return found ?? (IReadOnlyList<int>)Array.Empty<int>();
    }

    /// <summary>The first day of the rule at or after <paramref name="fromDay"/>, looking at most <paramref name="withinDays"/> days ahead; null when there is none in that window.</summary>
    public long? NextOccurrence(Recurrence rule, long fromDay, int withinDays)
    {
        if (withinDays < 0 || withinDays > 366 * 400) throw new EngineRefusedException(CalendarCodes.Range, "a search window is 0 to " + 366 * 400 + " days");
        var last = Math.Min(System.MaxDay, fromDay + withinDays);
        for (var day = Math.Max(fromDay, Math.Max(rule.StartDay, System.MinDay)); day <= last && day <= rule.UntilDay; day++)
        {
            var info = System.DayOf(day);
            if (rule.Matches(info, SeasonOf(info))) return day;
        }
        return null;
    }

    public long? NextHoliday(int holiday, long fromDay, int withinDays) => NextOccurrence(Holidays[holiday].Rule, fromDay, withinDays);
}
