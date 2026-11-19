using EngineCommon;

namespace WorldEventsEngine;

/// <summary>
/// One condition for one step of a branched world: in step <see cref="Step"/> the trial of <see cref="Hazard"/> in <see cref="Region"/> is taken as passed, the severity of its onset is drawn below <see cref="SeverityBelowPpm"/> parts per
/// million (the whole ladder is <see cref="WorldMath.Ppm"/>), and the hazard's trials in every other region of that step are not taken. It is the conditioning a rare-event sampler needs to restart many futures from the state just before
/// an onset: the trial has passed, and the severity is drawn again within the level the onset has already been conditioned on.
/// </summary>
public readonly record struct ForcedOnset(int Hazard, int Region, long Step, long SeverityBelowPpm);

public sealed partial class WorldRuntime
{
    private ForcedOnset? _forced;

    /// <summary>
    /// <see cref="Branch"/> (the same state at the same step, another seed, so the future is a fresh one) with <paramref name="forced"/> applied to the step it names, which must lie after this state's step. The severity of the forced onset
    /// is the engine's own draw for that onset, taken uniformly below the bound, so the rung it gets is the ladder's, conditioned on being among the worst <c>SeverityBelowPpm</c> parts per million. Nothing else about the step changes: the
    /// weather, the other hazards, the diseases and the director draw as they would. A condition that names something that is not there is refused with <see cref="WorldCodes.ForcedOnset"/>; this runtime is not changed.
    /// </summary>
    public WorldRuntime BranchForcingOnset(WorldSet reseeded, ForcedOnset forced)
    {
        ArgumentNullException.ThrowIfNull(reseeded);
        if (forced.Hazard < 0 || forced.Hazard >= reseeded.Hazards.Count) throw new EngineRefusedException(WorldCodes.ForcedOnset, "there is no hazard number " + forced.Hazard);
        if (forced.Region < 0 || forced.Region >= reseeded.Regions.Count) throw new EngineRefusedException(WorldCodes.ForcedOnset, "there is no region number " + forced.Region);
        if (reseeded.Regions[forced.Region].Exposure[forced.Hazard] == 0) throw new EngineRefusedException(WorldCodes.ForcedOnset, "region '" + reseeded.Regions[forced.Region].Id + "' is not exposed to hazard '" + reseeded.Hazards[forced.Hazard].Id + "', so no onset can start there");
        if (forced.Step <= Step) throw new EngineRefusedException(WorldCodes.ForcedOnset, "the forced step " + forced.Step + " is not after step " + Step);
        if (forced.SeverityBelowPpm < 1 || forced.SeverityBelowPpm > WorldMath.Ppm) throw new EngineRefusedException(WorldCodes.ForcedOnset, "the severity bound is 1 to " + WorldMath.Ppm + " ppm, not " + forced.SeverityBelowPpm);
        var branch = Branch(reseeded);
        branch._forced = forced;
        return branch;
    }

    /// <summary>True when the forced condition names this hazard in this step: the trials of its other regions are skipped.</summary>
    private bool ForcedStepOf(int hazard, long step) => _forced is { } forced && forced.Hazard == hazard && forced.Step == step;
}
