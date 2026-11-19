using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>An attribute of a percept the agent knows by id, or the value its kind declares for a thing it does not know.</summary>
internal sealed class KnownExpr(PerceptKindDef kind, Expr target, ValueDef attr) : Expr(attr.Kind,
    attr.Kind == FactKind.Integer ? new Interval(attr.Minimum, attr.Maximum) : attr.Kind == FactKind.Boolean ? Interval.Flag : default, ValueDef.TextLimit)
{
    internal override Val Eval(ThinkContext c)
    {
        var id = target.Eval(c).Text!;
        return c.Agent.Knowledge[kind.Index].TryGetValue(id, out var percept) ? percept.Values[attr.Index] : kind.Unknowns[attr.Index];
    }
}