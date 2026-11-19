using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class AiReader
{
    private readonly string _text;
    private readonly StrictJson _json = new(AiCodes.ForDocument);
    private readonly Conditions _conditions;
    private IReadOnlyList<KeyDef> _keys = Array.Empty<KeyDef>();
    private IReadOnlyList<PerceptKindDef> _percepts = Array.Empty<PerceptKindDef>();
    private IReadOnlyDictionary<string, IntentDef> _intents = new Dictionary<string, IntentDef>();
    private readonly Dictionary<string, KeyDef> _keyById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PerceptKindDef> _kindById = new(StringComparer.Ordinal);

    public AiReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public AiSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        var root = _json.Object(document.RootElement, "the document", ["format", "id", "keys", "percepts", "intents", "behaviors"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != 1) throw _json.Refuse(AiCodes.Format, "this reader reads format 1 only");
        var id = _json.Id(root, "id", "the document");
        _keys = ReadKeys(_json.Array(root, "keys", "the document", 0, 256));
        foreach (var key in _keys) _keyById[key.Id] = key;
        _percepts = ReadPercepts(_json.Array(root, "percepts", "the document", 0, 32));
        foreach (var kind in _percepts) _kindById[kind.Id] = kind;
        _intents = ReadIntents(_json.Array(root, "intents", "the document", 0, 64));
        var behaviors = ReadBehaviors(_json.Array(root, "behaviors", "the document", 1, 64));
        return new AiSet(id, Digest.Of(_text), _keys, _percepts, _intents, behaviors);
    }

    // ---- typed values -------------------------------------------------------------------------------------------------------------------

    private FactKind ReadKind(JsonElement owner, string where) => _json.Text(owner, "type", where, 16) switch
    {
        "int" => FactKind.Integer,
        "bool" => FactKind.Boolean,
        "string" => FactKind.String,
        var other => throw _json.Refuse(AiCodes.Format, where + ".type '" + other + "' must be int, bool or string"),
    };

    /// <summary>A declaration of a typed slot: its id, type and, for a whole number, its range (both ends or neither, and only for numbers).</summary>
    private ValueDef ReadValueDef(JsonElement owner, int index, string where)
    {
        var id = _json.Id(owner, "id", where);
        var kind = ReadKind(owner, where + " '" + id + "'");
        var has = _json.Has(owner, "min") || _json.Has(owner, "max");
        if (kind != FactKind.Integer)
        {
            if (has) throw _json.Refuse(AiCodes.Format, where + " '" + id + "' is not a whole number and has no range");
            return new ValueDef(index, id, kind, 0, 0);
        }
        if (!_json.Has(owner, "min") || !_json.Has(owner, "max")) throw _json.Refuse(AiCodes.MissingField, where + " '" + id + "' is a whole number and needs both min and max");
        var min = _json.Long(owner, "min", where + " '" + id + "'", -ValueDef.Limit, ValueDef.Limit);
        var max = _json.Long(owner, "max", where + " '" + id + "'", -ValueDef.Limit, ValueDef.Limit);
        return min <= max ? new ValueDef(index, id, kind, min, max) : throw _json.Refuse(AiCodes.Range, where + " '" + id + "' has min above max");
    }

    /// <summary>The value written in a field, of the declared type and inside the declared range.</summary>
    private Val ReadTyped(JsonElement owner, string field, ValueDef def, string where)
    {
        var element = owner.GetProperty(field);
        switch (def.Kind)
        {
            case FactKind.Boolean:
                if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw _json.Refuse(AiCodes.ValueType, where + "." + field + " must be true or false");
                return Val.Of(element.GetBoolean());
            case FactKind.Integer:
                if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var number)) throw _json.Refuse(AiCodes.ValueType, where + "." + field + " must be a whole number");
                return number >= def.Minimum && number <= def.Maximum ? Val.Of(number) : throw _json.Refuse(AiCodes.Range, where + "." + field + " " + number + " is outside " + def.Minimum + " to " + def.Maximum);
            case FactKind.String: break;
        }
        if (element.ValueKind != JsonValueKind.String) throw _json.Refuse(AiCodes.ValueType, where + "." + field + " must be a string");
        var text = element.GetString()!;
        return text.Length <= ValueDef.TextLimit ? Val.Of(text) : throw _json.Refuse(AiCodes.Range, where + "." + field + " is longer than " + ValueDef.TextLimit);
    }

    // ---- keys, percepts, intents -------------------------------------------------------------------------------------------------------

    private IReadOnlyList<KeyDef> ReadKeys(JsonElement array)
    {
        var read = new List<(ValueDef Def, Val Initial, bool Host)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "keys[" + n++ + "]";
            _json.Object(element, where, ["id", "type", "initial", "source"], "min", "max");
            var def = ReadValueDef(element, 0, where);
            if (!seen.Add(def.Id)) throw _json.Refuse(AiCodes.Duplicate, "key '" + def.Id + "' is declared twice");
            var source = _json.Text(element, "source", where, 16);
            if (source is not ("host" or "agent")) throw _json.Refuse(AiCodes.Format, where + ".source must be host or agent");
            read.Add((def, ReadTyped(element, "initial", def, where), source == "host"));
        }
        var ordered = read.OrderBy(r => r.Def.Id, StringComparer.Ordinal).ToList();
        return ordered.Select((r, i) => new KeyDef(r.Def with { Index = i }, r.Initial, r.Host)).ToList();
    }

    private IReadOnlyList<PerceptKindDef> ReadPercepts(JsonElement array)
    {
        var read = new List<(string Id, JsonElement Element)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "percepts[" + n++ + "]";
            _json.Object(element, where, ["kind", "attrs", "capacity", "memory", "owned"], "ownerCapacity");
            var id = _json.Id(element, "kind", where);
            if (!seen.Add(id)) throw _json.Refuse(AiCodes.Duplicate, "percept kind '" + id + "' is declared twice");
            read.Add((id, element));
        }
        var result = new List<PerceptKindDef>();
        foreach (var (id, element) in read.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var where = "percept kind '" + id + "'";
            var attrs = new List<(ValueDef Def, Val Unknown)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;
            foreach (var attr in _json.Array(element, "attrs", where, 0, 16).EnumerateArray())
            {
                var attrWhere = where + " attrs[" + count++ + "]";
                _json.Object(attr, attrWhere, ["id", "type", "unknown"], "min", "max");
                var def = ReadValueDef(attr, 0, attrWhere);
                if (def.Id is "id" or "owner" or "index") throw _json.Refuse(AiCodes.Reserved, attrWhere + " '" + def.Id + "' is a name the engine uses for every percept");
                if (!names.Add(def.Id)) throw _json.Refuse(AiCodes.Duplicate, where + " declares attribute '" + def.Id + "' twice");
                attrs.Add((def, ReadTyped(attr, "unknown", def, attrWhere)));
            }
            var ordered = attrs.OrderBy(a => a.Def.Id, StringComparer.Ordinal).ToList();
            var defs = ordered.Select((a, i) => a.Def with { Index = i }).ToList();
            var capacity = _json.Int(element, "capacity", where, 1, 65_536);
            var owned = _json.Bool(element, "owned", where);
            if (owned && !_json.Has(element, "ownerCapacity")) throw _json.Refuse(AiCodes.MissingField, where + " is owned and needs 'ownerCapacity', the most things one owner holds");
            if (!owned && _json.Has(element, "ownerCapacity")) throw _json.Refuse(AiCodes.Format, where + " has no owners, so 'ownerCapacity' means nothing");
            result.Add(new PerceptKindDef(result.Count, id, defs, ordered.Select(a => a.Unknown).ToList(), capacity,
                owned ? (int)_json.Int(element, "ownerCapacity", where, 1, capacity) : 0, (int)_json.Int(element, "memory", where, 1, 1_000_000_000), owned));
        }
        return result;
    }

    private IReadOnlyDictionary<string, IntentDef> ReadIntents(JsonElement array)
    {
        var result = new Dictionary<string, IntentDef>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "intents[" + n++ + "]";
            _json.Object(element, where, ["id", "args"]);
            var id = _json.Id(element, "id", where);
            if (result.ContainsKey(id)) throw _json.Refuse(AiCodes.Duplicate, "intent '" + id + "' is declared twice");
            var args = new List<ValueDef>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arg in _json.Array(element, "args", where, 0, 16).EnumerateArray())
            {
                var argWhere = "intent '" + id + "' argument";
                _json.Object(arg, argWhere, ["id", "type"], "min", "max");
                var def = ReadValueDef(arg, 0, argWhere);
                if (!names.Add(def.Id)) throw _json.Refuse(AiCodes.Duplicate, "intent '" + id + "' declares argument '" + def.Id + "' twice");
                args.Add(def);
            }
            result[id] = new IntentDef(id, args.OrderBy(a => a.Id, StringComparer.Ordinal).Select((a, i) => a with { Index = i }).ToList());
        }
        return result;
    }

    private IReadOnlyDictionary<string, BehaviorDef> ReadBehaviors(JsonElement array)
    {
        var result = new Dictionary<string, BehaviorDef>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "behaviors[" + n++ + "]";
            _json.Object(element, where, ["id", "every", "root"]);
            var id = _json.Id(element, "id", where);
            if (result.ContainsKey(id)) throw _json.Refuse(AiCodes.Duplicate, "behaviour '" + id + "' is declared twice");
            var context = new BehaviorContext(id);
            var root = ReadNode(element.GetProperty("root"), "behaviour '" + id + "' root", context, 1);
            result[id] = new BehaviorDef(id, _json.Int(element, "every", where, 1, 100_000), root, context.PersistentSlots, context.SlotMaxima, root.Bound, context.NodeCount);
        }
        return result;
    }
}