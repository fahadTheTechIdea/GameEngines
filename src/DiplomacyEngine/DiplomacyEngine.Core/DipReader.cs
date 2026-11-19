using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class DipReader
{
    public const int MaxRealms = 64, MaxSeasons = 16, MaxIdeologies = 16, MaxTraits = 32, MaxDeeds = 128, MaxTypes = 64, MaxCasusBelli = 64, MaxMissions = 32, MaxFactions = 8, MaxClauses = 6, MaxEffects = 8,
        MaxModifiers = 16, MaxAgentsInDocument = 1024, MaxHostFacts = 64, MaxDays = 36_500, MaxList = 1_000_000;

    private readonly string _text;
    private readonly StrictJson _json = new(DipCodes.ForDocument);
    private readonly Conditions _conditions;
    private readonly DipParts _parts = new();
    private Dictionary<string, int> _seasonIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _ideologyIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _traitIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _deedIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _typeIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _cbIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _realmIndex = new(StringComparer.Ordinal);

    public DipReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public DipSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        var root = _json.Object(document.RootElement, "the document", ["format", "id", "seed", "time", "limits", "seasons", "ideologies", "traits", "deeds", "rules", "casusBelli", "missions", "types", "realms", "agents", "facts"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != DipLoader.Format) throw _json.Refuse(DipCodes.Format, "this reader reads format " + DipLoader.Format + " only");
        _parts.Id = _json.Id(root, "id", "the document");
        _parts.Digest = Digest.Of(_text);
        _parts.Seed = (ulong)_json.Long(root, "seed", "the document", 0, long.MaxValue);
        var time = _json.Object(root.GetProperty("time"), "time", ["stepsPerDay"]);
        _parts.StepsPerDay = _json.Int(time, "stepsPerDay", "time", 1, 1440);
        ReadLimits(root.GetProperty("limits"));
        ReadSeasons(_json.Array(root, "seasons", "the document", 1, MaxSeasons));
        ReadIdeologies(_json.Array(root, "ideologies", "the document", 1, MaxIdeologies));
        ReadTraits(_json.Array(root, "traits", "the document", 0, MaxTraits));
        ReadDeeds(_json.Array(root, "deeds", "the document", 0, MaxDeeds));
        var cbArray = _json.Array(root, "casusBelli", "the document", 0, MaxCasusBelli);
        var typeArray = _json.Array(root, "types", "the document", 1, MaxTypes);
        var realmArray = _json.Array(root, "realms", "the document", 2, MaxRealms);
        _cbIndex = Indexed(IdsOf(cbArray, "casusBelli"));
        _typeIndex = Indexed(IdsOf(typeArray, "types"));
        _realmIndex = Indexed(IdsOf(realmArray, "realms"));
        ReadRules(root.GetProperty("rules"));
        _parts.HostFacts = ReadHostFacts(_json.Array(root, "facts", "the document", 0, MaxHostFacts));
        var granted = cbArray.EnumerateArray().Where(e => e.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String && source.GetString() == EnumNames.Name(CbSource.Granted))
            .Select(e => e.GetProperty("id").GetString()!).ToList();
        _parts.PairFacts = DipFacts.Pair(_parts.HostFacts, _typeIndex.Keys, granted);
        _parts.CasusBelli = ReadCasusBelli(cbArray);
        _parts.Missions = ReadMissions(_json.Array(root, "missions", "the document", 0, MaxMissions));
        _parts.Types = ReadTypes(typeArray);
        _parts.Realms = ReadRealms(realmArray);
        _parts.Agents = ReadAgents(_json.Array(root, "agents", "the document", 0, MaxAgentsInDocument));
        CheckAgainstLimits();
        return new DipSet(_parts);
    }

    // ---- shared helpers -------------------------------------------------------------------------------------------------------------

    private static Dictionary<string, int> Indexed(IReadOnlyList<string> ids) => ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.Ordinal);

    private int Ref(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Id(owner, field, where);
        if (!declared.TryGetValue(id, out var index)) throw _json.Refuse(DipCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
        return index;
    }

    /// <summary>A reference that may be empty, which means none (-1).</summary>
    private int OptionalRef(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Text(owner, field, where, 128);
        if (id.Length == 0) return -1;
        _json.CheckId(id, where + "." + field);
        if (!declared.TryGetValue(id, out var index)) throw _json.Refuse(DipCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
        return index;
    }

    private void Unique(HashSet<string> ids, string id, string what)
    {
        if (!ids.Add(id)) throw _json.Refuse(DipCodes.Duplicate, what + " '" + id + "' is declared twice");
    }

    private List<string> IdsOf(JsonElement array, string what)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("id", out _)) throw _json.Refuse(DipCodes.Format, what + "[" + ids.Count + "] must be an object with an id");
            var id = _json.Id(element, "id", what + "[" + ids.Count + "]");
            Unique(seen, id, what);
            ids.Add(id);
        }
        return ids;
    }

    private T Word<T>(JsonElement owner, string field, string where) where T : struct, Enum
    {
        var name = _json.Text(owner, field, where, 32);
        var member = EnumNames.Parse<T>(name);
        if (member is null) throw _json.Refuse(DipCodes.Format, where + "." + field + " '" + name + "' must be one of " + EnumNames.Choices<T>());
        return member.Value;
    }

    private Condition? OptionalWhen(JsonElement owner, string field, string where) =>
        _json.Has(owner, field) ? _conditions.Read(owner.GetProperty(field), where + "." + field, _parts.PairFacts) : null;

    // ---- the plain lists -------------------------------------------------------------------------------------------------------------

    private void ReadLimits(JsonElement element)
    {
        const string where = "limits";
        _json.Object(element, where, ["maxTreaties", "maxProposals", "maxWars", "maxAgents", "maxMissions", "maxCalls", "maxIncidents", "maxGrants", "maxDeedsPerPair"]);
        _parts.Limits = new LimitsDef(_json.Int(element, "maxTreaties", where, 1, 100_000), _json.Int(element, "maxProposals", where, 1, 100_000), _json.Int(element, "maxWars", where, 1, 10_000),
            _json.Int(element, "maxAgents", where, 1, 100_000), _json.Int(element, "maxMissions", where, 1, 100_000), _json.Int(element, "maxCalls", where, 1, 100_000), _json.Int(element, "maxIncidents", where, 1, 100_000),
            _json.Int(element, "maxGrants", where, 1, 100_000), _json.Int(element, "maxDeedsPerPair", where, 1, 64));
    }

    private void ReadSeasons(JsonElement array)
    {
        var seasons = new List<SeasonDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "seasons[" + seasons.Count + "]";
            _json.Object(element, where, ["id", "key", "warCost"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "season");
            seasons.Add(new SeasonDef(seasons.Count, id, _json.Id(element, "key", where), _json.Int(element, "warCost", where, 0, 10_000)));
        }
        _parts.Seasons = seasons;
        _seasonIndex = Indexed(seasons.Select(s => s.Id).ToList());
    }

    private void ReadIdeologies(JsonElement array)
    {
        var list = new List<IdeologyDef>();
        var ids = IdsOf(array, "ideologies");
        _ideologyIndex = Indexed(ids);
        foreach (var element in array.EnumerateArray())
        {
            var where = "ideologies[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "affinity"]);
            var affinity = new List<int>();
            foreach (var item in _json.Array(element, "affinity", where, ids.Count, ids.Count).EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var number)) throw _json.Refuse(DipCodes.Format, where + ".affinity[" + affinity.Count + "] must be a whole number");
                if (number < -1000 || number > 1000) throw _json.Refuse(DipCodes.Range, where + ".affinity[" + affinity.Count + "] must be -1000 to 1000, not " + number);
                affinity.Add(number);
            }
            list.Add(new IdeologyDef(list.Count, ids[list.Count], _json.Id(element, "key", where), affinity));
        }
        _parts.Ideologies = list;
    }

    private void ReadTraits(JsonElement array)
    {
        var list = new List<TraitDef>();
        var ids = IdsOf(array, "traits");
        _traitIndex = Indexed(ids);
        foreach (var element in array.EnumerateArray())
        {
            var where = "traits[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "acceptance", "reliability", "counterIntel", "spyBonus"]);
            list.Add(new TraitDef(list.Count, ids[list.Count], _json.Id(element, "key", where), _json.Int(element, "acceptance", where, -2000, 2000), _json.Int(element, "reliability", where, -1000, 1000),
                _json.Int(element, "counterIntel", where, 0, 1000), _json.Int(element, "spyBonus", where, -500, 500)));
        }
        _parts.Traits = list;
    }

    private void ReadDeeds(JsonElement array)
    {
        var list = new List<DeedDef>();
        var ids = IdsOf(array, "deeds");
        _deedIndex = Indexed(ids);
        foreach (var element in array.EnumerateArray())
        {
            var where = "deeds[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "value", "days"]);
            var value = _json.Int(element, "value", where, -1000, 1000);
            if (value == 0) throw _json.Refuse(DipCodes.Range, where + ".value of 0 changes nothing");
            list.Add(new DeedDef(list.Count, ids[list.Count], _json.Id(element, "key", where), value, _json.Int(element, "days", where, 1, MaxDays)));
        }
        _parts.Deeds = list;
    }

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (DipFacts.IsReserved(id)) throw _json.Refuse(DipCodes.Reserved, where + " '" + id + "' is a name the engine gives itself");
            if (facts.ContainsKey(id)) throw _json.Refuse(DipCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(DipCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(DipCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(DipCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(DipCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    private void CheckAgainstLimits()
    {
        if (_parts.Agents.Count > _parts.Limits.MaxAgents) throw _json.Refuse(DipCodes.Range, "the document starts with " + _parts.Agents.Count + " agents and limits.maxAgents is " + _parts.Limits.MaxAgents);
    }
}
