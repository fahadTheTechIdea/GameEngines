using System.Globalization;
using EconomyEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>One rule: when the <paramref name="Trigger"/> date comes, settle the manual schedule. A trigger is <c>weekday:&lt;id&gt;</c> (a weekday of the calendar: pay day on a Friday), <c>day:&lt;n&gt;</c> (a day of the month: tax day on the 15th) or <c>holiday:&lt;id&gt;</c> (a holiday began).</summary>
public sealed record EconomyDateRule(string Trigger, string Schedule);

/// <summary>
/// Pay days and tax dates on the calendar's own dates. The economy document declares a schedule <c>manual</c> when its date belongs to the calendar; this component listens to the calendar's day and holiday events and
/// asks the economy to settle that schedule, as a request (deferred, like every request), never a call. The calendar announces a day once however many days a skip crossed, so a date inside a skipped stretch
/// is not seen: a game that lets time skip and still wants its pay days settles those schedules itself.
/// </summary>
/// <remarks>Add it to the session after the calendar component and before the economy component, so a date that comes in this tick is settled in this tick.</remarks>
public sealed class EconomyCalendarDates : IGameComponent
{
    public const string BadRule = "ECON_DATE_RULE";

    private readonly Dictionary<string, string> _rules;
    private readonly string _calendarChannel;
    private readonly string _economyChannel;
    private IEventPort? _events;
    private long _requested;

    /// <param name="economy">The economy document: every schedule a rule names must be a manual schedule of it.</param>
    /// <param name="calendarChannel">The channel of the calendar component whose events are listened to (empty for the only calendar).</param>
    /// <param name="economyChannel">The channel of the economy component the requests go to.</param>
    public EconomyCalendarDates(string id, EconomySet economy, IEnumerable<EconomyDateRule> rules, string calendarChannel = "", string economyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        _rules = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            CheckTrigger(rule.Trigger);
            if (!economy.ScheduleById.TryGetValue(rule.Schedule, out var schedule)) throw new EngineRefusedException(BadRule, "rule '" + rule.Trigger + "' names the schedule '" + rule.Schedule + "', which the economy document does not have");
            if (!schedule.Manual) throw new EngineRefusedException(BadRule, "schedule '" + rule.Schedule + "' runs by itself; only a manual schedule takes its date from the calendar");
            if (!_rules.TryAdd(rule.Trigger, rule.Schedule)) throw new EngineRefusedException(BadRule, "trigger '" + rule.Trigger + "' has two rules");
        }
        _calendarChannel = calendarChannel;
        _economyChannel = economyChannel;
    }

    public string Id { get; }

    /// <summary>How many settlements have been requested.</summary>
    public long Requested => _requested;

    private static void CheckTrigger(string trigger)
    {
        var colon = trigger.IndexOf(':', StringComparison.Ordinal);
        var kind = colon < 0 ? "" : trigger[..colon];
        var value = colon < 0 ? "" : trigger[(colon + 1)..];
        var shaped = kind switch
        {
            "weekday" or "holiday" => value.Length > 0 && value.All(c => c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c is '-' or '.' or '_'),
            "day" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var day) && day >= 1,
            _ => false,
        };
        if (!shaped) throw new EngineRefusedException(BadRule, "trigger '" + trigger + "' must be weekday:<id>, day:<number> or holiday:<id>");
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarDayChanged>(d =>
        {
            if (d.Channel != _calendarChannel) return;
            if (d.Weekday.Length > 0) Triggered("weekday:" + d.Weekday, d.Tick, d.DayNumber);
            Triggered("day:" + d.Day.ToString(CultureInfo.InvariantCulture), d.Tick, d.DayNumber);
        });
        context.Events.Connect<CalendarHolidayStarted>(h =>
        {
            if (h.Channel == _calendarChannel) Triggered("holiday:" + h.Holiday, h.Tick, h.Year * 1000 + h.Day);
        });
    }

    private void Triggered(string trigger, long tick, long dayKey)
    {
        if (!_rules.TryGetValue(trigger, out var schedule)) return;
        _events!.Emit(new EconomySettleRequested(Id + ":" + tick + ":" + trigger + ":" + dayKey, schedule, _economyChannel));
        _requested++;
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the calendar events.
    }
}
