namespace DialogueEngine;

/// <summary>The one place a quest changes stage, so the rule that a stage may end the quest is applied the same way by an effect and by a stage rule.</summary>
internal static class Quests
{
    public static void Enter(StepContext context, QuestDef quest, string stage)
    {
        var def = quest.Stage(stage);
        var status = def.Ends ?? QuestStatus.Active;
        context.SetQuest(quest, new QuestProgress(status, stage));
    }
}