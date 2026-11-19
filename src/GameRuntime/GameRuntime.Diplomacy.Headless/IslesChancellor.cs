using CalendarEngine;

namespace GameRuntime.Diplomacy.Headless;

/// <summary>
/// The player of the demo, who only ever asks with events: she is Cinderfell's queen (the one realm the host decides for), so she answers every offer put to Cinderfell, and she pursues her own business on set days: a
/// treaty, a marriage, a sabotage, an embargo. The other realms are the engine's own: they accept or refuse by their acceptance scores. What she knows of the isles she learned from their events.
/// </summary>
public sealed class IslesChancellor : IGameComponent
{
    private readonly List<(long Day, string Kind, string A, string B, string C, long Number, long Number2)> _script;
    private readonly List<DiplomacyRequested> _answers = new();
    private IEventPort? _events;
    private long _first = -1;
    private long _day;
    private long _handled;
    private long _asked;

    public IslesChancellor(IEnumerable<(long Day, string Kind, string A, string B, string C, long Number, long Number2)> script) => _script = script.ToList();

    public string Id => "chancellor";

    public long Asked => _asked;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarDayChanged>(e =>
        {
            if (_first < 0) _first = e.DayNumber;
            _day = e.DayNumber - _first + 1;
        });
        context.Events.Connect<DiplomacyProposalChanged>(e =>
        {
            if (e.To == "cinder" && e.Change == "made") _answers.Add(new DiplomacyRequested("chancellor:answer:" + e.Proposal, "respond", "", "", "", e.Proposal, 1));
        });
    }

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        foreach (var answer in _answers) Ask(answer);
        _answers.Clear();
        if (_day < 1 || _day == _handled) return;
        _handled = _day;
        foreach (var (_, kind, a, b, c, number, number2) in _script.Where(s => s.Day == _day)) Ask(new DiplomacyRequested("chancellor:" + _asked, kind, a, b, c, number, number2));
    }

    private void Ask(DiplomacyRequested request)
    {
        _asked++;
        _events!.Emit(request);
    }
}
