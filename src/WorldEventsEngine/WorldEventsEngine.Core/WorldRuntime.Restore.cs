namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>Builds a runtime from a save that has already been checked (<see cref="WorldSave.Open"/> is the only way here).</summary>
    internal static WorldRuntime Restore(WorldSet set, WorldImage image)
    {
        var runtime = new WorldRuntime(set)
        {
            Step = image.Step,
            _season = image.Season,
            _regime = image.Regime,
            _storyteller = image.Storyteller,
            _difficulty = image.Difficulty,
            _stress = image.Stress,
            _phase = (DirectorPhase)image.Phase,
            _phaseStart = image.PhaseStart,
            _lastCard = image.LastCard,
            _draws = image.Draws,
            _suppressed = image.Suppressed,
            _dropped = image.Dropped,
            _played = image.Played,
        };
        image.CardReadyAt.CopyTo(runtime.CardReadyAt);
        image.GroupLockedUntil.CopyTo(runtime.GroupLockedUntil);
        image.HazardReadyAt.CopyTo(runtime.HazardReadyAt);
        image.Excitation.CopyTo(runtime.ExcitationArray);
        for (var r = 0; r < image.Regions.Count; r++)
        {
            var from = image.Regions[r];
            var to = runtime.Regions[r];
            to.Weather = from.Weather;
            to.Anomaly = from.Anomaly;
            to.Temp = from.Temp;
            to.Precip = from.Precip;
            to.Wind = from.Wind;
            to.Wetness = from.Wetness;
            to.SoilTenths = from.SoilTenths;
            to.River = from.River;
            to.Snow = from.Snow;
            to.DryStreak = from.DryStreak;
            from.Susceptible.CopyTo(to.Susceptible);
            from.Exposed.CopyTo(to.Exposed);
            from.Infectious.CopyTo(to.Infectious);
            from.Recovered.CopyTo(to.Recovered);
            from.Dead.CopyTo(to.Dead);
            from.Peak.CopyTo(to.Peak);
            for (var d = 0; d < from.Outbreak.Count; d++) to.Outbreak[d] = (OutbreakState)from.Outbreak[d];
            for (var i = 0; i < from.Interventions.Count; i++) to.Interventions[i] = (byte)from.Interventions[i];
        }
        foreach (var h in image.Hazards)
            runtime.Place(new HazardInstance { Hazard = h.Hazard, Region = h.Region, Rung = h.Rung, Phase = (HazardPhase)h.Phase, PhaseStart = h.PhaseStart, PhaseLength = h.PhaseLength, PeakLength = h.PeakLength, Power = h.Power });
        foreach (var t in image.Timed) runtime._timed.Add(new TimedModifier { Card = t.Card, Region = t.Region, Modifier = t.Modifier, Delta = t.Delta, Until = t.Until });
        foreach (var a in image.Agenda) runtime._agenda.Add(new AgendaItem { Due = a.Due, Card = a.Card, Region = a.Region, Parent = a.Parent, Link = a.Link });
        return runtime;
    }

    /// <summary>The digest of the state; equal states have equal digests.</summary>
    public string StateDigest() => WorldSave.StateDigest(this);
}
