using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed class ItemExpr : Expr
{
    private readonly ItemField _field;
    private readonly int _index;

    private ItemExpr(ItemField field, int index, FactKind kind, Interval range, int maxLength) : base(kind, range, maxLength)
    {
        _field = field;
        _index = index;
    }

    public static ItemExpr Attribute(ValueDef attr) => new(ItemField.Attr, attr.Index, attr.Kind,
        attr.Kind == FactKind.Integer ? new Interval(attr.Minimum, attr.Maximum) : attr.Kind == FactKind.Boolean ? Interval.Flag : default, ValueDef.TextLimit);

    public static ItemExpr Identity() => new(ItemField.Id, 0, FactKind.String, default, 128);
    public static ItemExpr Owner() => new(ItemField.Owner, 0, FactKind.String, default, 128);
    public static ItemExpr Place(int limit) => new(ItemField.Index, 0, FactKind.Integer, new Interval(0, limit - 1), 0);

    internal override Val Eval(ThinkContext c)
    {
        var frame = c.Scope[^1];
        switch (_field)
        {
            case ItemField.Attr: return frame.Item.Values[_index];
            case ItemField.Id: return Val.Of(frame.Item.Id);
            case ItemField.Owner: return Val.Of(frame.Item.Owner);
            case ItemField.Index: break;
        }
        return Val.Of(frame.Index);
    }
}