namespace AIEngine;

/// <summary>
/// Runs its child once for each percept a query matches, in id order, at most <c>limit</c> of them, with that percept as the item (and its place as the item
/// index). The child can never wait. Always succeeds, whatever the child reported.
/// </summary>
public sealed class ForEachNode : QueryNode
{
    private readonly int _limit;
    private readonly Node _child;

    internal ForEachNode(string id, PerceptKindDef from, int limit, Node child, Expr? of, Cond? where) : base(id, from, 1 + from.ScanBound(of is not null) + limit * child.Bound)
    {
        _limit = limit;
        _child = child;
        Of = of;
        Where = where;
    }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var found = c.Rent();
        Collect(c, found);
        var count = Math.Min(found.Count, _limit);
        c.Trace?.Note(found.Count + " found, " + count + " run");
        for (var i = 0; i < count; i++)
        {
            c.Scope.Add(new ScopeFrame(found[i], i));
            _child.Run(c);
            c.Scope.RemoveAt(c.Scope.Count - 1);
        }
        c.Return(found);
        return Status.Success;
    }
}