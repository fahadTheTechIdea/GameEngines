using ScenarioEngine.Contracts;

namespace CalendarEngine;

// The declarations of a calendar document, as plain immutable data. Every named thing has an id (stable, language free) and a localisation key (what a translator sees).

/// <summary>How a day is divided: hours, minutes and the ticks the caller counts a minute in.</summary>
public sealed record TimeDef(int HoursPerDay, int MinutesPerHour, int TicksPerMinute)
{
    public int MinutesPerDay => HoursPerDay * MinutesPerHour;
    public long TicksPerDay => (long)MinutesPerDay * TicksPerMinute;
}

public enum WeekMode { Continuous, RestartEachMonth }

public sealed record WeekdayDef(int Index, string Id, string Key);

/// <summary>A month. <paramref name="LeapDays"/> are added in a leap year; a month of 0 days and some leap days exists in leap years only. An intercalary month belongs to no week.</summary>
public sealed record MonthDef(int Index, string Id, string Key, int Days, int LeapDays, bool Intercalary);

/// <summary>An era counts years from <paramref name="From"/> (astronomical year). A descending era counts back to the start of the next one (1 BCE is year 0).</summary>
public sealed record EraDef(int Index, string Id, string Key, long From, bool Descending);

/// <summary>A part of the day starting at <paramref name="FromMinute"/> and lasting until the next one starts.</summary>
public sealed record DayPartDef(int Index, string Id, string Key, int FromMinute);

/// <summary>A season starts on a month and day (of a common year) and lasts until the next one; the last wraps over the year end. Sunrise and sunset are minutes of the day.</summary>
public sealed record SeasonDef(int Index, string Id, string Key, int Month, int Day, int Sunrise, int Sunset);

/// <summary>A moon with a period and a new moon, both exact fractions of a day counted from the epoch; eight phase keys, new moon first.</summary>
public sealed record MoonDef(int Index, string Id, string Key, long PeriodNumerator, long PeriodDenominator, long NewNumerator, long NewDenominator, IReadOnlyList<string> PhaseKeys);

public sealed record HolidayDef(int Index, string Id, string Key, Recurrence Rule, int Days, IReadOnlyList<string> Tags);

/// <summary>A date format: a locale message (with placeholders such as day, month, year) chosen by key.</summary>
public sealed record FormatDef(string Id, string Key);

/// <summary>A place or an activity an actor can be at or doing; the document declares them so a schedule cannot name one that does not exist.</summary>
public sealed record PlaceDef(string Id, string Key);

/// <summary>Something that overrides a routine for a while (an alarm, an invitation); it wins over a routine of equal or lower priority.</summary>
public sealed record InterruptDef(string Id, int Priority, string Location, string Activity);

/// <summary>A stretch of the day, [From, To) in minutes since the day began, with where and what.</summary>
public sealed record BlockDef(string Id, int From, int To, string Location, string Activity);

/// <summary>When a routine applies. Every field that is given must hold (AND); inside a list any entry may hold (OR).</summary>
public sealed record Selector(IReadOnlyList<int> Weekdays, IReadOnlyList<int> Months, IReadOnlyList<int> Seasons, IReadOnlyList<int> Holidays, Recurrence? Dates, Condition? When, IReadOnlyList<string> HostFactsNeeded)
{
    public bool IsEmpty => Weekdays.Count == 0 && Months.Count == 0 && Seasons.Count == 0 && Holidays.Count == 0 && Dates is null && When is null;
}

public sealed record RoutineDef(int Index, string Id, int Priority, Selector? Selector, IReadOnlyList<BlockDef> Blocks);

public sealed record ScheduleDef(int Index, string Id, IReadOnlyList<RoutineDef> Routines);

public enum AnswerSource { Routine, Interrupt }

/// <summary>Where an actor is and what it does, and which rule said so.</summary>
public sealed record ScheduleAnswer(string Schedule, string Location, string Activity, AnswerSource Source, string Routine, string Block, int Priority, int From, int To)
{
    /// <summary>The identity of the answer: when it changes, the actor has a new block.</summary>
    public string Key => Location + "|" + Activity + "|" + (Source == AnswerSource.Interrupt ? "interrupt:" : "routine:") + Routine + "/" + Block;
}
