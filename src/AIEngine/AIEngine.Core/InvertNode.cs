namespace AIEngine;

/// <summary>Swaps its child success and failure; Running stays Running.</summary>
public sealed class InvertNode(string id, Node child) : Node(id, -1, 1 + child.Bound, child.MayRun)
{
    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var status = child.Run(c);
        if (status == Status.Running) return Status.Running;
        return status == Status.Success ? Status.Failure : Status.Success;
    }
}