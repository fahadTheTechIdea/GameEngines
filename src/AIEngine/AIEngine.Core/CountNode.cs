namespace AIEngine;

/// <summary>Counts the percepts a query matches into one of the agent own number keys. Always succeeds.</summary>
public sealed class CountNode : QueryNode
{
    private readonly KeyDef _into;

    internal CountNode(string id, PerceptKindDef from, KeyDef into, Expr? of, Cond? where) : base(id, from, 1 + from.ScanBound(of is not null))
    {
        _into = into;
        Of = of;
        Where = where;
    }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var found = c.Rent();
        Collect(c, found);
        c.Agent.Keys[_into.Index] = Val.Of(found.Count);
        c.Trace?.Note(_into.Id + " = " + found.Count);
        c.Return(found);
        return Status.Success;
    }
}