using System.Text.Json;
using EngineCommon;

namespace WorldEventsEngine;

internal sealed partial class WorldReader
{
    /// <summary>Where an effect list sits decides which effects it may hold: a rung's modifiers are scaled by the hazard's intensity; a card's last for a duration; a global card has no region to start a hazard in.</summary>
    private enum EffectScope { Rung, Outbreak, RegionCard, GlobalCard }

    // ---- effects ---------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<EffectDef> ReadEffects(JsonElement owner, string where, EffectScope scope, string field = "effects")
    {
        var effects = new List<EffectDef>();
        foreach (var element in _json.Array(owner, field, where, 0, MaxEffects).EnumerateArray())
        {
            var at = where + "." + field + "[" + effects.Count + "]";
            effects.Add(ReadEffect(element, at, scope));
        }
        return effects;
    }

    private EffectDef ReadEffect(JsonElement element, string where, EffectScope scope)
    {
        if (element.ValueKind != JsonValueKind.Object) throw _json.Refuse(WorldCodes.Format, where + " must be an object");
        var present = Enum.GetValues<EffectKind>().Where(k => element.TryGetProperty(Words.Name(k), out _)).ToList();
        if (present.Count != 1) throw _json.Refuse(WorldCodes.Effect, where + " must hold exactly one of " + Words.Choices<EffectKind>());
        var kind = present[0];
        switch (kind)
        {
            case EffectKind.Signal:
                _json.Object(element, where, ["signal"]);
                return new EffectDef(kind, _json.Id(element, "signal", where), -1, 0, -1, 0);
            case EffectKind.Shock:
                _json.Object(element, where, ["shock", "scale"]);
                return new EffectDef(kind, _json.Id(element, "shock", where), -1, _json.Int(element, "scale", where, 1, 10_000), -1, 0);
            case EffectKind.Modifier:
            {
                if (scope == EffectScope.Outbreak) throw _json.Refuse(WorldCodes.Effect, where + ": a disease's modifiers are its 'burden', scaled by how many are ill");
                var duration = 0;
                if (scope is EffectScope.RegionCard or EffectScope.GlobalCard)
                {
                    _json.Object(element, where, ["modifier", "delta", "duration"]);
                    duration = _json.Int(element, "duration", where, 1, 1_000_000);
                }
                else _json.Object(element, where, ["modifier", "delta"]);
                var modifier = Ref(element, "modifier", where, _modifierIndex, "modifier");
                return new EffectDef(kind, _parts.Modifiers[modifier].Id, modifier, _json.Int(element, "delta", where, -2_000_000, 2_000_000), -1, duration);
            }
            case EffectKind.Hazard:
            {
                if (scope == EffectScope.GlobalCard) throw _json.Refuse(WorldCodes.Effect, where + ": a global card has no region to start a hazard in");
                _json.Object(element, where, ["hazard", "severity"]);
                var hazard = Ref(element, "hazard", where, _hazardIndex, "hazard");
                var rung = _json.Id(element, "severity", where);
                var at = _rungIds[hazard].IndexOf(rung);
                if (at < 0) throw _json.Refuse(WorldCodes.Reference, where + ".severity '" + rung + "' is not a rung of hazard '" + _json.Id(element, "hazard", where) + "'");
                return new EffectDef(kind, _json.Id(element, "hazard", where), hazard, 0, at, 0);
            }
            case EffectKind.Outbreak:
            {
                if (scope == EffectScope.GlobalCard) throw _json.Refuse(WorldCodes.Effect, where + ": a global card has no region to start an outbreak in");
                _json.Object(element, where, ["outbreak", "cases"]);
                var disease = Ref(element, "outbreak", where, _diseaseIndex, "disease");
                return new EffectDef(kind, _json.Id(element, "outbreak", where), disease, _json.Int(element, "cases", where, 1, 1_000_000), -1, 0);
            }
        }
        throw new InvalidOperationException("unknown effect kind " + kind);
    }

