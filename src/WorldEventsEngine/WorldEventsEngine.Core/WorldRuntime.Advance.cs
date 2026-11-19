using EngineCommon;
using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    private IReadOnlyDictionary<string, FactValue> _host = new Dictionary<string, FactValue>();

    /// <summary>
    /// Lives the steps after <see cref="Step"/> up to <paramref name="toStep"/>, one at a time, in order. <paramref name="seasonAt"/> gives the season (an id of the document) of each step: the calendar's, or
    /// <see cref="WorldRuntime.CycleSeasonAt"/>, the document's own cycle. <paramref name="host"/> are the facts the document declares for the host, as they are now, used for every step of the call. Everything is
    /// checked before the first step runs, so a refusal leaves the world as it was. Returns what happened, in order.
    /// </summary>
    public WorldChanges Advance(long toStep, Func<long, string> seasonAt, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(seasonAt);
        ArgumentNullException.ThrowIfNull(host);
        if (toStep < Step) throw new EngineRefusedException(WorldCodes.StepOrder, "the world is at step " + Step + " and does not go back to " + toStep);
        var changes = new WorldChanges();
        if (toStep == Step) return changes;
        if (toStep - Step > MaxAdvance) throw new EngineRefusedException(WorldCodes.StepOrder, "one call goes at most " + MaxAdvance + " steps; it was asked to go " + (toStep - Step));
        CheckHost(host);
        var seasons = new int[toStep - Step];
        for (var i = 0; i < seasons.Length; i++)
        {
            var id = seasonAt(Step + 1 + i);
            if (!Set.SeasonById.TryGetValue(id, out var season)) throw new EngineRefusedException(WorldCodes.UnknownSeason, "there is no season '" + id + "' (step " + (Step + 1 + i) + ")");
            seasons[i] = season.Index;
        }
        _host = host;
        for (var i = 0; i < seasons.Length; i++) Once(Step + 1, seasons[i], changes);
        return changes;
    }

    /// <summary>The season of the document's own cycle at a step, as an id: for a game with no calendar.</summary>
    public string CycleSeasonAt(long step) => Set.Seasons[Set.CycleSeason(step)].Id;

    private void CheckHost(IReadOnlyDictionary<string, FactValue> host)
    {
        foreach (var (id, definition) in Set.HostFacts)
        {
            if (!host.TryGetValue(id, out var value)) throw new EngineRefusedException(WorldCodes.FactMissing, "the document reads the host fact '" + id + "' and the host did not give it");
            if (value.Kind != definition.Kind) throw new EngineRefusedException(WorldCodes.FactInvalid, "host fact '" + id + "' is " + value.Kind + ", the document declares " + definition.Kind);
            if (definition.Kind == FactKind.Integer && (value.Integer < definition.Minimum || value.Integer > definition.Maximum))
                throw new EngineRefusedException(WorldCodes.FactInvalid, "host fact '" + id + "' is " + value.Integer + ", outside " + definition.Minimum + " to " + definition.Maximum);
        }
    }

    /// <summary>One step. The order is fixed: season, regime, weather and water, diseases, hazards (the ones going, then the ones starting), the director (stress and phase), the agenda, a draw, and what has run out.</summary>
    private void Once(long step, int season, WorldChanges changes)
    {
        Step = step;
        if (season != _season)
        {
            changes.Add(ChangeKind.Season, step, index: season, sub: _season);
            _season = season;
        }
        Touch();
        LiveRegime(step, season, changes);
        // Every region's weather of the step before is read by its neighbours, so the order the regions are lived in cannot show.
        for (var r = 0; r < _weatherBefore.Length; r++) _weatherBefore[r] = Regions[r].Weather;
        foreach (var region in Set.Regions) LiveWeather(region.Index, step, season, changes);
        Touch();
        LiveDiseases(step, season, changes);
        Touch();
        LiveHazards(step, season, changes);
        LiveDirector(step, changes);
        LiveAgenda(step, changes);
        LiveDraw(step, changes);
        ExpireTimed(step);
        Touch();
    }

    private void LiveRegime(long step, int season, WorldChanges changes)
    {
        if (Roll("regime", step * 4, WorldMath.Permille) < Set.Regimes[_regime].Persistence) return;
        var weights = Set.Regimes.Select(r => (long)r.Frequency[season]).ToList();
        var pick = WorldMath.Pick(weights, Roll("regime", step * 4 + 1, weights.Sum()));
        if (pick == _regime) return;
        changes.Add(ChangeKind.Regime, step, index: pick, sub: _regime);
        _regime = pick;
    }

    private void ExpireTimed(long step) => _timed.RemoveAll(t => t.Until <= step);
}
