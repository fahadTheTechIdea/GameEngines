namespace CombatEngine;

/// <summary>
/// What one weapon does, as numbers the caller read from the catalog: this engine never stores a weapon. Lengths are in thousandths of
/// a metre, the rate in thousandths of a shot per minute and accuracy in per-mille, so all arithmetic is integer.
/// </summary>
public sealed record WeaponSpec(string Id, string DamageType, long Damage, long Penetration, long RangeMilli, long AccuracyPermille, long RatePerMinuteMilli, long SplashMilli);
