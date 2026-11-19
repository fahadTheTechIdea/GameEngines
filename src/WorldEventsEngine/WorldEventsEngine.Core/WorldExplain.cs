using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

/// <summary>Says why a hazard did or did not start in a region, why a card can or cannot be drawn, how a disease stands and where the director is, from the same numbers the runtime uses: a pure reading that changes nothing.</summary>
public static class WorldExplain
{
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static RegionDef Region(WorldRuntime runtime, string id)
    {
        if (!runtime.Set.RegionById.TryGetValue(id, out var region)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + id + "'");
        return region;
    }

    private static void CheckHost(WorldRuntime runtime, IReadOnlyDictionary<string, FactValue> host)
    {
        foreach (var id in runtime.Set.HostFacts.Keys)
            if (!host.ContainsKey(id)) throw new EngineRefusedException(WorldCodes.FactMissing, "the host gave no fact '" + id + "', which the document declares");
    }

    public static string Hazard(WorldRuntime runtime, string hazardId, string regionId, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);
        var region = Region(runtime, regionId);
        if (!runtime.Set.HazardById.TryGetValue(hazardId, out var hazard)) throw new EngineRefusedException(WorldCodes.UnknownHazard, "there is no hazard '" + hazardId + "'");
        CheckHost(runtime, host);
        var set = runtime.Set;
        var text = new StringBuilder("hazard " + hazard.Id + " in " + region.Id);
        var active = runtime.ActiveAt(hazard.Index, region.Index);
        if (active is not null)
        {
            var rung = hazard.Ladder[active.Rung];
            text.Append(" is going: rung ").Append(rung.Id).Append(", ").Append(Words.Name(active.Phase)).Append(" since step ").Append(Number(active.PhaseStart)).Append(" (").Append(Number(active.PhaseLength)).Append(" steps), power ").Append(Number(active.Power)).Append(", intensity now ").Append(Number(runtime.Intensity(active)));
            if (rung.EscalatePpm > 0) text.Append("\n  may escalate to ").Append(hazard.Ladder[active.Rung + 1].Id).Append(" at ").Append(Number(rung.EscalatePpm)).Append(" ppm a step while at its peak");
            return text.ToString();
        }
        text.Append(" is not going");
        var exposure = region.Exposure[hazard.Index];
        text.Append("\n  exposure ").Append(Number(exposure)).Append(" per mille");
        if (exposure == 0) return text.Append(" (the region is never hit by this hazard on its own)").ToString();
        var ready = runtime.HazardReadyAt[hazard.Index * set.Regions.Count + region.Index];
        text.Append("\n  cooldown ").Append(ready > runtime.Step + 1 ? "until step " + Number(ready) : "over");
        var excluding = hazard.Excludes.Where(o => runtime.ActiveAt(o, region.Index) is not null).Select(o => set.Hazards[o].Id).ToList();
        text.Append("\n  excluded by ").Append(excluding.Count == 0 ? "nothing going" : string.Join(", ", excluding));
        var season = runtime.Season < 0 ? 0 : runtime.Season;
        text.Append("\n  chance a step in ").Append(set.Seasons[season].Id).Append(": ").Append(Number(runtime.OnsetChance(hazard, region.Index, season))).Append(" ppm (rate ").Append(Number(hazard.BasePpm)).Append(", season ").Append(Number(hazard.Seasonal[season])).Append(" per mille)");
        if (hazard.Excite.On) AppendExcitation(text, runtime, hazard, region.Index, season);
        if (hazard.When is not null) text.Append("\n  when ").Append(ConditionExplanation.Describe(hazard.When, runtime.LocalFactsFor(region.Index, host)));
        if (hazard.Paced) text.Append("\n  paced: the director is ").Append(Words.Name(runtime.Phase)).Append(runtime.Phase == DirectorPhase.Build ? ", so it may start" : ", so it is held back (" + Number(runtime.Suppressed) + " held back so far)");
        return text.ToString();
    }

    /// <summary>The two parts of the chance of an onset, side by side: the rate the hazard has on its own, and what earlier onsets here still add, with the numbers that make the second fall and the ratio that keeps the clusters finite.</summary>
    private static void AppendExcitation(StringBuilder text, WorldRuntime runtime, HazardDef hazard, int region, int season)
    {
        var excite = hazard.Excite;
        var own = runtime.OnsetBase(hazard, region, season);
        var added = runtime.ExcitationAt(hazard.Index, region);
        text.Append("\n  = own rate ").Append(Number(own)).Append(" + excitation ").Append(Number(added)).Append(" ppm (each onset here adds ").Append(Number(excite.JumpPpm)).Append(", the excitation falls by ").Append(Number(excite.DecayPermille))
            .Append(" per mille a step and never passes ").Append(Number(excite.CapPpm)).Append(")");
        var cluster = 1_000_000_000L / (1_000_000 - excite.BranchingPpm);
        text.Append("\n  branching ratio ").Append(Number(excite.BranchingPpm)).Append(" ppm: one onset causes that many more on average, so a cluster is about ").Append(Number(cluster / 1000)).Append('.').Append((cluster % 1000).ToString("D3", CultureInfo.InvariantCulture)).Append(" onsets in all");
    }

    /// <summary>Why a region's next weather is likelier to be one thing than another: its zone's weights, the regime's bias, and the weather its neighbours have now, with the tilt that makes of them (what a redraw this step would use).</summary>
    public static string Weather(WorldRuntime runtime, string regionId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var region = Region(runtime, regionId);
        var set = runtime.Set;
        var zone = set.Zones[region.Zone];
        var season = runtime.Season < 0 ? 0 : runtime.Season;
        var state = runtime.Regions[region.Index];
        var text = new StringBuilder("weather in " + region.Id + ": " + set.Weathers[state.Weather].Id + " now, zone " + zone.Id + ", spill " + Number(zone.Spill) + " per mille, persistence " + Number(zone.Seasons[season].Persistence) + " per mille in " + set.Seasons[season].Id);
        var graph = set.Graph;
        var neighbours = new List<string>();
        for (var edge = graph.FirstEdge(region.Index); edge < graph.EndEdge(region.Index); edge++)
        {
            var other = graph.Target(edge);
            neighbours.Add(set.Regions[other].Id + " " + set.Weathers[runtime.Regions[other].Weather].Id);
        }
        text.Append("\n  linked to ").Append(neighbours.Count == 0 ? "no region" : string.Join(", ", neighbours));
        var current = runtime.Regions.Select(r => r.Weather).ToList();
        var tilted = runtime.RedrawWeights(region.Index, season, current);
        var usual = set.Weathers.Select(w => (long)zone.Seasons[season].Frequency[w.Index] * set.Regimes[runtime.Regime].Bias[w.Index]).ToList();
        var usualTotal = usual.Sum();
        var tiltedTotal = tilted.Sum();
        text.Append("\n  if it changes now, per mille: ");
        text.Append(string.Join(", ", set.Weathers.Select(w => w.Id + " " + Number(usual[w.Index] * WorldMath.Permille / usualTotal) + (zone.Spill == 0 || neighbours.Count == 0 ? "" : " -> " + Number(tilted[w.Index] * WorldMath.Permille / tiltedTotal)))));
        text.Append(zone.Spill == 0 ? "\n  the zone lets its neighbours tilt nothing" : neighbours.Count == 0 ? "\n  the region has no link to tilt it" : "\n  (usual -> tilted by the neighbours' weather)");
        return text.ToString();
    }

    public static string Card(WorldRuntime runtime, string cardId, string regionId, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);
        if (!runtime.Set.CardById.TryGetValue(cardId, out var card)) throw new EngineRefusedException(WorldCodes.UnknownCard, "there is no card '" + cardId + "'");
        CheckHost(runtime, host);
        var set = runtime.Set;
        var teller = set.Storytellers[runtime.Storyteller];
        var text = new StringBuilder("card " + card.Id + " (" + Words.Name(card.Tier) + ", " + (card.Global ? "global" : "regional") + ", weight " + Number(card.Weight) + ")");
        var ready = runtime.CardReadyAt[card.Index];
        text.Append("\n  cooldown ").Append(ready > runtime.Step + 1 ? "until step " + Number(ready) : "over");
        if (card.Group >= 0) text.Append("\n  group ").Append(set.Groups[card.Group]).Append(runtime.GroupLockedUntil[card.Group] > runtime.Step + 1 ? " is held until step " + Number(runtime.GroupLockedUntil[card.Group]) : " is free");
        text.Append("\n  storyteller ").Append(teller.Id).Append(" gives the tier ").Append(Number(teller.Share[(int)card.Tier])).Append(" per mille");
        if (card.Tier == Tier.Bad) text.Append(", and bad cards come only while building (the director is ").Append(Words.Name(runtime.Phase)).Append(')');
        if (card.When is null) return text.Append("\n  no condition").ToString();
        if (card.Global)
        {
            text.Append("\n  when ").Append(ConditionExplanation.Describe(card.When, runtime.GlobalFactsFor(host)));
            return text.ToString();
        }
        var regions = regionId.Length == 0 ? set.Regions.Select(r => r.Id).ToList() : [Region(runtime, regionId).Id];
        foreach (var id in regions) text.Append("\n  when in ").Append(id).Append(": ").Append(ConditionExplanation.Describe(card.When, runtime.LocalFactsFor(set.RegionById[id].Index, host)));
        return text.ToString();
    }

    public static string Disease(WorldRuntime runtime, string diseaseId, string regionId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var region = Region(runtime, regionId);
        if (!runtime.Set.DiseaseById.TryGetValue(diseaseId, out var disease)) throw new EngineRefusedException(WorldCodes.UnknownDisease, "there is no disease '" + diseaseId + "'");
        var state = runtime.Regions[region.Index];
        var d = disease.Index;
        var living = state.Living(d);
        var text = new StringBuilder("disease " + disease.Id + " (" + Words.Name(disease.Model) + ") in " + region.Id + ": " + Words.Name(state.Outbreak[d]));
        text.Append("\n  susceptible ").Append(Number(state.Susceptible[d])).Append(", exposed ").Append(Number(state.Exposed[d])).Append(", infectious ").Append(Number(state.Infectious[d]))
            .Append(", recovered ").Append(Number(state.Recovered[d])).Append(", dead ").Append(Number(state.Dead[d])).Append(", peak infectious ").Append(Number(state.Peak[d]));
        if (living > 0)
        {
            var season = runtime.Season < 0 ? 0 : runtime.Season;
            // The basic reproduction number is beta over the recovery rate; the effective one scales it by the susceptible share. Both are per mille so they stay whole.
            var reproduction = (Int128)disease.BetaPpm * disease.Seasonal[season] / WorldMath.Permille * disease.Infectious * WorldMath.Permille / WorldMath.Ppm;
            var effective = reproduction * state.Susceptible[d] / living;
            text.Append("\n  reproduction number about ").Append(Number((long)reproduction)).Append(" per mille in ").Append(runtime.Set.Seasons[season].Id).Append(", effective about ").Append(Number((long)effective)).Append(" (below 1,000 the outbreak shrinks)");
        }
        var active = runtime.Set.Interventions.Where(i => i.Disease == d && state.Interventions[i.Index] != 0).Select(i => i.Id + ((state.Interventions[i.Index] & 2) != 0 ? " (automatic)" : "")).ToList();
        text.Append("\n  interventions on: ").Append(active.Count == 0 ? "none" : string.Join(", ", active));
        return text.ToString();
    }

    public static string Director(WorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var teller = runtime.Set.Storytellers[runtime.Storyteller];
        var text = new StringBuilder("director " + teller.Id + " on " + runtime.Set.Difficulties[runtime.Difficulty].Id + ": " + Words.Name(runtime.Phase) + " since step " + Number(runtime.PhaseStart) + ", stress " + Number(runtime.Stress));
        text.Append("\n  peaks at ").Append(Number(teller.PeakAt)).Append(", holds ").Append(Number(teller.PeakHold)).Append(" steps, relaxes below ").Append(Number(teller.RelaxBelow)).Append(" after at least ").Append(Number(teller.RestMin)).Append(" steps, decays ").Append(Number(teller.Decay)).Append(" per mille a step");
        text.Append("\n  cards ").Append(Number(runtime.Played)).Append(" played, last at step ").Append(Number(runtime.LastCard)).Append("; gap ").Append(Number(teller.GapMin)).Append(" to ").Append(Number(teller.GapMax)).Append(" steps");
        text.Append("\n  held back ").Append(Number(runtime.Suppressed)).Append(" paced onsets; dropped ").Append(Number(runtime.Dropped)).Append(" for want of room; agenda ").Append(Number(runtime.Agenda.Count)).Append("; hazards going ").Append(Number(runtime.ActiveHazards.Count));
        return text.ToString();
    }
}
