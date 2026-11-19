using System.Text.Json;
using EngineCommon;

namespace WorldEventsEngine;

internal sealed partial class WorldReader
{
    private void ReadDirector(JsonElement storytellers, JsonElement difficulties)
    {
        var tellers = new List<StorytellerDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in storytellers.EnumerateArray())
        {
            var where = "storytellers[" + tellers.Count + "]";
            _json.Object(element, where, ["id", "key", "decay", "peakAt", "relaxBelow", "peakHold", "restMin", "gapMin", "gapMax", "share"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "storyteller");
            var peakAt = _json.Int(element, "peakAt", where, 1, 1001);
            var relaxBelow = _json.Int(element, "relaxBelow", where, 0, 1000);
            if (relaxBelow >= peakAt) throw _json.Refuse(WorldCodes.Director, where + ": the director must relax below " + peakAt + " stress, not " + relaxBelow + ", or it would relax the moment it peaked");
            var gapMin = _json.Int(element, "gapMin", where, 0, 100_000);
            var gapMax = _json.Int(element, "gapMax", where, 1, 100_000);
            if (gapMin > gapMax) throw _json.Refuse(WorldCodes.Director, where + " waits at least " + gapMin + " steps between cards and at most " + gapMax);
            tellers.Add(new StorytellerDef(tellers.Count, id, _json.Id(element, "key", where), _json.Int(element, "decay", where, 0, 1000), peakAt, relaxBelow, _json.Int(element, "peakHold", where, 0, 100_000),
                _json.Int(element, "restMin", where, 0, 100_000), gapMin, gapMax, Ints(element, "share", where, Enum.GetValues<Tier>().Length, 0, 10_000)));
        }
        var levels = new List<DifficultyDef>();
        var seenLevels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in difficulties.EnumerateArray())
        {
            var where = "difficulties[" + levels.Count + "]";
            _json.Object(element, where, ["id", "key", "hazard", "bad", "disease", "stressGain"]);
            var id = _json.Id(element, "id", where);
            Unique(seenLevels, id, "difficulty");
            levels.Add(new DifficultyDef(levels.Count, id, _json.Id(element, "key", where), _json.Int(element, "hazard", where, 0, 10_000), _json.Int(element, "bad", where, 0, 10_000), _json.Int(element, "disease", where, 0, 10_000),
                _json.Int(element, "stressGain", where, 0, 10_000)));
        }
        _parts.Storytellers = tellers;
        _parts.Difficulties = levels;
    }

    private static readonly string[] CardFields = ["id", "key", "tier", "scope", "weight", "cooldown", "group", "duration", "intensity", "effects", "chain"];

    private IReadOnlyList<CardDef> ReadCards(JsonElement array)
    {
        var groups = new SortedSet<string>(StringComparer.Ordinal);
        var shaped = 0;
        foreach (var element in array.EnumerateArray())
        {
            var shape = "cards[" + shaped++ + "]";
            _json.Object(element, shape, CardFields, "when");
            var group = _json.Text(element, "group", shape, 128);
            if (group.Length > 0) groups.Add(_json.CheckId(group, shape + ".group"));
        }
        var groupIndex = Indexed(groups.ToList());
        _parts.Groups = groups.ToList();
        var cards = new List<CardDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "cards[" + cards.Count + "]";
            var tierName = _json.Text(element, "tier", where, 8);
            var tier = Words.Parse<Tier>(tierName);
            if (tier is null) throw _json.Refuse(WorldCodes.Format, where + ".tier '" + tierName + "' must be one of " + Words.Choices<Tier>());
            var scope = _json.Text(element, "scope", where, 8);
            if (scope is not ("region" or "global")) throw _json.Refuse(WorldCodes.Format, where + ".scope '" + scope + "' must be region or global");
            var global = scope == "global";
            var group = _json.Text(element, "group", where, 128);
            var intensity = _json.Int(element, "intensity", where, -1000, 1000);
            if (tier == Tier.Bad && intensity < 0 || tier == Tier.Good && intensity > 0) throw _json.Refuse(WorldCodes.Director, where + ": a " + Words.Name(tier.Value) + " card cannot " + (intensity < 0 ? "relieve" : "add") + " stress");
            cards.Add(new CardDef(cards.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), tier.Value, global, _json.Int(element, "weight", where, 0, 1_000_000),
                OptionalWhen(element, where, global ? _parts.GlobalFacts : _parts.LocalFacts), _json.Int(element, "cooldown", where, 0, 1_000_000), group.Length == 0 ? -1 : groupIndex[group],
                _json.Int(element, "duration", where, 0, 1_000_000), intensity, ReadEffects(element, where, global ? EffectScope.GlobalCard : EffectScope.RegionCard), ReadChain(element, where, global)));
        }
        return cards;
    }

    private IReadOnlyList<ChainDef> ReadChain(JsonElement owner, string where, bool global)
    {
        var chain = new List<ChainDef>();
        foreach (var element in _json.Array(owner, "chain", where, 0, MaxChain).EnumerateArray())
        {
            var at = where + ".chain[" + chain.Count + "]";
            _json.Object(element, at, ["card", "delay", "probability"], "when");
            chain.Add(new ChainDef(Ref(element, "card", at, _cardIndex, "card"), _json.Int(element, "delay", at, 1, 100_000), _json.Int(element, "probability", at, 1, 1000),
                OptionalWhen(element, at, global ? _parts.GlobalFacts : _parts.LocalFacts)));
        }
        return chain;
    }
}
