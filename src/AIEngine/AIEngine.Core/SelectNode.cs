namespace AIEngine;

/// <summary>
/// Chooses one percept a query matches and writes its id to one of the agent own text keys. Ties go to the lower id; <c>nth</c> counts from 0 in id order and
/// fails when there is no such place. On failure the key is set to the empty text and the node fails.
/// </summary>
public sealed class SelectNode : QueryNode
{
    private readonly KeyDef _into;
    private readonly PickKind _pick;
    private readonly ValueDef? _attr;
    private readonly Expr? _nth;

    internal SelectNode(string id, PerceptKindDef from, KeyDef into, PickKind pick, ValueDef? attr, Expr? nth, Expr? of, Cond? where) : base(id, from, 1 + from.ScanBound(of is not null))
    {
        _into = into;
        _pick = pick;
        _attr = attr;
        _nth = nth;
        Of = of;
        Where = where;
    }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        var found = c.Rent();
        Collect(c, found);
        Percept? chosen = null;
        switch (_pick)
        {
            case PickKind.Nth:
                var place = _nth!.Eval(c).Number;
                if (place >= 0 && place < found.Count) chosen = found[(int)place];
                break;
            case PickKind.Min:
                foreach (var percept in found)
                    if (chosen is null || percept.Values[_attr!.Index].Number < chosen.Values[_attr.Index].Number) chosen = percept;
                break;
            case PickKind.Max:
                foreach (var percept in found)
                    if (chosen is null || percept.Values[_attr!.Index].Number > chosen.Values[_attr.Index].Number) chosen = percept;
                break;
        }
        c.Agent.Keys[_into.Index] = Val.Of(chosen?.Id ?? "");
        if (c.Trace is not null) c.Trace.Note(_into.Id + " = '" + (chosen?.Id ?? "") + "' of " + found.Count + " found");
        c.Return(found);
        return chosen is null ? Status.Failure : Status.Success;
    }
}