using EngineCommon;

namespace DialogueEngine;

/// <summary>Moves an active quest to another stage; a stage that ends the quest ends it.</summary>
public sealed class SetStageEffect(QuestDef quest, string stage) : Effect
{
    public QuestDef Quest { get; } = quest;
    public string Stage { get; } = stage;

    internal override void Apply(StepContext context)
    {
        var progress = context.State.QuestValues[Quest.Id];
        if (progress.Status != QuestStatus.Active) throw new EngineRefusedException(DialogueCodes.QuestState, "quest '" + Quest.Id + "' is " + QuestStatuses.Name(progress.Status) + ", not active");
        Quests.Enter(context, Quest, Stage);
    }
}