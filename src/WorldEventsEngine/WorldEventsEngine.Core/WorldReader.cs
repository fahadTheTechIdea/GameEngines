using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

/// <summary>The reader of one document. It is single use: it holds the declarations as it finds them, in the order the document needs them.</summary>
internal sealed partial class WorldReader
{
    public const int MaxSeasons = 16, MaxWeathers = 16, MaxModifiers = 32, MaxZones = 32, MaxRegimes = 8, MaxRegions = 1024, MaxLinks = 8192, MaxHazards = 64, MaxRungs = 8, MaxDiseases = 32,
        MaxInterventions = 128, MaxStorytellers = 16, MaxDifficulties = 16, MaxCards = 512, MaxEffects = 8, MaxChain = 8, MaxHostFacts = 64, MaxReach = 8, MaxSeasonSteps = 3660, MaxPopulation = 1_000_000_000;

    /// <summary>The most a zone may let its neighbours tilt its weather (per mille). Below 1,000 so that a weather the zone can draw never has its weight tilted to nothing.</summary>
    public const int MaxSpill = 900;

    private readonly string _text;
    private readonly StrictJson _json = new(WorldCodes.ForDocument);
    private readonly Conditions _conditions;
    private readonly WorldParts _parts = new();
    private Dictionary<string, int> _seasonIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _weatherIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _modifierIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _hazardIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _diseaseIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _cardIndex = new(StringComparer.Ordinal);
    private Dictionary<string, int> _zoneIndex = new(StringComparer.Ordinal);
    private List<List<string>> _rungIds = new();

    public WorldReader(string text)
    {
        _text = text;
        _conditions = new Conditions(_json);
    }

    public WorldSet Read()
    {
        using var document = _json.Parse(_text, "the document");
        var root = _json.Object(document.RootElement, "the document",
            ["format", "id", "seed", "time", "limits", "hydrology", "seasons", "weathers", "modifiers", "zones", "regimes", "regions", "links", "hazards", "diseases", "interventions", "storytellers", "difficulties", "cards", "facts", "start"]);
        var format = _json.Long(root, "format", "the document", long.MinValue, long.MaxValue);
        if (format == 1) throw _json.Refuse(WorldCodes.FormatSuperseded, "format 1 is no longer read: add 'excite' to every hazard and 'spill' to every zone and say format " + WorldLoader.Format + " (nothing is migrated)");
        if (format != WorldLoader.Format) throw _json.Refuse(WorldCodes.Format, "this reader reads format " + WorldLoader.Format + " only");
        _parts.Id = _json.Id(root, "id", "the document");
        _parts.Digest = Digest.Of(_text);
        _parts.Seed = (ulong)_json.Long(root, "seed", "the document", 0, long.MaxValue);
        var time = _json.Object(root.GetProperty("time"), "time", ["stepsPerDay"]);
        _parts.StepsPerDay = _json.Int(time, "stepsPerDay", "time", 1, 1440);
        var limits = _json.Object(root.GetProperty("limits"), "limits", ["maxHazards", "maxEffects", "maxAgenda"]);
        _parts.Limits = new LimitsDef(_json.Int(limits, "maxHazards", "limits", 1, 100_000), _json.Int(limits, "maxEffects", "limits", 1, 100_000), _json.Int(limits, "maxAgenda", "limits", 1, 100_000));
        ReadHydrology(root.GetProperty("hydrology"));
        ReadSeasons(_json.Array(root, "seasons", "the document", 1, MaxSeasons));
        ReadModifiers(_json.Array(root, "modifiers", "the document", 0, MaxModifiers));
        ReadWeathers(_json.Array(root, "weathers", "the document", 1, MaxWeathers));
        ReadZones(_json.Array(root, "zones", "the document", 1, MaxZones));
        ReadRegimes(_json.Array(root, "regimes", "the document", 1, MaxRegimes));
        var hazardsArray = _json.Array(root, "hazards", "the document", 0, MaxHazards);
        var diseasesArray = _json.Array(root, "diseases", "the document", 0, MaxDiseases);
        var interventionsArray = _json.Array(root, "interventions", "the document", 0, MaxInterventions);
        var cardsArray = _json.Array(root, "cards", "the document", 0, MaxCards);
        var hazardIds = IdsOf(hazardsArray, "hazards");
        var diseaseIds = IdsOf(diseasesArray, "diseases");
        IdsOf(interventionsArray, "interventions");
        var cardIds = IdsOf(cardsArray, "cards");
        _hazardIndex = Indexed(hazardIds);
        _diseaseIndex = Indexed(diseaseIds);
        _cardIndex = Indexed(cardIds);
        _rungIds = RungIdsOf(hazardsArray);
        var host = ReadHostFacts(_json.Array(root, "facts", "the document", 0, MaxHostFacts));
        _parts.HostFacts = host;
        _parts.GlobalFacts = WorldFacts.Global(host);
        _parts.LocalFacts = WorldFacts.Local(hazardIds.Select((id, i) => (id, _rungIds[i].Count)).ToList(), diseaseIds, _parts.Modifiers, _parts.GlobalFacts);
        _parts.Regions = ReadRegions(_json.Array(root, "regions", "the document", 1, MaxRegions));
        _parts.Links = ReadLinks(_json.Array(root, "links", "the document", 0, MaxLinks));
        _parts.Hazards = ReadHazards(hazardsArray);
        CheckExclusions();
        _parts.Diseases = ReadDiseases(diseasesArray);
        _parts.Interventions = ReadInterventions(interventionsArray);
        ReadDirector(_json.Array(root, "storytellers", "the document", 1, MaxStorytellers), _json.Array(root, "difficulties", "the document", 1, MaxDifficulties));
        _parts.Cards = ReadCards(cardsArray);
        ReadStart(root.GetProperty("start"));
        return new WorldSet(_parts);
    }

