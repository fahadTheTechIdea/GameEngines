using EngineCommon;

namespace DialogueEngine;

/// <summary>Ends an active quest as completed, failed or abandoned, wherever it stands.</summary>
public sealed class FinishQuestEffect(QuestDef quest, QuestStatus status) : Effect
{
    public QuestDef Quest { get; } = quest;
    public QuestStatus Status { get; } = status;

    internal override void Apply(StepContext context)
    {
        var progress = context.State.QuestValues[Quest.Id];
        if (progress.Status != QuestStatus.Active) throw new EngineRefusedException(DialogueCodes.QuestState, "quest '" + Quest.Id + "' is " + QuestStatuses.Name(progress.Status) + ", not active");
        context.SetQuest(Quest, progress with { Status = Status });
    }
}