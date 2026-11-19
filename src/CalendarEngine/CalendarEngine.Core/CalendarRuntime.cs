using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>A season that began while time advanced: on which day, from which season to which.</summary>
public sealed record SeasonChange(long Day, int From, int To);

/// <summary>A holiday that began on a day while time advanced.</summary>
public sealed record HolidayStart(long Day, int Holiday);

/// <summary>
/// What advancing time crossed. The seasons crossed are all listed (a step crosses at most 400 days, so a handful); the holidays are the first <see cref="CalendarRuntime.MaxListed"/> in time order and the count says how many more there were, so a long jump never produces
/// an unbounded report and never hides that it left something out.
/// </summary>
public sealed record TimeStep(long From, long To, long DaysCrossed, bool Clamped, bool DayPartChanged, IReadOnlyList<SeasonChange> Seasons, IReadOnlyList<HolidayStart> Holidays, int HolidaysOmitted);

/// <summary>An actor whose block changed since it was last announced.</summary>
public sealed record ActorChange(string Actor, ScheduleAnswer Answer);

/// <summary>What the calendar remembers about an actor: its schedule, an interrupt in force, and the answer last announced for it.</summary>
public sealed class ActorState
{
    public ActorState(string id, string schedule) { Id = id; Schedule = schedule; }
    public string Id { get; }
    public string Schedule { get; }
    public string? Interrupt { get; set; }
    public long InterruptUntil { get; set; }
    public string Announced { get; set; } = "";
}

/// <summary>
/// The state of one calendar: the instant, how fast it moves, and the actors that follow schedules. It has no clock of its own. The game layer gives it the tick of each Update and it moves the instant by the
/// time scale (a rational number of calendar ticks per game tick, with the remainder carried exactly, so nothing drifts and nothing is lost). A single step never crosses more than
/// <see cref="MaxAdvanceDays"/> days; what is owed beyond that is kept as a backlog and paid in the next steps, so the work of one Update is bounded however far behind the game is.
/// </summary>
public sealed class CalendarRuntime
{
    public const int MaxListed = 64;
    public const int MaxAdvanceDays = 400;
    public const int MaxSkipDays = 366;
    public const int MaxScaleDays = 366;
    public const int MaxScaleDenominator = 10_000;
    public const long MaxElapsedTicks = 50_000_000;
    public const int MaxActors = 100_000;

    private readonly SortedDictionary<string, ActorState> _actors = new(StringComparer.Ordinal);

    private CalendarRuntime(CalendarSet set, long instant, long scaleNumerator, long scaleDenominator)
    {
        Set = set; Instant = instant; ScaleNumerator = scaleNumerator; ScaleDenominator = scaleDenominator;
    }

    public CalendarSet Set { get; }
    public long Instant { get; private set; }
    public long ScaleNumerator { get; private set; }
    public long ScaleDenominator { get; private set; }

    /// <summary>The part of a calendar tick already earned and not yet whole, in units of 1/<see cref="ScaleDenominator"/>; always below the denominator.</summary>
    public long ScaleRemainder { get; private set; }

    public bool Paused { get; private set; }

    /// <summary>The game tick of the latest step; the next step counts the ticks since it.</summary>
    public long LastTick { get; private set; }

    /// <summary>Calendar ticks earned and not yet applied because a step is bounded.</summary>
    public long Backlog { get; private set; }

    public IReadOnlyDictionary<string, ActorState> Actors => _actors;

    public static CalendarRuntime Create(CalendarSet set, long startInstant, long scaleNumerator, long scaleDenominator, long startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(set);
        set.System.MomentOf(startInstant);   // refuses an instant outside the supported years
        CheckScale(set, scaleNumerator, scaleDenominator);
        if (startTick < 0) throw new EngineRefusedException(CalendarCodes.TickOrder, "the start tick is not negative");
        return new CalendarRuntime(set, startInstant, scaleNumerator, scaleDenominator) { LastTick = startTick };
    }

    /// <summary>Rebuilds a runtime from checked save data; only the save reader calls it.</summary>
    internal static CalendarRuntime Restore(CalendarSet set, CalendarImage image)
    {
        var runtime = new CalendarRuntime(set, image.Instant, image.ScaleNumerator, image.ScaleDenominator)
        {
            ScaleRemainder = image.ScaleRemainder, Paused = image.Paused, LastTick = image.LastTick, Backlog = image.Backlog,
        };
        foreach (var actor in image.Actors)
            runtime._actors.Add(actor.Id, new ActorState(actor.Id, actor.Schedule) { Interrupt = actor.Interrupt, InterruptUntil = actor.InterruptUntil, Announced = actor.Announced });
        return runtime;
    }

