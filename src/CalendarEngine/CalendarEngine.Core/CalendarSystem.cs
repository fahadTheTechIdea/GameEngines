using EngineCommon;

namespace CalendarEngine;

/// <summary>A date as year (astronomical, 0 is 1 BCE), month (0-based index into the document months) and day (1-based).</summary>
public readonly record struct CalendarDate(long Year, int Month, int Day);

/// <summary>Everything about one day that does not depend on the time of day.</summary>
public readonly record struct DayInfo(long DayNumber, long Year, int Month, int Day, int DayOfYear, int Weekday, bool LeapYear, int DaysInMonth, int DaysInYear)
{
    public CalendarDate Date => new(Year, Month, Day);
}

/// <summary>An instant read as a day and a time of day.</summary>
public readonly record struct Moment(long Instant, DayInfo Day, int MinuteOfDay, int Hour, int Minute, int TickOfMinute);

/// <summary>
/// The date arithmetic of one calendar: a count of ticks since the start of the epoch year is a year, a month, a day, a weekday and a time of day, and back. It uses whole numbers only
/// (floor division, no floating point, no wall clock, no DateTime) and is a pure function of the document: the same ticks always give the same date. The proleptic Gregorian
/// calendar, the Julian calendar and invented calendars with their own months, week lengths, day lengths and leap rules all go through the same code.
/// </summary>
public sealed class CalendarSystem
{
    public const long MinYear = -100_000;
    public const long MaxYear = 100_000;

    private readonly int[] _commonCumulative;
    private readonly int[] _leapCumulative;

    public CalendarSystem(TimeDef time, long epochYear, int epochWeekday, WeekMode weekMode, IReadOnlyList<WeekdayDef> weekdays, IReadOnlyList<MonthDef> months, LeapRules leap, IReadOnlyList<EraDef> eras)
    {
        Time = time; EpochYear = epochYear; EpochWeekday = epochWeekday; WeekMode = weekMode; Weekdays = weekdays; Months = months; Leap = leap; Eras = eras;
        _commonCumulative = new int[months.Count + 1];
        _leapCumulative = new int[months.Count + 1];
        for (var m = 0; m < months.Count; m++)
        {
            _commonCumulative[m + 1] = _commonCumulative[m] + months[m].Days;
            _leapCumulative[m + 1] = _leapCumulative[m] + months[m].Days + months[m].LeapDays;
        }
        CommonYearDays = _commonCumulative[months.Count];
        LeapExtraDays = _leapCumulative[months.Count] - CommonYearDays;
        MinDay = YearStart(MinYear);
        MaxDay = YearStart(MaxYear + 1) - 1;
        MinInstant = checked(MinDay * time.TicksPerDay);
        MaxInstant = checked((MaxDay + 1) * time.TicksPerDay - 1);
    }

    public TimeDef Time { get; }
    public long EpochYear { get; }
    public int EpochWeekday { get; }
    public WeekMode WeekMode { get; }
    public IReadOnlyList<WeekdayDef> Weekdays { get; }
    public IReadOnlyList<MonthDef> Months { get; }
    public LeapRules Leap { get; }
    public IReadOnlyList<EraDef> Eras { get; }
    public int CommonYearDays { get; }
    public int LeapExtraDays { get; }
    public long MinDay { get; }
    public long MaxDay { get; }
    public long MinInstant { get; }
    public long MaxInstant { get; }

    // ---- years -----------------------------------------------------------------------------------------------------------------------

    public bool IsLeapYear(long year) => Leap.IsLeap(year);

    public int DaysInYear(long year) => CommonYearDays + (IsLeapYear(year) ? LeapExtraDays : 0);

    public int DaysInMonth(long year, int month) => Months[month].Days + (IsLeapYear(year) ? Months[month].LeapDays : 0);

    /// <summary>The day number (days since the first day of the epoch year) of the first day of a year.</summary>
    private long YearStart(long year) => (year - EpochYear) * CommonYearDays + (Leap.LeapsBefore(year) - Leap.LeapsBefore(EpochYear)) * LeapExtraDays;

