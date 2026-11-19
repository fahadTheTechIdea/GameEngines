using System.Text.Json;
using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed partial class AiReader
{
    private const long Bound = 4_000_000_000_000_000_000;
    private const int ConcatLimit = 4096;

    /// <summary>
    /// An expression: a whole number, true or false, a string, or an object with one of <c>key</c>, <c>item</c>, <c>known</c> or <c>op</c>. Its kind, its range and
    /// its longest length are worked out here, so what it is assigned to can be checked and arithmetic proven not to overflow.
    /// </summary>
    private Expr ReadExpr(JsonElement element, string where, ReadScope scope)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number)) throw _json.Refuse(AiCodes.Format, where + " must be a whole number");
                return Math.Abs(number) <= ValueDef.Limit ? new ConstExpr(FactKind.Integer, Val.Of(number)) : throw _json.Refuse(AiCodes.Range, where + " " + number + " is outside the allowed numbers");
            case JsonValueKind.True: return new ConstExpr(FactKind.Boolean, Val.Of(true));
            case JsonValueKind.False: return new ConstExpr(FactKind.Boolean, Val.Of(false));
            case JsonValueKind.String:
                var text = element.GetString()!;
                return text.Length <= ValueDef.TextLimit ? new ConstExpr(FactKind.String, Val.Of(text)) : throw _json.Refuse(AiCodes.Range, where + " is longer than " + ValueDef.TextLimit);
            case JsonValueKind.Object: break;
            default: throw _json.Refuse(AiCodes.Format, where + " must be a value or an expression object");
        }
        var names = element.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count == 1)
        {
            switch (names[0])
            {
                case "key": return ReadKeyExpr(element, where);
                case "item": return ReadItemExpr(element, where, scope);
                case "known": return ReadKnownExpr(element.GetProperty("known"), where + ".known", scope);
            }
        }
        if (names.Contains("op")) return ReadOpExpr(element, where, scope);
        throw _json.Refuse(AiCodes.UnknownField, where + " is not an expression: use a value, or an object with key, item, known or op");
    }

    private KeyExpr ReadKeyExpr(JsonElement element, string where)
    {
        var id = _json.Text(element, "key", where, 128);
        return _keyById.TryGetValue(id, out var key) ? new KeyExpr(key) : throw _json.Refuse(AiCodes.Reference, where + " names the unknown key '" + id + "'");
    }

    private Expr ReadItemExpr(JsonElement element, string where, ReadScope scope)
    {
        var field = _json.Text(element, "item", where, 128);
        if (scope.Empty) throw _json.Refuse(AiCodes.Reference, where + " reads the item but no loop or query is open here");
        var frame = scope.Innermost;
        switch (field)
        {
            case "id": return ItemExpr.Identity();
            case "owner": return ItemExpr.Owner();
            case "index": return frame.HasIndex ? ItemExpr.Place(frame.Limit) : throw _json.Refuse(AiCodes.Reference, where + ": the item has no place in a query condition");
        }
        return frame.Kind.TryAttr(field, out var attr) ? ItemExpr.Attribute(attr) : throw _json.Refuse(AiCodes.Reference, where + ": kind '" + frame.Kind.Id + "' has no attribute '" + field + "'");
    }

    private KnownExpr ReadKnownExpr(JsonElement element, string where, ReadScope scope)
    {
        _json.Object(element, where, ["from", "id", "attr"]);
        var kind = Kind(_json.Text(element, "from", where, 128), where);
        var target = ReadExpr(element.GetProperty("id"), where + ".id", scope);
        if (target.Kind != FactKind.String) throw _json.Refuse(AiCodes.ExprType, where + ".id must be a string");
        var name = _json.Text(element, "attr", where, 128);
        return kind.TryAttr(name, out var attr) ? new KnownExpr(kind, target, attr) : throw _json.Refuse(AiCodes.Reference, where + ": kind '" + kind.Id + "' has no attribute '" + name + "'");
    }

    private PerceptKindDef Kind(string id, string where) =>
        _kindById.TryGetValue(id, out var kind) ? kind : throw _json.Refuse(AiCodes.Reference, where + " names the unknown percept kind '" + id + "'");

    private OpExpr ReadOpExpr(JsonElement element, string where, ReadScope scope)
    {
        var name = _json.Text(element, "op", where, 16);
        var op = name switch
        {
            "tick" => OpKind.Tick,
            "add" => OpKind.Add,
            "sub" => OpKind.Sub,
            "mul" => OpKind.Mul,
            "div" => OpKind.Div,
            "mod" => OpKind.Mod,
            "min" => OpKind.Min,
            "max" => OpKind.Max,
            "concat" => OpKind.Concat,
            _ => throw _json.Refuse(AiCodes.Format, where + ": unknown operation '" + name + "'"),
        };
        if (op == OpKind.Tick)
        {
            _json.Object(element, where, ["op"]);
            return new OpExpr(op, Array.Empty<Expr>(), FactKind.Integer, new Interval(0, AiRuntime.TickLimit), 0);
        }
        _json.Object(element, where, ["op", "args"]);
        var list = _json.Array(element, "args", where, op == OpKind.Concat ? 2 : 2, op == OpKind.Concat ? 8 : 2);
        var args = list.EnumerateArray().Select((a, i) => ReadExpr(a, where + ".args[" + i + "]", scope)).ToList();
        if (op == OpKind.Concat)
        {
            if (args.Any(a => a.Kind != FactKind.String)) throw _json.Refuse(AiCodes.ExprType, where + ": concat joins strings only");
            var length = args.Sum(a => a.MaxLength);
            return length <= ConcatLimit ? new OpExpr(op, args, FactKind.String, default, length) : throw _json.Refuse(AiCodes.Range, where + ": the joined text could be longer than " + ConcatLimit);
        }
        if (args.Any(a => a.Kind != FactKind.Integer)) throw _json.Refuse(AiCodes.ExprType, where + ": " + name + " works on whole numbers only");
        return new OpExpr(op, args, FactKind.Integer, Combine(op, args[0].Range, args[1].Range, where), 0);
    }

    /// <summary>The range of a result from the ranges of the operands, or a refusal when it could leave the safe numbers or divide by something that could be zero.</summary>
    private Interval Combine(OpKind op, Interval a, Interval b, string where)
    {
        Int128 low, high;
        switch (op)
        {
            case OpKind.Add: low = (Int128)a.Low + b.Low; high = (Int128)a.High + b.High; break;
            case OpKind.Sub: low = (Int128)a.Low - b.High; high = (Int128)a.High - b.Low; break;
            case OpKind.Mul:
                var products = new[] { (Int128)a.Low * b.Low, (Int128)a.Low * b.High, (Int128)a.High * b.Low, (Int128)a.High * b.High };
                low = products.Min();
                high = products.Max();
                break;
            case OpKind.Min: low = Math.Min(a.Low, b.Low); high = Math.Min(a.High, b.High); break;
            case OpKind.Max: low = Math.Max(a.Low, b.Low); high = Math.Max(a.High, b.High); break;
            case OpKind.Div:
            case OpKind.Mod:
                if (b.Low < 1) throw _json.Refuse(AiCodes.Divisor, where + ": the divisor could be below 1 (its range is " + b.Low + " to " + b.High + "); wrap it in max with a positive number");
                if (op == OpKind.Div)
                {
                    var quotients = new[] { (Int128)(a.Low / b.Low), a.Low / b.High, a.High / b.Low, a.High / b.High };
                    low = quotients.Min();
                    high = quotients.Max();
                }
                else
                {
                    low = a.Low >= 0 ? 0 : -(Int128)(b.High - 1);
                    high = a.High < 0 ? 0 : Math.Min(a.High, b.High - 1);
                }
                break;
            default: throw _json.Refuse(AiCodes.Format, where + ": not an arithmetic operation");
        }
        return low >= -Bound && high <= Bound ? new Interval((long)low, (long)high) : throw _json.Refuse(AiCodes.Overflow, where + ": the result could be as large as " + high + " or as small as " + low);
    }

    /// <summary>What an expression is assigned to must be of the same kind and, being proven, fit: a number inside the range, a string no longer than allowed.</summary>
    private void Fits(Expr value, ValueDef target, string where)
    {
        if (value.Kind != target.Kind) throw _json.Refuse(AiCodes.ExprType, where + " is a " + value.Kind + " but '" + target.Id + "' is a " + target.Kind);
        if (target.Kind == FactKind.Integer && !value.Range.Within(target.Minimum, target.Maximum))
            throw _json.Refuse(AiCodes.Range, where + " could be " + value.Range.Low + " to " + value.Range.High + " but '" + target.Id + "' holds " + target.Minimum + " to " + target.Maximum);
        if (target.Kind == FactKind.String && value.MaxLength > ValueDef.TextLimit)
            throw _json.Refuse(AiCodes.Range, where + " could be longer than " + ValueDef.TextLimit);
    }
}