namespace DialogueEngine;

/// <summary>A stage of a quest: the journal text key, the rules that move the quest on, and whether reaching it ends the quest (completed or failed).</summary>
public sealed record StageDef(string Id, string JournalKey, IReadOnlyList<StageRule> Advance, QuestStatus? Ends);