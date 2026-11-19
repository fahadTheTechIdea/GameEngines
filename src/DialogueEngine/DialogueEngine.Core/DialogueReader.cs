using System.Collections.Immutable;
using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>The one reader of the document. The declarations are read first, then the names every later part may refer to, then the quests and conversations.</summary>
internal sealed partial class DialogueReader
{
    private const string Where = "document";
    private readonly string _text;
    private readonly ISpeakerCatalog _catalog;
    private readonly StrictJson _json = new(DialogueCodes.ForDocument);
    private readonly Conditions _conditions;

    private readonly Dictionary<string, VariableDef> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExternalFactDef> _externals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpeakerDef> _speakers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContentWarningDef> _warnings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDef> _quests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _questStages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _nodeIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConversationDef> _conversations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _calls = new(StringComparer.Ordinal);
    private string _current = "";
    private readonly Dictionary<string, List<string>> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _keyUses = new(StringComparer.Ordinal);
    private readonly HashSet<(string Quest, string Stage)> _stageReferences = new();
    private ImmutableDictionary<string, FactDefinition> _facts = ImmutableDictionary<string, FactDefinition>.Empty;

    public DialogueReader(string text, ISpeakerCatalog catalog)
    {
        _text = text;
        _catalog = catalog;
        _conditions = new Conditions(_json);
    }

    public DialogueSet Read()
    {
        using var document = _json.Parse(_text, Where);
        var root = _json.Object(document.RootElement, Where, ["format", "id", "variables", "externalFacts", "speakers", "conversations", "quests"], "historyLimit", "contentWarnings");
        var format = root.GetProperty("format");
        if (format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number) || number != DialogueLoader.Format)
            throw _json.Refuse(DialogueCodes.Format, "document.format must be " + DialogueLoader.Format);
        var id = _json.Id(root, "id", Where);

        ReadVariables(_json.Array(root, "variables", Where, 0, 256));
        ReadExternals(_json.Array(root, "externalFacts", Where, 0, 256));
        ReadSpeakers(_json.Array(root, "speakers", Where, 0, 256));
        if (_json.Has(root, "contentWarnings")) ReadWarnings(_json.Array(root, "contentWarnings", Where, 0, 32));
        var historyLimit = _json.Has(root, "historyLimit") ? _json.Int(root, "historyLimit", Where, 0, 1000) : DialogueSet.DefaultHistoryLimit;
        CollectNames(_json.Array(root, "quests", Where, 0, 256), _json.Array(root, "conversations", Where, 1, 256));
        BuildFacts();

        foreach (var quest in _json.Array(root, "quests", Where, 0, 256).EnumerateArray()) ReadQuest(quest);
        foreach (var conversation in _json.Array(root, "conversations", Where, 1, 256).EnumerateArray()) ReadConversation(conversation);
        CheckQuestStages();
        CheckCalls();
        CheckSilentChains();

