using System.Globalization;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// The game's ending: when at most one side still has a group standing (neither routed nor destroyed) the fight is over; that side
/// has won, or none is left and it is mutual. It learns who stands from <see cref="ForcesRestored"/> and <see cref="Standing"/> events
/// only and never reads the combat component.
/// </summary>
public sealed class SkirmishGoal(string id = "goal") : IGameComponent
{
    private readonly Dictionary<string, int> _sides = new(StringComparer.Ordinal);
    private readonly HashSet<string> _out = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private bool _ended;

    public string Id { get; } = id;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<ForcesRestored>(restored =>
        {
            foreach (var text in restored.Groups.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = text.Split(',');
                _sides[fields[0]] = int.Parse(fields[1], CultureInfo.InvariantCulture);
                if (fields[6] is "routed" or "destroyed") _out.Add(fields[0]);
            }
        });
        context.Events.Connect<Standing>(standing => { if (standing.State is "routed" or "destroyed") _out.Add(standing.Group); });
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (_ended || _out.Count == 0) return;
        var standing = _sides.Where(s => !_out.Contains(s.Key)).Select(s => s.Value).Distinct().ToArray();
        if (standing.Length > 1) return;
        _ended = true;
        events.Emit(new SessionEnded(context.Tick, standing.Length == 1 ? "side-" + standing[0] + "-wins" : "mutual"));
    }
}
