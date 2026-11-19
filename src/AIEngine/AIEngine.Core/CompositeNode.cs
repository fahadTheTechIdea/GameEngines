namespace AIEngine;

/// <summary>
/// A node that runs its children in order until one reports <paramref name="stop"/>. With <paramref name="resume"/> it remembers which child was running
/// and starts there next think; without, it starts from the first child every think.
/// </summary>
public abstract class CompositeNode(string id, int slot, IReadOnlyList<Node> children, bool resume, Status stop, Status done)
    : Node(id, slot, 1 + children.Sum(c => c.Bound), children.Any(c => c.MayRun))
{
    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var start = resume ? (int)c.Agent.Slots[Slot] : 0;
        for (var i = start; i < children.Count; i++)
        {
            var status = children[i].Run(c);
            if (status == Status.Running)
            {
                if (resume) c.Agent.Slots[Slot] = i;
                if (Slot >= 0) c.Alive[Slot] = true;
                return Status.Running;
            }
            if (status == stop)
            {
                if (resume) c.Agent.Slots[Slot] = 0;
                return stop;
            }
        }
        if (resume) c.Agent.Slots[Slot] = 0;
        return done;
    }
}