using RelationshipEngine;

namespace GameRuntime;

/// <summary>
/// The calendar's schedule satisfies needs: when a schedule block begins for an actor, the actor is doing that block's activity, and the relationship component is told so with a request (deferred, like every
/// request), so from the next step on the need sources of that activity feed the actor. A block that begins before the relationship component knows the actor is not lost: the latest one is kept and forwarded
/// the Update after the actor is known. The calendar says where an actor is expected to be; whether it is doing it is the game's business, so a game that lets an actor miss its block simply does not forward that change.
/// </summary>
/// <remarks>Add it to the session after the calendar component and before the relationship component, so a block that begins in this tick is answered in this tick.</remarks>
public sealed class RelationshipCalendarActivities : IGameComponent
{
    /// <summary>The most actors whose block can be waiting for the relationship component to know them; beyond it the block of a further unknown actor is dropped, which a game with that many unknown actors has bigger problems than.</summary>
    public const int MaxWaiting = 100_000;

    private readonly RelationshipComponent _relationships;
    private readonly string _calendarChannel;
    private readonly SortedDictionary<string, (string Activity, long Tick)> _waiting = new(StringComparer.Ordinal);
    private IEventPort? _events;
    private long _forwarded;
    private long _dropped;

    /// <param name="relationships">The relationship component whose actors are told.</param>
    /// <param name="calendarChannel">The channel of the calendar component whose blocks are forwarded (empty for the only calendar).</param>
    public RelationshipCalendarActivities(string id, RelationshipComponent relationships, string calendarChannel = "")
    {
        Id = id;
        ArgumentNullException.ThrowIfNull(relationships);
        _relationships = relationships;
        _calendarChannel = calendarChannel;
    }

    public string Id { get; }

    /// <summary>How many block changes were forwarded as activity requests.</summary>
    public long Forwarded => _forwarded;

    /// <summary>How many actors have a block waiting for the relationship component to know them.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>How many block changes were dropped because <see cref="MaxWaiting"/> actors were already waiting.</summary>
    public long Dropped => _dropped;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CalendarScheduleBlockChanged>(Changed);
    }

    private void Changed(CalendarScheduleBlockChanged change)
    {
        if (change.Channel != _calendarChannel) return;
        if (_relationships.Runtime.Actors.ContainsKey(change.Actor)) Forward(change.Actor, change.Activity, change.Tick);
        else if (_waiting.ContainsKey(change.Actor) || _waiting.Count < MaxWaiting) _waiting[change.Actor] = (change.Activity, change.Tick);
        else _dropped++;
    }

    private void Forward(string actor, string activity, long tick)
    {
        _events!.Emit(new RelationshipActivityRequested(Id + ":" + tick + ":" + actor, actor, activity, _relationships.Channel));
        _forwarded++;
    }

    public void Update(in UpdateContext context)
    {
        if (_waiting.Count == 0) return;
        foreach (var actor in _waiting.Keys.ToList())
        {
            if (!_relationships.Runtime.Actors.ContainsKey(actor)) continue;
            var (activity, tick) = _waiting[actor];
            _waiting.Remove(actor);
            Forward(actor, activity, tick);
        }
    }
}
