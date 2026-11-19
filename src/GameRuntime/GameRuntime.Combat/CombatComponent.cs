using System.Globalization;
using CombatEngine;

namespace GameRuntime;

/// <summary>
/// The combat rules as one game component. A request event (engagement, round, march, resupply) only queues; the component answers it
/// inside its own Update, in the order the requests arrived, using the tick of that Update and its own attempt counter as the only
/// sources of variation. It owns the state of every group and answers with events. Nothing about a fight changes outside Update.
/// </summary>
public sealed partial class CombatComponent(CombatResolver resolver, IReadOnlyList<Group> initial, ulong seed, int loggedShots = 0, string id = "combat") : IGameComponent
{
    public const string UnknownGroup = "COMBAT_UNKNOWN_GROUP";
    public const string SameGroup = "COMBAT_SAME_GROUP";

    private enum RequestKind { Engagement, Round, Move, Resupply, Reinforce, Refuel }

    /// <summary>One queued request. Exactly the member named by <see cref="Kind"/> is meaningful.</summary>
    private readonly record struct Pending(RequestKind Kind, string RequestId, EngagementRequested Engagement, RoundRequested Round, MoveRequested Move, ResupplyRequested Resupply, ReinforceRequested Reinforce, RefuelRequested Refuel);

    private readonly SortedDictionary<string, Group> _groups = new(initial.ToDictionary(g => g.Id, StringComparer.Ordinal), StringComparer.Ordinal);
    private readonly List<Pending> _pending = new();
    private IEventPort? _events;
    private long _lastTick;
    private long _attempt = 1;
    private long _engagements;
    private int _lastDistance = -1;

    public string Id { get; } = id;

    /// <summary>The state of each group by id, in id order.</summary>
    public IReadOnlyDictionary<string, Group> Groups => _groups;

    /// <summary>The roll attempt the next order will use. An order uses one attempt number and no number is used twice.</summary>
    public long Attempt => _attempt;

    /// <summary>How many engagements (orders) have been resolved, and the distance the last engagement or march left between the sides (-1 before the first).</summary>
    public long Engagements => _engagements;
    public int LastDistanceMetres => _lastDistance;

    /// <summary>True when no request is waiting for the next Update. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0;

    public string Fingerprint() => $"combat tick={_lastTick} state={StateHash.Of(_groups.Values)[..16]} attempt={_attempt} pending={_pending.Count}";

    /// <summary>Puts a verified checkpoint's state in place. Only valid before Init, on a component built from the same groups.</summary>
    public void Restore(IReadOnlyDictionary<string, Group> groups, long attempt, long engagements, int lastDistanceMetres)
    {
        foreach (var (key, group) in groups)
        {
            if (!_groups.ContainsKey(key)) throw new InvalidOperationException("the checkpoint names a group the session does not have: " + key);
            _groups[key] = group;
        }
        _attempt = attempt;
        _engagements = engagements;
        _lastDistance = lastDistanceMetres;
    }

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<EngagementRequested>(request => _pending.Add(new Pending(RequestKind.Engagement, request.RequestId, request, default, default, default, default, default)));
        context.Events.Connect<RoundRequested>(request => _pending.Add(new Pending(RequestKind.Round, request.RequestId, default, request, default, default, default, default)));
        context.Events.Connect<MoveRequested>(request => _pending.Add(new Pending(RequestKind.Move, request.RequestId, default, default, request, default, default, default)));
        context.Events.Connect<ResupplyRequested>(request => _pending.Add(new Pending(RequestKind.Resupply, request.RequestId, default, default, default, request, default, default)));
        context.Events.Connect<RefuelRequested>(request => _pending.Add(new Pending(RequestKind.Refuel, request.RequestId, default, default, default, default, default, request)));
        context.Events.Connect<ReinforceRequested>(request => _pending.Add(new Pending(RequestKind.Reinforce, request.RequestId, default, default, default, default, request, default)));
        context.Events.Emit(new ForcesRestored(string.Join(';', _groups.Values.Select(g =>
            string.Create(CultureInfo.InvariantCulture, $"{g.Id},{g.Side},{g.Health},{LongestRangeMetres(g)},{g.Morale},{g.SpeedMilli},{g.Standing}"))), _engagements, _lastDistance));
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _lastTick = context.Tick;
        if (_pending.Count == 0) return;
        var requests = _pending.ToArray();
        _pending.Clear();
        foreach (var request in requests) Answer(events, context.Tick, request);
    }

    private static long LongestRangeMetres(Group group) => group.Batteries.Count == 0 ? 0 : group.Batteries.Max(b => b.Weapon.RangeMilli) / 1000;

    private void Answer(IEventPort events, long tick, in Pending request)
    {

        try
        {
            switch (request.Kind)
            {
                case RequestKind.Engagement: AnswerEngagement(events, tick, request.Engagement); break;
                case RequestKind.Round: AnswerRound(events, tick, request.Round); break;
                case RequestKind.Move: AnswerMove(events, tick, request.Move); break;
                case RequestKind.Resupply: AnswerResupply(events, tick, request.Resupply); break;
                case RequestKind.Reinforce: AnswerReinforce(events, tick, request.Reinforce); break;
                case RequestKind.Refuel: AnswerRefuel(events, tick, request.Refuel); break;
            }
        }
        catch (CombatRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the state is untouched.
            events.Emit(new EngagementRefused(request.RequestId, tick, refused.Code));
        }
    }
}
