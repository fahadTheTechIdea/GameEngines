using System.Text.Json;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

internal sealed partial class DialogueReader
{
    private static readonly string[] Operations = ["set", "add", "startQuest", "setStage", "finishQuest", "signal"];

    private IReadOnlyList<Effect> ReadEffects(JsonElement owner, string field, string where, int minimum)
    {
        var effects = new List<Effect>();
        var index = 0;
        foreach (var element in _json.Array(owner, field, where, minimum, 16).EnumerateArray())
            effects.Add(ReadEffect(element, where + "." + field + "[" + index++ + "]"));
        return effects;
    }

    private Effect ReadEffect(JsonElement element, string where)
    {
        if (element.ValueKind != JsonValueKind.Object) throw _json.Refuse(DialogueCodes.Format, where + " must be an object");
        var operations = element.EnumerateObject().Select(p => p.Name).Where(n => Array.IndexOf(Operations, n) >= 0).ToList();
        if (operations.Count != 1) throw _json.Refuse(DialogueCodes.Format, where + " must name exactly one of " + string.Join(", ", Operations));
        switch (operations[0])
        {
            case "set":
            {
                _json.Object(element, where, ["set", "value"]);
                var variable = Variable(element, "set", where);
                var value = Literal(element.GetProperty("value"), variable.Kind, where + ".value");
                CheckValue(value, variable.Kind, variable.Minimum, variable.Maximum, variable.Allowed, where + ".value");
                return new SetEffect(variable, value);
            }
            case "add":
            {
                _json.Object(element, where, ["add", "by"]);
                var variable = Variable(element, "add", where);
                if (variable.Kind != FactKind.Integer) throw _json.Refuse(DialogueCodes.ValueType, where + ": '" + variable.Id + "' is not a number variable");
                return new AddEffect(variable, _json.Long(element, "by", where, -1_000_000_000, 1_000_000_000));
            }
            case "startQuest":
                _json.Object(element, where, ["startQuest"]);
                return new StartQuestEffect(Quest(element, "startQuest", where));
            case "setStage":
            {
                _json.Object(element, where, ["setStage", "stage"]);
                var quest = Quest(element, "setStage", where);
                var stage = _json.Id(element, "stage", where);
                if (!quest.HasStage(stage)) throw _json.Refuse(DialogueCodes.Reference, where + ": '" + stage + "' is not a stage of quest '" + quest.Id + "'");
                _stageReferences.Add((quest.Id, stage));
                return new SetStageEffect(quest, stage);
            }
            case "finishQuest":
            {
                _json.Object(element, where, ["finishQuest", "status"]);
                var quest = Quest(element, "finishQuest", where);
                var name = _json.Text(element, "status", where, 16);
                if (name is not ("completed" or "failed" or "abandoned")) throw _json.Refuse(DialogueCodes.Format, where + ".status must be completed, failed or abandoned");
                QuestStatuses.TryParse(name, out var status);
                return new FinishQuestEffect(quest, status);
            }
            case "signal":
                _json.Object(element, where, ["signal"], "args");
                return new SignalEffect(_json.Id(element, "signal", where), _json.Has(element, "args") ? ReadSignalArgs(element, where) : []);
        }
        throw new InvalidOperationException("an operation name passed the list but has no reader");
    }

    /// <summary>The arguments of a signal: fixed values (<c>text</c>, <c>int</c>, <c>bool</c>) or a <c>var</c> of the document read when the signal is raised.</summary>
    private IReadOnlyList<SignalArg> ReadSignalArgs(JsonElement element, string where)
    {
        var args = new List<SignalArg>();
        var index = 0;
        foreach (var arg in _json.Array(element, "args", where, 1, 8).EnumerateArray())
        {
            var at = where + ".args[" + index++ + "]";
            if (arg.ValueKind != JsonValueKind.Object || arg.EnumerateObject().Count() != 1) throw _json.Refuse(DialogueCodes.Format, at + " must be an object with one of text, int, bool or var");
            var property = arg.EnumerateObject().First();
            if (property.Name == "text") args.Add(new SignalArg(Literal(property.Value, FactKind.String, at + ".text"), null));
            else if (property.Name == "int")
            {
                    if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out var number) || number < -1_000_000_000 || number > 1_000_000_000)
                        throw _json.Refuse(DialogueCodes.ValueType, at + ".int must be a whole number from -1000000000 to 1000000000");
                args.Add(new SignalArg(FactValue.From(number), null));
            }
            else if (property.Name == "bool") args.Add(new SignalArg(Literal(property.Value, FactKind.Boolean, at + ".bool"), null));
            else if (property.Name == "var") args.Add(new SignalArg(null, Variable(arg, "var", at).Id));
            else throw _json.Refuse(DialogueCodes.UnknownField, at + " has '" + property.Name + "'; an argument is text, int, bool or var");
        }
        return args;
    }

    private VariableDef Variable(JsonElement element, string field, string where)
    {
        var id = _json.Id(element, field, where);
        return _variables.TryGetValue(id, out var variable) ? variable : throw _json.Refuse(DialogueCodes.Reference, where + ": unknown variable '" + id + "'");
    }

    private QuestDef Quest(JsonElement element, string field, string where)
    {
        var id = _json.Id(element, field, where);
        if (!_questStages.ContainsKey(id)) throw _json.Refuse(DialogueCodes.Reference, where + ": unknown quest '" + id + "'");
        // Quests are read in order; an effect in a conversation always comes after every quest, but a stage rule never holds an effect.
        return _quests.TryGetValue(id, out var quest) ? quest : throw new InvalidOperationException("quest '" + id + "' is named before it is read");
    }
}