using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace RelationshipEngine;

internal sealed partial class RelationshipReader
{
    private static readonly IReadOnlyDictionary<string, bool> EdgeSides = new Dictionary<string, bool>(StringComparer.Ordinal) { ["actorToTarget"] = false, ["targetToActor"] = true };
    private static readonly IReadOnlyDictionary<string, bool> Whom = new Dictionary<string, bool>(StringComparer.Ordinal) { ["actor"] = false, ["target"] = true };

    private IReadOnlyList<InteractionDef> ReadInteractions(JsonElement array)
    {
        var known = new Dictionary<string, FactDefinition>(RelationshipFacts.Definitions(_axes, _needs, _traits, _ladders, _bonds, _memories, _mood, _limits, _hostFacts), StringComparer.Ordinal);
        var interactions = new List<InteractionDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "interactions[" + interactions.Count + "]";
            _json.Object(element, where, ["id", "key", "tags", "requires", "cooldown", "gift", "desire", "accept", "acceptAt", "noise", "effects", "refusedEffects", "witness"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "interaction");
            var gift = _json.Bool(element, "gift", where);
            Condition? requires = null;
            IReadOnlyList<string> requiresFacts = Array.Empty<string>();
            var requiresElement = element.GetProperty("requires");
            if (requiresElement.ValueKind != JsonValueKind.Null)
            {
                requires = _conditions.Read(requiresElement, where + ".requires", known);
                requiresFacts = Conditions.FactsOf(requires);
            }
            var effects = ReadEffects(_json.Array(element, "effects", where, 0, 16), where + ".effects", gift);
            var refused = ReadEffects(_json.Array(element, "refusedEffects", where, 0, 16), where + ".refusedEffects", gift);
            WitnessDef? witness = null;
            var witnessElement = element.GetProperty("witness");
            if (witnessElement.ValueKind != JsonValueKind.Null)
            {
                var at = where + ".witness";
                _json.Object(witnessElement, at, ["memory", "scale", "max"]);
                witness = new WitnessDef(Ref(witnessElement, "memory", at, Index(_memories, m => m.Id), "memory"), _json.Int(witnessElement, "scale", at, 0, 2000), _json.Int(witnessElement, "max", at, 1, 8));
            }
            interactions.Add(new InteractionDef(interactions.Count, id, _json.Text(element, "key", where), ReadTags(element, "tags", where), requires, requiresFacts, _json.Int(element, "cooldown", where, 0, 1_000_000), gift,
                ReadRules(_json.Array(element, "desire", where, 0, 16), where + ".desire", known), ReadRules(_json.Array(element, "accept", where, 0, 16), where + ".accept", known),
                _json.Long(element, "acceptAt", where, -Bound, Bound), _json.Int(element, "noise", where, 0, 1000), effects, refused, witness));
        }
        return interactions;
    }

    private IReadOnlyList<RuleDef> ReadRules(JsonElement array, string where, IReadOnlyDictionary<string, FactDefinition> known)
    {
        var rules = new List<RuleDef>();
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + rules.Count + "]";
            _json.Object(element, at, ["when", "weight"]);
            var condition = _conditions.Read(element.GetProperty("when"), at + ".when", known);
            rules.Add(new RuleDef(condition, _json.Int(element, "weight", at, -1_000_000, 1_000_000), Conditions.FactsOf(condition)));
        }
        return rules;
    }

    private IReadOnlyList<EffectDef> ReadEffects(JsonElement array, string where, bool gift)
    {
        var effects = new List<EffectDef>();
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + effects.Count + "]";
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("kind", out _)) throw _json.Refuse(RelationshipCodes.MissingField, at + " is missing 'kind'");
            var kind = _json.Text(element, "kind", at, 16);
            if (!_effectReaders.TryGetValue(kind, out var read)) throw _json.Refuse(RelationshipCodes.Effect, at + ".kind '" + kind + "' must be axis, memory, moodlet, need or bond");
            var effect = read(element, at);
            if (effect.ByTaste && !gift) throw _json.Refuse(RelationshipCodes.Effect, at + " is scaled by taste but the interaction is not a gift");
            effects.Add(effect);
        }
        return effects;
    }

    private EffectDef ReadAxisEffect(JsonElement element, string at)
    {
        _json.Object(element, at, ["kind", "edge", "axis", "delta", "byTaste"]);
        var delta = _json.Long(element, "delta", at, -Bound, Bound);
        if (delta == 0) throw _json.Refuse(RelationshipCodes.Effect, at + ".delta must not be 0");
        return new EffectDef(EffectKind.Axis, Choice(element, "edge", at, EdgeSides), Ref(element, "axis", at, Index(_axes, a => a.Id), "axis"), delta, _json.Bool(element, "byTaste", at));
    }

    private EffectDef ReadMemoryEffect(JsonElement element, string at)
    {
        _json.Object(element, at, ["kind", "edge", "memory", "byTaste"]);
        return new EffectDef(EffectKind.Memory, Choice(element, "edge", at, EdgeSides), Ref(element, "memory", at, Index(_memories, m => m.Id), "memory"), 0, _json.Bool(element, "byTaste", at));
    }

    private EffectDef ReadMoodletEffect(JsonElement element, string at)
    {
        _json.Object(element, at, ["kind", "who", "moodlet", "byTaste"]);
        return new EffectDef(EffectKind.Moodlet, Choice(element, "who", at, Whom), Ref(element, "moodlet", at, Index(_moodlets, m => m.Id), "moodlet"), 0, _json.Bool(element, "byTaste", at));
    }

    private EffectDef ReadNeedEffect(JsonElement element, string at)
    {
        _json.Object(element, at, ["kind", "who", "need", "delta", "byTaste"]);
        var delta = _json.Long(element, "delta", at, -Bound, Bound);
        if (delta == 0) throw _json.Refuse(RelationshipCodes.Effect, at + ".delta must not be 0");
        return new EffectDef(EffectKind.Need, Choice(element, "who", at, Whom), Ref(element, "need", at, Index(_needs, n => n.Id), "need"), delta, _json.Bool(element, "byTaste", at));
    }

    private EffectDef ReadBondEffect(JsonElement element, string at)
    {
        _json.Object(element, at, ["kind", "bond", "set"]);
        return new EffectDef(EffectKind.Bond, false, Ref(element, "bond", at, Index(_bonds, b => b.Id), "bond"), _json.Bool(element, "set", at) ? 1 : 0, false);
    }
}
