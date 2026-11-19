namespace AIEngine;

/// <summary>Reports success whatever its child did, unless the child is still running.</summary>
public sealed class SucceedNode(string id, Node child) : Node(id, -1, 1 + child.Bound, child.MayRun)
{
    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        return child.Run(c) == Status.Running ? Status.Running : Status.Success;
    }
}