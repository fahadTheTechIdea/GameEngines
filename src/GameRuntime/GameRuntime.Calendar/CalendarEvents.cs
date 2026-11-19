namespace GameRuntime;

// Events of the calendar component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged.
// Time is in calendar ticks (CalendarEngine instants); dates travel as year, month id and day. Names are ids, never text: the host renders them in its locale with CalendarNames.
//
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. A game that runs more than one calendar (a world and a dungeon with its own clock)
// gives each its own component with its own channel; a component answers the requests of its channel only. One request gets one answer: from the component that serves its channel, or, from the
// CalendarChannels registry, a refusal with CAL_CHANNEL_UNKNOWN for a channel nobody serves.
//
// Order of what one Update raises: the answers to the requests in arrival order, then for time that passed: TimeAdvanced, DayChanged, SeasonChanged (in time order), HolidayStarted (in time order),
// DayPartChanged, and last the ScheduleBlockChanged of the actors whose block changed (in actor id order).

/// <summary>A request to change how fast the calendar runs: <paramref name="numerator"/>/<paramref name="denominator"/> calendar ticks per game tick. Only queued.</summary>
public readonly struct CalendarScaleRequested
{
    public CalendarScaleRequested(string requestId, long numerator, long denominator, string channel = "") { RequestId = requestId; Numerator = numerator; Denominator = denominator; Channel = channel; }
    public string RequestId { get; }
    public long Numerator { get; }
    public long Denominator { get; }
    public string Channel { get; }
}

/// <summary>A request to stop or resume the calendar.</summary>
public readonly struct CalendarPauseRequested
{
    public CalendarPauseRequested(string requestId, bool paused, string channel = "") { RequestId = requestId; Paused = paused; Channel = channel; }
    public string RequestId { get; }
    public bool Paused { get; }
    public string Channel { get; }
}

/// <summary>A request to skip forward at once to the time of day <paramref name="hour"/>:<paramref name="minute"/> of the day <paramref name="days"/> days after today (sleep until morning).</summary>
public readonly struct CalendarSkipRequested
{
    public CalendarSkipRequested(string requestId, long days, int hour, int minute, string channel = "") { RequestId = requestId; Days = days; Hour = hour; Minute = minute; Channel = channel; }
    public string RequestId { get; }
    public long Days { get; }
    public int Hour { get; }
    public int Minute { get; }
    public string Channel { get; }
}

