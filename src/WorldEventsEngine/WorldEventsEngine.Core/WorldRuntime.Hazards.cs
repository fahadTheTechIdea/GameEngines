using EngineCommon;
using EngineCommon.Graph;

namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>
    /// The hazards for one step: the ones that are going on move through their phases (onset, peak, recovery) and may escalate by a rung; then every hazard that a region is exposed to may start. An onset is a Bernoulli
    /// trial per step with the hazard's rate, scaled by the season, the region's exposure and the difficulty: the discretisation of a Poisson process whose chance of two onsets in one step is neglected (about half the
    /// square of the rate, so under 0.5% for rates below 1 in 100). A hazard that depends on the weather (a flood when the river is high) has the rate of certainty and a condition. How bad an onset is comes
    /// from the ladder's tails: the chance of at least this bad, as Gutenberg and Richter give earthquakes by magnitude. An onset reaches the regions linked to it, up to the hazard's hops, weaker by the falloff each hop.
    /// </summary>
    private void LiveHazards(long step, int season, WorldChanges changes)
    {
        foreach (var instance in _hazards.ToArray()) Progress(instance, step, changes);
        var regions = Set.Regions.Count;
        foreach (var hazard in Set.Hazards)
        {
            if (hazard.BasePpm == 0 && !hazard.Excite.On) continue;
            var stream = _hazardStreams[hazard.Index];
            var forcedStep = ForcedStepOf(hazard.Index, step);
            for (var r = 0; r < regions; r++)
            {
                if (hazard.Excite.On) Fade(hazard, r);
                var exposure = Set.Regions[r].Exposure[hazard.Index];
                if (exposure == 0 || ActiveAt(hazard.Index, r) is not null || HazardReadyAt[hazard.Index * regions + r] > step) continue;
                var forced = forcedStep && r == _forced!.Value.Region;
                if (forcedStep && !forced) continue;                    // the one forced trial of the step is the only trial of this hazard in it
                var chance = OnsetChance(hazard, r, season);
                var at = step * RegionSlots + r * HazardSlots;
                if (_watcher is not null && !forcedStep) _watcher.Trial(step, hazard.Index, r, VetoOf(hazard, r) == HazardVeto.None ? chance : 0);
                if (!forced && (chance <= 0 || Roll(stream, at, WorldMath.Ppm) >= chance)) continue;
                var veto = VetoOf(hazard, r);
                if (veto == HazardVeto.Pacing) _suppressed++;
                if (veto != HazardVeto.None) continue;
                var severity = Roll(stream, at + 1, forced ? _forced!.Value.SeverityBelowPpm : WorldMath.Ppm);
                var rung = 0;
                for (var k = 1; k < hazard.Ladder.Count; k++)
                    if (hazard.Ladder[k].TailPpm > severity) rung = k;
                if (!StartHazard(hazard.Index, r, rung, WorldMath.Permille, true, RollBits(stream, at + 2), step, changes)) continue;
                if (_watcher is not null && !forcedStep) _watcher.Started(step, hazard.Index, r, rung);
                Spread(hazard, r, rung, step, changes);
            }
        }
    }

    /// <summary>What can stop an onset whose trial has passed: nothing, an exclusion (a hazard it excludes is going on there), its condition not holding, or the director's pacing. Checked in this order, and read-only.</summary>
    private enum HazardVeto
    {
        None,
        Excluded,
        Condition,
        Pacing,
    }

    private HazardVeto VetoOf(HazardDef hazard, int region)
    {
        if (hazard.Excludes.Any(other => ActiveAt(other, region) is not null)) return HazardVeto.Excluded;
        if (hazard.When is not null && !Holds(hazard.When, region)) return HazardVeto.Condition;
        if (hazard.Paced && _phase != DirectorPhase.Build) return HazardVeto.Pacing;
        return HazardVeto.None;
    }

    /// <summary>The chance, in parts per million, that the hazard starts in the region in one step of the season: the part that is its own rate (<see cref="OnsetBase"/>) plus the excitation earlier onsets left (at most certainty).</summary>
    internal long OnsetChance(HazardDef hazard, int region, int season) => Math.Min(WorldMath.Ppm, OnsetBase(hazard, region, season) + ExcitationAt(hazard.Index, region));

    /// <summary>The part of the chance that does not depend on earlier onsets: the hazard's rate, times the season's share, the region's exposure and the difficulty's share, each per mille.</summary>
    internal long OnsetBase(HazardDef hazard, int region, int season) =>
        (long)hazard.BasePpm * hazard.Seasonal[season] / WorldMath.Permille * Set.Regions[region].Exposure[hazard.Index] / WorldMath.Permille * Set.Difficulties[_difficulty].Hazard / WorldMath.Permille;

    /// <summary>One step of the excitation falling: it loses the hazard's decay share of itself (rounded down, so it reaches 0 and a cluster ends for good).</summary>
    private void Fade(HazardDef hazard, int region)
    {
        var at = hazard.Index * Set.Regions.Count + region;
        _excitation[at] = _excitation[at] * (WorldMath.Permille - hazard.Excite.DecayPermille) / WorldMath.Permille;
    }

    /// <summary>An onset of the hazard in the region (one the world made, a card played or a script began; not one that only spread there) adds the hazard's jump to the region's excitation, up to the cap.</summary>
    private void Excite(HazardDef hazard, int region)
    {
        if (!hazard.Excite.On) return;
        var at = hazard.Index * Set.Regions.Count + region;
        _excitation[at] = Math.Min(hazard.Excite.CapPpm, _excitation[at] + hazard.Excite.JumpPpm);
    }

    private void Spread(HazardDef hazard, int origin, int rung, long step, WorldChanges changes)
    {
        if (hazard.ReachHops == 0) return;
        // The shared breadth-first search finds how many hops each region is from the origin; the power of a region is the falloff applied once per hop (each step rounded down), the same for every region at that distance.
        var reach = Bfs.Run(Set.Graph, [origin], hazard.ReachHops, _reach);
        if (reach.Code.Length != 0) throw new EngineRefusedException(WorldCodes.Links, "the reach of hazard '" + hazard.Id + "' was refused by the graph library: " + reach.Code);
        var hops = reach.Hops;
        for (var region = 0; region < hops.Length; region++)
        {
            var distance = hops[region];
            if (distance <= 0) continue;
            var power = WorldMath.Permille;
            for (var hop = 0; hop < distance; hop++) power = power * hazard.Falloff / WorldMath.Permille;
            if (power == 0 || Set.Regions[region].Exposure[hazard.Index] == 0 || ActiveAt(hazard.Index, region) is not null) continue;
            if (hazard.Excludes.Any(other => ActiveAt(other, region) is not null)) continue;
            StartHazard(hazard.Index, region, rung, power, false, RollBits(_hazardStreams[hazard.Index], step * RegionSlots + region * HazardSlots + 2), step, changes);
        }
    }

    /// <summary>Starts a hazard in a region at a rung and a power (per mille of full strength). Returns false when it is already there or the list of active hazards is full (counted in <see cref="Dropped"/>).</summary>
    private bool StartHazard(int hazard, int region, int rung, int power, bool origin, ulong durationBits, long step, WorldChanges changes)
    {
        if (ActiveAt(hazard, region) is not null) return false;
        if (_hazards.Count >= Set.Limits.MaxHazards)
        {
            _dropped++;
            return false;
        }
        var def = Set.Hazards[hazard];
        var rungDef = def.Ladder[rung];
        var duration = rungDef.MinDuration + (int)(durationBits % (ulong)(rungDef.MaxDuration - rungDef.MinDuration + 1));
        var instance = new HazardInstance { Hazard = hazard, Region = region, Rung = rung, Power = power, PeakLength = duration, PhaseStart = step };
        if (rungDef.OnsetSteps > 0)
        {
            instance.Phase = HazardPhase.Onset;
            instance.PhaseLength = rungDef.OnsetSteps;
        }
        else
        {
            instance.Phase = HazardPhase.Peak;
            instance.PhaseLength = duration;
        }
        Place(instance);
        Touch();
        changes.Add(ChangeKind.Hazard, step, region, hazard, rung, (int)HazardChange.Started, power);
        if (origin)
        {
            Excite(def, region);
            AddStress(rungDef.Stress, true);
        }
        IssueEffects(rungDef.Effects, region, "hazard:" + def.Id, power, -1, step, changes);
        return true;
    }

    private void Progress(HazardInstance instance, long step, WorldChanges changes)
    {
        var def = Set.Hazards[instance.Hazard];
        var rung = def.Ladder[instance.Rung];
        var elapsed = step - instance.PhaseStart + 1;
        while (elapsed > instance.PhaseLength)
        {
            instance.PhaseStart += instance.PhaseLength;
            if (instance.Phase == HazardPhase.Onset)
            {
                instance.Phase = HazardPhase.Peak;
                instance.PhaseLength = instance.PeakLength;
            }
            else if (instance.Phase == HazardPhase.Peak && rung.RecoverySteps > 0)
            {
                instance.Phase = HazardPhase.Recovery;
                instance.PhaseLength = rung.RecoverySteps;
                changes.Add(ChangeKind.Hazard, step, instance.Region, instance.Hazard, instance.Rung, (int)HazardChange.Receding, instance.Power);
            }
            else
            {
                Remove(instance);
                HazardReadyAt[instance.Hazard * Set.Regions.Count + instance.Region] = step + 1 + def.Cooldown;
                changes.Add(ChangeKind.Hazard, step, instance.Region, instance.Hazard, instance.Rung, (int)HazardChange.Ended, instance.Power);
                return;
            }
            elapsed = step - instance.PhaseStart + 1;
        }
        if (instance.Phase != HazardPhase.Peak || rung.EscalatePpm == 0) return;
        var stream = _hazardStreams[instance.Hazard];
        var at = step * RegionSlots + instance.Region * HazardSlots;
        if (Roll(stream, at + 3, WorldMath.Ppm) >= rung.EscalatePpm) return;
        instance.Rung++;
        var worse = def.Ladder[instance.Rung];
        instance.PeakLength = worse.MinDuration + (int)(RollBits(stream, at + 2) % (ulong)(worse.MaxDuration - worse.MinDuration + 1));
        instance.Phase = HazardPhase.Peak;
        instance.PhaseStart = step;
        instance.PhaseLength = instance.PeakLength;
        Touch();
        changes.Add(ChangeKind.Hazard, step, instance.Region, instance.Hazard, instance.Rung, (int)HazardChange.Escalated, instance.Power);
        AddStress(worse.Stress, true);
        IssueEffects(worse.Effects, instance.Region, "hazard:" + def.Id, instance.Power, -1, step, changes);
    }

    /// <summary>How strong a hazard is now in its region, per mille: it rises over the onset, holds at the peak, and falls over the recovery, times the power it reached the region with.</summary>
    public int Intensity(HazardInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var elapsed = Math.Max(1, Step - instance.PhaseStart + 1);
        long level;
        if (instance.Phase == HazardPhase.Onset) level = Math.Min(WorldMath.Permille, elapsed * WorldMath.Permille / instance.PhaseLength);
        else if (instance.Phase == HazardPhase.Peak) level = WorldMath.Permille;
        else level = WorldMath.Permille - Math.Min(WorldMath.Permille, elapsed * WorldMath.Permille / (instance.PhaseLength + 1));
        return (int)(level * instance.Power / WorldMath.Permille);
    }
}
