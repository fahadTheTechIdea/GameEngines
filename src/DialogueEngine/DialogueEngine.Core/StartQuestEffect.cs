using EngineCommon;

namespace DialogueEngine;

/// <summary>Starts a quest that has not started: it becomes active at its first stage.</summary>
public sealed class StartQuestEffect(QuestDef quest) : Effect
{
    public QuestDef Quest { get; } = quest;

    internal override void Apply(StepContext context)
    {
        var progress = context.State.QuestValues[Quest.Id];
        if (progress.Status != QuestStatus.NotStarted) throw new EngineRefusedException(DialogueCodes.QuestState, "quest '" + Quest.Id + "' is " + QuestStatuses.Name(progress.Status) + ", not not-started");
        Quests.Enter(context, Quest, Quest.StartStage);
    }
}