using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed class KeyExpr(KeyDef key) : Expr(key.Value.Kind, key.Value.Kind == FactKind.Integer ? new Interval(key.Value.Minimum, key.Value.Maximum) : key.Value.Kind == FactKind.Boolean ? Interval.Flag : default, ValueDef.TextLimit)
{
    internal override Val Eval(ThinkContext c) => c.Agent.Keys[key.Index];
}