namespace CombatEngine;

/// <summary>What one battery did in an engagement, counted by outcome. Every shot earned is in exactly one count.</summary>
public sealed record BatteryReport(string Battery, long Earned, long Fired, long Hits, long Misses, long OutOfRange, long NoAmmo, long Wasted, long Damage);
