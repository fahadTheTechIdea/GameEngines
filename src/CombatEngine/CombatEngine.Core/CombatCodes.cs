namespace CombatEngine;

/// <summary>Every code the engine reports: shot and engagement outcomes in results, and the reasons rules or requests are refused.</summary>
public static class CombatCodes
{
    // Outcomes in results.
    public const string Hit = "COMBAT_HIT";
    public const string Miss = "COMBAT_MISS";
    public const string OutOfRange = "COMBAT_OUT_OF_RANGE";
    public const string NoAmmo = "COMBAT_NO_AMMO";
    public const string TargetDestroyed = "COMBAT_TARGET_DESTROYED";
    public const string Routed = "COMBAT_ROUTED";
    public const string Destroyed = "COMBAT_DESTROYED";
    public const string Suppressed = "COMBAT_SUPPRESSED";

    // Rules content refused.
    public const string RulesFormat = "COMBAT_RULES_FORMAT";
    public const string RulesUnknownField = "COMBAT_RULES_UNKNOWN_FIELD";
    public const string RulesMissingField = "COMBAT_RULES_MISSING_FIELD";
    public const string RulesRange = "COMBAT_RULES_RANGE";
    public const string RulesDuplicate = "COMBAT_RULES_DUPLICATE";
    public const string RulesBands = "COMBAT_RULES_BANDS";

    // Request refused.
    public const string UnknownDamageType = "COMBAT_UNKNOWN_DAMAGE_TYPE";
    public const string UnknownCover = "COMBAT_UNKNOWN_COVER";
    public const string UnknownTraining = "COMBAT_UNKNOWN_TRAINING";
    public const string UnknownTerrain = "COMBAT_UNKNOWN_TERRAIN";
    public const string UnknownBattery = "COMBAT_UNKNOWN_BATTERY";
    public const string GroupWithdrawn = "COMBAT_GROUP_WITHDRAWN";
    public const string BadRequest = "COMBAT_BAD_REQUEST";
    public const string SameSide = "COMBAT_SAME_SIDE";
    public const string TooManyShots = "COMBAT_TOO_MANY_SHOTS";
    public const string NumericRange = "COMBAT_NUMERIC_RANGE";
}
