namespace CombatEngine;

/// <summary>
/// The one rule that turns a hit into damage. Nothing else in the engine computes damage. Order: training and cover scale the damage;
/// the armor rating, scaled by the damage type's factor and lowered by penetration, mitigates it through armor / (armor + K); a damage
/// type that checks armor loses a share of the damage when penetration did not beat the armor; a hit never does less than the floor.
/// </summary>
public static class DamageRule
{
    public static long Compute(CombatRules rules, long baseDamage, string damageType, long penetration, long armorRating, int trainingDamagePercent, int coverDamagePercent)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var type = rules.DamageType(damageType);
        var scaled = baseDamage * trainingDamagePercent / 100 * coverDamagePercent / 100;
        if (scaled <= 0) return 0;
        var armor = armorRating * type.ArmorFactorPercent / 100;
        var effective = Math.Max(0, armor - penetration);
        var damage = scaled * rules.ArmorConstant / (rules.ArmorConstant + effective);
        if (type.ArmorChecked && penetration < armor) damage = damage * (100 - rules.NonPiercingCutPercent) / 100;
        return Math.Max(rules.MinimumDamage, damage);
    }
}
