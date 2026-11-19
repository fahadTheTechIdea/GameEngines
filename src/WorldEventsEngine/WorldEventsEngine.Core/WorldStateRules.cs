using EngineCommon;

namespace WorldEventsEngine;

/// <summary>
/// What a world state may be. <see cref="CheckRanges"/> is about the data alone (counts, indexes, bounds) and runs before the save's digest is read; <see cref="CheckAgainstDocument"/> is about what the state says
/// about its document (people are conserved, a hazard's phase fits its rung, an agenda entry is the chain entry it names) and runs last. Both refuse with <see cref="WorldCodes.SaveState"/> and say what is wrong.
/// </summary>
internal static class WorldStateRules
{
    private const long Limit = 1_000_000_000_000;

    private static EngineRefusedException Bad(string why) => new(WorldCodes.SaveState, "the save's state is not possible: " + why);

    private static void In(long value, long minimum, long maximum, string what)
    {
        if (value < minimum || value > maximum) throw Bad(what + " is " + value + ", not " + minimum + " to " + maximum);
    }

    private static void Count(int count, int expected, string what)
    {
        if (count != expected) throw Bad(what + " holds " + count + " entries, the document needs " + expected);
    }

    public static void CheckRanges(WorldSet set, WorldImage image)
    {
        var regions = set.Regions.Count;
        var diseases = set.Diseases.Count;
        In(image.Step, -1, Limit, "step");
        In(image.Season, -1, set.Seasons.Count - 1, "season");
        In(image.Regime, 0, set.Regimes.Count - 1, "regime");
        In(image.Storyteller, 0, set.Storytellers.Count - 1, "storyteller");
        In(image.Difficulty, 0, set.Difficulties.Count - 1, "difficulty");
        In(image.Stress, 0, 1000, "stress");
        In(image.Phase, 0, Enum.GetValues<DirectorPhase>().Length - 1, "director phase");
        In(image.PhaseStart, 0, Math.Max(image.Step, 0), "director phase start");
        In(image.LastCard, -1, Math.Max(image.Step, 0), "last card");
        In(image.Draws, 0, Limit, "draws");
        In(image.Suppressed, 0, Limit, "suppressed");
        In(image.Dropped, 0, Limit, "dropped");
        In(image.Played, 0, Limit, "played");
        Count(image.CardReadyAt.Count, set.Cards.Count, "the card cooldowns");
        Count(image.GroupLockedUntil.Count, set.Groups.Count, "the group locks");
        Count(image.HazardReadyAt.Count, set.Hazards.Count * regions, "the hazard cooldowns");
        foreach (var value in image.CardReadyAt.Concat(image.GroupLockedUntil).Concat(image.HazardReadyAt)) In(value, 0, Limit, "a cooldown");
        Count(image.Excitation.Count, set.Hazards.Count * regions, "the hazard excitations");
        for (var i = 0; i < image.Excitation.Count; i++) In(image.Excitation[i], 0, set.Hazards[i / regions].Excite.CapPpm, "the excitation of hazard '" + set.Hazards[i / regions].Id + "' (its cap, 0 when it excites nothing)");
        Count(image.Regions.Count, regions, "the regions");
        foreach (var region in image.Regions)
        {
            In(region.Weather, 0, set.Weathers.Count - 1, "weather");
            In(region.Anomaly, -1_000_000, 1_000_000, "temperature anomaly");
            In(region.Temp, -WorldFacts.MaxTemperature, WorldFacts.MaxTemperature, "temperature");
            In(region.Precip, 0, 1_000_000, "precipitation");
            In(region.Wind, 0, 100_000, "wind");
            In(region.Wetness, 0, WorldFacts.Saturate, "wetness");
            In(region.SoilTenths, 0, set.Hydrology.SoilCapacity, "soil water");
            In(region.River, 0, WorldFacts.Saturate, "river");
            In(region.Snow, 0, WorldFacts.Saturate, "snow");
            In(region.DryStreak, 0, WorldFacts.Saturate, "dry steps");
            foreach (var list in new[] { region.Susceptible, region.Exposed, region.Infectious, region.Recovered, region.Dead, region.Peak })
            {
                Count(list.Count, diseases, "a compartment list");
                foreach (var people in list) In(people, 0, WorldFacts.Saturate, "a compartment");
            }
            Count(region.Outbreak.Count, diseases, "the outbreak states");
            foreach (var state in region.Outbreak) In(state, 0, Enum.GetValues<OutbreakState>().Length - 1, "an outbreak state");
            Count(region.Interventions.Count, set.Interventions.Count, "the intervention flags");
            foreach (var flags in region.Interventions) In(flags, 0, 3, "intervention flags");
        }
        In(image.Hazards.Count, 0, set.Limits.MaxHazards, "the number of active hazards");
        foreach (var h in image.Hazards)
        {
            In(h.Hazard, 0, set.Hazards.Count - 1, "an active hazard");
            In(h.Region, 0, regions - 1, "an active hazard's region");
            In(h.Rung, 0, set.Hazards[h.Hazard].Ladder.Count - 1, "an active hazard's rung");
            In(h.Phase, 0, Enum.GetValues<HazardPhase>().Length - 1, "a hazard's phase");
            In(h.PhaseStart, -1, Math.Max(image.Step, 0), "a hazard's phase start");
            In(h.PhaseLength, 1, 1_000_000, "a hazard's phase length");
            In(h.PeakLength, 1, 1_000_000, "a hazard's peak length");
            In(h.Power, 1, 1000, "a hazard's power");
        }
        In(image.Timed.Count, 0, set.Limits.MaxEffects, "the number of timed modifiers");
        foreach (var t in image.Timed)
        {
            In(t.Card, 0, set.Cards.Count - 1, "a timed modifier's card");
            In(t.Region, -1, regions - 1, "a timed modifier's region");
            In(t.Modifier, 0, set.Modifiers.Count - 1, "a timed modifier");
            In(t.Delta, -2_000_000, 2_000_000, "a timed modifier's change");
            In(t.Until, 0, Limit, "a timed modifier's end");
        }
        In(image.Agenda.Count, 0, set.Limits.MaxAgenda, "the length of the agenda");
        foreach (var a in image.Agenda)
        {
            In(a.Due, 0, Limit, "an agenda step");
            In(a.Card, 0, set.Cards.Count - 1, "an agenda card");
            In(a.Region, -1, regions - 1, "an agenda region");
            In(a.Parent, 0, set.Cards.Count - 1, "an agenda parent");
            In(a.Link, 0, set.Cards[a.Parent].Chain.Count - 1, "an agenda chain entry");
        }
    }

