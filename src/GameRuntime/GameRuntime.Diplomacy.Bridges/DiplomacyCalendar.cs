using CalendarEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// How the diplomacy takes its time and its seasons from the calendar (the contract of docs/CALENDAR_ENGINE.md section 5): the calendar instant is the one coordinate of time, so a diplomacy step is a whole number of
/// calendar ticks counted from an origin instant, <c>ticks per day / steps per day</c> ticks long, and the season of a step is the calendar's season of the day that instant falls in (leap years and moved season starts
/// included). A treaty term is counted in days by the diplomacy, so a term given in months or years is turned into days by the calendar's own arithmetic from the date it is made. This class checks that a diplomacy
/// document and a calendar document fit each other, and hands the diplomacy component its clock and its host facts; nothing here keeps a clock or a date of its own.
/// </summary>
public static class DiplomacyCalendar
{
    public const string Mismatch = "DIP_CALENDAR_MISMATCH";

    /// <summary>
    /// Refuses a pair of documents that do not fit: a step that is not a whole number of calendar ticks; a season the diplomacy has and the calendar has not (a war would be tired by a season the calendar never reaches)
    /// or the other way round; and a host fact in the calendar's namespace (<c>cal.</c>) that the calendar does not give (or gives as another type, or with a range the document's range does not cover). Found when the game
    /// is assembled, not as a refusal on every Update.
    /// </summary>
    public static void Check(DipSet diplomacy, CalendarSet calendar)
    {
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(calendar);
        var ticksPerDay = calendar.Time.TicksPerDay;
        if (ticksPerDay % diplomacy.StepsPerDay != 0)
            throw new EngineRefusedException(Mismatch, "a day of " + ticksPerDay + " calendar ticks does not divide into " + diplomacy.StepsPerDay + " diplomacy steps");
        foreach (var season in diplomacy.Seasons)
            if (!calendar.Seasons.Any(s => s.Id == season.Id)) throw new EngineRefusedException(Mismatch, "the diplomacy has a cost of war for season '" + season.Id + "', which the calendar does not have");
        foreach (var season in calendar.Seasons)
            if (!diplomacy.SeasonById.ContainsKey(season.Id)) throw new EngineRefusedException(Mismatch, "the calendar has season '" + season.Id + "', for which the diplomacy has no cost of war");
        var offered = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(calendar.System, calendar.Moons), StringComparer.Ordinal);
        foreach (var (id, definition) in calendar.HostFacts) offered[id] = definition;
        foreach (var (id, wanted) in diplomacy.HostFacts.Where(f => f.Key.StartsWith(CalendarFacts.Prefix, StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the calendar gives no fact '" + id + "', which the diplomacy document reads");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is " + given.Kind + " in the calendar and " + wanted.Kind + " in the diplomacy document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "fact '" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the calendar, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the diplomacy document declares");
        }
    }

    /// <summary>The clock that gives the step and the season the calendar is at: step 0 is the instant <paramref name="originInstant"/>. Add the diplomacy component to the session after the calendar component.</summary>
    public static IDiplomacyClock Clock(CalendarComponent calendar, DipSet diplomacy, long originInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(diplomacy);
        Check(diplomacy, calendar.Runtime.Set);
        return new CalendarDiplomacyClock(calendar, calendar.Runtime.Set.Time.TicksPerDay / diplomacy.StepsPerDay, originInstant);
    }

    /// <summary>
    /// The host facts of the diplomacy component when the calendar and the game share the hosting: the calendar facts together with the game's own (a crisis level). A fact both give is refused when the facts are read,
    /// because the diplomacy would have two answers.
    /// </summary>
    public static Func<IReadOnlyDictionary<string, FactValue>> HostFacts(CalendarComponent calendar, Func<IReadOnlyDictionary<string, FactValue>> game)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(game);
        return () =>
        {
            var facts = new Dictionary<string, FactValue>(calendar.Facts(), StringComparer.Ordinal);
            foreach (var (id, value) in game())
                if (!facts.TryAdd(id, value)) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is given by both the calendar and the game");
            return facts;
        };
    }

    /// <summary>
    /// How many days a term of some months and years is, counted by the calendar from the date it is today: the months first, then the years, each as the calendar's own arithmetic does it (a month that is shorter
    /// ends on its last day; a leap year adds its day). The diplomacy takes this number as the term of a treaty. A term that is not at least a day and at most the longest the diplomacy allows is refused.
    /// </summary>
    public static int TermDays(CalendarComponent calendar, int months, int years)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (months < 0 || years < 0) throw new EngineRefusedException(DipCodes.Terms, "a term of " + months + " months and " + years + " years goes back");
        var system = calendar.Runtime.Set.System;
        var start = system.MomentOf(calendar.Runtime.Instant).Day.Date;
        var end = system.AddYears(system.AddMonths(start, months), years);
        var days = system.DaysBetween(start, end);
        if (days < 1 || days > 36_500) throw new EngineRefusedException(DipCodes.Terms, "a term of " + months + " months and " + years + " years is " + days + " days, and a treaty runs for 1 to 36500");
        return (int)days;
    }

    private sealed class CalendarDiplomacyClock(CalendarComponent calendar, long ticksPerStep, long origin) : IDiplomacyClock
    {
        /// <summary>The step of the calendar instant now; the session tick is not used, because the calendar has already moved by the scale for this tick. Before the origin the step is -1, which the component refuses as a clock fault once it has lived a step.</summary>
        public long StepAt(long tick)
        {
            var since = calendar.Runtime.Instant - origin;
            return since < 0 ? -1 : since / ticksPerStep;
        }

        /// <summary>The calendar's own season of the day the step begins in: a pure function of the calendar document and the step.</summary>
        public string SeasonAt(long step)
        {
            var set = calendar.Runtime.Set;
            var moment = set.System.MomentOf(origin + step * ticksPerStep);
            return set.Seasons[set.SeasonOf(moment.Day)].Id;
        }
    }
}
