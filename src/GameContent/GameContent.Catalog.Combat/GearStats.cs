namespace GameContent.Catalog;

/// <summary>
/// The ids of the stats combat reads from the catalog's gear vocabulary. This is the only place that knows them: combat itself
/// reads numbers the adapter hands over, and the numbers are the catalog's.
/// </summary>
public static class GearStats
{
    public const string Damage = "gear.damage";
    public const string DamageType = "gear.damage-type";
    public const string Range = "gear.range";
    public const string RateOfFire = "gear.rate-of-fire";
    public const string Accuracy = "gear.accuracy";
    public const string Penetration = "gear.penetration";
    public const string SplashRadius = "gear.splash-radius";
    public const string ArmorRating = "gear.armor-rating";
    public const string Structure = "gear.structure";
    public const string Speed = "gear.speed";
}
