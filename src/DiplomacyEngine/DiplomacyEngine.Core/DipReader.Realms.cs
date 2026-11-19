using System.Text.Json;
using EngineCommon;

namespace DiplomacyEngine;

internal sealed partial class DipReader
{
    private IReadOnlyList<RealmDef> ReadRealms(JsonElement array)
    {
        var list = new List<RealmDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "realms[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "ideology", "traits", "might", "market", "leader", "control", "stability", "legitimacy", "successionPpm", "standing", "factions"]);
            var traits = new List<int>();
            foreach (var item in _json.Array(element, "traits", where, 0, MaxTraits).EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.String ? item.GetString()! : "";
                if (!_traitIndex.TryGetValue(name, out var trait)) throw _json.Refuse(DipCodes.Reference, where + ".traits '" + name + "' is not a declared trait");
                if (traits.Contains(trait)) throw _json.Refuse(DipCodes.Duplicate, where + ".traits names '" + name + "' twice");
                traits.Add(trait);
            }
            var market = _json.Text(element, "market", where, 128);
            if (market.Length > 0) _json.CheckId(market, where + ".market");
            var leader = _json.Text(element, "leader", where, 128);
            if (leader.Length > 0) _json.CheckId(leader, where + ".leader");
            var standing = element.GetProperty("standing");
            var at = where + ".standing";
            _json.Object(standing, at, ["trust", "fear", "honour", "infamy"]);
            list.Add(new RealmDef(list.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), Ref(element, "ideology", where, _ideologyIndex, "ideology"), traits, _json.Long(element, "might", where, 0, DipFacts.Saturate),
                market, leader, Word<Control>(element, "control", where), _json.Int(element, "stability", where, 0, 1000), _json.Int(element, "legitimacy", where, 0, 1000), _json.Int(element, "successionPpm", where, 0, 1_000_000),
                _json.Int(standing, "trust", at, -1000, 1000), _json.Int(standing, "fear", at, -1000, 1000), _json.Int(standing, "honour", at, -1000, 1000), _json.Int(standing, "infamy", at, 0, 1000),
                ReadFactions(_json.Array(element, "factions", where, 1, MaxFactions), where + ".factions")));
        }
        var markets = list.Where(r => r.Market.Length > 0).GroupBy(r => r.Market, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (markets is not null) throw _json.Refuse(DipCodes.Duplicate, "market '" + markets.Key + "' belongs to two realms");
        var leaders = list.Where(r => r.Leader.Length > 0).GroupBy(r => r.Leader, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (leaders is not null) throw _json.Refuse(DipCodes.Duplicate, "leader '" + leaders.Key + "' leads two realms");
        return list;
    }

    private IReadOnlyList<FactionDef> ReadFactions(JsonElement array, string where)
    {
        var list = new List<FactionDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(element, at, ["id", "key", "share", "satisfaction", "likes"]);
            var id = _json.Id(element, "id", at);
            Unique(seen, id, "faction");
            var likes = new List<LikeDef>();
            foreach (var item in _json.Array(element, "likes", at, 0, Enum.GetValues<PoliticalEvent>().Length).EnumerateArray())
            {
                var lw = at + ".likes[" + likes.Count + "]";
                _json.Object(item, lw, ["event", "delta"]);
                var political = Word<PoliticalEvent>(item, "event", lw);
                if (likes.Any(l => l.Event == political)) throw _json.Refuse(DipCodes.Duplicate, lw + " repeats the event '" + EnumNames.Name(political) + "'");
                likes.Add(new LikeDef(political, _json.Int(item, "delta", lw, -1000, 1000)));
            }
            list.Add(new FactionDef(list.Count, id, _json.Id(element, "key", at), _json.Int(element, "share", at, 1, 1000), _json.Int(element, "satisfaction", at, 0, 1000), likes));
        }
        return list;
    }

    private IReadOnlyList<AgentDef> ReadAgents(JsonElement array)
    {
        var list = new List<AgentDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "agents[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "realm", "skill"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "agent");
            list.Add(new AgentDef(id, _json.Id(element, "key", where), Ref(element, "realm", where, _realmIndex, "realm"), _json.Int(element, "skill", where, 0, 1000)));
        }
        return list;
    }
}