    private static EngineRefusedException Refuse(string code, string message) => new(code, message);

    // ---- date <-> day number ---------------------------------------------------------------------------------------------------------

    public bool IsValid(CalendarDate date) =>
        date.Year >= MinYear && date.Year <= MaxYear && date.Month >= 0 && date.Month < Months.Count && date.Day >= 1 && date.Day <= DaysInMonth(date.Year, date.Month);

    public long DayNumber(CalendarDate date)
    {
        if (date.Year < MinYear || date.Year > MaxYear) throw Refuse(CalendarCodes.DateInvalid, "year " + date.Year + " is outside " + MinYear + " to " + MaxYear);
        if (date.Month < 0 || date.Month >= Months.Count) throw Refuse(CalendarCodes.DateInvalid, "month " + date.Month + " is not one of the " + Months.Count + " months");
        var length = DaysInMonth(date.Year, date.Month);
        if (date.Day < 1 || date.Day > length) throw Refuse(CalendarCodes.DateInvalid, "day " + date.Day + " is not in " + Months[date.Month].Id + " of year " + date.Year + " (" + length + " days)");
        var cumulative = IsLeapYear(date.Year) ? _leapCumulative : _commonCumulative;
        return YearStart(date.Year) + cumulative[date.Month] + date.Day - 1;
    }

    public DayInfo DayOf(long dayNumber)
    {
        if (dayNumber < MinDay || dayNumber > MaxDay) throw Refuse(CalendarCodes.InstantRange, "day " + dayNumber + " is outside " + MinDay + " to " + MaxDay);
        // The mean year is (CommonYearDays * period + leap years * extra days) / period days long; the estimate is at most a year off, and two loops settle it.
        var perPeriod = (long)CommonYearDays * Leap.Period + (long)Leap.LeapPerPeriod * LeapExtraDays;
        var year = EpochYear + LeapRules.FloorDiv(checked(dayNumber * Leap.Period), perPeriod);
        while (YearStart(year) > dayNumber) year--;
        while (YearStart(year + 1) <= dayNumber) year++;
        var leap = IsLeapYear(year);
        var cumulative = leap ? _leapCumulative : _commonCumulative;
        var inYear = (int)(dayNumber - YearStart(year));
        var month = 0;
        while (cumulative[month + 1] <= inYear) month++;
        var day = inYear - cumulative[month] + 1;
        var length = cumulative[month + 1] - cumulative[month];
        int weekday;
        if (WeekMode == WeekMode.Continuous) weekday = (int)LeapRules.FloorMod(dayNumber + EpochWeekday, Weekdays.Count);
        else weekday = Months[month].Intercalary ? -1 : (day - 1) % Weekdays.Count;
        return new DayInfo(dayNumber, year, month, day, inYear + 1, weekday, leap, length, cumulative[Months.Count]);
    }

    // ---- instant <-> moment ----------------------------------------------------------------------------------------------------------

    public Moment MomentOf(long instant)
    {
        if (instant < MinInstant || instant > MaxInstant) throw Refuse(CalendarCodes.InstantRange, "instant " + instant + " is outside " + MinInstant + " to " + MaxInstant);
        var ticksPerDay = Time.TicksPerDay;
        var day = LeapRules.FloorDiv(instant, ticksPerDay);
        var within = instant - day * ticksPerDay;
        var minute = (int)(within / Time.TicksPerMinute);
        return new Moment(instant, DayOf(day), minute, minute / Time.MinutesPerHour, minute % Time.MinutesPerHour, (int)(within % Time.TicksPerMinute));
    }

