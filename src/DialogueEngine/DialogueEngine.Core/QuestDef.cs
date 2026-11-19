namespace DialogueEngine;

public sealed record QuestDef(string Id, string TitleKey, string StartStage, IReadOnlyList<StageDef> Stages)
{
    public StageDef Stage(string id) => Stages.First(s => s.Id == id);

    public bool HasStage(string id) => Stages.Any(s => s.Id == id);
}