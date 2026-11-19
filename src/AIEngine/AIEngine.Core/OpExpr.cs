using System.Text;
using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed class OpExpr(OpKind op, IReadOnlyList<Expr> args, FactKind kind, Interval range, int maxLength) : Expr(kind, range, maxLength)
{
    internal override Val Eval(ThinkContext c)
    {
        switch (op)
        {
            case OpKind.Tick: return Val.Of(c.Tick);
            case OpKind.Add: return Val.Of(args[0].Eval(c).Number + args[1].Eval(c).Number);
            case OpKind.Sub: return Val.Of(args[0].Eval(c).Number - args[1].Eval(c).Number);
            case OpKind.Mul: return Val.Of(args[0].Eval(c).Number * args[1].Eval(c).Number);
            case OpKind.Div: return Val.Of(args[0].Eval(c).Number / args[1].Eval(c).Number);
            case OpKind.Mod: return Val.Of(args[0].Eval(c).Number % args[1].Eval(c).Number);
            case OpKind.Min: return Val.Of(Math.Min(args[0].Eval(c).Number, args[1].Eval(c).Number));
            case OpKind.Max: return Val.Of(Math.Max(args[0].Eval(c).Number, args[1].Eval(c).Number));
            case OpKind.Concat: break;
        }
        var text = new StringBuilder();
        foreach (var part in args) text.Append(part.Eval(c).Text);
        return Val.Of(text.ToString());
    }
}