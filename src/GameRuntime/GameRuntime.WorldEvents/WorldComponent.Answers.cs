using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

public sealed partial class WorldComponent
{
    /// <summary>Answers one request.</summary>
    private void Answer(IEventPort events, long tick, in WorldPending request)
    {
        try
        {
            var kind = WorldRequestNames.KindOf(request.Kind);
            if (kind is null) throw new EngineRefusedException(UnknownKind, "there is no request kind '" + request.Kind + "'");
            Perform(events, tick, request, kind.Value);
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the world has already been left as it was.
            events.Emit(new WorldRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
    }

    private void Perform(IEventPort events, long tick, in WorldPending request, WorldRequestKind kind)
    {
        switch (kind)
        {
            case WorldRequestKind.Intervene: Done(events, tick, request, Runtime.Intervene(request.A, request.B, request.Number != 0)); return;
            case WorldRequestKind.Storyteller: Done(events, tick, request, Runtime.UseStoryteller(request.A)); return;
            case WorldRequestKind.Difficulty: Done(events, tick, request, Runtime.UseDifficulty(request.A)); return;
            case WorldRequestKind.Hazard: Done(events, tick, request, Runtime.BeginHazard(request.A, request.B, request.C)); return;
            case WorldRequestKind.Outbreak: Done(events, tick, request, Runtime.SeedOutbreak(request.A, request.B, request.Number)); return;
            case WorldRequestKind.Card: Done(events, tick, request, Runtime.PlayCard(request.A, request.B)); return;
            case WorldRequestKind.Weather: WeatherAnswer(events, tick, request); return;
            case WorldRequestKind.Disease: DiseaseAnswer(events, tick, request); return;
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    /// <summary>The request was applied: the answer, then the changes it made.</summary>
    private void Done(IEventPort events, long tick, in WorldPending request, WorldChanges changes)
    {
        events.Emit(new WorldApplied(Id, Channel, request.RequestId, tick));
        foreach (var raise in EventsOf(tick, changes)) raise(events);
    }

    private void WeatherAnswer(IEventPort events, long tick, in WorldPending request)
    {
        if (!Runtime.Set.RegionById.TryGetValue(request.A, out var region)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + request.A + "'");
        var state = Runtime.Regions[region.Index];
        events.Emit(new WorldWeatherAnswered(Id, Channel, request.RequestId, tick, region.Id, Runtime.Set.Weathers[state.Weather].Id, state.Temp, state.Precip, state.Wind, Runtime.SoilPermille(region.Index), state.River, state.Snow));
    }

    private void DiseaseAnswer(IEventPort events, long tick, in WorldPending request)
    {
        if (!Runtime.Set.RegionById.TryGetValue(request.A, out var region)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + request.A + "'");
        if (!Runtime.Set.DiseaseById.TryGetValue(request.B, out var disease)) throw new EngineRefusedException(WorldCodes.UnknownDisease, "there is no disease '" + request.B + "'");
        var state = Runtime.Regions[region.Index];
        var d = disease.Index;
        events.Emit(new WorldDiseaseAnswered(Id, Channel, request.RequestId, tick, region.Id, disease.Id, state.Susceptible[d], state.Exposed[d], state.Infectious[d], state.Recovered[d], state.Dead[d], Words.Name(state.Outbreak[d])));
    }
}
