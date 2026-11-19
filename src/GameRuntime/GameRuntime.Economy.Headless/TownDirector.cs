using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime.Economy.Headless;

/// <summary>
/// The game around the three towns: it decides each morning whether it rains and whether the plague is about (days 50 to 58), lets a caravan trade by the player on some days (a seeded roll picks the day, the
/// town, the good and the amount), and plays the story: a drought in Riverton on day 20, the central bank of the florin printing more on day 30, the florin devalued on day 40, and a loan from the world to
/// Hillfort on day 60. It only ever asks with events; what it knows of the world it learned from the calendar's day events.
/// </summary>
public sealed class TownDirector : IGameComponent
{
    private static readonly string[] Markets = ["riverton", "hillfort", "seaport"];
    private static readonly string[] Goods = ["grain", "cloth", "iron", "ale"];

    private readonly ulong _seed;
    private readonly Dictionary<string, FactValue> _weather = new(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
    private IEventPort? _events;
    private long _day = -1;
    private long _handled = -1;
    private long _requested;
    private long _firstDay = -1;

    public TownDirector(ulong seed) => _seed = seed;

    public string Id => "town-director";

    /// <summary>The host facts for the calendar: today's weather and the plague.</summary>
    public IReadOnlyDictionary<string, FactValue> Weather => _weather;

    /// <summary>How many requests the director has made.</summary>
    public long Requested => _requested;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarDayChanged>(e =>
        {
            if (_firstDay < 0) _firstDay = e.DayNumber;
            _day = e.DayNumber - _firstDay + 1;
            _weather["weather.raining"] = FactValue.From(DeterministicRoll.Permille(_seed, "rain", e.DayNumber) < 300);
            _weather["story.plague"] = FactValue.From(_day is >= 50 and <= 58 ? 2L : 0L);
        });
    }

    private void Ask<T>(string what, Func<string, T> make) where T : struct
    {
        _requested++;
        var id = "director:" + _day + ":" + what;
        _events!.Emit(make(id));
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        if (_day < 1 || _day == _handled) return;
        _handled = _day;
        var day = _day;
        if (DeterministicRoll.Permille(_seed, "caravan", day) < 280)
        {
            var market = Markets[DeterministicRoll.Below(_seed, "caravan-market", day, Markets.Length)];
            var good = Goods[DeterministicRoll.Below(_seed, "caravan-good", day, Goods.Length)];
            var quantity = 20 + DeterministicRoll.Below(_seed, "caravan-quantity", day, 60);
            var buy = DeterministicRoll.Permille(_seed, "caravan-side", day) < 600;
            Ask("caravan", id => new EconomyTradeRequested(id, "caravan", market, good, buy ? quantity : -quantity));
        }
        if (day == 20) Ask("drought", id => new EconomyShockRequested(id, "drought", "riverton"));
        if (day == 30) Ask("press", id => new EconomyGrowthRequested(id, "florin", 3000));
        if (day == 35) Ask("discovery", id => new EconomyRecipeUnlockRequested(id, "alloy-steel"));
        if (day == 40) Ask("devaluation", id => new EconomyShockRequested(id, "devaluation", "seaport"));
        if (day == 60) Ask("loan", id => new EconomyLendRequested(id, "relief", "world", "hillfort", 8000, 4000, 6, "bullet", "taxday", 2));
        if (day == 75) Ask("blockade", id => new EconomyShockRequested(id, "blockade", ""));
    }
}
