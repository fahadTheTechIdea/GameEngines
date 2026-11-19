namespace CombatEngine;

/// <summary>How a damage type meets armor: the share of the armor rating that applies, and whether failing to penetrate cuts the damage.</summary>
public sealed record DamageTypeRule(string Id, int ArmorFactorPercent, bool ArmorChecked);

/// <summary>Accuracy at a share of the weapon's range: the first band whose limit reaches the share applies.</summary>
public sealed record RangeBand(int UpToPercent, int AccuracyPercent);

public sealed record CoverRule(string Id, int AccuracyPercent, int DamagePercent);

/// <summary>A training level: its effect on accuracy and damage and the morale a group of that training starts with.</summary>
public sealed record TrainingRule(string Id, int AccuracyPercent, int DamagePercent, int MoralePermille);

/// <summary>
/// The validated rules of a game's combat: curves and factors only. It holds no weapon, armor or unit number; those belong to the
/// catalog and arrive with each request. It is immutable, so one instance serves any number of engagements.
/// </summary>
public sealed record CombatRules(
    int ArmorConstant,
    int MinimumDamage,
    int NonPiercingCutPercent,
    int HitChanceFloorPermille,
    int HitChanceCeilingPermille,
    int PersonnelHealth,
    IReadOnlyList<DamageTypeRule> DamageTypes,
    IReadOnlyList<RangeBand> RangeBands,
    IReadOnlyList<CoverRule> Cover,
    IReadOnlyList<TrainingRule> Training,
    int SuppressionHits,
    int SuppressedAccuracyPercent,
    int RoutStrengthPercent,
    int SplashSpacingMilli,
    int SplashMaxTargets,
    int SplashDamagePercent,
    int MaxEngagementSeconds,
    int MaxShotsPerBattery,
    MoraleRules Morale,
    IReadOnlyList<TerrainRule> Terrain,
    int FootSpeedMilliPerSecond)
{
    public DamageTypeRule DamageType(string id) =>
        DamageTypes.FirstOrDefault(t => t.Id == id) ?? throw new CombatRefusedException(CombatCodes.UnknownDamageType, "the rules have no damage type '" + id + "'");

    public CoverRule CoverLevel(string id) =>
        Cover.FirstOrDefault(c => c.Id == id) ?? throw new CombatRefusedException(CombatCodes.UnknownCover, "the rules have no cover level '" + id + "'");

    public TrainingRule TrainingLevel(string id) =>
        Training.FirstOrDefault(t => t.Id == id) ?? throw new CombatRefusedException(CombatCodes.UnknownTraining, "the rules have no training level '" + id + "'");

    public TerrainRule TerrainType(string id) =>
        Terrain.FirstOrDefault(t => t.Id == id) ?? throw new CombatRefusedException(CombatCodes.UnknownTerrain, "the rules have no terrain '" + id + "'");
}
