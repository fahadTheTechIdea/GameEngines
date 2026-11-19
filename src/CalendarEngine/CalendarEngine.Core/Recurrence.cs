using EngineCommon;

namespace CalendarEngine;

public enum RecurrenceFreq { Daily, Weekly, Monthly, Yearly }

/// <summary>A weekday, every time (ordinal 0) or the nth in the month or year (1 is the first, -1 the last).</summary>
public readonly record struct ByDay(int Weekday, int Ordinal);

/// <summary>
/// A recurrence rule over calendar days, the subset of RFC 5545 section 3.3.10 that a day-granular game calendar needs: FREQ (daily, weekly, monthly, yearly), INTERVAL, UNTIL, BYMONTH, BYMONTHDAY
/// (negative counts from the end), BYDAY with ordinals, and the DTSTART the interval counts from. COUNT and BYSETPOS are not supported (the document refuses them as unknown fields), because
/// counting occurrences needs enumeration from the start. BYSEASON is an extension for calendars that have seasons. Months, weekdays and years are the document's own, so the rule works in a fictional calendar.
/// The rule is a pure predicate on a day; it holds no state.
/// </summary>
public sealed class Recurrence
{
    private readonly CalendarSystem _system;

    private Recurrence(CalendarSystem system, RecurrenceFreq freq, int interval, CalendarDate start, CalendarDate? until, int[] byMonth, int[] byMonthDay, ByDay[] byDay, int[] bySeason, bool ordinalInYear)
    {
        _system = system;
        Freq = freq; Interval = interval; Start = start; Until = until;
        ByMonth = byMonth; ByMonthDay = byMonthDay; ByDay = byDay; BySeason = bySeason; OrdinalInYear = ordinalInYear;
        StartDay = system.DayNumber(start);
        UntilDay = until is null ? long.MaxValue : system.DayNumber(until.Value);
        StartWeek = system.WeekMode == WeekMode.Continuous ? LeapRules.FloorDiv(StartDay + system.EpochWeekday, system.Weekdays.Count) : 0;
    }

    public RecurrenceFreq Freq { get; }
    public int Interval { get; }
    public CalendarDate Start { get; }
    public CalendarDate? Until { get; }
    public int[] ByMonth { get; }
    public int[] ByMonthDay { get; }
    public ByDay[] ByDay { get; }
    public int[] BySeason { get; }
    public bool OrdinalInYear { get; }
    public long StartDay { get; }
    public long UntilDay { get; }
    private long StartWeek { get; }

    private static EngineRefusedException Refuse(string message) => new(CalendarCodes.Recurrence, message);

