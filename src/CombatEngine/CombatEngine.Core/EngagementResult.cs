namespace CombatEngine;

/// <summary>The outcome: both groups after the engagement, a report per battery, the first logged shots, engagement-level codes and a hash of the resulting state.</summary>
public sealed record EngagementResult(Group Attacker, Group Defender, IReadOnlyList<BatteryReport> Batteries, IReadOnlyList<ShotRecord> Shots, IReadOnlyList<string> Codes, string Hash);