    private static void CheckScale(CalendarSet set, long numerator, long denominator)
    {
        if (denominator < 1 || denominator > MaxScaleDenominator) throw new EngineRefusedException(CalendarCodes.Scale, "the scale denominator is 1 to " + MaxScaleDenominator + ", not " + denominator);
        var most = checked(MaxScaleDays * set.Time.TicksPerDay * denominator);
        if (numerator < 1 || numerator > most)
            throw new EngineRefusedException(CalendarCodes.Scale, "the scale numerator is 1 to " + most + " (at most " + MaxScaleDays + " days of calendar per game tick), not " + numerator);
    }

    // ---- the clock -------------------------------------------------------------------------------------------------------------------

    public void SetScale(long numerator, long denominator)
    {
        CheckScale(Set, numerator, denominator);
        ScaleNumerator = numerator;
        ScaleDenominator = denominator;
        ScaleRemainder = 0;   // a fraction of a calendar tick earned at the old speed is dropped, as RuntimeClock does when its speed changes
    }

    public void SetPaused(bool paused) => Paused = paused;

    /// <summary>
    /// Brings the calendar to game tick <paramref name="tick"/>: earns the calendar ticks of the game ticks since the last step (none while paused) and applies as many as one step may. Returns what was crossed,
    /// or null when the instant did not move.
    /// </summary>
    public TimeStep? Step(long tick)
    {
        if (tick < LastTick) throw new EngineRefusedException(CalendarCodes.TickOrder, "tick " + tick + " is before the latest step at tick " + LastTick);
        var elapsed = tick - LastTick;
        if (elapsed > MaxElapsedTicks) throw new EngineRefusedException(CalendarCodes.TickOrder, "tick " + tick + " is " + elapsed + " ticks after the latest step; at most " + MaxElapsedTicks + " is accepted at once");
        LastTick = tick;
        if (Paused) return null;
        {
            var earned = checked(elapsed * ScaleNumerator + ScaleRemainder);
            Backlog += earned / ScaleDenominator;
            ScaleRemainder = earned % ScaleDenominator;
        }
        // Time beyond the last supported instant can never be applied: it is dropped, and the step that reaches the end says so.
        var remaining = Set.System.MaxInstant - Instant;
        var clamped = Backlog > remaining;
        if (clamped) Backlog = remaining;
        var apply = Math.Min(Backlog, MaxAdvanceDays * Set.Time.TicksPerDay);
        if (apply == 0) return null;
        Backlog -= apply;
        return Advance(apply, clamped);
    }

    /// <summary>Moves the instant forward to <paramref name="target"/> at once (sleep until morning). It does not depend on the scale or on pause.</summary>
    public TimeStep SkipTo(long target)
    {
        Set.System.MomentOf(target);
        if (target <= Instant) throw new EngineRefusedException(CalendarCodes.Skip, "a skip goes forward: " + target + " is not after " + Instant);
        if (target - Instant > MaxSkipDays * Set.Time.TicksPerDay) throw new EngineRefusedException(CalendarCodes.Skip, "a skip is at most " + MaxSkipDays + " days");
        return Advance(target - Instant, false);
    }

    private TimeStep Advance(long ticks, bool clamped)
    {
        var system = Set.System;
        var ticksPerDay = Set.Time.TicksPerDay;
        var from = Instant;
        var target = from + ticks;
        var dayFrom = LeapRules.FloorDiv(from, ticksPerDay);
        var dayTo = LeapRules.FloorDiv(target, ticksPerDay);
        var partChanged = dayTo != dayFrom || Set.DayPartOf(MinuteOf(from, dayFrom)) != Set.DayPartOf(MinuteOf(target, dayTo));
        var seasons = new List<SeasonChange>();
        var holidays = new List<HolidayStart>();
        var holidaysOmitted = 0;
        if (dayTo > dayFrom)
        {
            var season = Set.SeasonOf(system.DayOf(dayFrom));
            for (var day = dayFrom + 1; day <= dayTo; day++)
            {
                var info = system.DayOf(day);
                var now = Set.SeasonOf(info);
                if (now != season)
                {
                    seasons.Add(new SeasonChange(day, season, now));
                    season = now;
                }
                foreach (var holiday in Set.HolidaysStartingOn(info))
                    if (holidays.Count < MaxListed) holidays.Add(new HolidayStart(day, holiday)); else holidaysOmitted++;
            }
        }
        Instant = target;
        return new TimeStep(from, target, dayTo - dayFrom, clamped, partChanged, seasons, holidays, holidaysOmitted);
    }