    /// <summary>Builds a rule, applying the RFC defaults (a monthly rule with no day repeats on the start day of month, and so on) and refusing combinations that mean nothing.</summary>
    public static Recurrence Create(CalendarSystem system, RecurrenceFreq freq, int interval, CalendarDate start, CalendarDate? until, int[] byMonth, int[] byMonthDay, ByDay[] byDay, int[] bySeason, int seasonCount)
    {
        if (interval < 1 || interval > 1000) throw Refuse("interval must be 1 to 1000, not " + interval);
        if (!system.IsValid(start)) throw Refuse("the start date is not a day of the calendar");
        if (until is { } end)
        {
            if (!system.IsValid(end)) throw Refuse("the until date is not a day of the calendar");
            if (system.DayNumber(end) < system.DayNumber(start)) throw Refuse("until is before the start");
        }
        foreach (var month in byMonth) if (month < 0 || month >= system.Months.Count) throw Refuse("bymonth names month " + month + " which the calendar does not have");
        foreach (var season in bySeason) if (season < 0 || season >= seasonCount) throw Refuse("byseason names a season the calendar does not have");
        var longest = system.Months.Max(m => m.Days + m.LeapDays);
        foreach (var day in byMonthDay)
            if (day == 0 || day < -longest || day > longest) throw Refuse("bymonthday " + day + " is 0 or beyond the longest month (" + longest + " days)");
        var weeks = system.Weekdays.Count;
        var ordinalInYear = false;
        foreach (var by in byDay)
        {
            if (by.Weekday < 0 || by.Weekday >= weeks) throw Refuse("byday names a weekday the calendar does not have");
            if (by.Ordinal == 0) continue;
            if (freq is RecurrenceFreq.Daily or RecurrenceFreq.Weekly) throw Refuse("an ordinal weekday (the 2nd Monday) needs a monthly or yearly rule");
            if (Math.Abs(by.Ordinal) > (freq == RecurrenceFreq.Monthly || byMonth.Length > 0 ? 6 : 60)) throw Refuse("ordinal " + by.Ordinal + " is beyond what a period can hold");
            ordinalInYear = freq == RecurrenceFreq.Yearly && byMonth.Length == 0;
            if (ordinalInYear && system.WeekMode == WeekMode.RestartEachMonth) throw Refuse("weeks restart each month, so an ordinal weekday in a whole year means nothing; give bymonth");
        }
        if (freq == RecurrenceFreq.Weekly && system.WeekMode == WeekMode.RestartEachMonth && interval != 1)
            throw Refuse("weeks restart each month, so a weekly rule cannot skip weeks (interval must be 1)");

        // RFC 5545 defaults: the part a frequency would otherwise leave open is taken from the start date.
        switch (freq)
        {
            case RecurrenceFreq.Weekly when byDay.Length == 0:
                var weekday = system.DayOf(system.DayNumber(start)).Weekday;
                if (weekday < 0) throw Refuse("a weekly rule with no byday repeats on the weekday of its start, and the start is on a day outside the weeks");
                byDay = [new ByDay(weekday, 0)];
                break;
            case RecurrenceFreq.Monthly when byMonthDay.Length == 0 && byDay.Length == 0:
                byMonthDay = [start.Day];
                break;
            case RecurrenceFreq.Yearly when byMonth.Length == 0 && byMonthDay.Length == 0 && byDay.Length == 0 && bySeason.Length == 0:
                byMonth = [start.Month];
                byMonthDay = [start.Day];
                break;
            case RecurrenceFreq.Yearly when byMonth.Length > 0 && byMonthDay.Length == 0 && byDay.Length == 0:
                byMonthDay = [start.Day];
                break;
            case RecurrenceFreq.Daily:
            case RecurrenceFreq.Weekly:
            case RecurrenceFreq.Monthly:
            case RecurrenceFreq.Yearly:
                break;
        }
        return new Recurrence(system, freq, interval, start, until, Sorted(byMonth), Sorted(byMonthDay), byDay.OrderBy(b => b.Weekday).ThenBy(b => b.Ordinal).ToArray(), Sorted(bySeason), ordinalInYear);
    }

    private static int[] Sorted(int[] values) => values.Distinct().OrderBy(v => v).ToArray();

    /// <summary>True when the day is one of the rule days. <paramref name="season"/> is the season index of the day (read only when the rule has a byseason part).</summary>
    public bool Matches(in DayInfo day, int season)
    {
        if (day.DayNumber < StartDay || day.DayNumber > UntilDay) return false;
        switch (Freq)
        {
            case RecurrenceFreq.Daily:
                if ((day.DayNumber - StartDay) % Interval != 0) return false;
                break;
            case RecurrenceFreq.Weekly:
                if (_system.WeekMode == WeekMode.Continuous && (LeapRules.FloorDiv(day.DayNumber + _system.EpochWeekday, _system.Weekdays.Count) - StartWeek) % Interval != 0) return false;
                break;
            case RecurrenceFreq.Monthly:
                if (((day.Year - Start.Year) * _system.Months.Count + day.Month - Start.Month) % Interval != 0) return false;
                break;
            case RecurrenceFreq.Yearly:
                if ((day.Year - Start.Year) % Interval != 0) return false;
                break;
        }
        if (ByMonth.Length > 0 && Array.IndexOf(ByMonth, day.Month) < 0) return false;
        if (BySeason.Length > 0 && Array.IndexOf(BySeason, season) < 0) return false;
        if (ByMonthDay.Length > 0)
        {
            var hit = false;
            foreach (var wanted in ByMonthDay)
                if (wanted > 0 ? day.Day == wanted : day.DaysInMonth + wanted + 1 == day.Day) { hit = true; break; }
            if (!hit) return false;
        }
        if (ByDay.Length > 0)
        {
            var hit = false;
            var weeks = _system.Weekdays.Count;
            foreach (var by in ByDay)
            {
                if (by.Weekday != day.Weekday) continue;
                if (by.Ordinal == 0) { hit = true; break; }
                var from = OrdinalInYear ? day.DayOfYear : day.Day;
                var length = OrdinalInYear ? day.DaysInYear : day.DaysInMonth;
                var nth = by.Ordinal > 0 ? (from - 1) / weeks + 1 : -((length - from) / weeks + 1);
                if (nth == by.Ordinal) { hit = true; break; }
            }
            if (!hit) return false;
        }
        return true;
    }
}
