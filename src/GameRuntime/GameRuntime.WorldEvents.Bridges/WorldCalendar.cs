using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// How the world takes its time and its seasons from the calendar (the contract of docs/CALENDAR_ENGINE.md section 5): the calendar instant is the one coordinate of time, so a world step is a whole number of calendar
/// ticks counted from an origin instant, <c>ticks per day / steps per day</c> ticks long, and the season of a step is the calendar's season of the day that instant falls in (leap years and moved season starts included).
/// This class checks that a world document and a calendar document fit each other, and hands the world component its clock and its host facts; nothing here keeps a clock or a date of its own.
/// </summary>
public static class WorldCalendar
{
    public const string Mismatch = "WE_CALENDAR_MISMATCH";

    /// <summary>
    /// Refuses a pair of documents that do not fit: a step that is not a whole number of calendar ticks; a season the world has and the calendar has not (the world would be asked for a season it has no climate for) or
    /// the other way round; and a host fact in the calendar's namespace (<c>cal.</c>) that the calendar does not give (or gives as another type, or with a range the document's range does not cover). Found when the
    /// game is assembled, not as a refusal on every Update.
    /// </summary>
    public static void Check(WorldSet world, CalendarSet calendar)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(calendar);
        var ticksPerDay = calendar.Time.TicksPerDay;
        if (ticksPerDay % world.StepsPerDay != 0)
            throw new EngineRefusedException(Mismatch, "a day of " + ticksPerDay + " calendar ticks does not divide into " + world.StepsPerDay + " world steps");
        foreach (var season in world.Seasons)
            if (!calendar.Seasons.Any(s => s.Id == season.Id)) throw new EngineRefusedException(Mismatch, "the world has a climate for season '" + season.Id + "', which the calendar does not have");
        foreach (var season in calendar.Seasons)
            if (!world.SeasonById.ContainsKey(season.Id)) throw new EngineRefusedException(Mismatch, "the calendar has season '" + season.Id + "', for which the world has no climate");
        var offered = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(calendar.System, calendar.Moons), StringComparer.Ordinal);
        foreach (var (id, definition) in calendar.HostFacts) offered[id] = definition;
        foreach (var (id, wanted) in world.HostFacts.Where(f => f.Key.StartsWith(CalendarFacts.Prefix, StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the calendar gives no fact '" + id + "', which the world document reads");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is " + given.Kind + " in the calendar and " + wanted.Kind + " in the world document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "fact '" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the calendar, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the world document declares");
        }
    }

    /// <summary>The clock that gives the step and the season the calendar is at: step 0 is the instant <paramref name="originInstant"/>. Add the world component to the session after the calendar component.</summary>
    public static IWorldClock Clock(CalendarComponent calendar, WorldSet world, long originInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(world);
        Check(world, calendar.Runtime.Set);
        return new CalendarWorldClock(calendar, calendar.Runtime.Set.Time.TicksPerDay / world.StepsPerDay, originInstant);
    }

    /// <summary>
    /// The host facts of the world component when the calendar and the game share the hosting: the calendar facts together with the game's own (a garrison's strength). A fact both give is refused when the facts are
    /// read, because the world would have two answers.
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

    private sealed class CalendarWorldClock(CalendarComponent calendar, long ticksPerStep, long origin) : IWorldClock
    {
        /// <summary>The step of the calendar instant now; the session tick is not used, because the calendar has already moved by the scale for this tick. Before the origin the step is -1, which the world component refuses as a clock fault once it has lived a step.</summary>
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
