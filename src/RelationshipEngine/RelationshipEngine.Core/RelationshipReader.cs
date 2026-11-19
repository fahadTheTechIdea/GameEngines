using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace RelationshipEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class RelationshipReader
{
    public const int MaxActorsCeiling = 100_000;
    public const int MaxEdgesCeiling = 256;
    public const int MaxMemoriesCeiling = 64;
    public const int MaxMoodletsCeiling = 64;
    public const long Bound = 1_000_000;
    private const long FormatBeforeSocial = 1;
    private const long FormatBeforeSchedule = 2;

    private readonly string _text;
    private readonly StrictJson _json = new(RelationshipCodes.ForDocument);
    private readonly Conditions _conditions;
    private readonly IReadOnlyDictionary<string, Func<JsonElement, string, EffectDef>> _effectReaders;

    private int _stepsPerDay;
    private IReadOnlyDictionary<string, FactDefinition> _hostFacts = new Dictionary<string, FactDefinition>();
    private IReadOnlyList<AxisDef> _axes = Array.Empty<AxisDef>();
    private IReadOnlyList<NeedDef> _needs = Array.Empty<NeedDef>();
    private IReadOnlyList<MoodletDef> _moodlets = Array.Empty<MoodletDef>();
    private IReadOnlyList<MemoryDef> _memories = Array.Empty<MemoryDef>();
    private IReadOnlyList<TraitDef> _traits = Array.Empty<TraitDef>();
    private IReadOnlyList<LadderDef> _ladders = Array.Empty<LadderDef>();
    private IReadOnlyList<BondDef> _bonds = Array.Empty<BondDef>();
    private IReadOnlyList<GiftDef> _gifts = Array.Empty<GiftDef>();
    private MoodDef _mood = null!;
    private LimitsDef _limits = null!;

    public RelationshipReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
        _effectReaders = new Dictionary<string, Func<JsonElement, string, EffectDef>>(StringComparer.Ordinal)
        {
            ["axis"] = ReadAxisEffect, ["memory"] = ReadMemoryEffect, ["moodlet"] = ReadMoodletEffect, ["need"] = ReadNeedEffect, ["bond"] = ReadBondEffect,
        };
    }

    public RelationshipSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        // A format 1 document has no social block, so it is named before the fields are checked: the author is told to move to format 2, not that "social" is missing.
        if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("format", out var declared) && declared.ValueKind == JsonValueKind.Number && declared.TryGetInt64(out var number) && number == FormatBeforeSocial)
            throw _json.Refuse(RelationshipCodes.OldFormat, "format " + FormatBeforeSocial + " documents have no social block (communities, standing, balance); this reader reads format " + RelationshipLoader.Format + " only");
        // A format 2 document lacks the required social.schedule, so it is named before the fields are checked, whether or not it carries a schedule: there is no migration and no silent acceptance.
        if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("format", out var declaredTwo) && declaredTwo.ValueKind == JsonValueKind.Number && declaredTwo.TryGetInt64(out var numberTwo) && numberTwo == FormatBeforeSchedule)
            throw _json.Refuse(RelationshipCodes.FormatTwo, "format " + FormatBeforeSchedule + " documents lack the required social.schedule (\"atomic\" or \"spread\"); to migrate, set \"format\" to " + RelationshipLoader.Format + " and add social.schedule; this reader reads format " + RelationshipLoader.Format + " only and format " + FormatBeforeSchedule + " is no longer read");
        var root = _json.Object(document.RootElement, "the document",
            ["format", "id", "seed", "time", "limits", "axes", "needs", "sources", "mood", "moodlets", "memories", "traits", "ladders", "bonds", "gifts", "tastes", "gossip", "social", "interactions", "facts"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != RelationshipLoader.Format) throw _json.Refuse(RelationshipCodes.Format, "this reader reads format " + RelationshipLoader.Format + " only");
        var id = _json.Id(root, "id", "the document");
        var seed = (ulong)_json.Long(root, "seed", "the document", 0, 9_000_000_000_000_000);
        var time = _json.Object(root.GetProperty("time"), "time", ["stepsPerDay"]);
        _stepsPerDay = _json.Int(time, "stepsPerDay", "time", 1, 288);
        _limits = ReadLimits(root.GetProperty("limits"));
        _hostFacts = ReadHostFacts(_json.Array(root, "facts", "the document", 0, 64));
        _axes = ReadAxes(_json.Array(root, "axes", "the document", 1, 8));
        _needs = ReadNeeds(_json.Array(root, "needs", "the document", 0, 16));
        var sources = ReadSources(_json.Array(root, "sources", "the document", 0, 256));
        _mood = ReadMood(root.GetProperty("mood"));
        _moodlets = ReadMoodlets(_json.Array(root, "moodlets", "the document", 0, 256));
        _memories = ReadMemories(_json.Array(root, "memories", "the document", 0, 256));
        _traits = ReadTraits(_json.Array(root, "traits", "the document", 0, 256));
        _ladders = ReadLadders(_json.Array(root, "ladders", "the document", 0, 8));
        _bonds = ReadBonds(_json.Array(root, "bonds", "the document", 0, 32));
        _gifts = ReadGifts(_json.Array(root, "gifts", "the document", 0, 256));
        var tastes = ReadTastes(_json.Array(root, "tastes", "the document", 5, 5));
        var gossip = ReadGossip(root.GetProperty("gossip"));
        var social = ReadSocial(root.GetProperty("social"));
        var interactions = ReadInteractions(_json.Array(root, "interactions", "the document", 0, 256));
        return new RelationshipSet(id, Digest.Of(_text), seed, _stepsPerDay, _limits, _axes, _needs, sources, _mood, _moodlets, _memories, _traits, _ladders, _bonds, _gifts, tastes, gossip, social, interactions, _hostFacts);
    }

    // ---- shared helpers -------------------------------------------------------------------------------------------------------------

    /// <summary>Reads a text field that must be one of a closed set of words, so an unknown word is refused at one place.</summary>
    private T Choice<T>(JsonElement owner, string field, string where, IReadOnlyDictionary<string, T> words)
    {
        var word = _json.Text(owner, field, where, 32);
        return words.TryGetValue(word, out var value) ? value : throw _json.Refuse(RelationshipCodes.Format, where + "." + field + " '" + word + "' must be one of " + string.Join(", ", words.Keys));
    }

    /// <summary>The index a reference names, or a refusal that says what was not declared.</summary>
    private int Ref(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Id(owner, field, where);
        return declared.TryGetValue(id, out var index) ? index : throw _json.Refuse(RelationshipCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
    }

    private static Dictionary<string, int> Index<T>(IReadOnlyList<T> list, Func<T, string> id) => list.Select((item, i) => (id(item), i)).ToDictionary(p => p.Item1, p => p.i, StringComparer.Ordinal);

    private IReadOnlyList<string> ReadTags(JsonElement owner, string field, string where)
    {
        var tags = new List<string>();
        foreach (var tag in _json.Array(owner, field, where, 0, 8).EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String) throw _json.Refuse(RelationshipCodes.Format, where + "." + field + " must hold texts");
            var text = _json.CheckId(tag.GetString()!, where + "." + field);
            if (tags.Contains(text)) throw _json.Refuse(RelationshipCodes.Duplicate, where + "." + field + " repeats '" + text + "'");
            tags.Add(text);
        }
        tags.Sort(StringComparer.Ordinal);
        return tags;
    }

    private void Unique(HashSet<string> ids, string id, string what) { if (!ids.Add(id)) throw _json.Refuse(RelationshipCodes.Duplicate, what + " '" + id + "' is declared twice"); }

    private LimitsDef ReadLimits(JsonElement element)
    {
        _json.Object(element, "limits", ["maxActors", "maxEdgesPerActor", "maxMemoriesPerEdge", "maxMoodlets"]);
        return new LimitsDef(_json.Int(element, "maxActors", "limits", 1, MaxActorsCeiling), _json.Int(element, "maxEdgesPerActor", "limits", 1, MaxEdgesCeiling),
            _json.Int(element, "maxMemoriesPerEdge", "limits", 1, MaxMemoriesCeiling), _json.Int(element, "maxMoodlets", "limits", 1, MaxMoodletsCeiling));
    }

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (RelationshipFacts.IsReserved(id)) throw _json.Refuse(RelationshipCodes.Reserved, where + " '" + id + "': the prefixes 'actor.', 'target.', 'edge.' and 'reverse.' are the engine's own");
            if (facts.ContainsKey(id)) throw _json.Refuse(RelationshipCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(RelationshipCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(RelationshipCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(RelationshipCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(RelationshipCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    // ---- axes ------------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<AxisDef> ReadAxes(JsonElement array)
    {
        var axes = new List<AxisDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "axes[" + axes.Count + "]";
            _json.Object(element, where, ["id", "key", "min", "max", "baseline", "driftPerDay"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "axis");
            if (RelationshipFacts.AxisNameClashes(id)) throw _json.Refuse(RelationshipCodes.Reserved, where + " '" + id + "': an edge fact of that name already means something else");
            var min = _json.Long(element, "min", where, -Bound, Bound);
            var max = _json.Long(element, "max", where, -Bound, Bound);
            if (min >= max) throw _json.Refuse(RelationshipCodes.Range, where + " needs min below max");
            var baseline = _json.Long(element, "baseline", where, min, max);
            var drift = _json.Long(element, "driftPerDay", where, 0, max - min);
            axes.Add(new AxisDef(axes.Count, id, _json.Text(element, "key", where), min, max, baseline, drift));
        }
        return axes;
    }

    // ---- needs and sources -----------------------------------------------------------------------------------------------------------

    private IReadOnlyList<NeedDef> ReadNeeds(JsonElement array)
    {
        var needs = new List<NeedDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "needs[" + needs.Count + "]";
            _json.Object(element, where, ["id", "key", "max", "initial", "decayPerDay", "critical", "rearm", "urgency", "decayModifiers"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "need");
            var max = _json.Long(element, "max", where, 1, 100_000);
            var initial = _json.Long(element, "initial", where, 0, max);
            var decay = _json.Long(element, "decayPerDay", where, 0, Bound);
            var critical = _json.Long(element, "critical", where, 0, max);
            var rearm = _json.Long(element, "rearm", where, critical, max);
            var curve = ReadCurve(_json.Array(element, "urgency", where, 2, 32), where + ".urgency", max);
            var modifiers = new List<DecayModifierDef>();
            foreach (var modifier in _json.Array(element, "decayModifiers", where, 0, 8).EnumerateArray())
            {
                var at = where + ".decayModifiers[" + modifiers.Count + "]";
                _json.Object(modifier, at, ["when", "permille"]);
                // Only host facts: the condition is evaluated once per step for everyone, so it cannot depend on one actor.
                var condition = _conditions.Read(modifier.GetProperty("when"), at + ".when", _hostFacts);
                modifiers.Add(new DecayModifierDef(condition, _json.Int(modifier, "permille", at, 0, 10_000), Conditions.FactsOf(condition)));
            }
            needs.Add(new NeedDef(needs.Count, id, _json.Text(element, "key", where), max, initial, decay, critical, rearm, curve, modifiers));
        }
        return needs;
    }

    private IReadOnlyList<CurvePoint> ReadCurve(JsonElement array, string where, long max)
    {
        var points = new List<CurvePoint>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 2) throw _json.Refuse(RelationshipCodes.Curve, where + " holds [level, urgency] pairs");
            var level = element[0];
            var urgency = element[1];
            if (level.ValueKind != JsonValueKind.Number || !level.TryGetInt64(out var l) || urgency.ValueKind != JsonValueKind.Number || !urgency.TryGetInt64(out var u))
                throw _json.Refuse(RelationshipCodes.Curve, where + " pairs are whole numbers");
            if (l < 0 || l > max) throw _json.Refuse(RelationshipCodes.Range, where + " level " + l + " is outside 0 to " + max);
            if (u < 0 || u > 1000) throw _json.Refuse(RelationshipCodes.Range, where + " urgency " + u + " is outside 0 to 1000");
            if (points.Count > 0 && l <= points[^1].Level) throw _json.Refuse(RelationshipCodes.Curve, where + " levels must increase");
            points.Add(new CurvePoint(l, u));
        }
        if (points[0].Level != 0 || points[^1].Level != max) throw _json.Refuse(RelationshipCodes.Curve, where + " must start at level 0 and end at the need's max " + max);
        return points;
    }

    private IReadOnlyList<SourceDef> ReadSources(JsonElement array)
    {
        var sources = new List<SourceDef>();
        var needIndex = Index(_needs, n => n.Id);
        var seen = new HashSet<(string, int)>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "sources[" + sources.Count + "]";
            _json.Object(element, where, ["activity", "need", "perDay"]);
            var activity = _json.Id(element, "activity", where);
            var need = Ref(element, "need", where, needIndex, "need");
            if (!seen.Add((activity, need))) throw _json.Refuse(RelationshipCodes.Duplicate, where + " repeats activity '" + activity + "' for need '" + _needs[need].Id + "'");
            sources.Add(new SourceDef(activity, need, _json.Long(element, "perDay", where, 1, Bound)));
        }
        return sources;
    }

    // ---- mood ------------------------------------------------------------------------------------------------------------------------

    private MoodDef ReadMood(JsonElement element)
    {
        _json.Object(element, "mood", ["min", "max", "base", "hysteresis", "needWeights", "states"]);
        var min = _json.Long(element, "min", "mood", -Bound, Bound);
        var max = _json.Long(element, "max", "mood", -Bound, Bound);
        if (min >= max) throw _json.Refuse(RelationshipCodes.Range, "mood needs min below max");
        var baseValue = _json.Long(element, "base", "mood", min, max);
        var hysteresis = _json.Long(element, "hysteresis", "mood", 0, (max - min) / 4);
        var needIndex = Index(_needs, n => n.Id);
        var weights = new List<NeedWeight>();
        foreach (var weight in _json.Array(element, "needWeights", "mood", 0, 16).EnumerateArray())
        {
            var where = "mood.needWeights[" + weights.Count + "]";
            _json.Object(weight, where, ["need", "weight", "neutral"]);
            var need = Ref(weight, "need", where, needIndex, "need");
            if (weights.Any(w => w.Need == need)) throw _json.Refuse(RelationshipCodes.Duplicate, where + " repeats need '" + _needs[need].Id + "'");
            weights.Add(new NeedWeight(need, _json.Long(weight, "weight", where, -5000, 5000), _json.Long(weight, "neutral", where, 0, _needs[need].Max)));
        }
        var states = ReadSteps(_json.Array(element, "states", "mood", 1, 16), "mood.states", min, max);
        return new MoodDef(min, max, baseValue, hysteresis, weights, states);
    }

    /// <summary>Named steps over a value: ids unique, <c>from</c> strictly ascending, the first at the bottom of the range so every value has a step.</summary>
    private IReadOnlyList<StepDef> ReadSteps(JsonElement array, string where, long min, long max)
    {
        var steps = new List<StepDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + steps.Count + "]";
            _json.Object(element, at, ["id", "key", "from"]);
            var id = _json.Id(element, "id", at);
            Unique(ids, id, "step");
            var from = _json.Long(element, "from", at, min, max);
            if (steps.Count == 0 && from != min) throw _json.Refuse(RelationshipCodes.Ladder, at + ".from must be " + min + ", the bottom of the range, so every value has a step");
            if (steps.Count > 0 && from <= steps[^1].From) throw _json.Refuse(RelationshipCodes.Ladder, at + ".from must be above the step before it");
            steps.Add(new StepDef(id, _json.Text(element, "key", at), from));
        }
        return steps;
    }
}
