using EngineCommon;

namespace WorldEventsEngine;

// The calls a game or a script makes between steps. Each is checked whole before anything changes, and a refusal leaves the world exactly as it was. What a call did is returned, stamped with the step the world is at.

public sealed partial class WorldRuntime
{
    private RegionDef RegionNamed(string id)
    {
        if (!Set.RegionById.TryGetValue(id, out var region)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + id + "'");
        return region;
    }

    /// <summary>Starts or stops an intervention in a region by hand. Starting one that is already started by hand, or stopping one that is not, is refused; an intervention the automatic trigger started is its own and stays until the trigger lets it go.</summary>
    public WorldChanges Intervene(string region, string intervention, bool on)
    {
        var where = RegionNamed(region);
        if (!Set.InterventionById.TryGetValue(intervention, out var def)) throw new EngineRefusedException(WorldCodes.UnknownIntervention, "there is no intervention '" + intervention + "'");
        var flags = Regions[where.Index].Interventions[def.Index];
        var manual = (flags & 1) != 0;
        if (on && manual) throw new EngineRefusedException(WorldCodes.AlreadyActive, "intervention '" + intervention + "' is already on in region '" + region + "'");
        if (!on && !manual) throw new EngineRefusedException(WorldCodes.NotApplicable, "intervention '" + intervention + "' was not started by hand in region '" + region + "'");
        Regions[where.Index].Interventions[def.Index] = (byte)(on ? flags | 1 : flags & ~1);
        var changes = new WorldChanges();
        changes.Add(ChangeKind.Intervention, Step, where.Index, def.Index, on ? 1 : 0, 0);
        return changes;
    }

    /// <summary>Changes the storyteller. The stress and the phase stay: a different storyteller reads them differently from the next step.</summary>
    public WorldChanges UseStoryteller(string id)
    {
        if (!Set.StorytellerById.TryGetValue(id, out var teller)) throw new EngineRefusedException(WorldCodes.UnknownProfile, "there is no storyteller '" + id + "'");
        _storyteller = teller.Index;
        return new WorldChanges();
    }

    public WorldChanges UseDifficulty(string id)
    {
        if (!Set.DifficultyById.TryGetValue(id, out var level)) throw new EngineRefusedException(WorldCodes.UnknownProfile, "there is no difficulty '" + id + "'");
        _difficulty = level.Index;
        return new WorldChanges();
    }

    /// <summary>Starts a hazard in a region at a rung now, whatever its chance and the director's pace say (a script, a story beat). It spreads like any onset. Refused if it is already there or the list of active hazards is full.</summary>
    public WorldChanges BeginHazard(string region, string hazard, string severity)
    {
        var where = RegionNamed(region);
        if (!Set.HazardById.TryGetValue(hazard, out var def)) throw new EngineRefusedException(WorldCodes.UnknownHazard, "there is no hazard '" + hazard + "'");
        var rung = def.Ladder.FirstOrDefault(r => r.Id == severity);
        if (rung is null) throw new EngineRefusedException(WorldCodes.UnknownSeverity, "hazard '" + hazard + "' has no severity '" + severity + "'");
        if (ActiveAt(def.Index, where.Index) is not null) throw new EngineRefusedException(WorldCodes.AlreadyActive, "hazard '" + hazard + "' is already going in region '" + region + "'");
        if (_hazards.Count >= Set.Limits.MaxHazards) throw new EngineRefusedException(WorldCodes.Capacity, "there are already " + Set.Limits.MaxHazards + " hazards going");
        var changes = new WorldChanges();
        if (StartHazard(def.Index, where.Index, rung.Index, WorldMath.Permille, true, (ulong)Draw(long.MaxValue), Step, changes)) Spread(def, where.Index, rung.Index, Step, changes);
        return changes;
    }

    /// <summary>Puts cases of a disease into a region from its susceptible people. Refused if the count is not at least one or no one is susceptible.</summary>
    public WorldChanges SeedOutbreak(string region, string disease, long cases)
    {
        var where = RegionNamed(region);
        if (!Set.DiseaseById.TryGetValue(disease, out var def)) throw new EngineRefusedException(WorldCodes.UnknownDisease, "there is no disease '" + disease + "'");
        if (cases < 1 || cases > WorldFacts.Saturate) throw new EngineRefusedException(WorldCodes.Count, "cases are 1 to " + WorldFacts.Saturate + ", not " + cases);
        if (Regions[where.Index].Susceptible[def.Index] == 0) throw new EngineRefusedException(WorldCodes.NotApplicable, "no one in region '" + region + "' is susceptible to '" + disease + "'");
        Introduce(where.Index, def.Index, cases);
        Touch();
        var changes = new WorldChanges();
        TrackOutbreak(where.Index, def.Index, Step, changes);
        return changes;
    }

    /// <summary>Plays a card now, whatever its weight, cooldown, condition and the director's pace say. A regional card needs its region; a global card has none.</summary>
    public WorldChanges PlayCard(string card, string region)
    {
        if (!Set.CardById.TryGetValue(card, out var def)) throw new EngineRefusedException(WorldCodes.UnknownCard, "there is no card '" + card + "'");
        var at = -1;
        if (def.Global)
        {
            if (region.Length > 0) throw new EngineRefusedException(WorldCodes.NotApplicable, "card '" + card + "' is global and takes no region");
        }
        else at = RegionNamed(region).Index;
        var changes = new WorldChanges();
        FireCard(def.Index, at, CardReason.Forced, Step, changes);
        Touch();
        return changes;
    }
}
