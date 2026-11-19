using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

/// <summary>
/// The calendar at one instant, with the facts the host supplied for it. A question about where somebody is asks the context, which works out once which routines of each schedule apply (a day
/// level fact, the same for every actor on that schedule) and then answers each actor with a lookup. Build one per instant you ask about; it holds nothing between instants.
/// </summary>
public sealed class CalendarContext
{
    private readonly IReadOnlyDictionary<string, FactValue> _host;
    private readonly Lazy<Dictionary<string, FactValue>> _facts;
    private readonly RoutineDef[]?[] _applicable;
    private IReadOnlyList<int>? _holidays;

    private CalendarContext(CalendarSet set, Moment moment, IReadOnlyDictionary<string, FactValue> host)
    {
        Set = set; Moment = moment; _host = host;
        Season = set.SeasonOf(moment.Day);
        _applicable = new RoutineDef[]?[set.Schedules.Count];
        _facts = new Lazy<Dictionary<string, FactValue>>(() =>
        {
            var all = CalendarFacts.Read(set, moment);
            foreach (var (id, value) in host) all[id] = value;
            return all;
        });
    }

    public CalendarSet Set { get; }
    public Moment Moment { get; }
    public int Season { get; }

    /// <summary>The holidays on during this day, by index.</summary>
    public IReadOnlyList<int> Holidays => _holidays ??= Set.HolidaysOn(Moment.Day);

    /// <summary>Every fact a condition or a sensor can read at this moment: the calendar own and the host's.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts => _facts.Value;

    /// <param name="hostFacts">The facts the document declares for the host, with values of the declared kind and range. Facts the document does not declare are refused; a declared fact a condition needs and is not given is refused when that condition is asked.</param>
    public static CalendarContext At(CalendarSet set, long instant, IReadOnlyDictionary<string, FactValue>? hostFacts = null)
    {
        ArgumentNullException.ThrowIfNull(set);
        var host = hostFacts ?? new Dictionary<string, FactValue>();
        foreach (var (id, value) in host)
        {
            if (!set.HostFacts.TryGetValue(id, out var def)) throw new EngineRefusedException(CalendarCodes.FactInvalid, "'" + id + "' is not a fact this calendar declares for the host");
            if (value.Kind != def.Kind) throw new EngineRefusedException(CalendarCodes.FactInvalid, "fact '" + id + "' is " + def.Kind + ", not " + value.Kind);
            if (def.Kind == FactKind.Integer && (value.Integer < def.Minimum || value.Integer > def.Maximum))
                throw new EngineRefusedException(CalendarCodes.FactInvalid, "fact '" + id + "' is " + value.Integer + ", outside " + def.Minimum + " to " + def.Maximum);
        }
        return new CalendarContext(set, set.System.MomentOf(instant), host);
    }

    // ---- routines --------------------------------------------------------------------------------------------------------------------

    /// <summary>The reason a selector holds or not, field by field; null when it holds.</summary>
    internal string? WhyNot(Selector selector)
    {
        var day = Moment.Day;
        var system = Set.System;
        if (selector.Weekdays.Count > 0 && (day.Weekday < 0 || !selector.Weekdays.Contains(day.Weekday)))
            return "weekday is " + (day.Weekday < 0 ? "none" : system.Weekdays[day.Weekday].Id) + ", not one of " + string.Join(", ", selector.Weekdays.Select(w => system.Weekdays[w].Id));
        if (selector.Months.Count > 0 && !selector.Months.Contains(day.Month))
            return "month is " + system.Months[day.Month].Id + ", not one of " + string.Join(", ", selector.Months.Select(m => system.Months[m].Id));
        if (selector.Seasons.Count > 0 && !selector.Seasons.Contains(Season))
            return "season is " + Set.Seasons[Season].Id + ", not one of " + string.Join(", ", selector.Seasons.Select(s => Set.Seasons[s].Id));
        if (selector.Holidays.Count > 0 && !selector.Holidays.Any(Holidays.Contains))
            return "no holiday of " + string.Join(", ", selector.Holidays.Select(h => Set.Holidays[h].Id)) + " is on";
        if (selector.Dates is { } dates && !dates.Matches(day, Season)) return "the date rule does not include this day";
        if (selector.When is { } when)
        {
            foreach (var needed in selector.HostFactsNeeded)
                if (!_host.ContainsKey(needed)) throw new EngineRefusedException(CalendarCodes.FactMissing, "a routine reads the fact '" + needed + "', which the host did not supply");
            if (!Conditions.Evaluate(when, Facts)) return "condition is false: " + ConditionExplanation.Describe(when, Facts);
        }
        return null;
    }

    /// <summary>The routines of a schedule that apply to this day, highest priority first.</summary>
    public IReadOnlyList<RoutineDef> Applicable(ScheduleDef schedule)
    {
        var cached = _applicable[schedule.Index];
        if (cached is not null) return cached;
        var list = new List<RoutineDef>();
        foreach (var routine in schedule.Routines)
            if (routine.Selector is null || WhyNot(routine.Selector) is null) list.Add(routine);
        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return _applicable[schedule.Index] = list.ToArray();
    }

    internal static BlockDef? BlockAt(RoutineDef routine, int minute)
    {
        var blocks = routine.Blocks;
        int low = 0, high = blocks.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            if (minute < blocks[middle].From) high = middle - 1;
            else if (minute >= blocks[middle].To) low = middle + 1;
            else return blocks[middle];
        }
        return null;
    }

    public ScheduleDef Schedule(string id) =>
        Set.ScheduleById.TryGetValue(id, out var schedule) ? schedule : throw new EngineRefusedException(CalendarCodes.UnknownSchedule, "there is no schedule '" + id + "'");

    /// <summary>Where the owner of a schedule is, and what it does, at this moment. An interrupt wins over a routine of equal or lower priority.</summary>
    public ScheduleAnswer WhereIs(string schedule, string? interrupt = null)
    {
        var def = Schedule(schedule);
        var chosen = Choose(def, out var block);
        if (interrupt is not null)
        {
            if (!Set.InterruptById.TryGetValue(interrupt, out var kind)) throw new EngineRefusedException(CalendarCodes.UnknownInterrupt, "there is no interrupt '" + interrupt + "'");
            if (kind.Priority >= chosen.Priority) return new ScheduleAnswer(def.Id, kind.Location, kind.Activity, AnswerSource.Interrupt, kind.Id, "", kind.Priority, Moment.MinuteOfDay, Moment.MinuteOfDay);
        }
        return new ScheduleAnswer(def.Id, block.Location, block.Activity, AnswerSource.Routine, chosen.Id, block.Id, chosen.Priority, block.From, block.To);
    }

    internal RoutineDef Choose(ScheduleDef schedule, out BlockDef block)
    {
        foreach (var routine in Applicable(schedule))
        {
            var found = BlockAt(routine, Moment.MinuteOfDay);
            if (found is not null) { block = found; return routine; }
        }
        // The base routine applies every day and covers every minute (the loader checked both), so a schedule always has an answer.
        throw new InvalidOperationException("schedule '" + schedule.Id + "' has no block at minute " + Moment.MinuteOfDay + "; the loader should have refused it");
    }
}
