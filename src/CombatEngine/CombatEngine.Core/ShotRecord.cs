namespace CombatEngine;

/// <summary>One shot in the explained log: who fired, which shot, what happened (a code), the roll against the chance, and the damage done.</summary>
public sealed record ShotRecord(string Battery, long Index, string Code, long Roll, long ChancePermille, long Damage);
