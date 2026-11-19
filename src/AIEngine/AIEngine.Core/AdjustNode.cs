using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// Changes what the agent believes about a thing it knows by id after it has acted on it (it asked for stock, so it now believes there is less), saturating at
/// the attribute range. Fails when the agent does not know the thing. The next time the host tells the agent about it, the host word wins.
/// </summary>
public sealed class AdjustNode(string id, PerceptKindDef from, Expr target, ValueDef attr, Expr by) : Node(id, -1, 1, false)
{
    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var name = target.Eval(c).Text!;
        if (!c.Agent.Knowledge[from.Index].TryGetValue(name, out var percept)) return Status.Failure;
        var next = Math.Clamp(percept.Values[attr.Index].Number + by.Eval(c).Number, attr.Minimum, attr.Maximum);
        percept.Values[attr.Index] = Val.Of(next);
        c.Trace?.Note("'" + name + "' " + attr.Id + " = " + next);
        return Status.Success;
    }
}