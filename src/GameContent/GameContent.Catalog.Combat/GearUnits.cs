namespace GameContent.Catalog;

/// <summary>
/// The unit each combat stat must be declared in by the catalog vocabulary. The adapter converts from exactly these units; a pack that
/// declares range in kilometres would otherwise be read a thousand times too short, so a different unit is refused, not guessed at.
/// </summary>
public static class GearUnits
{
    public static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [GearStats.Damage] = "1",
        [GearStats.Range] = "m",
        [GearStats.RateOfFire] = "/min",
        [GearStats.Accuracy] = "%",
        [GearStats.Penetration] = "1",
        [GearStats.SplashRadius] = "m",
        [GearStats.ArmorRating] = "1",
        [GearStats.Structure] = "1",
        [GearStats.Speed] = "km/h",
    };

    /// <summary>Refuses with <see cref="CombatAdaptCodes.UnitMismatch"/> unless the set declares every combat stat in the unit the adapter converts from.</summary>
    public static void Check(CatalogSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        foreach (var (id, unit) in Expected)
        {
            if (!set.TryStat(id, out var stat)) throw new CombatAdaptRefusedException(CombatAdaptCodes.UnitMismatch, "the vocabulary has no stat '" + id + "', which combat reads");
            if (stat.Unit != unit) throw new CombatAdaptRefusedException(CombatAdaptCodes.UnitMismatch, "stat '" + id + "' is declared in '" + stat.Unit + "', but the combat adapter converts from '" + unit + "'");
        }
    }
}
