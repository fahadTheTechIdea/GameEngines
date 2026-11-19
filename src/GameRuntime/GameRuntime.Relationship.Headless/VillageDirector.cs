using CalendarEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

namespace GameRuntime.Relationship.Headless;

/// <summary>
/// The game around the village: it puts six villagers on two calendar schedules and into the relationships at the start, decides each morning whether it rains (a seeded roll per day), invites the guards to the
/// square on Sunday noon (a calendar interrupt, so shopkeepers and guards meet), and each hour lets every villager who stands with others pick what it most wants to do to one of them (the highest desire among the
/// interactions the relationship engine says are available) and ask for it. It only ever asks with events; what it knows of the world it learned from the calendar's events and from the relationship runtime's
/// read-only answers.
/// </summary>
public sealed class VillageDirector : IGameComponent
{
    public static readonly IReadOnlyList<(string Actor, string Schedule, string Traits, string Tastes)> Villagers =
    [
        ("alice", "shopkeeper", "kind", "flower=2"), ("bob", "shopkeeper", "glutton;grumpy", "food=-1"), ("cara", "shopkeeper", "sociable", ""),
        ("dan", "guard", "stoic", "junk=1"), ("eve", "guard", "kind;sociable", "pretty=1"), ("fay", "guard", "grumpy", "sweet=2"),
    ];

    private static readonly string[] Gifts = ["flowers", "pie", "stone"];

    private readonly ulong _seed;
    private readonly RelationshipComponent _relationships;
    private readonly CalendarComponent _calendar;
    private readonly SortedDictionary<string, string> _where = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FactValue> _weather = new(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
    private IEventPort? _events;
    private long _lastHour = -1;
    private long _invitedDay = -1;
    private bool _sunday;
    private long _requested;

    public VillageDirector(ulong seed, RelationshipComponent relationships, CalendarComponent calendar)
    {
        _seed = seed;
        _relationships = relationships;
        _calendar = calendar;
    }

    public string Id => "village-director";

    /// <summary>The host facts for the calendar: today's weather.</summary>
    public IReadOnlyDictionary<string, FactValue> Weather => _weather;

    /// <summary>How many interactions the villagers have asked for.</summary>
    public long Requested => _requested;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarScheduleBlockChanged>(e => _where[e.Actor] = e.Location);
        context.Events.Connect<CalendarDayChanged>(e =>
        {
            _weather["weather.raining"] = FactValue.From(DeterministicRoll.Permille(_seed, "rain", e.DayNumber) < 300);
            _sunday = e.Weekday == "sunday";
        });
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        if (context.Tick == 1)
            foreach (var (actor, schedule, traits, tastes) in Villagers)
            {
                events.Emit(new CalendarActorAddRequested("calendar-" + actor, actor, schedule));
                events.Emit(new RelationshipActorAddRequested("village-" + actor, actor, traits, tastes, _relationships.Channel));
            }
        var set = _calendar.Runtime.Set;
        var instant = _calendar.Runtime.Instant;
        var moment = set.System.MomentOf(instant);
        var hour = instant / set.Time.TicksPerMinute / set.Time.MinutesPerHour;
        if (hour == _lastHour || context.Tick < 5) return;
        _lastHour = hour;
        var dayNumber = moment.Day.DayNumber;
        if (_sunday && moment.Hour == 12 && _invitedDay != dayNumber)
        {
            _invitedDay = dayNumber;
            foreach (var (actor, schedule, _, _) in Villagers)
                if (schedule == "guard") events.Emit(new CalendarInterruptRequested("invite-" + actor + "-" + dayNumber, actor, "invitation", 6L * set.Time.MinutesPerHour * set.Time.TicksPerMinute));
        }
        Socialise(events, context.Tick, dayNumber, moment.Hour);
    }

    /// <summary>Villagers who stand in the same place (not at home or on watch) meet; each in id order asks for the interaction it wants most with one of the others.</summary>
    private void Socialise(IEventPort events, long tick, long dayNumber, int hour)
    {
        var runtime = _relationships.Runtime;
        var facts = _calendar.Facts();
        var busy = new HashSet<string>(StringComparer.Ordinal);
        var meeting = _where.Where(w => w.Value is not ("home" or "guardhouse") && runtime.Actors.ContainsKey(w.Key)).GroupBy(w => w.Value).Where(g => g.Count() > 1).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        foreach (var place in meeting)
        {
            var present = place.Select(p => p.Key).OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (var actor in present)
            {
                if (!busy.Add(actor)) continue;
                var best = (Score: 0L, Interaction: "", Target: "");
                foreach (var other in present.Where(o => o != actor && !busy.Contains(o)))
                    foreach (var offer in runtime.Offers(actor, other, facts))
                        if (offer.Available && offer.Desire > best.Score) best = (offer.Desire, offer.Interaction, other);
                if (best.Interaction.Length == 0) { busy.Remove(actor); continue; }
                busy.Add(best.Target);
                var interaction = runtime.Set.InteractionById[best.Interaction];
                var gift = interaction.Gift ? Gifts[(int)((dayNumber + hour) % Gifts.Length)] : "";
                var witnesses = string.Join(';', present.Where(p => p != actor && p != best.Target).Take(interaction.Witness?.Max ?? 0));
                events.Emit(new RelationshipInteractRequested("meet-" + tick + "-" + actor, best.Interaction, actor, best.Target, interaction.Witness is null ? "" : witnesses, gift, _relationships.Channel));
                _requested++;
            }
        }
    }
}
