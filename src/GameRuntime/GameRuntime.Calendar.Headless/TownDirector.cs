using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime.Calendar.Headless;

/// <summary>
/// The game around the calendar: it puts five villagers on two schedules at the start, rings a storm bell on the third day, has the player sleep through the night of the fifth, and decides each morning
/// whether it rains (a seeded roll per day, so the same seed gives the same weather). It only asks the calendar component for things with events and tells it the weather through the host-facts delegate.
/// </summary>
public sealed class TownDirector(ulong seed, long stormTick, long sleepTick) : IGameComponent
{
    public static readonly IReadOnlyList<(string Actor, string Schedule)> Villagers =
        [("mira", "scholar"), ("tomas", "scholar"), ("oleg", "fisher"), ("pia", "fisher"), ("quill", "scholar")];

    private IEventPort? _events;
    private readonly Dictionary<string, FactValue> _weather = new(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false) };

    public string Id => "town-director";

    /// <summary>The host facts for the calendar: today's weather.</summary>
    public IReadOnlyDictionary<string, FactValue> Weather => _weather;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        // A new day brings new weather. The calendar reads it in its next Update, so the schedules change one tick after midnight.
        context.Events.Connect<CalendarDayChanged>(e => _weather["weather.raining"] = FactValue.From(DeterministicRoll.Permille(seed, "rain", e.DayNumber) < 300));
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        if (context.Tick == 1)
            foreach (var (actor, schedule) in Villagers) events.Emit(new CalendarActorAddRequested("add-" + actor, actor, schedule));
        if (context.Tick == stormTick) events.Emit(new CalendarInterruptRequested("storm-mira", "mira", "storm-shelter", 120));
        if (context.Tick == sleepTick) events.Emit(new CalendarSkipRequested("sleep", 1, 6, 0));
        if (context.Tick == sleepTick + 1) events.Emit(new CalendarWhereRequested("where-oleg", "oleg"));
        if (context.Tick == sleepTick + 2) events.Emit(new CalendarSkipRequested("sleep-too-far", 400, 6, 0));
    }
}
