using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace EconomyEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class EconomyReader
{
    public const int MaxMarkets = 1000;
    public const int MaxGoods = 1000;
    public const int MaxRoutes = 10_000;
    public const int MaxCurrencies = 32;
    public const int MaxHistory = 64;
    public const long MaxQuantity = 1_000_000_000;
    public const long MaxCapacity = 1_000_000_000_000;

    private readonly string _text;
    private readonly StrictJson _json = new(EconomyCodes.ForDocument);
    private readonly Conditions _conditions;
    private IReadOnlyDictionary<string, FactDefinition> _hostFacts = new Dictionary<string, FactDefinition>();
    private IReadOnlyList<CurrencyDef> _currencies = Array.Empty<CurrencyDef>();
    private IReadOnlyList<GoodDef> _goods = Array.Empty<GoodDef>();
    private IReadOnlyList<ScheduleDef> _schedules = Array.Empty<ScheduleDef>();
    private IReadOnlyList<PolicyDef> _policies = Array.Empty<PolicyDef>();
    private IReadOnlyList<MarketDef> _markets = Array.Empty<MarketDef>();
    private IReadOnlyList<RouteDef> _routes = Array.Empty<RouteDef>();
    private Dictionary<string, int> _currencyIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _goodIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _scheduleIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _policyIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _marketIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _routeIndex = new(StringComparer.Ordinal);

    public EconomyReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public EconomySet Read()
    {
        using var document = _json.Parse(_text, "the document");
        CheckFormatFirst(document.RootElement);
        var root = _json.Object(document.RootElement, "the document",
            ["format", "id", "seed", "time", "limits", "price", "routing", "recipes", "currencies", "goods", "bands", "schedules", "policies", "modifiers", "shocks", "markets", "routes", "loans", "facts"]);
        if (_json.Long(root, "format", "the document", long.MinValue, long.MaxValue) != EconomyLoader.Format) throw _json.Refuse(EconomyCodes.Format, "this reader reads format " + EconomyLoader.Format + " only");
        var id = _json.Id(root, "id", "the document");
        var seed = (ulong)_json.Long(root, "seed", "the document", 0, 9_000_000_000_000_000);
        var time = _json.Object(root.GetProperty("time"), "time", ["stepsPerDay", "periodSteps"]);
        var stepsPerDay = _json.Int(time, "stepsPerDay", "time", 1, 288);
        var periodSteps = _json.Int(time, "periodSteps", "time", 1, 100_000);
        var limits = ReadLimits(root.GetProperty("limits"));
        var price = ReadPrice(root.GetProperty("price"));
        var routing = ReadRouting(root.GetProperty("routing"));
        _hostFacts = ReadHostFacts(_json.Array(root, "facts", "the document", 0, 64));
        _currencies = ReadCurrencies(_json.Array(root, "currencies", "the document", 1, MaxCurrencies));
        _goods = ReadGoods(_json.Array(root, "goods", "the document", 1, MaxGoods));
        var bands = ReadBands(_json.Array(root, "bands", "the document", 1, 8));
        _schedules = ReadSchedules(_json.Array(root, "schedules", "the document", 0, 64));
        _policies = ReadPolicies(_json.Array(root, "policies", "the document", 0, 256));
        var modifiers = ReadModifiers(_json.Array(root, "modifiers", "the document", 0, 256));
        _recipes = ReadRecipes(_json.Array(root, "recipes", "the document", 0, MaxRecipes));
        _markets = ReadMarkets(_json.Array(root, "markets", "the document", 1, MaxMarkets));
        var recipeOrder = OrderRecipes(_recipes);
        _routes = ReadRoutes(_json.Array(root, "routes", "the document", 0, MaxRoutes));
        var shocks = ReadShocks(_json.Array(root, "shocks", "the document", 0, 256));
        var loans = ReadLoans(_json.Array(root, "loans", "the document", 0, 100_000), limits);
        return new EconomySet(id, Digest.Of(_text), seed, stepsPerDay, periodSteps, routing, limits, price, _currencies, _goods, bands, _schedules, _policies, modifiers, shocks, _markets, _routes, loans, _hostFacts, _recipes, recipeOrder);
    }

    /// <summary>
    /// A document of another format is refused for its format before anything else about it is looked at, so an old (format 1) document is told it is old and not that a field it never had is missing.
    /// </summary>
    private void CheckFormatFirst(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt64(out var number) || number == EconomyLoader.Format) return;
        if (number == 2) throw _json.Refuse(EconomyCodes.Format2, "this reader reads format " + EconomyLoader.Format + " only; format 2 is no longer read (format 3 gave recipes their meaning: a recipe array that was empty may now hold production recipes, and every market has a lines array; format 4 adds recipe requirements and line throttles)");
        if (number == 3) throw _json.Refuse(EconomyCodes.Format3, "this reader reads format " + EconomyLoader.Format + " only; format 3 is no longer read (format 4 makes supply shocks and modifiers act on what a recipe makes, and adds the optional recipe field 'requires', goods a line must hold but never uses up, and the optional line field 'throttle', a margin throttle; raise the format to 5 and review the shocks; format 5 adds tool wear and the profit margin)");
        if (number == 4) throw _json.Refuse(EconomyCodes.Format4, "this reader reads format " + EconomyLoader.Format + " only; format 4 is no longer read (format 5 adds the optional 'wear' of a required good, 'units' lost for every 'runs' runs of a line, and the optional 'costs' of a line's throttle, a wage and an overhead per run that make its margin a profit margin; a format 4 document has neither, so raise the format to 5)");
        if (number == 5) throw _json.Refuse(EconomyCodes.Format5, "this reader reads format " + EconomyLoader.Format + " only; format 5 is no longer read (format 6 makes a required good, a tool or catalyst, held by one line at a time within a period: a line that runs holds the amount it requires and a later line of the same market sees only the rest; a format 5 document whose lines share a required good would run differently, so raise the format to 6 and review them)");
        throw _json.Refuse(EconomyCodes.Format, "this reader reads format " + EconomyLoader.Format + " only; format " + number + " is no longer read");
    }

    private static readonly IReadOnlyDictionary<string, RoutingMode> RoutingModes = new Dictionary<string, RoutingMode>(StringComparer.Ordinal)
    {
        ["direct"] = RoutingMode.Direct, ["landed"] = RoutingMode.Landed,
    };

    private RoutingMode ReadRouting(JsonElement element)
    {
        _json.Object(element, "routing", ["mode"]);
        return Choice(element, "mode", "routing", RoutingModes);
    }

    // ---- shared helpers -------------------------------------------------------------------------------------------------------------

    /// <summary>Reads a text field that must be one of a closed set of words, so an unknown word is refused at one place.</summary>
    private T Choice<T>(JsonElement owner, string field, string where, IReadOnlyDictionary<string, T> words)
    {
        var word = _json.Text(owner, field, where, 32);
        if (words.TryGetValue(word, out var value)) return value;
        throw _json.Refuse(EconomyCodes.Format, where + "." + field + " '" + word + "' must be one of " + string.Join(", ", words.Keys));
    }

    /// <summary>The index a reference names, or a refusal that says what was not declared.</summary>
    private int Ref(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Id(owner, field, where);
        if (declared.TryGetValue(id, out var index)) return index;
        throw _json.Refuse(EconomyCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
    }

    /// <summary>A reference that may be empty (-1).</summary>
    private int OptionalRef(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Text(owner, field, where, 128);
        if (id.Length == 0) return -1;
        _json.CheckId(id, where + "." + field);
        if (declared.TryGetValue(id, out var index)) return index;
        throw _json.Refuse(EconomyCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
    }

    private static Dictionary<string, int> Index<T>(IReadOnlyList<T> list, Func<T, string> id) => list.Select((item, i) => (id(item), i)).ToDictionary(p => p.Item1, p => p.i, StringComparer.Ordinal);

    private IReadOnlyList<string> ReadTags(JsonElement owner, string field, string where)
    {
        var tags = new List<string>();
        foreach (var tag in _json.Array(owner, field, where, 0, 8).EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String) throw _json.Refuse(EconomyCodes.Format, where + "." + field + " must hold texts");
            var text = _json.CheckId(tag.GetString()!, where + "." + field);
            if (tags.Contains(text)) throw _json.Refuse(EconomyCodes.Duplicate, where + "." + field + " repeats '" + text + "'");
            tags.Add(text);
        }
        tags.Sort(StringComparer.Ordinal);
        return tags;
    }

    private void Unique(HashSet<string> ids, string id, string what)
    {
        if (!ids.Add(id)) throw _json.Refuse(EconomyCodes.Duplicate, what + " '" + id + "' is declared twice");
    }

    private LimitsDef ReadLimits(JsonElement element)
    {
        _json.Object(element, "limits", ["maxShipments", "maxLoans", "maxShocks", "history"]);
        return new LimitsDef(_json.Int(element, "maxShipments", "limits", 1, 1_000_000), _json.Int(element, "maxLoans", "limits", 0, 100_000),
            _json.Int(element, "maxShocks", "limits", 1, 10_000), _json.Int(element, "history", "limits", 0, MaxHistory));
    }

    private PriceRules ReadPrice(JsonElement element)
    {
        _json.Object(element, "price", ["responseMin", "responseMax", "settlePeriods", "bandMargin", "noise", "arrearsPenalty"]);
        return new PriceRules(_json.Int(element, "responseMin", "price", 0, 1000), _json.Int(element, "responseMax", "price", 1000, 10_000), _json.Int(element, "settlePeriods", "price", 1, 1000),
            _json.Int(element, "bandMargin", "price", 0, 1000), _json.Int(element, "noise", "price", 0, 500), _json.Int(element, "arrearsPenalty", "price", 0, 1000));
    }

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (EconomyFacts.IsReserved(id)) throw _json.Refuse(EconomyCodes.Reserved, where + " '" + id + "': the prefixes 'market.' and 'currency.' are the engine's own");
            if (facts.ContainsKey(id)) throw _json.Refuse(EconomyCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(EconomyCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(EconomyCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(EconomyCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(EconomyCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    private IReadOnlyList<CurrencyDef> ReadCurrencies(JsonElement array)
    {
        var list = new List<CurrencyDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "currencies[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "reference", "anchor", "money", "growthPpm", "maxRateStepPpm"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "currency");
            var reference = _json.Bool(element, "reference", where);
            var anchor = _json.Long(element, "anchor", where, 1, EconomyFacts.MaxRate);
            if (reference && anchor != EconomyMath.Ppm) throw _json.Refuse(EconomyCodes.Currency, where + " is the reference currency, so its anchor is " + EconomyMath.Ppm);
            list.Add(new CurrencyDef(list.Count, id, _json.Id(element, "key", where), reference, anchor, _json.Long(element, "money", where, 0, EconomyFacts.MaxMoney),
                _json.Long(element, "growthPpm", where, -500_000, 500_000), _json.Long(element, "maxRateStepPpm", where, 0, EconomyMath.Ppm)));
        }
        var references = list.Count(c => c.Reference);
        if (references != 1) throw _json.Refuse(EconomyCodes.Currency, "exactly one currency is the reference, not " + references);
        _currencyIndex = Index(list, c => c.Id);
        return list;
    }

    private IReadOnlyList<GoodDef> ReadGoods(JsonElement array)
    {
        var list = new List<GoodDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "goods[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "base", "demandElasticity", "supplyElasticity", "floor", "ceiling", "speed", "maxStep", "inertia", "decay", "weight", "tags"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "good");
            list.Add(new GoodDef(list.Count, id, _json.Id(element, "key", where), _json.Long(element, "base", where, 1000, 1_000_000_000), _json.Int(element, "demandElasticity", where, 0, 5000),
                _json.Int(element, "supplyElasticity", where, 0, 5000), _json.Int(element, "floor", where, 1, 1000), _json.Int(element, "ceiling", where, 1000, 100_000), _json.Int(element, "speed", where, 0, 1000),
                _json.Int(element, "maxStep", where, 1, 1000), _json.Int(element, "inertia", where, 0, 950), _json.Int(element, "decay", where, 0, 1000), _json.Long(element, "weight", where, 0, 1_000_000),
                ReadTags(element, "tags", where)));
        }
        _goodIndex = Index(list, g => g.Id);
        return list;
    }

    private IReadOnlyList<BandDef> ReadBands(JsonElement array)
    {
        var list = new List<BandDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "bands[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "from"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "band");
            var from = _json.Long(element, "from", where, 0, EconomyFacts.MaxRatio);
            if (list.Count == 0 && from != 0) throw _json.Refuse(EconomyCodes.Band, where + " is the first band and starts at 0");
            if (list.Count > 0 && from <= list[^1].From) throw _json.Refuse(EconomyCodes.Band, where + " starts at " + from + ", not above the band before it (" + list[^1].From + ")");
            list.Add(new BandDef(list.Count, id, _json.Id(element, "key", where), from));
        }
        return list;
    }

    private IReadOnlyList<ScheduleDef> ReadSchedules(JsonElement array)
    {
        var list = new List<ScheduleDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "schedules[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "every", "offset", "manual"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "schedule");
            var every = _json.Int(element, "every", where, 1, 10_000);
            var offset = _json.Int(element, "offset", where, 0, 9_999);
            if (offset >= every) throw _json.Refuse(EconomyCodes.Schedule, where + " has offset " + offset + ", which must be below every (" + every + ")");
            list.Add(new ScheduleDef(list.Count, id, _json.Id(element, "key", where), every, offset, _json.Bool(element, "manual", where)));
        }
        _scheduleIndex = Index(list, s => s.Id);
        return list;
    }

    private static readonly IReadOnlyDictionary<string, PolicyKind> PolicyKinds = new Dictionary<string, PolicyKind>(StringComparer.Ordinal)
    {
        ["sales"] = PolicyKind.Sales, ["import"] = PolicyKind.Import, ["export"] = PolicyKind.Export, ["subsidy"] = PolicyKind.Subsidy,
    };

    private IReadOnlyList<PolicyDef> ReadPolicies(JsonElement array)
    {
        var list = new List<PolicyDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "policies[" + list.Count + "]";
            _json.Object(element, where, ["id", "kind", "rate", "perUnit", "tags", "schedule"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "policy");
            var kind = Choice(element, "kind", where, PolicyKinds);
            var rate = _json.Int(element, "rate", where, 0, 1000);
            var perUnit = _json.Long(element, "perUnit", where, 0, 1_000_000_000);
            var schedule = OptionalRef(element, "schedule", where, _scheduleIndex, "schedule");
            if (kind == PolicyKind.Subsidy)
            {
                if (rate != 0 || perUnit == 0 || schedule != -1) throw _json.Refuse(EconomyCodes.Policy, where + " is a subsidy: it has a perUnit amount, no rate and no schedule (it is paid every period)");
            }
            else if (rate == 0 || perUnit != 0 || schedule == -1) throw _json.Refuse(EconomyCodes.Policy, where + " is a " + kind.ToString().ToLowerInvariant() + " tax: it has a rate and a schedule to be remitted on, and no perUnit amount");
            list.Add(new PolicyDef(list.Count, id, kind, rate, perUnit, ReadTags(element, "tags", where), schedule));
        }
        _policyIndex = Index(list, p => p.Id);
        return list;
    }

    private static readonly IReadOnlyDictionary<string, ModifierKind> ModifierKinds = new Dictionary<string, ModifierKind>(StringComparer.Ordinal)
    {
        ["supply"] = ModifierKind.Supply, ["demand"] = ModifierKind.Demand,
    };

    private IReadOnlyList<ModifierDef> ReadModifiers(JsonElement array)
    {
        var list = new List<ModifierDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "modifiers[" + list.Count + "]";
            _json.Object(element, where, ["id", "kind", "good", "tag", "permille", "when"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "modifier");
            var good = OptionalRef(element, "good", where, _goodIndex, "good");
            var tag = _json.Text(element, "tag", where, 128);
            if (tag.Length > 0) _json.CheckId(tag, where + ".tag");
            if (good != -1 && tag.Length > 0) throw _json.Refuse(EconomyCodes.Format, where + " names a good and a tag; use one");
            var condition = _conditions.Read(element.GetProperty("when"), where + ".when", _hostFacts);
            list.Add(new ModifierDef(id, Choice(element, "kind", where, ModifierKinds), good, tag, _json.Int(element, "permille", where, -1000, 10_000), condition, Conditions.FactsOf(condition)));
        }
        return list;
    }
}