/// <summary>A request to have the calendar follow an actor along a schedule.</summary>
public readonly struct CalendarActorAddRequested
{
    public CalendarActorAddRequested(string requestId, string actor, string schedule, string channel = "") { RequestId = requestId; Actor = actor; Schedule = schedule; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Schedule { get; }
    public string Channel { get; }
}

/// <summary>A request to stop following an actor.</summary>
public readonly struct CalendarActorRemoveRequested
{
    public CalendarActorRemoveRequested(string requestId, string actor, string channel = "") { RequestId = requestId; Actor = actor; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Channel { get; }
}

/// <summary>A request to interrupt an actor for <paramref name="durationTicks"/> calendar ticks with an interrupt the document declares.</summary>
public readonly struct CalendarInterruptRequested
{
    public CalendarInterruptRequested(string requestId, string actor, string kind, long durationTicks, string channel = "") { RequestId = requestId; Actor = actor; Kind = kind; DurationTicks = durationTicks; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Kind { get; }
    public long DurationTicks { get; }
    public string Channel { get; }
}

/// <summary>A request to end an actor's interrupt now.</summary>
public readonly struct CalendarInterruptClearRequested
{
    public CalendarInterruptClearRequested(string requestId, string actor, string channel = "") { RequestId = requestId; Actor = actor; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Channel { get; }
}

/// <summary>A request for where an actor is and what it does now.</summary>
public readonly struct CalendarWhereRequested
{
    public CalendarWhereRequested(string requestId, string actor, string channel = "") { RequestId = requestId; Actor = actor; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Channel { get; }
}

/// <summary>A request was carried out. Raised for every request that changes the calendar, so a requester never has to guess that silence meant success.</summary>
public readonly struct CalendarApplied
{
    public CalendarApplied(string source, string channel, string requestId, long tick) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the calendar is exactly as it was. A request for a channel nobody serves is refused by the registry with <see cref="CalendarChannels.UnknownChannel"/>.</summary>
public readonly struct CalendarRefused
{
    public CalendarRefused(string source, string channel, string requestId, long tick, string code) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }

    /// <summary>The id of the component that refused; the registry's id for an unserved channel.</summary>
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>Time moved from one instant to another. <paramref name="clamped"/>: time earned beyond the last supported instant was dropped. <paramref name="holidaysOmitted"/>: holidays that began and are not announced as HolidayStarted.</summary>
public readonly struct CalendarTimeAdvanced
{
    public CalendarTimeAdvanced(string source, string channel, long tick, long fromInstant, long toInstant, long daysCrossed, bool clamped, int holidaysOmitted)
    { Source = source; Channel = channel; Tick = tick; FromInstant = fromInstant; ToInstant = toInstant; DaysCrossed = daysCrossed; Clamped = clamped; HolidaysOmitted = holidaysOmitted; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long FromInstant { get; }
    public long ToInstant { get; }
    public long DaysCrossed { get; }
    public bool Clamped { get; }
    public int HolidaysOmitted { get; }
}

/// <summary>A new day began (or several: <paramref name="daysCrossed"/>). The date is the new current one.</summary>
public readonly struct CalendarDayChanged
{
    public CalendarDayChanged(string source, string channel, long tick, long year, string month, int day, string weekday, long dayNumber, long daysCrossed)
    { Source = source; Channel = channel; Tick = tick; Year = year; Month = month; Day = day; Weekday = weekday; DayNumber = dayNumber; DaysCrossed = daysCrossed; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Year { get; }
    public string Month { get; }
    public int Day { get; }

    /// <summary>The weekday id; empty for a day outside the weeks.</summary>
    public string Weekday { get; }
    public long DayNumber { get; }
    public long DaysCrossed { get; }
}

/// <summary>The part of the day, or the day, changed: the pair (day, part) is new. Carries what a sensor needs to know of the time.</summary>
public readonly struct CalendarDayPartChanged
{
    public CalendarDayPartChanged(string source, string channel, long tick, string dayPart, int hour, int minute, string season, bool isDay, int lightPermille, long year, string month, int day)
    { Source = source; Channel = channel; Tick = tick; DayPart = dayPart; Hour = hour; Minute = minute; Season = season; IsDay = isDay; LightPermille = lightPermille; Year = year; Month = month; Day = day; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string DayPart { get; }
    public int Hour { get; }
    public int Minute { get; }
    public string Season { get; }
    public bool IsDay { get; }
    public int LightPermille { get; }
    public long Year { get; }
    public string Month { get; }
    public int Day { get; }
}

/// <summary>A season began, on the given day.</summary>
public readonly struct CalendarSeasonChanged
{
    public CalendarSeasonChanged(string source, string channel, long tick, string from, string to, long year, string month, int day)
    { Source = source; Channel = channel; Tick = tick; From = from; To = to; Year = year; Month = month; Day = day; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string From { get; }
    public string To { get; }
    public long Year { get; }
    public string Month { get; }
    public int Day { get; }
}

/// <summary>A holiday began on the given day. <paramref name="tags"/> are the holiday's tags joined by ','.</summary>
public readonly struct CalendarHolidayStarted
{
    public CalendarHolidayStarted(string source, string channel, long tick, string holiday, string tags, int days, long year, string month, int day)
    { Source = source; Channel = channel; Tick = tick; Holiday = holiday; Tags = tags; Days = days; Year = year; Month = month; Day = day; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Holiday { get; }
    public string Tags { get; }
    public int Days { get; }
    public long Year { get; }
    public string Month { get; }
    public int Day { get; }
}

/// <summary>An actor's schedule put it in a new block (or an interrupt began or ended). <paramref name="origin"/> is <c>routine</c> or <c>interrupt</c>; <paramref name="block"/> is empty for an interrupt.</summary>
public readonly struct CalendarScheduleBlockChanged
{
    public CalendarScheduleBlockChanged(string source, string channel, long tick, string actor, string schedule, string location, string activity, string origin, string routine, string block, int priority)
    { Source = source; Channel = channel; Tick = tick; Actor = actor; Schedule = schedule; Location = location; Activity = activity; Origin = origin; Routine = routine; Block = block; Priority = priority; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Schedule { get; }
    public string Location { get; }
    public string Activity { get; }
    public string Origin { get; }
    public string Routine { get; }
    public string Block { get; }
    public int Priority { get; }
}

/// <summary>The answer to a <see cref="CalendarWhereRequested"/>.</summary>
public readonly struct CalendarWhereAnswered
{
    public CalendarWhereAnswered(string source, string channel, string requestId, long tick, string actor, string schedule, string location, string activity, string origin, string routine, string block, int priority)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Actor = actor; Schedule = schedule; Location = location; Activity = activity; Origin = origin; Routine = routine; Block = block; Priority = priority; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Schedule { get; }
    public string Location { get; }
    public string Activity { get; }
    public string Origin { get; }
    public string Routine { get; }
    public string Block { get; }
    public int Priority { get; }
}