    private int MinuteOf(long instant, long day) => (int)((instant - day * Set.Time.TicksPerDay) / Set.Time.TicksPerMinute);

    // ---- actors ----------------------------------------------------------------------------------------------------------------------

    public void AddActor(string id, string schedule)
    {
        if (id.Length == 0 || id.Length > 128) throw new EngineRefusedException(CalendarCodes.Range, "an actor id is 1 to 128 characters");
        if (!Set.ScheduleById.ContainsKey(schedule)) throw new EngineRefusedException(CalendarCodes.UnknownSchedule, "there is no schedule '" + schedule + "'");
        if (_actors.ContainsKey(id)) throw new EngineRefusedException(CalendarCodes.DuplicateActor, "actor '" + id + "' already follows a schedule");
        if (_actors.Count >= MaxActors) throw new EngineRefusedException(CalendarCodes.Capacity, "a calendar follows at most " + MaxActors + " actors");
        _actors.Add(id, new ActorState(id, schedule));
    }

    public void RemoveActor(string id)
    {
        if (!_actors.Remove(id)) throw new EngineRefusedException(CalendarCodes.UnknownActor, "there is no actor '" + id + "'");
    }

    private ActorState Actor(string id) => _actors.TryGetValue(id, out var actor) ? actor : throw new EngineRefusedException(CalendarCodes.UnknownActor, "there is no actor '" + id + "'");

    /// <summary>Puts an interrupt on an actor for a number of calendar ticks. One of lower priority than the interrupt already in force is refused; an equal or higher one replaces it.</summary>
    public void Interrupt(string actorId, string kind, long durationTicks)
    {
        var actor = Actor(actorId);
        if (!Set.InterruptById.TryGetValue(kind, out var def)) throw new EngineRefusedException(CalendarCodes.UnknownInterrupt, "there is no interrupt '" + kind + "'");
        if (durationTicks < 1 || durationTicks > MaxSkipDays * Set.Time.TicksPerDay) throw new EngineRefusedException(CalendarCodes.Range, "an interrupt lasts 1 to " + MaxSkipDays * Set.Time.TicksPerDay + " ticks, not " + durationTicks);
        if (Instant + durationTicks > Set.System.MaxInstant) throw new EngineRefusedException(CalendarCodes.InstantRange, "the interrupt would end after the last supported instant");
        if (actor.Interrupt is { } current && Set.InterruptById[current].Priority > def.Priority)
            throw new EngineRefusedException(CalendarCodes.Outranked, "actor '" + actorId + "' has interrupt '" + current + "' of priority " + Set.InterruptById[current].Priority + ", which outranks '" + kind + "' of " + def.Priority);
        actor.Interrupt = kind;
        actor.InterruptUntil = Instant + durationTicks;
    }

    public void ClearInterrupt(string actorId)
    {
        var actor = Actor(actorId);
        actor.Interrupt = null;
        actor.InterruptUntil = 0;
    }

    public CalendarContext Context(IReadOnlyDictionary<string, FactValue>? hostFacts = null) => CalendarContext.At(Set, Instant, hostFacts);

    /// <summary>Where one actor is now, interrupt included.</summary>
    public ScheduleAnswer WhereIs(CalendarContext context, string actorId)
    {
        var actor = Actor(actorId);
        return context.WhereIs(actor.Schedule, actor.Interrupt is not null && actor.InterruptUntil > context.Moment.Instant ? actor.Interrupt : null);
    }

    /// <summary>
    /// The actors whose answer differs from the one last announced, in actor id order, at most <paramref name="max"/> of them; each is marked announced. An actor never announced is announced first, so the
    /// host learns where everybody is. Those over the limit stay unannounced and are listed by the next call, so none is lost.
    /// </summary>
    public IReadOnlyList<ActorChange> Refresh(CalendarContext context, int max)
    {
        if (max < 1) throw new EngineRefusedException(CalendarCodes.Range, "a refresh announces at least 1 actor");
        var changes = new List<ActorChange>();
        foreach (var actor in _actors.Values)
        {
            if (actor.Interrupt is not null && actor.InterruptUntil <= context.Moment.Instant) { actor.Interrupt = null; actor.InterruptUntil = 0; }   // an interrupt that has run out
            var answer = context.WhereIs(actor.Schedule, actor.Interrupt is not null && actor.InterruptUntil > context.Moment.Instant ? actor.Interrupt : null);
            if (answer.Key == actor.Announced) continue;
            if (changes.Count >= max) break;
            actor.Announced = answer.Key;
            changes.Add(new ActorChange(actor.Id, answer));
        }
        return changes;
    }
}
