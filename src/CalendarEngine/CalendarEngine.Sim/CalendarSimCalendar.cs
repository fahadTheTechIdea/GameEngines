using Beep.SimEngine;
using Beep.SimEngine.Content;
using CalendarEngine;
using EngineCommon;

namespace CalendarEngine.Sim;

/// <summary>
/// A calendar document as SimEngine's own <see cref="ICalendar"/>: it maps a simulation tick to a calendar instant and reads the date as named <see cref="CalendarField"/>s. SimEngine already has a time
/// notion (a tick is a rational number of seconds, <see cref="ISimulationTime"/>), and its calendar is an optional projection of ticks; this is a projection that uses the whole calendar document
/// (months, leap years, weeks, seasons, day parts) instead of a fixed cycle. It has no clock: the tick it is asked about is the only time there is.
/// </summary>
/// <remarks>
/// One simulation minute (<c>time.ExactTicks(GameDuration.Minutes(1))</c> ticks) is one calendar minute, which is <c>TicksPerMinute</c> calendar ticks; the instant of a tick is the start instant plus
/// <c>tick * TicksPerMinute / ticksPerSimMinute</c> (rounded down). <see cref="Validate"/> refuses a time whose minute does not divide, or is not divided by, the calendar minute, so no fraction of a tick drifts.
/// </remarks>
public sealed class CalendarSimCalendar : ICalendar
{
    private readonly CalendarSet _set;
    private readonly long _startInstant;

    /// <param name="startInstant">The calendar instant of simulation tick 0.</param>
    public CalendarSimCalendar(CalendarSet set, long startInstant)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
        _set.System.MomentOf(startInstant);
        _startInstant = startInstant;
    }

    public void Validate(ISimulationTime time)
    {
        var simMinute = SimTicksPerMinute(time);
        var calendarMinute = _set.Time.TicksPerMinute;
        if (simMinute % calendarMinute != 0 && calendarMinute % simMinute != 0)
            throw new ContentException("calendar '" + _set.Id + "': a simulation minute of " + simMinute + " ticks and a calendar minute of " + calendarMinute + " ticks do not divide one another");
    }

    private static long SimTicksPerMinute(ISimulationTime time)
    {
        ArgumentNullException.ThrowIfNull(time);
        long ticks;
        try { ticks = time.ExactTicks(GameDuration.Minutes(1)); }
        catch (ArgumentException exception) { throw new ContentException("a minute is not a whole number of simulation ticks: " + exception.Message); }
        return ticks >= 1 ? ticks : throw new ContentException("a minute is shorter than one simulation tick");
    }

    /// <summary>The calendar instant of a simulation tick.</summary>
    public long InstantOf(ISimulationTime time, long tick)
    {
        if (tick < 0) throw new ArgumentException("a tick cannot be negative", nameof(tick));
        try { return checked(_startInstant + tick * _set.Time.TicksPerMinute / SimTicksPerMinute(time)); }
        catch (OverflowException exception) { throw new ArgumentException("tick " + tick + " is beyond the supported years: " + exception.Message, nameof(tick)); }
    }

    public CalendarReading Project(ISimulationTime time, long tick)
    {
        var instant = InstantOf(time, tick);
        Moment moment;
        try { moment = _set.System.MomentOf(instant); }
        catch (EngineRefusedException refusal) { throw new ArgumentException(refusal.Code + ": " + refusal.Message, nameof(tick)); }
        var day = moment.Day;
        var system = _set.System;
        var fields = new List<CalendarField>
        {
            new("year", day.Year, 0),
            new("month", day.Month + 1, system.Months.Count),
            new("day", day.Day, day.DaysInMonth),
            new("day-of-year", day.DayOfYear, day.DaysInYear),
            new("weekday", day.Weekday + 1, system.Weekdays.Count),
            new("hour", moment.Hour, _set.Time.HoursPerDay),
            new("minute", moment.Minute, _set.Time.MinutesPerHour),
            new("season", _set.SeasonOf(day) + 1, _set.Seasons.Count),
            new("day-part", _set.DayPartOf(moment.MinuteOfDay) + 1, _set.DayParts.Count),
        };
        return new CalendarReading(_set.Id, tick, fields);
    }
}
