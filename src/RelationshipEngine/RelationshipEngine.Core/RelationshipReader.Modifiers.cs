using System.Text.Json;
using EngineCommon;

namespace RelationshipEngine;

internal sealed partial class RelationshipReader
{
    private static readonly IReadOnlyDictionary<string, DecayKind> DecayKinds = new Dictionary<string, DecayKind>(StringComparer.Ordinal)
    {
        ["fixed"] = DecayKind.Fixed, ["linear"] = DecayKind.Linear, ["halfLife"] = DecayKind.HalfLife,
    };

    private static readonly IReadOnlyDictionary<string, StackMode> StackModes = new Dictionary<string, StackMode>(StringComparer.Ordinal)
    {
        ["refresh"] = StackMode.Refresh, ["stack"] = StackMode.Stack, ["replace"] = StackMode.Replace, ["unique"] = StackMode.Unique,
    };

    private static readonly IReadOnlyDictionary<string, Sign> Signs = new Dictionary<string, Sign>(StringComparer.Ordinal) { ["gain"] = Sign.Gain, ["loss"] = Sign.Loss };

    private DecayDef ReadDecay(JsonElement owner, string where, int duration)
    {
        var element = _json.Object(owner.GetProperty("decay"), where + ".decay", ["kind"], "halfLife");
        var kind = Choice(element, "kind", where + ".decay", DecayKinds);
        var has = _json.Has(element, "halfLife");
        if (kind == DecayKind.HalfLife)
        {
            if (!has) throw _json.Refuse(RelationshipCodes.Decay, where + ".decay of kind halfLife needs 'halfLife'");
            return new DecayDef(kind, _json.Int(element, "halfLife", where + ".decay", 1, duration));
        }
        if (has) throw _json.Refuse(RelationshipCodes.Decay, where + ".decay of kind " + _json.Text(element, "kind", where, 32) + " has no 'halfLife'");
        return new DecayDef(kind, 0);
    }

    private StackDef ReadStack(JsonElement owner, string where)
    {
        var element = _json.Object(owner.GetProperty("stack"), where + ".stack", ["mode", "max"]);
        var mode = Choice(element, "mode", where + ".stack", StackModes);
        var max = _json.Int(element, "max", where + ".stack", 1, 16);
        if (mode != StackMode.Stack && max != 1) throw _json.Refuse(RelationshipCodes.Stack, where + ".stack: only the mode 'stack' keeps more than one instance, so max must be 1");
        return new StackDef(mode, max);
    }

    // ---- moodlets ---------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<MoodletDef> ReadMoodlets(JsonElement array)
    {
        var moodlets = new List<MoodletDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "moodlets[" + moodlets.Count + "]";
            _json.Object(element, where, ["id", "key", "magnitude", "duration", "decay", "stack", "tags"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "moodlet");
            var magnitude = _json.Long(element, "magnitude", where, -Bound, Bound);
            if (magnitude == 0) throw _json.Refuse(RelationshipCodes.Range, where + ".magnitude must not be 0");
            var duration = _json.Int(element, "duration", where, 1, 1_000_000);
            moodlets.Add(new MoodletDef(moodlets.Count, id, _json.Text(element, "key", where), magnitude, duration, ReadDecay(element, where, duration), ReadStack(element, where), ReadTags(element, "tags", where)));
        }
        return moodlets;
    }

