using EngineCommon;
using EngineCommon.Balance;
using WorldEventsEngine;

/// <summary>
/// The one rule for telling a natural onset from the rest, shared by the per-onset sweep and the rare-event sampler. A hazard onset is a <c>Started</c> change; the engine draws its rung from the ladder only for an onset the world makes
/// itself, and that onset starts at full power (1,000 per mille), while an onset that spread from a neighbour starts weaker (the falloff, hop by hop). So a natural onset is a <c>Started</c> change at full power among the changes
/// of the step itself, not among those of the scripted calls (a script or a card chooses its rung). A hazard that spreads without losing power (reach above 0 hops and falloff 1,000) cannot be told apart this way, and is refused for the
/// per-onset and rare-event modes (<see cref="BalanceCodes.OnsetAmbiguous"/>) instead of being guessed at.
/// </summary>
internal static class OriginRule
{
    private const int FullPower = 1000;

    public static void Require(WorldSet set)
    {
        foreach (var hazard in set.Hazards)
            if (hazard.ReachHops > 0 && hazard.Falloff >= FullPower)
                throw new EngineRefusedException(BalanceCodes.OnsetAmbiguous, "hazard '" + hazard.Id + "' spreads at full power (reach " + hazard.ReachHops + ", falloff " + hazard.Falloff + "), so a natural onset cannot be told from a spread one");
    }

    /// <summary>True for the change at <paramref name="index"/> of a step's changes when it is a natural onset of a hazard; <paramref name="fromStep"/> is how many of the step's changes came from scripted calls.</summary>
    public static bool IsNaturalOnset(WorldChange change, int index, int fromStep) =>
        index >= fromStep && change.Kind == ChangeKind.Hazard && change.Sub2 == (int)HazardChange.Started && change.Amount == FullPower;
}
