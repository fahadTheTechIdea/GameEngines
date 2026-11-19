using AIEngine;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// Tells AI agents what time it is, as a percept they perceive: knowledge, not truth. An agent does not read the calendar; it knows the time of day only when something tells it, and what it knows is the
/// percept it was last told (with the tick it was told at, so the AI document can let it go stale). The bridge posts the percept to the agents the host names for this moment (those who can see the sky or
/// hear the bell, as <c>perceivers</c> says), when what they would perceive of the sky changes (the part of the day, the day, the season, whether the sun is up, the light level) and once at the start. Which agents perceive is the host's decision; the bridge never posts to an agent
/// the host did not name.
/// </summary>
/// <remarks>
/// The AI document must declare the percept kind with exactly these attributes: <c>daypart</c> (string), <c>season</c> (string), <c>is_day</c> (bool) and <c>light</c> (int covering 0 to 1000). The
/// constructor checks that against the document and refuses otherwise, so a mismatch is found when the game is assembled, not as a refusal on every post.
/// Add the bridge to the session after the calendar component and before the AI component, so a change is perceived in the tick it happens. The light level moves a little every minute of twilight, so the agents are told then every minute; that is the news.
/// </remarks>
public sealed class CalendarAiBridge : IGameComponent
{
    public const string BadKind = "CALAI_PERCEPT_KIND";

    private readonly CalendarComponent _calendar;
    private readonly string _aiComponent;
    private readonly string _kind;
    private readonly string _perceptId;
    private readonly Func<IReadOnlyList<string>> _perceivers;
    private IEventPort? _events;
    private bool _changed = true;   // tell the agents once at the start
    private long _posted;
    private (long Day, int Part, int Season, bool IsDay, int Light) _told = (long.MinValue, -1, -1, false, -1);

    /// <param name="aiComponent">The id of the AI component whose agents are told.</param>
    /// <param name="aiSet">The AI document, to check the percept kind against.</param>
    /// <param name="kind">The percept kind that carries the time.</param>
    /// <param name="perceivers">The agents that can perceive the time now, in the order they are told.</param>
    public CalendarAiBridge(string id, CalendarComponent calendar, string aiComponent, AiSet aiSet, string kind, string perceptId, Func<IReadOnlyList<string>> perceivers)
    {
        ArgumentNullException.ThrowIfNull(aiSet);
        Id = id;
        ArgumentNullException.ThrowIfNull(calendar);
        _calendar = calendar;
        _aiComponent = aiComponent;
        _kind = kind;
        _perceptId = perceptId;
        ArgumentNullException.ThrowIfNull(perceivers);
        _perceivers = perceivers;
        Check(aiSet, kind);
    }

    public string Id { get; }

    /// <summary>How many percepts have been posted.</summary>
    public long Posted => _posted;

    private static void Check(AiSet set, string kind)
    {
        if (!set.TryPercept(kind, out var def)) throw new EngineRefusedException(BadKind, "the AI document has no percept kind '" + kind + "'");
        if (def.Owned) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' is owned, but the time belongs to no one");
        var expected = new (string Id, FactKind Kind)[] { ("daypart", FactKind.String), ("season", FactKind.String), ("is_day", FactKind.Boolean), ("light", FactKind.Integer) };
        if (def.Attrs.Count != expected.Length) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare exactly daypart, season, is_day and light");
        foreach (var (attr, type) in expected)
        {
            if (!def.TryAttr(attr, out var declared) || declared.Kind != type) throw new EngineRefusedException(BadKind, "percept kind '" + kind + "' must declare '" + attr + "' as " + type);
            if (type == FactKind.Integer && (declared.Minimum > 0 || declared.Maximum < 1000)) throw new EngineRefusedException(BadKind, "attribute '" + attr + "' of '" + kind + "' must allow 0 to 1000");
        }
    }

    /// <summary>Tells the perceivers again at the next Update, because who perceives has changed (an agent came outdoors, or a bell was built).</summary>
    public void Notify() => _changed = true;

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        if (_events is null) throw new InvalidOperationException("Update before Init");
        var events = _events;
        var set = _calendar.Runtime.Set;
        var moment = set.System.MomentOf(_calendar.Runtime.Instant);
        var season = set.SeasonOf(moment.Day);
        var light = set.DaylightAt(season, moment.MinuteOfDay);
        var part = set.DayPartOf(moment.MinuteOfDay);
        // What the agents would be told: the part of the day, the season, whether the sun is up and how bright it is. They are told again only when that changes (the sun rises or sets inside a part of the day).
        var state = (moment.Day.DayNumber, part, season, light.IsDay, light.LightPermille);
        var agents = _perceivers();
        if (agents.Count == 0) return;   // nobody perceives: nothing is told and nothing is marked as told, so the first who can is told at once
        if (!_changed && state == _told) return;
        _changed = false;
        _told = state;
        var attrs = AiValues.Encode(new[]
        {
            new KeyValuePair<string, FactValue>("daypart", FactValue.From(set.DayParts[part].Id)),
            new KeyValuePair<string, FactValue>("season", FactValue.From(set.Seasons[season].Id)),
            new KeyValuePair<string, FactValue>("is_day", FactValue.From(light.IsDay)),
            new KeyValuePair<string, FactValue>("light", FactValue.From((long)light.LightPermille)),
        });
        foreach (var agent in agents)
        {
            events.Emit(new AiPerceptPosted(_aiComponent, Id + ":" + context.Tick, agent, _kind, _perceptId, "", attrs));
            _posted++;
        }
    }
}
