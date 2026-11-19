namespace AIEngine;

public sealed class ConditionNode : Node
{
    private readonly Cond _when;

    internal ConditionNode(string id, Cond when) : base(id, -1, 1, false) { _when = when; }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var holds = _when.Test(c);
        c.Trace?.Note(holds ? "holds" : "does not hold");
        return holds ? Status.Success : Status.Failure;
    }
}