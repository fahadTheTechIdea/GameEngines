using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed class ConstExpr(FactKind kind, Val value) : Expr(kind, kind == FactKind.Integer ? Interval.Exactly(value.Number) : kind == FactKind.Boolean ? Interval.Exactly(value.Number) : default, value.Text?.Length ?? 0)
{
    internal override Val Eval(ThinkContext c) => value;
}