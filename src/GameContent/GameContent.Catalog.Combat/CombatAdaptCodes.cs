namespace GameContent.Catalog;

/// <summary>Why the catalog could not be turned into combat specs. The adapter refuses rather than invent a number.</summary>
public static class CombatAdaptCodes
{
    public const string MixedFuel = "COMBAT_ADAPT_MIXED_FUEL";
    public const string MissingStat = "COMBAT_ADAPT_MISSING_STAT";
    public const string NotAnItem = "COMBAT_ADAPT_NOT_AN_ITEM";
    public const string NotAFormation = "COMBAT_ADAPT_NOT_A_FORMATION";
    public const string NotAmmunition = "COMBAT_ADAPT_NOT_AMMUNITION";
    public const string UnitMismatch = "COMBAT_ADAPT_UNIT_MISMATCH";
}
