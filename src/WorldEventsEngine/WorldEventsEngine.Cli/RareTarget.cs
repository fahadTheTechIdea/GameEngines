using EngineCommon;
using EngineCommon.Balance;
using WorldEventsEngine;

/// <summary>The one reading of a rare-event target, <c>hazard:rung</c>, against a document's hazards and rungs, shared by both splitting models.</summary>
internal static class RareTarget
{
    /// <summary>The hazard and the rung the target names; <see cref="BalanceCodes.RareTarget"/> when either is not there, naming what is.</summary>
    public static (HazardDef Hazard, RungDef Rung) Read(WorldSet set, string target)
    {
        var parts = target.Split(':');
        if (parts.Length != 2) throw new EngineRefusedException(BalanceCodes.RareTarget, "the target is <hazard>:<rung>, not '" + target + "'");
        var hazard = set.Hazards.FirstOrDefault(h => h.Id == parts[0]);
        if (hazard is null) throw new EngineRefusedException(BalanceCodes.RareTarget, "there is no hazard '" + parts[0] + "'; the hazards are " + string.Join(", ", set.Hazards.Select(h => h.Id)));
        var rung = hazard.Ladder.FirstOrDefault(r => r.Id == parts[1]);
        if (rung is null) throw new EngineRefusedException(BalanceCodes.RareTarget, "hazard '" + hazard.Id + "' has no rung '" + parts[1] + "'; its rungs are " + string.Join(", ", hazard.Ladder.Select(r => r.Id)));
        return (hazard, rung);
    }
}