    // ---- hazards ---------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<HazardDef> ReadHazards(JsonElement array)
    {
        var hazards = new List<HazardDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "hazards[" + hazards.Count + "]";
            _json.Object(element, where, ["id", "key", "kind", "paced", "ratePpm", "excite", "seasonal", "excludes", "cooldown", "reach", "ladder"], "when");
            var kindName = _json.Text(element, "kind", where, 16);
            var kind = Words.Parse<HazardKind>(kindName);
            if (kind is null) throw _json.Refuse(WorldCodes.Format, where + ".kind '" + kindName + "' must be one of " + Words.Choices<HazardKind>());
            var reach = _json.Object(element.GetProperty("reach"), where + ".reach", ["hops", "falloff"]);
            var self = hazards.Count;
            var named = new List<int>();
            foreach (var other in _json.Array(element, "excludes", where, 0, MaxHazards).EnumerateArray())
            {
                if (other.ValueKind != JsonValueKind.String || !_hazardIndex.TryGetValue(other.GetString()!, out var excluded)) throw _json.Refuse(WorldCodes.Reference, where + ".excludes names a hazard that is not declared");
                if (excluded == self) throw _json.Refuse(WorldCodes.Hazard, where + " excludes itself");
                if (named.Contains(excluded)) throw _json.Refuse(WorldCodes.Duplicate, where + ".excludes names '" + other.GetString() + "' twice");
                named.Add(excluded);
            }
            var ladder = ReadLadder(element.GetProperty("ladder"), where);
            hazards.Add(new HazardDef(self, _json.Id(element, "id", where), _json.Id(element, "key", where), kind.Value, _json.Bool(element, "paced", where), _json.Int(element, "ratePpm", where, 0, 1_000_000),
                ReadExcite(element.GetProperty("excite"), where + ".excite"), Ints(element, "seasonal", where, _parts.Seasons.Count, 0, 10_000), OptionalWhen(element, where, _parts.LocalFacts), _json.Int(element, "cooldown", where, 0, 1_000_000), named,
                _json.Int(reach, "hops", where + ".reach", 0, MaxReach), _json.Int(reach, "falloff", where + ".reach", 0, 1000), ladder));
        }
        return hazards;
    }

    /// <summary>
    /// The self-excitation of a hazard. A hazard that does not excite itself says so with a jump of 0 and a cap of 0. One that does must have a branching ratio below one, or each onset would cause more than one more on
    /// average and the chance of an onset would climb to its cap and stay (<see cref="WorldCodes.Nonstationary"/>), and a cap of at least one jump, or an onset would add nothing.
    /// </summary>
    private ExciteDef ReadExcite(JsonElement element, string where)
    {
        _json.Object(element, where, ["jumpPpm", "decayPermille", "capPpm"]);
        var excite = new ExciteDef(_json.Int(element, "jumpPpm", where, 0, 1_000_000), _json.Int(element, "decayPermille", where, 1, 1000), _json.Int(element, "capPpm", where, 0, 1_000_000));
        if (!excite.On && excite.CapPpm != 0) throw _json.Refuse(WorldCodes.Hazard, where + ".capPpm must be 0 when jumpPpm is 0: nothing is excited, so there is nothing to cap");
        if (excite.On && excite.CapPpm < excite.JumpPpm) throw _json.Refuse(WorldCodes.Hazard, where + ".capPpm " + excite.CapPpm + " is below jumpPpm " + excite.JumpPpm + ": one onset would add nothing");
        if (!excite.Stationary)
            throw _json.Refuse(WorldCodes.Nonstationary, where + " has a branching ratio of at least 1 (jumpPpm " + excite.JumpPpm + " x (1000 - decayPermille " + excite.DecayPermille + ") is not below 1000000 x " + excite.DecayPermille + "): the clusters would never end");
        return excite;
    }

    private IReadOnlyList<RungDef> ReadLadder(JsonElement array, string hazardWhere)
    {
        var rungs = new List<RungDef>();
        var previousTail = int.MaxValue;
        foreach (var element in array.EnumerateArray())
        {
            var where = hazardWhere + ".ladder[" + rungs.Count + "]";
            _json.Object(element, where, ["id", "key", "tailPpm", "minDuration", "maxDuration", "onsetSteps", "recoverySteps", "stress", "escalatePpm", "effects"]);
            var tail = _json.Int(element, "tailPpm", where, 1, 1_000_000);
            if (rungs.Count == 0 && tail != 1_000_000) throw _json.Refuse(WorldCodes.Hazard, where + ".tailPpm must be 1000000 on the first rung: every onset is at least that bad");
            if (tail >= previousTail) throw _json.Refuse(WorldCodes.Hazard, where + ".tailPpm " + tail + " does not fall below the rung before it (" + previousTail + "): a worse onset is rarer");
            previousTail = tail;
            var min = _json.Int(element, "minDuration", where, 1, 1_000_000);
            var max = _json.Int(element, "maxDuration", where, 1, 1_000_000);
            if (min > max) throw _json.Refuse(WorldCodes.Hazard, where + " lasts at least " + min + " and at most " + max);
            var escalate = _json.Int(element, "escalatePpm", where, 0, 1_000_000);
            if (escalate > 0 && rungs.Count == array.GetArrayLength() - 1) throw _json.Refuse(WorldCodes.Hazard, where + " is the worst rung and cannot escalate");
            rungs.Add(new RungDef(rungs.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), tail, min, max, _json.Int(element, "onsetSteps", where, 0, 100_000),
                _json.Int(element, "recoverySteps", where, 0, 100_000), _json.Int(element, "stress", where, 0, 1000), escalate, ReadEffects(element, where, EffectScope.Rung)));
        }
        return rungs;
    }

    /// <summary>Mutual exclusion is symmetric in meaning: if A says it excludes B, an active B stops A, and the document must say so both ways or the order of the hazards would decide.</summary>
    private void CheckExclusions()
    {
        foreach (var hazard in _parts.Hazards)
            foreach (var other in hazard.Excludes)
                if (!_parts.Hazards[other].Excludes.Contains(hazard.Index))
                    throw _json.Refuse(WorldCodes.Hazard, "hazard '" + hazard.Id + "' excludes '" + _parts.Hazards[other].Id + "' but not the other way round");
    }

    // ---- diseases --------------------------------------------------------------------------------------------------------------------

    private IReadOnlyList<DiseaseDef> ReadDiseases(JsonElement array)
    {
        var diseases = new List<DiseaseDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "diseases[" + diseases.Count + "]";
            _json.Object(element, where, ["id", "key", "model", "betaPpm", "incubation", "infectious", "waning", "fatality", "seasonal", "seedPpm", "seedCases", "outbreakAt", "paced", "stress", "burdenAt", "burden", "onOutbreak"], "when");
            var modelName = _json.Text(element, "model", where, 8);
            var model = Words.Parse<DiseaseModel>(modelName);
            if (model is null) throw _json.Refuse(WorldCodes.Format, where + ".model '" + modelName + "' must be one of " + Words.Choices<DiseaseModel>());
            var incubation = _json.Int(element, "incubation", where, 0, MaxSeasonSteps);
            if (model == DiseaseModel.Sir && incubation != 0) throw _json.Refuse(WorldCodes.Disease, where + " is SIR, which has no incubation period; use seir");
            if (model == DiseaseModel.Seir && incubation == 0) throw _json.Refuse(WorldCodes.Disease, where + " is SEIR and needs an incubation of at least one step");
            var seedPpm = _json.Int(element, "seedPpm", where, 0, 1_000_000);
            var seedCases = _json.Int(element, "seedCases", where, 0, 1_000_000);
            if ((seedPpm > 0) != (seedCases > 0)) throw _json.Refuse(WorldCodes.Disease, where + ": seedPpm and seedCases are both zero (no spontaneous cases) or both above zero");
            diseases.Add(new DiseaseDef(diseases.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), model.Value, _json.Int(element, "betaPpm", where, 0, 500_000), incubation,
                _json.Int(element, "infectious", where, 1, MaxSeasonSteps), _json.Int(element, "waning", where, 0, 36_600), _json.Int(element, "fatality", where, 0, 1000), Ints(element, "seasonal", where, _parts.Seasons.Count, 0, 10_000),
                seedPpm, seedCases, _json.Int(element, "outbreakAt", where, 1, 1000), _json.Bool(element, "paced", where), _json.Int(element, "stress", where, 0, 1000), OptionalWhen(element, where, _parts.LocalFacts),
                ReadModifierEffects(_json.Array(element, "burden", where, 0, MaxModifiers), where + ".burden"), _json.Int(element, "burdenAt", where, 1, 1000), ReadEffects(element, where, EffectScope.Outbreak, "onOutbreak")));
        }
        return diseases;
    }

    private IReadOnlyList<InterventionDef> ReadInterventions(JsonElement array)
    {
        var list = new List<InterventionDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "interventions[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "disease", "betaPermille", "flowPermille", "vaccinate", "treat", "fatalityPermille", "autoAt"]);
            list.Add(new InterventionDef(list.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), Ref(element, "disease", where, _diseaseIndex, "disease"), _json.Int(element, "betaPermille", where, 0, 5000),
                _json.Int(element, "flowPermille", where, 0, 1000), _json.Int(element, "vaccinate", where, 0, 1000), _json.Int(element, "treat", where, 100, 10_000), _json.Int(element, "fatalityPermille", where, 0, 5000),
                _json.Int(element, "autoAt", where, 0, 1000)));
        }
        return list;
    }
}
