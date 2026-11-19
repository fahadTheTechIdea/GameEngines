using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

namespace GameRuntime;

/// <summary>
/// How the tutorial takes its time from the calendar (the contract of docs/CALENDAR_ENGINE.md section 5): the calendar instant is the one coordinate of time, so a tutorial step is a whole number of calendar ticks
/// counted from an origin instant, <c>ticks per day / steps per day</c> ticks long. Hint idle times and tip durations are counted in these steps, so they are game time: they stand still while the calendar is
/// paused (a player in a menu is not stuck) and run faster when the game is sped up. This class checks that a tutorial document and a calendar document fit each other, and hands the tutorial component its clock
/// and its host facts; nothing here keeps a clock or a date of its own.
/// </summary>
public static class TutorialCalendar
{
    public const string Mismatch = "TUT_CALENDAR_MISMATCH";

    /// <summary>
    /// Refuses a pair of documents that do not fit: a step that is not a whole number of calendar ticks, and a host fact in the calendar's namespace (<c>cal.</c>) that the calendar does not give (or gives as
    /// another type, or with a range the document's range does not cover). The other host facts are the game's. Found when the game is assembled, not as a refusal on every Update.
    /// </summary>
    public static void Check(TutorialSet tutorial, CalendarSet calendar)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        ArgumentNullException.ThrowIfNull(calendar);
        var ticksPerDay = calendar.Time.TicksPerDay;
        if (ticksPerDay % tutorial.StepsPerDay != 0)
            throw new EngineRefusedException(Mismatch, "a day of " + ticksPerDay + " calendar ticks does not divide into " + tutorial.StepsPerDay + " tutorial steps");
        var offered = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(calendar.System, calendar.Moons), StringComparer.Ordinal);
        foreach (var (id, definition) in calendar.HostFacts) offered[id] = definition;
        foreach (var (id, wanted) in tutorial.HostFacts.Where(f => f.Key.StartsWith(CalendarFacts.Prefix, StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the calendar gives no fact '" + id + "', which the tutorial document reads");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is " + given.Kind + " in the calendar and " + wanted.Kind + " in the tutorial document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "fact '" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the calendar, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the tutorial document declares");
        }
    }

    /// <summary>The clock that gives the step the calendar is at: step 0 is the instant <paramref name="originInstant"/>. Add the tutorial component to the session after the calendar component.</summary>
    public static ITutorialClock Clock(CalendarComponent calendar, TutorialSet tutorial, long originInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(tutorial);
        Check(tutorial, calendar.Runtime.Set);
        return new CalendarTutorialClock(calendar, calendar.Runtime.Set.Time.TicksPerDay / tutorial.StepsPerDay, originInstant);
    }

    /// <summary>
    /// The host facts of the tutorial component when the calendar and the game share the hosting: the calendar facts together with the game's own (what the player is doing, whether the game is paused). A fact
    /// both give is refused when the facts are read, because the tutorial would have two answers.
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

    private sealed class CalendarTutorialClock(CalendarComponent calendar, long ticksPerStep, long origin) : ITutorialClock
    {
        /// <summary>The step of the calendar instant now; the session tick is not used, because the calendar has already moved by the scale for this tick. Before the origin the step is -1, which the tutorial component refuses as a clock fault.</summary>
        public long StepAt(long tick)
        {
            var since = calendar.Runtime.Instant - origin;
            return since < 0 ? -1 : since / ticksPerStep;
        }
    }
}
