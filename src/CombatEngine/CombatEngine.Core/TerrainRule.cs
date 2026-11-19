namespace CombatEngine;

/// <summary>Ground a fight or a march crosses: the share of normal hit chance it allows (sight lines) and the share of normal speed (going).</summary>
public sealed record TerrainRule(string Id, int AccuracyPercent, int MovementPercent);
