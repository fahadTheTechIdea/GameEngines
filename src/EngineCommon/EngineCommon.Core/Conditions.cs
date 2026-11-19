using System.Collections.Immutable;
using System.Text.Json;
using ScenarioEngine.Contracts;

namespace EngineCommon;

/// <summary>
/// The one condition grammar of the game stack is ScenarioEngine own (all, any, not, compare on a fact, depth and size limits). This reads it
/// strictly from a document for an engine that has facts of its own, checks every fact against that engine declarations, and says which facts
/// a condition needs, so a caller can gather exactly those and nothing else.
/// </summary>
public sealed class Conditions(StrictJson json)
{
    /// <summary>The condition at <paramref name="element"/>, or a refusal with the engine own format and reference codes.</summary>
    public Condition Read(JsonElement element, string where, IReadOnlyDictionary<string, FactDefinition> facts)
    {
        CheckShape(element, where, 1);
        Condition condition;
        try { condition = ConditionLogic.Parse(element); }
        catch (ScenarioContractException exception) { throw json.Refuse(json.Codes.Range, where + ": " + exception.Message); }
        try { ConditionLogic.Validate(condition, facts); }
        catch (ScenarioContractException exception) { throw json.Refuse(exception.Code == "CONDITION_FACT" ? json.Codes.Reference : json.Codes.Format, where + ": " + exception.Message); }
        return condition;
    }

    /// <summary>The grammar allows exactly one of all, any, not, or the three fields fact, op and value; anything else is refused instead of being ignored.</summary>
    private void CheckShape(JsonElement element, string where, int depth)
    {
        if (depth > 8) throw json.Refuse(json.Codes.Range, where + " nests conditions too deep");
        if (element.ValueKind != JsonValueKind.Object) throw json.Refuse(json.Codes.Format, where + " must be a condition object");
        var names = element.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count == 1 && names[0] is "all" or "any")
        {
            var list = element.GetProperty(names[0]);
            if (list.ValueKind != JsonValueKind.Array) throw json.Refuse(json.Codes.Format, where + "." + names[0] + " must be an array");
            foreach (var child in list.EnumerateArray()) CheckShape(child, where + "." + names[0], depth + 1);
        }
        else if (names.Count == 1 && names[0] == "not") CheckShape(element.GetProperty("not"), where + ".not", depth + 1);
        else if (names.Count == 3 && names.Contains("fact") && names.Contains("op") && names.Contains("value")) { }
        else throw json.Refuse(json.Codes.UnknownField, where + " is not a condition: use one of all, any, not, or fact with op and value");
    }

    /// <summary>The facts a condition reads, each once, in the order first met.</summary>
    public static IReadOnlyList<string> FactsOf(Condition condition)
    {
        var found = new List<string>();
        void Visit(Condition node)
        {
            switch (node)
            {
                case AllCondition all: foreach (var child in all.Children) Visit(child); break;
                case AnyCondition any: foreach (var child in any.Children) Visit(child); break;
                case NotCondition not: Visit(not.Child); break;
                case CompareCondition compare: if (!found.Contains(compare.Fact)) found.Add(compare.Fact); break;
            }
        }
        Visit(condition);
        return found;
    }

    public static bool Evaluate(Condition condition, IReadOnlyDictionary<string, FactValue> facts) => ConditionLogic.Evaluate(condition, facts);
}