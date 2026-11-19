namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>
    /// The weather of one region for one step. It is a first-order Markov chain with persistence: with the zone's persistence the weather stays; otherwise it is drawn again from the zone's weights for the season, tilted by
    /// the regime (the front that is over everything). Drawing again from the whole set, the current weather included, makes the long-run share of each weather equal to its weight whatever the persistence, so
    /// persistence changes the spells and not the climate. The temperature is the zone's seasonal mean, plus a first-order autoregressive anomaly (the carried-over warmth or cold) whose long-run spread is the zone's sigma,
    /// plus the weather's own offset.
    /// </summary>
    private void LiveWeather(int r, long step, int season, WorldChanges changes)
    {
        var region = Set.Regions[r];
        var state = Regions[r];
        var climate = Set.Zones[region.Zone].Seasons[season];
        var stream = _weatherStreams[r];
        var at = step * WeatherSlots;
        var before = state.Weather;
        if (Roll(stream, at, WorldMath.Permille) >= climate.Persistence)
        {
            var weights = RedrawWeights(r, season, _weatherBefore);
            state.Weather = WorldMath.Pick(weights, Roll(stream, at + 1, weights.Sum()));
        }
        var weather = Set.Weathers[state.Weather];
        var innovation = WorldMath.Isqrt(1_000_000L - (long)climate.Phi * climate.Phi);
        state.Anomaly = state.Anomaly * climate.Phi / WorldMath.Permille + WorldMath.Noise(RollBits(stream, at + 2), climate.Sigma) * innovation / WorldMath.Permille;
        state.Temp = WorldMath.Clamp(climate.Temp + state.Anomaly + weather.TempOffset, -WorldFacts.MaxTemperature, WorldFacts.MaxTemperature);
        state.Wind = WorldMath.Clamp(climate.Wind + weather.Wind + WorldMath.Noise(RollBits(stream, at + 5), climate.Wind / 2 + 10), 0, 100_000);
        state.Precip = 0;
        if (weather.Wet)
        {
            // Rain is the weather's mean amount times a uniform 0.4 to 1.6, and now and then (the hydrology's share) several times more: a mixture that gives the floods their heavy tail.
            var amount = (long)weather.Precip * (400 + Roll(stream, at + 3, 1201)) / WorldMath.Permille;
            if (Roll(stream, at + 4, WorldMath.Permille) < Set.Hydrology.HeavyPermille) amount = amount * Set.Hydrology.HeavyFactor / WorldMath.Permille;
            state.Precip = Math.Min(amount, 1_000_000);
        }
        LiveWater(state);
        if (state.Weather != before) changes.Add(ChangeKind.Weather, step, r, state.Weather, before, amount: state.Temp);
    }

    /// <summary>
    /// The weights a region draws its next weather with: the zone's weights for the season, times the regime's bias, tilted by what the regions linked to it have (<paramref name="neighbourWeather"/> holds the weather index of every
    /// region). The tilt of weather k is 1000 + spill x (share of the neighbours that have k - the share of k in the untilted weights), all per mille, so when the neighbours have the zone's own climate nothing is tilted, and a
    /// neighbourhood that is wetter than usual makes rain likelier. A zone with no spill, or a region with no link, draws with the untilted weights, as if it were alone. Weights are whole numbers (the untilted weights times the tilt).
    /// </summary>
    internal long[] RedrawWeights(int region, int season, IReadOnlyList<int> neighbourWeather)
    {
        var def = Set.Regions[region];
        var zone = Set.Zones[def.Zone];
        var climate = zone.Seasons[season];
        var bias = Set.Regimes[_regime].Bias;
        var weights = new long[Set.Weathers.Count];
        long total = 0;
        for (var w = 0; w < weights.Length; w++)
        {
            weights[w] = (long)climate.Frequency[w] * bias[w];
            total += weights[w];
        }
        var graph = Set.Graph;
        var degree = graph.OutDegree(region);
        if (zone.Spill == 0 || degree == 0) return weights;
        var counts = new long[weights.Length];
        for (var edge = graph.FirstEdge(region); edge < graph.EndEdge(region); edge++) counts[neighbourWeather[graph.Target(edge)]]++;
        for (var w = 0; w < weights.Length; w++)
        {
            var neighbours = counts[w] * WorldMath.Permille / degree;
            var usual = weights[w] * WorldMath.Permille / total;
            weights[w] *= WorldMath.Permille + zone.Spill * (neighbours - usual) / WorldMath.Permille;
        }
        return weights;
    }

    /// <summary>
    /// The water of a region: a snowpack that collects precipitation below freezing and melts by the degree above it; a soil bucket (tenths of a millimetre, its size the hydrology's capacity) that takes rain and melt and
    /// loses water by the degree to evaporation; what the bucket cannot hold runs off into a river store that drains by a share a step. All of it in whole tenths of a millimetre, rounded down.
    /// </summary>
    private void LiveWater(RegionState state)
    {
        var hydrology = Set.Hydrology;
        state.Wetness = WorldMath.Saturated(state.Wetness * (WorldMath.Permille - hydrology.WetDecay) / WorldMath.Permille + state.Precip);
        state.DryStreak = state.Precip == 0 ? WorldMath.Saturated(state.DryStreak + 1) : 0;
        long rain = 0;
        if (state.Temp < hydrology.Freezing) state.Snow = WorldMath.Saturated(state.Snow + state.Precip);
        else rain = state.Precip;
        var melt = Math.Min(state.Snow, Math.Max(0, state.Temp - hydrology.Freezing) * hydrology.MeltPerDegree / 100);
        state.Snow -= melt;
        var soil = state.SoilTenths + rain + melt;
        var evaporation = Math.Min(soil, Math.Max(0, state.Temp) * hydrology.EvapPerDegree / 100);
        soil -= evaporation;
        var runoff = Math.Max(0, soil - hydrology.SoilCapacity);
        state.SoilTenths = Math.Min(soil, hydrology.SoilCapacity);
        state.River = WorldMath.Saturated(state.River * (WorldMath.Permille - hydrology.DrainPermille) / WorldMath.Permille + runoff);
    }

    /// <summary>The soil's water as per mille of what the bucket holds.</summary>
    public int SoilPermille(int region) => (int)(Regions[region].SoilTenths * WorldMath.Permille / Set.Hydrology.SoilCapacity);
}
