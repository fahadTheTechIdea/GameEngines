namespace GameRuntime.Alchemy.Headless;

/// <summary>The game's ending: when every element of the content has been discovered the game is won. It learns what is known only from events.</summary>
public sealed class AlchemyGoal(int totalElements, string id = "goal") : IGameComponent
{
    public const string EndingId = "all-discovered";

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private bool _ended;

    public string Id { get; } = id;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<KnowledgeRestored>(restored =>
        {
            foreach (var element in restored.Elements.Split(',', StringSplitOptions.RemoveEmptyEntries)) _known.Add(element);
        });
        context.Events.Connect<DiscoveryMade>(made => { if (made.Kind == "element") _known.Add(made.Id); });
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (_ended || _known.Count < totalElements) return;
        _ended = true;
        events.Emit(new SessionEnded(context.Tick, EndingId));
    }
}
