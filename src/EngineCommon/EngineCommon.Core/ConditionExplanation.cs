using System.Globalization;
using System.Text;
using ScenarioEngine.Contracts;

namespace EngineCommon;

/// <summary>
/// Says why a condition of the shared grammar held or did not: each comparison with the value the fact had, and each <c>all</c>, <c>any</c> and <c>not</c> with its result, in the order the
/// condition is written (so the same condition and facts always give the same text). It evaluates through the same <see cref="ConditionLogic"/> the engines use, never a second implementation.
/// </summary>
public static class ConditionExplanation
{
    /// <param name="facts">Every fact the condition reads; a missing one is the caller's mistake and is refused by the evaluator.</param>
    public static string Describe(Condition condition, IReadOnlyDictionary<string, FactValue> facts)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(facts);
        var text = new StringBuilder();
        Write(text, condition, facts);
        return text.ToString();
    }

    private static void Write(StringBuilder text, Condition condition, IReadOnlyDictionary<string, FactValue> facts)
    {
        var result = ConditionLogic.Evaluate(condition, facts);
        switch (condition)
        {
            case AllCondition all:
                text.Append("all(");
                Children(text, all.Children, facts);
                break;
            case AnyCondition any:
                text.Append("any(");
                Children(text, any.Children, facts);
                break;
            case NotCondition not:
                text.Append("not(");
                Write(text, not.Child, facts);
                break;
            case CompareCondition compare:
                text.Append(compare.Fact).Append(' ').Append(compare.Operation).Append(' ').Append(Show(compare.Value)).Append(" [").Append(Show(facts[compare.Fact])).Append(']');
                text.Append(result ? " = true" : " = false");
                return;
        }
        text.Append(") = ").Append(result ? "true" : "false");
    }

    private static void Children(StringBuilder text, IEnumerable<Condition> children, IReadOnlyDictionary<string, FactValue> facts)
    {
        var first = true;
        foreach (var child in children)
        {
            if (!first) text.Append(", ");
            first = false;
            Write(text, child, facts);
        }
    }

    private static string Show(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return "\"" + value.Text + "\"";
    }
}
