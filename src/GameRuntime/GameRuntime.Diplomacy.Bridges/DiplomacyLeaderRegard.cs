using DiplomacyEngine;
using EngineCommon;
using RelationshipEngine;

namespace GameRuntime;

/// <summary>
/// The leaders' own feelings, carried into the diplomacy. A realm's document names its leader; the relationship layer holds what each leader thinks of the others on an axis (affinity, trust). This bridge reads the
/// opinion of each leader of each other leader on the chosen axis, stretches the axis to the diplomacy's minus 1,000 to 1,000, and tells the diplomacy (a request, so deferred and answered in its next Update) whenever
/// one has changed since it was last told; the diplomacy weighs it in a realm's regard of another by the document's weight. The relationship layer never learns a diplomacy exists and the diplomacy never reads a
/// relationship. It looks again only when the relationship layer has lived a step or answered a request since the last look, so a quiet relationship layer costs nothing. A leader the relationship layer does not know yet is
/// left out until it does.
/// </summary>
public sealed class DiplomacyLeaderRegard : IGameComponent
{
    public const string BadAxis = "DIP_LEADER_AXIS";

    private readonly DiplomacyComponent _diplomacy;
    private readonly RelationshipComponent _relationships;
    private readonly int _axis;
    private readonly int _axisMin;
    private readonly int _axisMax;
    private readonly string _diplomacyChannel;
    private readonly Dictionary<(int, int), int> _told = new();
    private (long Step, long Answered) _seen = (long.MinValue, -1);
    private IEventPort? _events;
    private long _sent;

    /// <param name="axis">The relationship axis whose opinion is the leader's regard (an id of the relationship document).</param>
    public DiplomacyLeaderRegard(string id, DiplomacyComponent diplomacy, RelationshipComponent relationships, string axis, string diplomacyChannel = "")
    {
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(relationships);
        Id = id;
        if (!relationships.Runtime.Set.AxisById.TryGetValue(axis, out var def)) throw new EngineRefusedException(BadAxis, "the relationship document has no axis '" + axis + "'");
        if (def.Max <= def.Min) throw new EngineRefusedException(BadAxis, "axis '" + axis + "' has no range to stretch");
        if (!diplomacy.Runtime.Set.Realms.Any(r => r.Leader.Length > 0)) throw new EngineRefusedException(BadAxis, "no realm of the diplomacy document names a leader");
        _diplomacy = diplomacy;
        _relationships = relationships;
        _axis = def.Index;
        _axisMin = (int)def.Min;
        _axisMax = (int)def.Max;
        _diplomacyChannel = diplomacyChannel;
    }

    public string Id { get; }

    /// <summary>How many leader's regards have been sent.</summary>
    public long Sent => _sent;

    /// <summary>An opinion on the axis, stretched from the axis to minus 1,000 to 1,000 round the middle of the axis.</summary>
    public static int Stretch(long opinion, long minimum, long maximum) => (int)Math.Clamp((opinion * 2 - (minimum + maximum)) * 1000 / (maximum - minimum), -1000, 1000);

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        var runtime = _relationships.Runtime;
        var now = (runtime.Step, _relationships.Answered);
        if (now == _seen) return;
        _seen = now;
        var realms = _diplomacy.Runtime.Set.Realms;
        foreach (var holder in realms)
        {
            if (holder.Leader.Length == 0 || !runtime.Actors.ContainsKey(holder.Leader)) continue;
            foreach (var subject in realms)
            {
                if (subject.Index == holder.Index || subject.Leader.Length == 0 || !runtime.Actors.ContainsKey(subject.Leader)) continue;
                var regard = Stretch(runtime.Opinion(holder.Leader, subject.Leader, _axis), _axisMin, _axisMax);
                if (_told.TryGetValue((holder.Index, subject.Index), out var before) ? before == regard : regard == 0) continue;
                _told[(holder.Index, subject.Index)] = regard;
                events.Emit(new DiplomacyRequested(Id + ":" + context.Tick + ":" + _sent, DiplomacyRequestNames.NameOf(DiplomacyRequestKind.Leader), holder.Id, subject.Id, "", regard, 0, _diplomacyChannel));
                _sent++;
            }
        }
    }
}
