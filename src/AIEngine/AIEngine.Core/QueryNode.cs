namespace AIEngine;

/// <summary>
/// A node that looks through what the agent knows about one kind of thing, in id order: all of them, or only those owned by something (<c>of</c>), or only those
/// a condition holds for (<c>where</c>, read with the candidate as the item). Looking costs one work unit for each percept of that kind the agent holds.
/// </summary>
public abstract class QueryNode(string id, PerceptKindDef from, long bound) : Node(id, -1, bound, false)
{
    private readonly PerceptKindDef _from = from;

    internal Expr? Of { get; init; }
    internal Cond? Where { get; init; }

    internal void Collect(ThinkContext c, List<Percept> into)
    {
        var known = c.Agent.Knowledge[_from.Index];
        var owner = Of?.Eval(c).Text;
        if (owner is null)
        {
            c.Charge(known.Count);
            foreach (var percept in known.Values) Take(c, percept, into);
            return;
        }
        var owned = known.OwnedBy(owner);
        if (owned is null) return;
        c.Charge(owned.Count);
        foreach (var percept in owned) Take(c, percept, into);
    }

    private void Take(ThinkContext c, Percept percept, List<Percept> into)
    {
        if (Where is not null)
        {
            c.Scope.Add(new ScopeFrame(percept, 0));
            var holds = Where.Test(c);
            c.Scope.RemoveAt(c.Scope.Count - 1);
            if (!holds) return;
        }
        into.Add(percept);
    }
}