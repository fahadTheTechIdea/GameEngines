using System.Text.Json;
using System.Text.RegularExpressions;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class TutorialReader
{
    public const int MaxSteps = 1000;
    public const int MaxTips = 256;
    public const int MaxEvents = 128;
    public const int MaxSkills = 64;
    public const int MaxHostFacts = 64;
    public const int MaxPages = 32;
    public const int MaxLanguages = 64;
    public const int MaxInputs = 64;
    public const int MaxHints = 4;
    public const int MaxRequires = 16;
    public const int MaxTipShows = 1000;
    public const int TimeCeiling = 1_000_000;

    /// <summary>The ways a player may enter once language and story are done; the document lists the ones its game offers.</summary>
    public static readonly IReadOnlyList<string> EntryKinds = ["sign-in", "register", "host", "join", "guest"];

    private static readonly Regex LanguageTag = new("^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _text;
    private readonly StrictJson _json = new(TutorialCodes.ForDocument);
    private readonly Conditions _conditions;
    private IReadOnlyDictionary<string, FactDefinition> _facts = new Dictionary<string, FactDefinition>();
    private IReadOnlyDictionary<string, FactBinding> _bindings = new Dictionary<string, FactBinding>();
    private IReadOnlyList<EventDef> _events = Array.Empty<EventDef>();
    private IReadOnlyList<SkillDef> _skills = Array.Empty<SkillDef>();
    private Dictionary<string, int> _stepIndex = new(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, Motion> Motions = new Dictionary<string, Motion>(StringComparer.Ordinal) { ["none"] = Motion.None, ["pulse"] = Motion.Pulse, ["slide"] = Motion.Slide };
    private static readonly IReadOnlyDictionary<string, HintKind> HintKinds = new Dictionary<string, HintKind>(StringComparer.Ordinal) { ["nudge"] = HintKind.Nudge, ["explain"] = HintKind.Explain, ["show"] = HintKind.Show };

    public TutorialReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public TutorialSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        var root = _json.Object(document.RootElement, "the document", ["format", "id", "time", "limits", "facts", "events", "inputs", "skills", "journey", "steps", "tips"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != TutorialLoader.Format) throw _json.Refuse(TutorialCodes.Format, "this reader reads format " + TutorialLoader.Format + " only");
        var id = _json.Id(root, "id", "the document");
        var time = _json.Object(root.GetProperty("time"), "time", ["stepsPerDay"]);
        var stepsPerDay = _json.Int(time, "stepsPerDay", "time", 1, TimeCeiling);
        var limits = ReadLimits(root.GetProperty("limits"));
        var hostFacts = ReadHostFacts(_json.Array(root, "facts", "the document", 0, MaxHostFacts));
        _events = ReadEvents(_json.Array(root, "events", "the document", 0, MaxEvents));
        _skills = ReadSkills(_json.Array(root, "skills", "the document", 0, MaxSkills));
        var stepsArray = _json.Array(root, "steps", "the document", 1, MaxSteps);
        var tipsArray = _json.Array(root, "tips", "the document", 0, MaxTips);
        var stepIds = IdsOf(stepsArray, "steps");
        var tipIds = IdsOf(tipsArray, "tips");
        _stepIndex = stepIds.Select((s, i) => (s, i)).ToDictionary(p => p.s, p => p.i, StringComparer.Ordinal);
        (_facts, _bindings) = TutorialFacts.Build(_events.Select(e => e.Id).ToList(), _skills.Select(s => (s.Id, s.Mastered)).ToList(), stepIds, tipIds.Select(t => (t, MaxTipShows)).ToList(), hostFacts);
        var inputs = ReadInputs(_json.Array(root, "inputs", "the document", 0, MaxInputs));
        var steps = ReadSteps(stepsArray);
        var tips = ReadTips(tipsArray);
        var journey = ReadJourney(root.GetProperty("journey"));
        return new TutorialSet(id, Digest.Of(_text), stepsPerDay, limits, _events, inputs, _skills, steps, tips, journey, hostFacts, _facts, _bindings);
    }

    // ---- shared helpers -------------------------------------------------------------------------------------------------------------

    /// <summary>Reads a text field that must be one of a closed set of words, so an unknown word is refused at one place.</summary>
    private T Choice<T>(JsonElement owner, string field, string where, IReadOnlyDictionary<string, T> words)
    {
        var word = _json.Text(owner, field, where, 32);
        return words.TryGetValue(word, out var value) ? value : throw _json.Refuse(TutorialCodes.Format, where + "." + field + " '" + word + "' must be one of " + string.Join(", ", words.Keys));
    }

    /// <summary>The index a reference names, or a refusal that says what was not declared.</summary>
    private int Ref(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Id(owner, field, where);
        return declared.TryGetValue(id, out var index) ? index : throw _json.Refuse(TutorialCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
    }

    /// <summary>A reference that may be empty (nothing); returns -1 for nothing.</summary>
    private int OptionalRef(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Text(owner, field, where, 128);
        if (id.Length == 0) return -1;
        _json.CheckId(id, where + "." + field);
        return declared.TryGetValue(id, out var index) ? index : throw _json.Refuse(TutorialCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
    }

    private static Dictionary<string, int> Index<T>(IReadOnlyList<T> list, Func<T, string> id) => list.Select((item, i) => (id(item), i)).ToDictionary(p => p.Item1, p => p.i, StringComparer.Ordinal);

    private void Unique(HashSet<string> ids, string id, string what)
    {
        if (!ids.Add(id)) throw _json.Refuse(TutorialCodes.Duplicate, what + " '" + id + "' is declared twice");
    }

    /// <summary>The ids of a list of objects, checked and unique, so facts can be built before the objects are read whole.</summary>
    private List<string> IdsOf(JsonElement array, string what)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("id", out _)) throw _json.Refuse(TutorialCodes.Format, what + "[" + ids.Count + "] must be an object with an id");
            var id = _json.Id(element, "id", what + "[" + ids.Count + "]");
            Unique(seen, id, what.TrimEnd('s'));
            ids.Add(id);
        }
        return ids;
    }

    private LimitsDef ReadLimits(JsonElement element)
    {
        _json.Object(element, "limits", ["maxActiveSteps", "maxVisibleTips", "tipGap"]);
        return new LimitsDef(_json.Int(element, "maxActiveSteps", "limits", 1, 8), _json.Int(element, "maxVisibleTips", "limits", 1, 64), _json.Int(element, "tipGap", "limits", 0, TimeCeiling));
    }

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (TutorialFacts.IsReserved(id)) throw _json.Refuse(TutorialCodes.Reserved, where + " '" + id + "': the prefixes event., step., tip., skill., tutorial., journey. and setting. are the engine's own");
            if (facts.ContainsKey(id)) throw _json.Refuse(TutorialCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(TutorialCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(TutorialCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(TutorialCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(TutorialCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    private IReadOnlyList<EventDef> ReadEvents(JsonElement array)
    {
        var events = new List<EventDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "events[" + events.Count + "]";
            _json.Object(element, where, ["id"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "event");
            events.Add(new EventDef(events.Count, id));
        }
        return events;
    }

    private IReadOnlyList<InputDef> ReadInputs(JsonElement array)
    {
        var inputs = new List<InputDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var events = Index(_events, e => e.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "inputs[" + inputs.Count + "]";
            _json.Object(element, where, ["action", "event"]);
            var action = _json.Id(element, "action", where);
            if (action.StartsWith("tutorial.", StringComparison.Ordinal) || action.StartsWith("journey.", StringComparison.Ordinal))
                throw _json.Refuse(TutorialCodes.Reserved, where + " '" + action + "': the actions tutorial. and journey. are the engine's own controls");
            Unique(seen, action, "input action");
            inputs.Add(new InputDef(action, Ref(element, "event", where, events, "event")));
        }
        return inputs;
    }

    private IReadOnlyList<SkillDef> ReadSkills(JsonElement array)
    {
        var skills = new List<SkillDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var events = Index(_events, e => e.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "skills[" + skills.Count + "]";
            _json.Object(element, where, ["id", "key", "mastered", "evidence"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "skill");
            var evidence = new List<EvidenceDef>();
            var used = new HashSet<int>();
            foreach (var item in _json.Array(element, "evidence", where, 1, 16).EnumerateArray())
            {
                var at = where + ".evidence[" + evidence.Count + "]";
                _json.Object(item, at, ["event", "points"]);
                var e = Ref(item, "event", at, events, "event");
                if (!used.Add(e)) throw _json.Refuse(TutorialCodes.Skill, at + " repeats event '" + _events[e].Id + "'");
                evidence.Add(new EvidenceDef(e, _json.Int(item, "points", at, 1, 1000)));
            }
            skills.Add(new SkillDef(skills.Count, id, _json.Id(element, "key", where), _json.Int(element, "mastered", where, 1, 1_000_000), evidence));
        }
        return skills;
    }

    /// <summary>The facts a condition reads that mean "since the step began": legal only in a step's completion.</summary>
    private List<string> SinceFacts(Condition condition) => Conditions.FactsOf(condition).Where(f => _bindings[f].Source == FactSource.EventSince).ToList();

    private Condition ReadScoped(JsonElement element, string where)
    {
        var condition = _conditions.Read(element, where, _facts);
        var since = SinceFacts(condition);
        if (since.Count > 0) throw _json.Refuse(TutorialCodes.Scope, where + " reads '" + since[0] + "', which only a step's completion condition may read");
        return condition;
    }
}
