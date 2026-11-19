using CalendarEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// How the relationships take their time from the calendar (the contract of docs/CALENDAR_ENGINE.md section 5): the calendar instant is the one coordinate of time, so a relationship step is a whole number of
/// calendar ticks counted from an origin instant, <c>ticks per day / steps per day</c> ticks long. This class checks that a relationship document and a calendar document fit each other, and hands the relationship
/// component its clock and its host facts; nothing here keeps a clock or a date of its own.
/// </summary>
public static class RelationshipCalendar
{
    public const string Mismatch = "REL_CALENDAR_MISMATCH";

    /// <summary>
    /// Refuses a pair of documents that do not fit: a step that is not a whole number of calendar ticks, a host fact the calendar does not give (or gives as another type, or with a range the document's
    /// range does not cover), and an activity that satisfies a need but is not an activity of the calendar. Found when the game is assembled, not as a refusal on every Update.
    /// </summary>
    public static void Check(RelationshipSet relationships, CalendarSet calendar)
    {
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(calendar);
        var ticksPerDay = calendar.Time.TicksPerDay;
        if (ticksPerDay % relationships.StepsPerDay != 0)
            throw new EngineRefusedException(Mismatch, "a day of " + ticksPerDay + " calendar ticks does not divide into " + relationships.StepsPerDay + " relationship steps");
        var offered = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(calendar.System, calendar.Moons), StringComparer.Ordinal);
        foreach (var (id, definition) in calendar.HostFacts) offered[id] = definition;
        foreach (var (id, wanted) in relationships.HostFacts)
        {
            if (!offered.TryGetValue(id, out var given)) throw new EngineRefusedException(Mismatch, "the calendar gives no fact '" + id + "', which the relationship document reads");
            if (given.Kind != wanted.Kind) throw new EngineRefusedException(Mismatch, "fact '" + id + "' is " + given.Kind + " in the calendar and " + wanted.Kind + " in the relationship document");
            if (wanted.Kind == FactKind.Integer && (given.Minimum < wanted.Minimum || given.Maximum > wanted.Maximum))
                throw new EngineRefusedException(Mismatch, "fact '" + id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the calendar, outside the " + wanted.Minimum + " to " + wanted.Maximum + " the relationship document declares");
        }
        var activities = calendar.Activities.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var source in relationships.Sources)
            if (!activities.Contains(source.Activity)) throw new EngineRefusedException(Mismatch, "activity '" + source.Activity + "' satisfies the need '" + relationships.Needs[source.Need].Id + "' but is not an activity of the calendar");
    }

    /// <summary>The clock that gives the step the calendar is at: step 0 is the instant <paramref name="originInstant"/>. Add the relationship component to the session after the calendar component.</summary>
    public static IRelationshipClock Clock(CalendarComponent calendar, RelationshipSet relationships, long originInstant)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(relationships);
        Check(relationships, calendar.Runtime.Set);
        return new CalendarRelationshipClock(calendar, calendar.Runtime.Set.Time.TicksPerDay / relationships.StepsPerDay, originInstant);
    }

    /// <summary>The host facts of the relationship component when the calendar is the host: the calendar facts together with the facts the calendar host gives.</summary>
    public static Func<IReadOnlyDictionary<string, FactValue>> HostFacts(CalendarComponent calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return () => calendar.Facts();
    }

    private sealed class CalendarRelationshipClock(CalendarComponent calendar, long ticksPerStep, long origin) : IRelationshipClock
    {
        /// <summary>The step of the calendar instant now; the session tick is not used, because the calendar has already moved by the scale for this tick. Before the origin the step is -1, which the relationship component refuses as a clock fault.</summary>
        public long StepAt(long tick)
        {
            var since = calendar.Runtime.Instant - origin;
            return since < 0 ? -1 : since / ticksPerStep;
        }
    }
}
