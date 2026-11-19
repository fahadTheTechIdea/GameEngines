namespace GameContent.Catalog;

/// <summary>A stat (or a skill) of the game's character vocabulary and the ranges members draw from at each training level.</summary>
public sealed record StatPlan(string Id, TrainingRanges Ranges);
