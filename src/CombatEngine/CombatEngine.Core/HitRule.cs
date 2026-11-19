namespace CombatEngine;

/// <summary>The chance, in per-mille, that a shot hits: the weapon's accuracy scaled by the range band, the shooter's training, the terrain, being suppressed and the target's cover, then clamped.</summary>
public static class HitRule
{
    /// <summary>The accuracy percentage of the band holding <paramref name="distanceMilli"/> as a share of the weapon's range.</summary>
    public static int BandPercent(CombatRules rules, long distanceMilli, long rangeMilli)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var share = rangeMilli <= 0 ? 100 : distanceMilli * 100 / rangeMilli;
        return rules.RangeBands.First(b => share <= b.UpToPercent).AccuracyPercent;
    }

    /// <summary>Area weapons ignore cover when they hit: the blast does not care what the target hides behind.</summary>
    public static long ChancePermille(CombatRules rules, long accuracyPermille, int bandPercent, int trainingPercent, int terrainPercent, bool suppressed, int coverPercent, bool area)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var chance = accuracyPermille * bandPercent / 100 * trainingPercent / 100 * terrainPercent / 100;
        if (suppressed) chance = chance * rules.SuppressedAccuracyPercent / 100;
        if (!area) chance = chance * coverPercent / 100;
        return Math.Clamp(chance, rules.HitChanceFloorPermille, rules.HitChanceCeilingPermille);
    }
}
