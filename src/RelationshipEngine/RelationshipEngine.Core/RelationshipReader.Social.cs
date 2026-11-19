using System.Text.Json;
using EngineCommon;

namespace RelationshipEngine;

internal sealed partial class RelationshipReader
{
    private IReadOnlyList<LadderDef> ReadLadders(JsonElement array)
    {
        var ladders = new List<LadderDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var axisIndex = Index(_axes, a => a.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "ladders[" + ladders.Count + "]";
            _json.Object(element, where, ["id", "axis", "hysteresis", "steps"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "ladder");
            var axis = Ref(element, "axis", where, axisIndex, "axis");
            var def = _axes[axis];
            var hysteresis = _json.Long(element, "hysteresis", where, 0, (def.Max - def.Min) / 4);
            ladders.Add(new LadderDef(ladders.Count, id, axis, hysteresis, ReadSteps(_json.Array(element, "steps", where, 1, 16), where + ".steps", def.Min, def.Max)));
        }
        return ladders;
    }

    private IReadOnlyList<BondDef> ReadBonds(JsonElement array)
    {
        var bonds = new List<BondDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var axisIndex = Index(_axes, a => a.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "bonds[" + bonds.Count + "]";
            _json.Object(element, where, ["id", "key", "symmetric", "exclusive", "break"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "bond");
            var axis = -1;
            long below = 0;
            var breakElement = element.GetProperty("break");
            if (breakElement.ValueKind != JsonValueKind.Null)
            {
                _json.Object(breakElement, where + ".break", ["axis", "below"]);
                axis = Ref(breakElement, "axis", where + ".break", axisIndex, "axis");
                below = _json.Long(breakElement, "below", where + ".break", _axes[axis].Min + 1, _axes[axis].Max);
            }
            bonds.Add(new BondDef(bonds.Count, id, _json.Text(element, "key", where), _json.Bool(element, "symmetric", where), _json.Bool(element, "exclusive", where), axis, below));
        }
        return bonds;
    }

    private IReadOnlyList<GiftDef> ReadGifts(JsonElement array)
    {
        var gifts = new List<GiftDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "gifts[" + gifts.Count + "]";
            _json.Object(element, where, ["id", "key", "tags"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "gift");
            gifts.Add(new GiftDef(gifts.Count, id, _json.Text(element, "key", where), ReadTags(element, "tags", where)));
        }
        return gifts;
    }

    /// <summary>Five permille values in the order hate, dislike, neutral, like, love; they must not fall as the taste rises.</summary>
    private IReadOnlyList<int> ReadTastes(JsonElement array)
    {
        var tastes = new List<int>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value)) throw _json.Refuse(RelationshipCodes.Format, "tastes holds whole numbers");
            if (value < -5000 || value > 5000) throw _json.Refuse(RelationshipCodes.Range, "tastes[" + tastes.Count + "] " + value + " is outside -5000 to 5000");
            if (tastes.Count > 0 && value < tastes[^1]) throw _json.Refuse(RelationshipCodes.Range, "tastes[" + tastes.Count + "]: a better taste may not scale an effect lower than a worse one");
            tastes.Add(value);
        }
        return tastes;
    }

    private GossipDef ReadGossip(JsonElement element)
    {
        _json.Object(element, "gossip", ["trustAxis", "minTrust", "maxTellings", "maxListeners"]);
        var axis = Ref(element, "trustAxis", "gossip", Index(_axes, a => a.Id), "axis");
        return new GossipDef(axis, _json.Long(element, "minTrust", "gossip", _axes[axis].Min, _axes[axis].Max), _json.Int(element, "maxTellings", "gossip", 0, 8), _json.Int(element, "maxListeners", "gossip", 0, 8));
    }
}
