using EngineCommon;
using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    private int _version;
    private Dictionary<string, FactValue>? _globalCache;
    private int _globalVersion = -1;
    private Dictionary<string, FactValue>?[]? _localCache;
    private int[]? _localVersion;

    /// <summary>Something a fact reads has changed: the facts built so far are stale.</summary>
    private void Touch() => _version++;

    private bool Holds(ScenarioEngine.Contracts.Condition condition, int region) => Conditions.Evaluate(condition, LocalFactsOf(region));

    private bool HoldsGlobal(ScenarioEngine.Contracts.Condition condition) => Conditions.Evaluate(condition, GlobalFactsNow());

    private Dictionary<string, FactValue> GlobalFactsNow()
    {
        if (_globalCache is not null && _globalVersion == _version) return _globalCache;
        var facts = new Dictionary<string, FactValue>(Set.GlobalFacts.Count, StringComparer.Ordinal)
        {
            ["season"] = FactValue.From(_season < 0 ? "" : Set.Seasons[_season].Id),
            ["regime"] = FactValue.From(Set.Regimes[_regime].Id),
            ["step"] = FactValue.From(WorldMath.Saturated(Step)),
            ["stress"] = FactValue.From(_stress),
            ["phase"] = FactValue.From(Words.Name(_phase)),
            ["storyteller"] = FactValue.From(Set.Storytellers[_storyteller].Id),
            ["difficulty"] = FactValue.From(Set.Difficulties[_difficulty].Id),
            ["hazards_active"] = FactValue.From((long)_hazards.Count),
        };
        foreach (var (id, value) in _host) facts[id] = value;
        _globalCache = facts;
        _globalVersion = _version;
        return facts;
    }

    private Dictionary<string, FactValue> LocalFactsOf(int region)
    {
        _localCache ??= new Dictionary<string, FactValue>?[Set.Regions.Count];
        _localVersion ??= Enumerable.Repeat(-1, Set.Regions.Count).ToArray();
        if (_localCache[region] is { } cached && _localVersion[region] == _version) return cached;
        var facts = new Dictionary<string, FactValue>(GlobalFactsNow(), StringComparer.Ordinal);
        AddRegionFacts(facts, region, "");
        _localCache[region] = facts;
        _localVersion[region] = _version;
        return facts;
    }

    /// <summary>The region's own facts under <paramref name="prefix"/>: its weather and water, how bad each hazard is in it (0 for none), how many are ill of each disease, and each modifier.</summary>
    private void AddRegionFacts(Dictionary<string, FactValue> facts, int region, string prefix)
    {
        var state = Regions[region];
        facts[prefix + "temp"] = FactValue.From(state.Temp);
        facts[prefix + "precip"] = FactValue.From(state.Precip);
        facts[prefix + "wetness"] = FactValue.From(state.Wetness);
        facts[prefix + "wind"] = FactValue.From(state.Wind);
        facts[prefix + "weather"] = FactValue.From(Set.Weathers[state.Weather].Id);
        facts[prefix + "soil"] = FactValue.From((long)SoilPermille(region));
        facts[prefix + "river"] = FactValue.From(state.River);
        facts[prefix + "snow"] = FactValue.From(state.Snow);
        facts[prefix + "dry_steps"] = FactValue.From(state.DryStreak);
        long dead = 0;
        for (var d = 0; d < Set.Diseases.Count; d++)
        {
            var living = state.Living(d);
            dead += state.Dead[d];
            var infected = state.Exposed[d] + state.Infectious[d];
            facts[prefix + "disease." + Set.Diseases[d].Id + ".infected"] = FactValue.From(WorldMath.Saturated(infected));
            facts[prefix + "disease." + Set.Diseases[d].Id + ".prevalence"] = FactValue.From(living == 0 ? 0L : infected * WorldMath.Permille / living);
        }
        facts[prefix + "population"] = FactValue.From(WorldMath.Saturated(Set.Regions[region].Population - dead));
        foreach (var hazard in Set.Hazards) facts[prefix + "hazard." + hazard.Id] = FactValue.From(ActiveAt(hazard.Index, region) is { } instance ? instance.Rung + 1L : 0L);
        foreach (var modifier in Set.Modifiers) facts[prefix + "mod." + modifier.Id] = FactValue.From((long)Modifier(region, modifier.Index));
    }

    /// <summary>
    /// What a modifier is in a region now: its base, plus the weather's change, plus each hazard's change scaled by how strong the hazard is, plus each disease's burden scaled by how many are ill (full at the disease's
    /// burden line), plus the timed changes of cards (the region's and the world's), held inside the modifier's range.
    /// </summary>
    public int Modifier(int region, int modifier)
    {
        var def = Set.Modifiers[modifier];
        var state = Regions[region];
        long value = def.Base;
        foreach (var effect in Set.Weathers[state.Weather].Modifiers)
            if (effect.Modifier == modifier) value += effect.Delta;
        foreach (var instance in _hazards)
        {
            if (instance.Region != region) continue;
            var intensity = Intensity(instance);
            foreach (var effect in Set.Hazards[instance.Hazard].Ladder[instance.Rung].Effects)
                if (effect.Kind == EffectKind.Modifier && effect.Index == modifier) value += (long)effect.Amount * intensity / WorldMath.Permille;
        }
        foreach (var disease in Set.Diseases)
        {
            var living = state.Living(disease.Index);
            if (living == 0) continue;
            var prevalence = (state.Exposed[disease.Index] + state.Infectious[disease.Index]) * WorldMath.Permille / living;
            foreach (var effect in disease.Burden)
                if (effect.Modifier == modifier) value += (long)effect.Delta * Math.Min(prevalence, disease.BurdenAt) / disease.BurdenAt;
        }
        foreach (var timed in _timed)
            if (timed.Modifier == modifier && (timed.Region < 0 || timed.Region == region)) value += timed.Delta;
        return (int)WorldMath.Clamp(value, def.Minimum, def.Maximum);
    }

    /// <summary>
    /// The world as facts for the other layers, under <paramref name="prefix"/>: the world's (<c>world.season</c>, <c>world.stress</c>) and, for the region named, its own (<c>world.vale.temp</c>,
    /// <c>world.vale.hazard.flood</c>, <c>world.vale.mod.visibility</c>); an empty region gives the world's only. An unknown region is refused.
    /// </summary>
    public IReadOnlyDictionary<string, FactValue> Facts(string prefix, string region)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(region);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var name in WorldFacts.GlobalNames) facts[prefix + "world." + name] = GlobalFactsNow()[name];
        if (region.Length == 0) return facts;
        if (!Set.RegionById.TryGetValue(region, out var def)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + region + "'");
        AddRegionFacts(facts, def.Index, prefix + "world." + region + ".");
        return facts;
    }

    /// <summary>The facts a condition reads in a region, with the host facts given to the last <see cref="Advance"/> (the explain tools read them with the host facts they are given).</summary>
    internal IReadOnlyDictionary<string, FactValue> LocalFactsFor(int region, IReadOnlyDictionary<string, FactValue> host)
    {
        _host = host;
        Touch();
        return LocalFactsOf(region);
    }

    internal IReadOnlyDictionary<string, FactValue> GlobalFactsFor(IReadOnlyDictionary<string, FactValue> host)
    {
        _host = host;
        Touch();
        return GlobalFactsNow();
    }
}
