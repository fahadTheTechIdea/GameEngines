using EngineCommon;
using System.Text.Json;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

internal sealed partial class DialogueReader
{
    private void ReadQuest(JsonElement element)
    {
        var where = "quest";
        _json.Object(element, where, ["id", "title", "start", "stages"]);
        var id = _json.Id(element, "id", where);
        where = "quest '" + id + "'";
        var title = Key(element, "title", where, []);
        var start = _json.Id(element, "start", where);
        var names = _questStages[id];
        if (!names.Contains(start)) throw _json.Refuse(DialogueCodes.Reference, where + ".start '" + start + "' is not one of its stages");
        var stages = new List<StageDef>();
        foreach (var stage in _json.Array(element, "stages", where, 1, 64).EnumerateArray())
        {
            var at = where + ".stages";
            _json.Object(stage, at, ["id", "journal", "advance"], "ends");
            var stageId = _json.Id(stage, "id", at);
            at = where + " stage '" + stageId + "'";
            var journal = Key(stage, "journal", at, []);
            QuestStatus? ends = null;
            if (_json.Has(stage, "ends"))
            {
                var name = _json.Text(stage, "ends", at, 16);
                if (name is not ("completed" or "failed")) throw _json.Refuse(DialogueCodes.Format, at + ".ends must be completed or failed");
                ends = name == "completed" ? QuestStatus.Completed : QuestStatus.Failed;
            }
            var rules = new List<StageRule>();
            foreach (var rule in _json.Array(stage, "advance", at, 0, 16).EnumerateArray())
            {
                _json.Object(rule, at + ".advance", ["when", "to"]);
                var to = _json.Id(rule, "to", at + ".advance");
                if (!names.Contains(to)) throw _json.Refuse(DialogueCodes.Reference, at + ".advance goes to '" + to + "', which is not a stage of the quest");
                _stageReferences.Add((id, to));
                rules.Add(new StageRule(ReadGuard(rule.GetProperty("when"), at + ".advance.when"), to));
            }
            if (ends is not null && rules.Count > 0) throw _json.Refuse(DialogueCodes.Format, at + " ends the quest and cannot also advance");
            stages.Add(new StageDef(stageId, journal, rules, ends));
        }
        _quests[id] = new QuestDef(id, title, start, stages);
    }

    /// <summary>A stage no rule, effect or start leads to can never be reached; it is refused so a typo does not hide a dead part of a quest.</summary>
    private void CheckQuestStages()
    {
        foreach (var quest in _quests.Values)
            foreach (var stage in quest.Stages)
                if (stage.Id != quest.StartStage && !_stageReferences.Contains((quest.Id, stage.Id)))
                    throw _json.Refuse(DialogueCodes.Unreachable, "quest '" + quest.Id + "' stage '" + stage.Id + "' is never reached by a rule or an effect");
    }

    // ---- conditions ---------------------------------------------------------------------------------------------------------------------

    private Guard ReadGuard(JsonElement element, string where)
    {
        var condition = _conditions.Read(element, where, _facts);
        CheckValues(condition, where);
        return new Guard(condition, Conditions.FactsOf(condition));
    }

    /// <summary>A comparison with a fixed list of values (a quest status, a stage, a text variable with allowed values) may only name a value from the list.</summary>
    private void CheckValues(Condition condition, string where)
    {
        switch (condition)
        {
            case AllCondition all: foreach (var child in all.Children) CheckValues(child, where); break;
            case AnyCondition any: foreach (var child in any.Children) CheckValues(child, where); break;
            case NotCondition not: CheckValues(not.Child, where); break;
            case CompareCondition compare: CheckCompare(compare, where); break;
        }
    }

    private void CheckCompare(CompareCondition compare, string where)
    {
        var fact = compare.Fact;
        if (compare.Value.Kind != FactKind.String) return;
        var text = compare.Value.Text!;
        if (fact.StartsWith("quest.", StringComparison.Ordinal))
        {
            var dot = fact.LastIndexOf('.');
            var quest = fact[6..dot];
            if (fact.EndsWith(".status", StringComparison.Ordinal) && !QuestStatuses.TryParse(text, out _))
                throw _json.Refuse(DialogueCodes.Reference, where + ": '" + text + "' is not a quest status");
            if (fact.EndsWith(".stage", StringComparison.Ordinal) && text.Length > 0 && !_questStages[quest].Contains(text))
                throw _json.Refuse(DialogueCodes.Reference, where + ": '" + text + "' is not a stage of quest '" + quest + "'");
        }
        else if (fact.StartsWith("var.", StringComparison.Ordinal))
        {
            var variable = _variables[fact[4..]];
            if (variable.Allowed.Count > 0 && !variable.Allowed.Contains(text))
                throw _json.Refuse(DialogueCodes.Reference, where + ": '" + text + "' is not an allowed value of variable '" + variable.Id + "'");
        }
    }
}