using GameRuntime.Field.Headless;

namespace GameRuntime.Briefing.Headless;

/// <summary>
/// The player of the briefing. It asks the dialogue component for the conversation, goes on after each line, takes the answer it was given when the choice comes, and lets the
/// squads fire only after the answer that says so. While the quest runs it asks the dialogue component to look again each time the one fact the quest watches changes (how many foes still stand).
/// Everything it does is a request event answered in the dialogue component's next Update.
/// </summary>
public sealed class BriefingDirector(string answer, Func<long> foes, string id = "director") : IGameComponent
{
    private long _asked = -1;

    private IEventPort? _events;
    private bool _started;
    private bool _advance;
    private bool _choose;
    private bool _questOpen;
    private int _request;

    public string Id { get; } = id;

    /// <summary>True once the briefing said the squads may fire. The field game asks this before its commanders may order a round.</summary>
    public bool Authorised { get; private set; }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<DialogueLineShown>(_ => _advance = true);
        context.Events.Connect<DialogueChoicesShown>(_ => _choose = true);
        context.Events.Connect<DialogueSignalRaised>(signal => { if (signal.Name == "fire-at-will") Authorised = true; });
        context.Events.Connect<DialogueQuestChanged>(quest => _questOpen = quest.Status == "active");
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        var id = "d" + (_request++).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!_started)
        {
            _started = true;
            events.Emit(new DialogueStartRequested(id, "briefing"));
        }
        else if (_advance)
        {
            _advance = false;
            events.Emit(new DialogueAdvanceRequested(id));
        }
        else if (_choose)
        {
            _choose = false;
            events.Emit(new DialogueChooseRequested(id, answer));
        }
        else if (_questOpen && foes() != _asked)
        {
            // The one fact the quest watches changed: ask the quest to look again.
            _asked = foes();
            events.Emit(new DialogueQuestsUpdateRequested(id));
        }
    }
}