    public static void CheckAgainstDocument(WorldSet set, WorldImage image)
    {
        for (var r = 0; r < set.Regions.Count; r++)
        {
            var region = image.Regions[r];
            for (var d = 0; d < set.Diseases.Count; d++)
            {
                var people = region.Susceptible[d] + region.Exposed[d] + region.Infectious[d] + region.Recovered[d] + region.Dead[d];
                if (people != set.Regions[r].Population) throw Bad("region '" + set.Regions[r].Id + "' has " + people + " people in disease '" + set.Diseases[d].Id + "', the document says " + set.Regions[r].Population);
                if (set.Diseases[d].Model == DiseaseModel.Sir && region.Exposed[d] != 0) throw Bad("disease '" + set.Diseases[d].Id + "' is SIR and has exposed people");
                if (region.Peak[d] < region.Infectious[d]) throw Bad("the peak of '" + set.Diseases[d].Id + "' in '" + set.Regions[r].Id + "' is below those infectious now");
                var infected = region.Exposed[d] + region.Infectious[d];
                if ((region.Outbreak[d] == 0) != (infected == 0)) throw Bad("the outbreak state of '" + set.Diseases[d].Id + "' in '" + set.Regions[r].Id + "' does not match who is infected");
            }
            for (var i = 0; i < set.Interventions.Count; i++)
                if ((region.Interventions[i] & 2) != 0 && set.Interventions[i].AutoAt == 0) throw Bad("intervention '" + set.Interventions[i].Id + "' has no automatic trigger and is marked as started by it");
        }
        var seen = new HashSet<(int, int)>();
        foreach (var h in image.Hazards)
        {
            if (!seen.Add((h.Hazard, h.Region))) throw Bad("hazard '" + set.Hazards[h.Hazard].Id + "' is going twice in region '" + set.Regions[h.Region].Id + "'");
            var rung = set.Hazards[h.Hazard].Ladder[h.Rung];
            if (h.PeakLength < rung.MinDuration || h.PeakLength > rung.MaxDuration) throw Bad("a hazard's peak lasts " + h.PeakLength + ", outside the rung's " + rung.MinDuration + " to " + rung.MaxDuration);
            var phase = (HazardPhase)h.Phase;
            var expected = phase == HazardPhase.Onset ? rung.OnsetSteps : phase == HazardPhase.Peak ? h.PeakLength : rung.RecoverySteps;
            if (h.PhaseLength != expected) throw Bad("a hazard in its " + Words.Name(phase) + " lasts " + h.PhaseLength + " steps, its rung says " + expected);
            if (image.Step - h.PhaseStart + 1 > h.PhaseLength) throw Bad("a hazard has gone on past its phase");
        }
        foreach (var t in image.Timed)
            if (t.Until <= image.Step) throw Bad("a timed modifier ended at " + t.Until + " and is still held at step " + image.Step);
        foreach (var a in image.Agenda)
            if (set.Cards[a.Parent].Chain[a.Link].Card != a.Card) throw Bad("an agenda entry is not the card its chain entry names");
    }
}