    // ---- memories ---------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<MemoryDef> ReadMemories(JsonElement array)
    {
        // Ids first, because a memory may name another (or itself) as what it becomes when it is told.
        var ids = new List<string>();
        foreach (var element in array.EnumerateArray())
        {
            _json.Object(element, "memories[" + ids.Count + "]", ["id", "key", "effects", "duration", "decay", "stack", "tags", "spread"]);
            var id = _json.Id(element, "id", "memories[" + ids.Count + "]");
            if (ids.Contains(id)) throw _json.Refuse(RelationshipCodes.Duplicate, "memory '" + id + "' is declared twice");
            ids.Add(id);
        }
        var index = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.Ordinal);
        var axisIndex = Index(_axes, a => a.Id);
        var memories = new List<MemoryDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "memories[" + memories.Count + "]";
            var duration = _json.Int(element, "duration", where, 1, 10_000_000);
            var effects = new List<MemoryEffect>();
            foreach (var effect in _json.Array(element, "effects", where, 1, 4).EnumerateArray())
            {
                var at = where + ".effects[" + effects.Count + "]";
                _json.Object(effect, at, ["axis", "magnitude"]);
                var axis = Ref(effect, "axis", at, axisIndex, "axis");
                if (effects.Any(e => e.Axis == axis)) throw _json.Refuse(RelationshipCodes.Duplicate, at + " repeats axis '" + _axes[axis].Id + "'");
                var magnitude = _json.Long(effect, "magnitude", at, -Bound, Bound);
                if (magnitude == 0) throw _json.Refuse(RelationshipCodes.Range, at + ".magnitude must not be 0");
                effects.Add(new MemoryEffect(axis, magnitude));
            }
            SpreadDef? spread = null;
            var spreadElement = element.GetProperty("spread");
            if (spreadElement.ValueKind != JsonValueKind.Null)
            {
                var at = where + ".spread";
                _json.Object(spreadElement, at, ["as", "permille", "chance", "hops"]);
                spread = new SpreadDef(Ref(spreadElement, "as", at, index, "memory"), _json.Int(spreadElement, "permille", at, 0, 1000), _json.Int(spreadElement, "chance", at, 0, 1000), _json.Int(spreadElement, "hops", at, 1, 4));
            }
            memories.Add(new MemoryDef(memories.Count, ids[memories.Count], _json.Text(element, "key", where), effects, duration, ReadDecay(element, where, duration), ReadStack(element, where), ReadTags(element, "tags", where), spread));
        }
        return memories;
    }

    // ---- traits ----------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<TraitDef> ReadTraits(JsonElement array)
    {
        var traits = new List<TraitDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var needIndex = Index(_needs, n => n.Id);
        var axisIndex = Index(_axes, a => a.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "traits[" + traits.Count + "]";
            _json.Object(element, where, ["id", "moodBase", "needDecay", "moodScale", "axisScale"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "trait");
            var needDecay = new List<NeedDecayScale>();
            foreach (var scale in _json.Array(element, "needDecay", where, 0, 16).EnumerateArray())
            {
                var at = where + ".needDecay[" + needDecay.Count + "]";
                _json.Object(scale, at, ["need", "permille"]);
                var need = Ref(scale, "need", at, needIndex, "need");
                if (needDecay.Any(n => n.Need == need)) throw _json.Refuse(RelationshipCodes.Duplicate, at + " repeats need '" + _needs[need].Id + "'");
                needDecay.Add(new NeedDecayScale(need, _json.Int(scale, "permille", at, 0, 10_000)));
            }
            var moodScales = new List<MoodScale>();
            foreach (var scale in _json.Array(element, "moodScale", where, 0, 16).EnumerateArray())
            {
                var at = where + ".moodScale[" + moodScales.Count + "]";
                _json.Object(scale, at, ["tag", "permille"]);
                var tag = _json.Id(scale, "tag", at);
                if (moodScales.Any(m => m.Tag == tag)) throw _json.Refuse(RelationshipCodes.Duplicate, at + " repeats tag '" + tag + "'");
                moodScales.Add(new MoodScale(tag, _json.Int(scale, "permille", at, 0, 10_000)));
            }
            var axisScales = new List<AxisScale>();
            foreach (var scale in _json.Array(element, "axisScale", where, 0, 16).EnumerateArray())
            {
                var at = where + ".axisScale[" + axisScales.Count + "]";
                _json.Object(scale, at, ["axis", "sign", "permille"]);
                var axis = Ref(scale, "axis", at, axisIndex, "axis");
                var sign = Choice(scale, "sign", at, Signs);
                if (axisScales.Any(a => a.Axis == axis && a.Sign == sign)) throw _json.Refuse(RelationshipCodes.Duplicate, at + " repeats axis '" + _axes[axis].Id + "' for the same sign");
                axisScales.Add(new AxisScale(axis, sign, _json.Int(scale, "permille", at, 0, 10_000)));
            }
            traits.Add(new TraitDef(id, _json.Long(element, "moodBase", where, -Bound, Bound), needDecay, moodScales, axisScales));
        }
        return traits;
    }
}
