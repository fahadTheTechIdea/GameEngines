namespace CombatEngine;

/// <summary>What a round adds to the weapon that fires it: damage and penetration, and optionally its own damage type and splash.</summary>
public sealed record AmmoSpec(string Id, string? DamageType, long Damage, long Penetration, long SplashMilli);