    // ---- shared helpers -------------------------------------------------------------------------------------------------------------

    private static Dictionary<string, int> Indexed(IReadOnlyList<string> ids) => ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.Ordinal);

    private int Ref(JsonElement owner, string field, string where, IReadOnlyDictionary<string, int> declared, string what)
    {
        var id = _json.Id(owner, field, where);
        if (!declared.TryGetValue(id, out var index)) throw _json.Refuse(WorldCodes.Reference, where + "." + field + " '" + id + "' is not a declared " + what);
        return index;
    }

    private void Unique(HashSet<string> ids, string id, string what)
    {
        if (!ids.Add(id)) throw _json.Refuse(WorldCodes.Duplicate, what + " '" + id + "' is declared twice");
    }

    /// <summary>The ids of a list of objects, checked and unique, so references and facts can be built before the objects are read whole.</summary>
    private List<string> IdsOf(JsonElement array, string what)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("id", out _)) throw _json.Refuse(WorldCodes.Format, what + "[" + ids.Count + "] must be an object with an id");
            var id = _json.Id(element, "id", what + "[" + ids.Count + "]");
            Unique(seen, id, what.TrimEnd('s'));
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>The rung ids of every hazard, read before the hazards are, because one hazard may start another at a named rung.</summary>
    private List<List<string>> RungIdsOf(JsonElement hazards)
    {
        var all = new List<List<string>>();
        foreach (var hazard in hazards.EnumerateArray())
        {
            var where = "hazards[" + all.Count + "]";
            if (!hazard.TryGetProperty("ladder", out var ladder) || ladder.ValueKind != JsonValueKind.Array) throw _json.Refuse(WorldCodes.MissingField, where + " is missing 'ladder'");
            if (ladder.GetArrayLength() is < 1 or > MaxRungs) throw _json.Refuse(WorldCodes.Range, where + ".ladder must hold 1 to " + MaxRungs + " rungs");
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rung in ladder.EnumerateArray())
            {
                if (rung.ValueKind != JsonValueKind.Object || !rung.TryGetProperty("id", out _)) throw _json.Refuse(WorldCodes.Format, where + ".ladder[" + ids.Count + "] must be an object with an id");
                var id = _json.Id(rung, "id", where + ".ladder[" + ids.Count + "]");
                Unique(seen, id, "rung");
                ids.Add(id);
            }
            all.Add(ids);
        }
        return all;
    }

    private int[] Ints(JsonElement owner, string field, string where, int count, int minimum, int maximum)
    {
        var array = _json.Array(owner, field, where, count, count);
        var values = new int[count];
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var number)) throw _json.Refuse(WorldCodes.Format, where + "." + field + "[" + i + "] must be a whole number");
            if (number < minimum || number > maximum) throw _json.Refuse(WorldCodes.Range, where + "." + field + "[" + i + "] must be " + minimum + " to " + maximum + ", not " + number);
            values[i++] = number;
        }
        return values;
    }

    private Condition? OptionalWhen(JsonElement owner, string where, IReadOnlyDictionary<string, FactDefinition> facts) =>
        _json.Has(owner, "when") ? _conditions.Read(owner.GetProperty("when"), where + ".when", facts) : null;

    // ---- the climate -----------------------------------------------------------------------------------------------------------------

    private void ReadHydrology(JsonElement element)
    {
        const string where = "hydrology";
        _json.Object(element, where, ["freezing", "soilCapacity", "initialSoil", "evapPerDegree", "meltPerDegree", "drainPermille", "wetDecay", "heavyPermille", "heavyFactor"]);
        _parts.Hydrology = new HydrologyDef(_json.Int(element, "freezing", where, -2000, 2000), _json.Int(element, "soilCapacity", where, 1, 100_000), _json.Int(element, "initialSoil", where, 0, 1000),
            _json.Int(element, "evapPerDegree", where, 0, 1000), _json.Int(element, "meltPerDegree", where, 0, 10_000), _json.Int(element, "drainPermille", where, 1, 1000), _json.Int(element, "wetDecay", where, 1, 1000),
            _json.Int(element, "heavyPermille", where, 0, 1000), _json.Int(element, "heavyFactor", where, 1000, 20_000));
    }

    private void ReadSeasons(JsonElement array)
    {
        var seasons = new List<SeasonDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "seasons[" + seasons.Count + "]";
            _json.Object(element, where, ["id", "key", "steps"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "season");
            seasons.Add(new SeasonDef(seasons.Count, id, _json.Id(element, "key", where), _json.Int(element, "steps", where, 1, MaxSeasonSteps)));
        }
        _parts.Seasons = seasons;
        _seasonIndex = Indexed(seasons.Select(s => s.Id).ToList());
    }

    private void ReadModifiers(JsonElement array)
    {
        var modifiers = new List<ModifierDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "modifiers[" + modifiers.Count + "]";
            _json.Object(element, where, ["id", "key", "base", "min", "max"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "modifier");
            var min = _json.Int(element, "min", where, -1_000_000, 1_000_000);
            var max = _json.Int(element, "max", where, -1_000_000, 1_000_000);
            var origin = _json.Int(element, "base", where, -1_000_000, 1_000_000);
            if (min > max || origin < min || origin > max) throw _json.Refuse(WorldCodes.Range, where + " needs min <= base <= max");
            modifiers.Add(new ModifierDef(modifiers.Count, id, _json.Id(element, "key", where), origin, min, max));
        }
        _parts.Modifiers = modifiers;
        _modifierIndex = Indexed(modifiers.Select(m => m.Id).ToList());
    }

    private IReadOnlyList<ModifierEffect> ReadModifierEffects(JsonElement array, string where)
    {
        var list = new List<ModifierEffect>();
        var used = new HashSet<int>();
        foreach (var item in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(item, at, ["modifier", "delta"]);
            var modifier = Ref(item, "modifier", at, _modifierIndex, "modifier");
            if (!used.Add(modifier)) throw _json.Refuse(WorldCodes.Duplicate, at + " changes modifier '" + _parts.Modifiers[modifier].Id + "' twice");
            list.Add(new ModifierEffect(modifier, _json.Int(item, "delta", at, -2_000_000, 2_000_000)));
        }
        return list;
    }

    private void ReadWeathers(JsonElement array)
    {
        var weathers = new List<WeatherDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "weathers[" + weathers.Count + "]";
            _json.Object(element, where, ["id", "key", "tempOffset", "precip", "wind", "wet", "modifiers"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "weather");
            var wet = _json.Bool(element, "wet", where);
            var precip = _json.Int(element, "precip", where, 0, 100_000);
            if (wet != (precip > 0)) throw _json.Refuse(WorldCodes.Climate, where + " is " + (wet ? "wet and gives no precipitation" : "dry and gives precipitation"));
            weathers.Add(new WeatherDef(weathers.Count, id, _json.Id(element, "key", where), _json.Int(element, "tempOffset", where, -5000, 5000), precip, _json.Int(element, "wind", where, 0, 2000), wet,
                ReadModifierEffects(_json.Array(element, "modifiers", where, 0, MaxModifiers), where + ".modifiers")));
        }
        _parts.Weathers = weathers;
        _weatherIndex = Indexed(weathers.Select(w => w.Id).ToList());
    }

    private void ReadZones(JsonElement array)
    {
        var zones = new List<ZoneDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seasons = _parts.Seasons.Count;
        var weathers = _parts.Weathers.Count;
        foreach (var element in array.EnumerateArray())
        {
            var where = "zones[" + zones.Count + "]";
            _json.Object(element, where, ["id", "key", "koppen", "spill", "seasons"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "zone");
            var climates = new List<ClimateDef>();
            foreach (var item in _json.Array(element, "seasons", where, seasons, seasons).EnumerateArray())
            {
                var at = where + ".seasons[" + climates.Count + "]";
                _json.Object(item, at, ["temp", "sigma", "phi", "wind", "persistence", "frequency"]);
                var frequency = Ints(item, "frequency", at, weathers, 0, 1_000_000);
                if (frequency.Sum(f => (long)f) == 0) throw _json.Refuse(WorldCodes.Climate, at + ".frequency gives no weather any weight");
                climates.Add(new ClimateDef(_json.Int(item, "temp", at, -6000, 6000), _json.Int(item, "sigma", at, 0, 3000), _json.Int(item, "phi", at, 0, 999), _json.Int(item, "wind", at, 0, 2000),
                    _json.Int(item, "persistence", at, 0, 1000), frequency));
            }
            zones.Add(new ZoneDef(zones.Count, id, _json.Id(element, "key", where), _json.Text(element, "koppen", where, 8), _json.Int(element, "spill", where, 0, MaxSpill), climates));
        }
        _parts.Zones = zones;
        _zoneIndex = Indexed(zones.Select(z => z.Id).ToList());
    }

    private void ReadRegimes(JsonElement array)
    {
        var regimes = new List<RegimeDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "regimes[" + regimes.Count + "]";
            _json.Object(element, where, ["id", "key", "persistence", "frequency", "bias"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "regime");
            regimes.Add(new RegimeDef(regimes.Count, id, _json.Id(element, "key", where), _json.Int(element, "persistence", where, 0, 1000), Ints(element, "frequency", where, _parts.Seasons.Count, 0, 1_000_000),
                Ints(element, "bias", where, _parts.Weathers.Count, 0, 10_000)));
        }
        for (var s = 0; s < _parts.Seasons.Count; s++)
            if (regimes.Sum(r => (long)r.Frequency[s]) == 0) throw _json.Refuse(WorldCodes.Climate, "no regime can be chosen in season '" + _parts.Seasons[s].Id + "'");
        // Whatever regime holds, every zone must still be able to draw some weather in every season, or the draw would have nothing to choose.
        foreach (var regime in regimes)
            foreach (var zone in _parts.Zones)
                for (var s = 0; s < _parts.Seasons.Count; s++)
                    if (Enumerable.Range(0, _parts.Weathers.Count).Sum(w => (long)zone.Seasons[s].Frequency[w] * regime.Bias[w]) == 0)
                        throw _json.Refuse(WorldCodes.Climate, "regime '" + regime.Id + "' leaves zone '" + zone.Id + "' no weather in season '" + _parts.Seasons[s].Id + "'");
        _parts.Regimes = regimes;
    }

    // ---- the places ------------------------------------------------------------------------------------------------------------------

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (WorldFacts.IsReserved(id)) throw _json.Refuse(WorldCodes.Reserved, where + " '" + id + "' is a name the engine gives itself");
            if (facts.ContainsKey(id)) throw _json.Refuse(WorldCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(WorldCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(WorldCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(WorldCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(WorldCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    private IReadOnlyList<RegionDef> ReadRegions(JsonElement array)
    {
        var regions = new List<RegionDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "regions[" + regions.Count + "]";
            _json.Object(element, where, ["id", "key", "zone", "market", "population", "exposure", "immune"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "region");
            var market = _json.Text(element, "market", where, 128);
            if (market.Length > 0) _json.CheckId(market, where + ".market");
            var exposure = new int[_hazardIndex.Count];
            var exposed = new HashSet<int>();
            foreach (var item in _json.Array(element, "exposure", where, 0, MaxHazards).EnumerateArray())
            {
                var at = where + ".exposure[" + exposed.Count + "]";
                _json.Object(item, at, ["hazard", "permille"]);
                var hazard = Ref(item, "hazard", at, _hazardIndex, "hazard");
                if (!exposed.Add(hazard)) throw _json.Refuse(WorldCodes.Duplicate, at + " repeats hazard '" + _json.Id(item, "hazard", at) + "'");
                exposure[hazard] = _json.Int(item, "permille", at, 1, 10_000);
            }
            var immune = new int[_diseaseIndex.Count];
            var immunised = new HashSet<int>();
            foreach (var item in _json.Array(element, "immune", where, 0, MaxDiseases).EnumerateArray())
            {
                var at = where + ".immune[" + immunised.Count + "]";
                _json.Object(item, at, ["disease", "permille"]);
                var disease = Ref(item, "disease", at, _diseaseIndex, "disease");
                if (!immunised.Add(disease)) throw _json.Refuse(WorldCodes.Duplicate, at + " repeats disease '" + _json.Id(item, "disease", at) + "'");
                immune[disease] = _json.Int(item, "permille", at, 1, 1000);
            }
            regions.Add(new RegionDef(regions.Count, id, _json.Id(element, "key", where), Ref(element, "zone", where, _zoneIndex, "zone"), market, _json.Long(element, "population", where, 0, MaxPopulation), exposure, immune));
        }
        return regions;
    }

    private IReadOnlyList<LinkDef> ReadLinks(JsonElement array)
    {
        var links = new List<LinkDef>();
        var index = _parts.Regions.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        var seen = new HashSet<(int, int)>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "links[" + links.Count + "]";
            _json.Object(element, where, ["a", "b", "flowPpm"]);
            var a = Ref(element, "a", where, index, "region");
            var b = Ref(element, "b", where, index, "region");
            if (a == b) throw _json.Refuse(WorldCodes.Links, where + " joins region '" + _parts.Regions[a].Id + "' to itself");
            if (!seen.Add((Math.Min(a, b), Math.Max(a, b)))) throw _json.Refuse(WorldCodes.Links, where + " joins '" + _parts.Regions[a].Id + "' and '" + _parts.Regions[b].Id + "' a second time");
            links.Add(new LinkDef(a, b, _json.Int(element, "flowPpm", where, 0, 1_000_000)));
        }
        return links;
    }

    private void ReadStart(JsonElement element)
    {
        _json.Object(element, "start", ["storyteller", "difficulty"]);
        _parts.StartStoryteller = Ref(element, "storyteller", "start", Indexed(_parts.Storytellers.Select(s => s.Id).ToList()), "storyteller");
        _parts.StartDifficulty = Ref(element, "difficulty", "start", Indexed(_parts.Difficulties.Select(d => d.Id).ToList()), "difficulty");
    }
}