        var keys = _keys.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value.AsReadOnly(), StringComparer.Ordinal);
        var uses = _keyUses.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value.AsReadOnly(), StringComparer.Ordinal);
        return new DialogueSet(id, Digest.Of(_text), _variables, _externals, _speakers, _conversations, _quests, keys, uses, _warnings, historyLimit);
    }

    // ---- declarations -------------------------------------------------------------------------------------------------------------------

    private void ReadVariables(JsonElement array)
    {
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "variables[" + index++ + "]";
            _json.Object(element, where, ["id", "type", "initial"], "min", "max", "allowed");
            var id = _json.Id(element, "id", where);
            var type = _json.Text(element, "type", where, 16);
            var kind = KindOf(type, where);
            long min = 0, max = 0;
            IReadOnlyList<string> allowed = [];
            if (kind == FactKind.Integer)
            {
                Require(element, "min", where); Require(element, "max", where); Forbid(element, "allowed", where);
                min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                max = _json.Long(element, "max", where, min, 1_000_000_000);
            }
            else
            {
                Forbid(element, "min", where); Forbid(element, "max", where);
                if (kind == FactKind.Boolean) Forbid(element, "allowed", where);
                else if (_json.Has(element, "allowed"))
                {
                    var list = _json.Array(element, "allowed", where, 1, 64).EnumerateArray().Select(a => a.ValueKind == JsonValueKind.String ? a.GetString()! : throw _json.Refuse(DialogueCodes.Format, where + ".allowed must hold strings")).ToList();
                    if (list.Distinct(StringComparer.Ordinal).Count() != list.Count) throw _json.Refuse(DialogueCodes.Duplicate, where + ".allowed repeats a value");
                    allowed = list;
                }
            }
            var initial = Literal(element.GetProperty("initial"), kind, where + ".initial");
            CheckValue(initial, kind, min, max, allowed, where + ".initial");
            Add(_variables, id, new VariableDef(id, kind, initial, min, max, allowed), "variable");
        }
    }

    private void ReadExternals(JsonElement array)
    {
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "externalFacts[" + index++ + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            var kind = KindOf(_json.Text(element, "type", where, 16), where);
            long min = 0, max = 0;
            if (kind == FactKind.Integer)
            {
                Require(element, "min", where); Require(element, "max", where);
                min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                max = _json.Long(element, "max", where, min, 1_000_000_000);
            }
            else { Forbid(element, "min", where); Forbid(element, "max", where); }
            Add(_externals, id, new ExternalFactDef(id, kind, min, max), "external fact");
        }
    }

    private void ReadWarnings(JsonElement array)
    {
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "contentWarnings[" + index++ + "]";
            _json.Object(element, where, ["tag", "fact", "action"]);
            var tag = _json.Id(element, "tag", where);
            if (!tag.StartsWith(ContentWarningDef.Prefix, StringComparison.Ordinal) || tag.Length == ContentWarningDef.Prefix.Length)
                throw _json.Refuse(DialogueCodes.WarningUndeclared, where + ": a content-warning tag starts with '" + ContentWarningDef.Prefix + "' and names the warning, not '" + tag + "'");
            var fact = _json.Id(element, "fact", where);
            if (!_externals.TryGetValue(fact, out var external) || external.Kind != FactKind.Boolean)
                throw _json.Refuse(DialogueCodes.WarningFact, where + ": '" + fact + "' must be a declared true/false external fact (true: this player has blocked the warning)");
            var action = _json.Text(element, "action", where, 8) switch
            {
                "skip" => WarningAction.Skip,
                "replace" => WarningAction.Replace,
                _ => (WarningAction?)null,
            } ?? throw _json.Refuse(DialogueCodes.Format, where + ".action must be skip or replace");
            Add(_warnings, tag, new ContentWarningDef(tag, fact, action), "content warning");
        }
    }

    /// <summary>The declared warnings a set of tags names. A tag that starts with <c>cw.</c> must be declared: a typo would otherwise show unwarned content.</summary>
    private IReadOnlyList<ContentWarningDef> WarningsOf(IReadOnlyList<string> tags, string where)
    {
        var found = new List<ContentWarningDef>();
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(ContentWarningDef.Prefix, StringComparison.Ordinal)) continue;
            if (!_warnings.TryGetValue(tag, out var warning)) throw _json.Refuse(DialogueCodes.WarningUndeclared, where + ": the tag '" + tag + "' names a content warning the document does not declare");
            found.Add(warning);
        }
        return found;
    }

    /// <summary>A warning that replaces needs the alternative to show; an alternative with nothing to replace it is a mistake.</summary>
    private void CheckAlternative(JsonElement owner, IReadOnlyList<ContentWarningDef> warnings, string where)
    {
        var replaces = warnings.Any(w => w.Action == WarningAction.Replace);
        if (replaces && !_json.Has(owner, "alternative")) throw _json.Refuse(DialogueCodes.WarningAlternative, where + " carries a content warning that replaces blocked content, so it needs an 'alternative' key");
        if (!replaces && _json.Has(owner, "alternative")) throw _json.Refuse(DialogueCodes.WarningAlternative, where + " has an 'alternative' but no tag of a warning that replaces");
    }

    private void ReadSpeakers(JsonElement array)
    {
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "speakers[" + index++ + "]";
            _json.Object(element, where, ["id", "portraits"], "emotions");
            var id = _json.Id(element, "id", where);
            if (!_catalog.HasSpeaker(id)) throw _json.Refuse(DialogueCodes.UnknownSpeaker, where + ": '" + id + "' is not a character of the catalog");
            var portraits = _json.Array(element, "portraits", where, 1, 64).EnumerateArray()
                .Select((p, i) => p.ValueKind == JsonValueKind.String ? _json.CheckId(p.GetString()!, where + ".portraits[" + i + "]") : throw _json.Refuse(DialogueCodes.Format, where + ".portraits must hold strings")).ToList();
            if (portraits.Distinct(StringComparer.Ordinal).Count() != portraits.Count) throw _json.Refuse(DialogueCodes.Duplicate, where + ".portraits repeats a portrait");
            var emotions = _json.Has(element, "emotions") ? Ids(_json.Array(element, "emotions", where, 1, 64), where + ".emotions") : [];
            Add(_speakers, id, new SpeakerDef(id, portraits, emotions), "speaker");
        }
    }

    /// <summary>Quest, stage, conversation and node names, so a condition or an effect may refer to any of them wherever it is written.</summary>
    private void CollectNames(JsonElement quests, JsonElement conversations)
    {
        var index = 0;
        foreach (var quest in quests.EnumerateArray())
        {
            var where = "quests[" + index++ + "]";
            if (quest.ValueKind != JsonValueKind.Object || !quest.TryGetProperty("id", out _) || !quest.TryGetProperty("stages", out var stages) || stages.ValueKind != JsonValueKind.Array)
                throw _json.Refuse(DialogueCodes.Format, where + " must be an object with an id and stages");
            var id = _json.Id(quest, "id", where);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in stages.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object || !stage.TryGetProperty("id", out _)) throw _json.Refuse(DialogueCodes.Format, where + ".stages must hold objects with an id");
                if (!names.Add(_json.Id(stage, "id", where + ".stages"))) throw _json.Refuse(DialogueCodes.Duplicate, where + " has two stages with the same id");
            }
            if (_questStages.ContainsKey(id)) throw _json.Refuse(DialogueCodes.Duplicate, "two quests are called '" + id + "'");
            _questStages[id] = names;
        }
        index = 0;
        foreach (var conversation in conversations.EnumerateArray())
        {
            var where = "conversations[" + index++ + "]";
            if (conversation.ValueKind != JsonValueKind.Object || !conversation.TryGetProperty("id", out _) || !conversation.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                throw _json.Refuse(DialogueCodes.Format, where + " must be an object with an id and nodes");
            var id = _json.Id(conversation, "id", where);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("id", out _)) throw _json.Refuse(DialogueCodes.Format, where + ".nodes must hold objects with an id");
                if (!names.Add(_json.Id(node, "id", where + ".nodes"))) throw _json.Refuse(DialogueCodes.Duplicate, where + " has two nodes with the same id");
            }
            if (_nodeIds.ContainsKey(id)) throw _json.Refuse(DialogueCodes.Duplicate, "two conversations are called '" + id + "'");
            _nodeIds[id] = names;
        }
    }

    private void BuildFacts()
    {
        var facts = ImmutableDictionary.CreateBuilder<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var v in _variables.Values) facts["var." + v.Id] = v.Kind == FactKind.Integer ? new FactDefinition(v.Kind, v.Minimum, v.Maximum) : new FactDefinition(v.Kind);
        foreach (var e in _externals.Values) facts["ext." + e.Id] = e.Kind == FactKind.Integer ? new FactDefinition(e.Kind, e.Minimum, e.Maximum) : new FactDefinition(e.Kind);
        foreach (var (quest, stages) in _questStages)
        {
            facts["quest." + quest + ".status"] = new FactDefinition(FactKind.String);
            facts["quest." + quest + ".stage"] = new FactDefinition(FactKind.String);
        }
        foreach (var (conversation, nodes) in _nodeIds)
            foreach (var node in nodes) facts["visits." + conversation + "/" + node] = new FactDefinition(FactKind.Integer, 0, StepContext.MaxVisits);
        _facts = facts.ToImmutable();
    }

    // ---- small helpers --------------------------------------------------------------------------------------------------------------------

    private FactKind KindOf(string type, string where) => (type switch
    {
        "int" => FactKind.Integer,
        "bool" => FactKind.Boolean,
        "string" => FactKind.String,
        _ => (FactKind?)null,
    }) ?? throw _json.Refuse(DialogueCodes.Format, where + ".type must be int, bool or string, not '" + type + "'");

    /// <summary>An array of distinct ids.</summary>
    private IReadOnlyList<string> Ids(JsonElement array, string where)
    {
        var ids = array.EnumerateArray().Select((e, i) => e.ValueKind == JsonValueKind.String ? _json.CheckId(e.GetString()!, where + "[" + i + "]") : throw _json.Refuse(DialogueCodes.Format, where + " must hold strings")).ToList();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw _json.Refuse(DialogueCodes.Duplicate, where + " repeats an entry");
        return ids;
    }

    private void Require(JsonElement element, string field, string where)
    {
        if (!_json.Has(element, field)) throw _json.Refuse(DialogueCodes.MissingField, where + " is missing '" + field + "'");
    }

    private void Forbid(JsonElement element, string field, string where)
    {
        if (_json.Has(element, field)) throw _json.Refuse(DialogueCodes.UnknownField, where + " may not have '" + field + "' for this type");
    }

    private FactValue Literal(JsonElement value, FactKind kind, string where) => (kind switch
    {
        FactKind.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) => FactValue.From(n),
        FactKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => FactValue.From(value.GetBoolean()),
        FactKind.String when value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 256 => FactValue.From(value.GetString()!),
        _ => (FactValue?)null,
    }) ?? throw _json.Refuse(DialogueCodes.ValueType, where + " must be a " + kind.ToString().ToLowerInvariant() + " value");

    private void CheckValue(FactValue value, FactKind kind, long min, long max, IReadOnlyList<string> allowed, string where)
    {
        if (kind == FactKind.Integer && (value.Integer < min || value.Integer > max)) throw _json.Refuse(DialogueCodes.Range, where + " is " + value.Integer + ", outside " + min + " to " + max);
        if (kind == FactKind.String && allowed.Count > 0 && !allowed.Contains(value.Text!)) throw _json.Refuse(DialogueCodes.Range, where + " '" + value.Text + "' is not one of the allowed values");
    }

    private void Add<T>(Dictionary<string, T> into, string id, T value, string what)
    {
        if (!into.TryAdd(id, value)) throw _json.Refuse(DialogueCodes.Duplicate, "two " + what + "s are called '" + id + "'");
    }

    /// <summary>A localisation key and the placeholder names its text may use. A key used twice must be used with the same names.</summary>
    private string Key(JsonElement owner, string field, string where, IEnumerable<string> placeholders)
    {
        var key = _json.Id(owner, field, where);
        if (!_keyUses.TryGetValue(key, out var uses)) _keyUses[key] = uses = new List<string>();
        uses.Add(where);
        var names = placeholders.OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (_keys.TryGetValue(key, out var known))
        {
            if (!known.SequenceEqual(names)) throw _json.Refuse(DialogueCodes.Duplicate, where + "." + field + ": key '" + key + "' is used with different placeholders elsewhere");
        }
        else _keys[key] = names;
        return key;
    }
}