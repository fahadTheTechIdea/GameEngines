namespace DialogueEngine;

/// <summary>When the condition holds, the quest moves to the stage.</summary>
public sealed record StageRule(Guard When, string To);