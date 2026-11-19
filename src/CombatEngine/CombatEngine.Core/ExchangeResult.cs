namespace CombatEngine;

/// <summary>Both groups after a simultaneous exchange, what each side's fire did, and a hash of the resulting state.</summary>
public sealed record ExchangeResult(Group First, Group Second, EngagementResult FirstFired, EngagementResult SecondFired, string Hash);
