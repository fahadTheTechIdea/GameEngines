namespace AIEngine;

/// <summary>After its child has finished (success or failure) the node fails, without running it, until <c>ticks</c> ticks have passed: the child can run again that many ticks after it ran.</summary>
public sealed class CooldownNode(string id, int slot, long ticks, Node child) : Node(id, slot, 1 + child.Bound, child.MayRun)
{
    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var slots = c.Agent.Slots;
        if (slots[Slot] != 0 && c.Tick < slots[Slot] - 1)
        {
            c.Trace?.Note("cooling down until tick " + (slots[Slot] - 1));
            return Status.Failure;
        }
        var status = child.Run(c);
        if (status != Status.Running) slots[Slot] = c.Tick + ticks + 1;
        return status;
    }
}