    public long InstantOf(long dayNumber, int minuteOfDay, int tickOfMinute = 0)
    {
        if (minuteOfDay < 0 || minuteOfDay >= Time.MinutesPerDay) throw Refuse(CalendarCodes.DateInvalid, "minute of day " + minuteOfDay + " is outside 0 to " + (Time.MinutesPerDay - 1));
        if (tickOfMinute < 0 || tickOfMinute >= Time.TicksPerMinute) throw Refuse(CalendarCodes.DateInvalid, "tick " + tickOfMinute + " is outside 0 to " + (Time.TicksPerMinute - 1));
        if (dayNumber < MinDay || dayNumber > MaxDay) throw Refuse(CalendarCodes.InstantRange, "day " + dayNumber + " is outside " + MinDay + " to " + MaxDay);
        return dayNumber * Time.TicksPerDay + (long)minuteOfDay * Time.TicksPerMinute + tickOfMinute;
    }

    public long InstantOf(CalendarDate date, int hour, int minute, int tickOfMinute = 0)
    {
        if (hour < 0 || hour >= Time.HoursPerDay) throw Refuse(CalendarCodes.DateInvalid, "hour " + hour + " is outside 0 to " + (Time.HoursPerDay - 1));
        if (minute < 0 || minute >= Time.MinutesPerHour) throw Refuse(CalendarCodes.DateInvalid, "minute " + minute + " is outside 0 to " + (Time.MinutesPerHour - 1));
        return InstantOf(DayNumber(date), hour * Time.MinutesPerHour + minute, tickOfMinute);
    }

    // ---- eras ------------------------------------------------------------------------------------------------------------------------

    /// <summary>The era of a year and the year counted in it (1 is the first year of the era); null when the document has no eras.</summary>
    public (EraDef Era, long YearOfEra)? EraOf(long year)
    {
        if (Eras.Count == 0) return null;
        var index = 0;
        for (var i = 0; i < Eras.Count; i++)
            if (Eras[i].From <= year) index = i;
        var era = Eras[index];
        return (era, era.Descending ? Eras[index + 1].From - year : year - era.From + 1);
    }

    // ---- date arithmetic -------------------------------------------------------------------------------------------------------------

    public const int MaxMonthsStep = 100_000;

    public CalendarDate AddDays(CalendarDate date, long days)
    {
        long target;
        try { target = checked(DayNumber(date) + days); }
        catch (OverflowException) { throw Refuse(CalendarCodes.InstantRange, "adding " + days + " days leaves the supported years"); }
        return DayOf(target).Date;
    }

    /// <summary>Adds present months (a month with no days that year is skipped); a day beyond the end of the target month moves to the month last day, as java.time does.</summary>
    public CalendarDate AddMonths(CalendarDate date, int months)
    {
        if (months < -MaxMonthsStep || months > MaxMonthsStep) throw Refuse(CalendarCodes.Range, "a step of " + months + " months is beyond " + MaxMonthsStep);
        DayNumber(date);   // refuses an invalid date
        var year = date.Year;
        var month = date.Month;
        var step = months < 0 ? -1 : 1;
        for (var left = Math.Abs(months); left > 0; left--)
        {
            do
            {
                month += step;
                if (month >= Months.Count) { month = 0; year++; }
                else if (month < 0) { month = Months.Count - 1; year--; }
                if (year < MinYear || year > MaxYear) throw Refuse(CalendarCodes.InstantRange, "adding " + months + " months leaves the supported years");
            }
            while (DaysInMonth(year, month) == 0);
        }
        return new CalendarDate(year, month, Math.Min(date.Day, DaysInMonth(year, month)));
    }

    /// <summary>Adds years; when the month does not exist in the target year (a leap-only month) the date moves to the last day of the month before it that does.</summary>
    public CalendarDate AddYears(CalendarDate date, int years)
    {
        DayNumber(date);
        var year = checked(date.Year + years);
        if (year < MinYear || year > MaxYear) throw Refuse(CalendarCodes.InstantRange, "adding " + years + " years leaves the supported years");
        var month = date.Month;
        while (DaysInMonth(year, month) == 0) month--;
        return new CalendarDate(year, month, Math.Min(date.Day, DaysInMonth(year, month)));
    }

    /// <summary>Days from <paramref name="from"/> to <paramref name="to"/> (negative when to is earlier).</summary>
    public long DaysBetween(CalendarDate from, CalendarDate to) => DayNumber(to) - DayNumber(from);
}
