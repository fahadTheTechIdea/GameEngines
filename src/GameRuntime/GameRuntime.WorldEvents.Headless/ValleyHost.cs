using CalendarEngine;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime.WorldEvents.Headless;

/// <summary>
/// What the demo's game keeps that the layers read as host facts: how strong the garrison is (the world's raiders only come to a weak one), and, for the calendar and the economy, whether it is raining in Riverton and
/// whether the plague is about (0 none, 1 a few cases, 2 an outbreak): the economy document reads those two through the calendar, so the weather and the plague the world made reach the markets through the host.
/// </summary>
public sealed class ValleyHost
{
    public long Garrison { get; set; } = 30;

    /// <summary>The world the host reads; set once it exists.</summary>
    public WorldRuntime? World { get; set; }

    public IReadOnlyDictionary<string, FactValue> Game() => new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["game.garrison"] = FactValue.From(Garrison) };

    public IReadOnlyDictionary<string, FactValue> CalendarHost()
    {
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        if (World is null) return facts;
        var set = World.Set;
        var weather = set.Weathers[World.Regions[set.RegionById["riverton"].Index].Weather].Id;
        facts["weather.raining"] = FactValue.From(weather is "rain" or "storm");
        var plague = set.DiseaseById["plague"].Index;
        var worst = World.Regions.Max(r => (long)r.Outbreak[plague]);
        facts["story.plague"] = FactValue.From(worst);
        return facts;
    }
}

/// <summary>
/// The player of the demo, who only ever asks with events: she sends the plague hospital to a region the moment an outbreak is announced and takes it away when the outbreak is contained, moves the garrison in on
/// day 150, asks for peace (the Phoebe storyteller) on day 200 and for the hard difficulty on day 300. What she knows of the world she learned from its events.
/// </summary>
public sealed class ValleyPlayer : IGameComponent
{
    private readonly ValleyHost _host;
    private IEventPort? _events;
    private long _first = -1;
    private long _day;
    private long _handled;
    private long _asked;
    private readonly bool _scripted;

    public ValleyPlayer(ValleyHost host, bool scripted = true)
    {
        _host = host;
        _scripted = scripted;
    }

    public string Id => "valley-player";

    public long Asked => _asked;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarDayChanged>(e =>
        {
            if (_first < 0) _first = e.DayNumber;
            _day = e.DayNumber - _first + 1;
        });
        context.Events.Connect<WorldOutbreakChanged>(e =>
        {
            if (e.Disease != "plague") return;
            if (e.Change == "outbreak") Ask("hospital-" + e.Region, new WorldRequested("player:" + _asked, "intervene", e.Region, "hospital", "", 1));
            else if (e.Change is "contained" or "ended") Ask("hospital-off-" + e.Region, new WorldRequested("player:" + _asked, "intervene", e.Region, "hospital", "", 0));
        });
    }

    private void Ask(string what, WorldRequested request)
    {
        _asked++;
        _events!.Emit(request);
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        if (!_scripted || _day < 1 || _day == _handled) return;
        _handled = _day;
        if (_day == 60) Ask("weather", new WorldRequested("player:" + _asked, "weather", "riverton"));
        if (_day == 150) _host.Garrison = 80;
        if (_day == 200) Ask("peace", new WorldRequested("player:" + _asked, "storyteller", "phoebe"));
        if (_day == 300) Ask("hard", new WorldRequested("player:" + _asked, "difficulty", "hard"));
    }
}
