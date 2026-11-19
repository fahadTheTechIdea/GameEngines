using CalendarEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// How the economy takes its time from the calendar (the contract of docs/CALENDAR_ENGINE.md section 5): the calendar instant is the one coordinate of time, so an economy step is a whole number of calendar
/// ticks counted from an origin instant, <c>ticks per day / steps per day</c> ticks long, and a period is the document's <c>periodSteps</c> of them. This class checks that an economy document and a calendar
/// document fit each other, and hands the economy component its clock and its host facts; nothing here keeps a clock or a date of its own.
/// </summary>
public static class EconomyCalendar
{
    public const string Mismatch = "ECON_CALENDAR_MISMATCH";

    /// <summary>
    /// Refuses a pair of documents that do not fit: a step that is not a whole number of calendar ticks, and a host fact the calendar does not give (or gives as another type, or with a range the document's
    /// range does not cover). Found when the game is assembled, not as a refusal on every Update.
    /// </summary>
    public static void Check(EconomySet economy, CalendarSet calendar)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(calendar);
        var ticksPerDay = calendar.Time.TicksPerDay;
        if (ticksPerDay % economy.StepsPerDay != 0)
            throw new EngineRefusedException(Mismatch, "a day of " + ticksPerDay + " calendar ticks does not divide into " + economy.StepsPerDay + " economy steps");
        var offered = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(calendar.System, calendar.Moons), StringComparer.Ordinal);
        foreach (var (id, definition) in calendar.HostFacts) offered[id] = definition;
        foreach (var (id, wanted) in economy.HostFacts)
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the calendar gives no fact '" + id + "', which the economy document reads");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is " + given.Kind + " in the calendar and " + wanted.Kind + " in the economy document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "fact '" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the calendar, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the economy document declares");
        }
    }

    /// <summary>The clock that gives the step the calendar is at: step 0 is the instant <paramref name="originInstant"/>. Add the economy component to the session after the calendar component.</summary>
    public static IEconomyClock Clock(CalendarComponent calendar, EconomySet economy, long originInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(economy);
        Check(economy, calendar.Runtime.Set);
        return new CalendarEconomyClock(calendar, calendar.Runtime.Set.Time.TicksPerDay / economy.StepsPerDay, originInstant);
    }

    /// <summary>The host facts of the economy component when the calendar is the host: the calendar facts together with the facts the calendar host gives.</summary>
    public static Func<IReadOnlyDictionary<string, FactValue>> HostFacts(CalendarComponent calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return () => calendar.Facts();
    }

    private sealed class CalendarEconomyClock(CalendarComponent calendar, long ticksPerStep, long origin) : IEconomyClock
    {
        /// <summary>The step of the calendar instant now; the session tick is not used, because the calendar has already moved by the scale for this tick. Before the origin the step is -1, which the economy component refuses as a clock fault.</summary>
        public long StepAt(long tick)
        {
            var since = calendar.Runtime.Instant - origin;
            return since < 0 ? -1 : since / ticksPerStep;
        }
    }
}
