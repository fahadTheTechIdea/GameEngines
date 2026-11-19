using System.Text.Json;
using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed partial class AiReader
{
    /// <summary>A node and everything below it. Ids are unique in a behaviour, the tree is bounded in depth and size, and each kind reads exactly the fields it has.</summary>
    private Node ReadNode(JsonElement element, string where, BehaviorContext context, int depth)
    {
        if (depth > BehaviorContext.MaxDepth) throw _json.Refuse(AiCodes.TooDeep, where + " is more than " + BehaviorContext.MaxDepth + " nodes deep");
        if (element.ValueKind != JsonValueKind.Object) throw _json.Refuse(AiCodes.Format, where + " must be an object");
        if (!element.TryGetProperty("kind", out var kindElement)) throw _json.Refuse(AiCodes.MissingField, where + " is missing 'kind'");
        if (kindElement.ValueKind != JsonValueKind.String) throw _json.Refuse(AiCodes.Format, where + ".kind must be a string");
        var kind = kindElement.GetString()!;
        if (!element.TryGetProperty("id", out _)) throw _json.Refuse(AiCodes.MissingField, where + " is missing 'id'");
        var id = _json.Id(element, "id", where);
        if (!context.NodeIds.Add(id)) throw _json.Refuse(AiCodes.Duplicate, "node id '" + id + "' is used twice in behaviour '" + context.Behavior + "'");
        if (context.NodeCount > BehaviorContext.MaxNodes) throw _json.Refuse(AiCodes.TooMany, "behaviour '" + context.Behavior + "' has more than " + BehaviorContext.MaxNodes + " nodes");
        where = "node '" + id + "' of behaviour '" + context.Behavior + "'";
        switch (kind)
        {
            case "sequence": return ReadComposite(element, where, context, depth, id, sequence: true);
            case "selector": return ReadComposite(element, where, context, depth, id, sequence: false);
            case "condition":
                _json.Object(element, where, ["id", "kind", "when"]);
                return new ConditionNode(id, ReadCond(element.GetProperty("when"), where + ".when", context.Scope));
            case "let": return ReadLet(element, where, context, id);
            case "act": return ReadAct(element, where, context, id);
            case "invert":
                _json.Object(element, where, ["id", "kind", "child"]);
                return new InvertNode(id, ReadNode(element.GetProperty("child"), where + ".child", context, depth + 1));
            case "succeed":
                _json.Object(element, where, ["id", "kind", "child"]);
                return new SucceedNode(id, ReadNode(element.GetProperty("child"), where + ".child", context, depth + 1));
            case "cooldown":
                _json.Object(element, where, ["id", "kind", "ticks", "child"]);
                var ticks = _json.Long(element, "ticks", where, 1, ValueDef.Limit);
                var cooled = ReadNode(element.GetProperty("child"), where + ".child", context, depth + 1);
                return new CooldownNode(id, context.Claim(persistent: true, AiRuntime.TickLimit + ValueDef.Limit + 1), ticks, cooled);
            case "utility": return ReadUtility(element, where, context, depth, id);
            case "random": return ReadRandom(element, where, context, depth, id);
            case "forEach": return ReadForEach(element, where, context, depth, id);
            case "count": return ReadCount(element, where, context, id);
            case "select": return ReadSelect(element, where, context, id);
            case "adjust": return ReadAdjust(element, where, context, id);
        }
        throw _json.Refuse(AiCodes.Format, where + " has the unknown kind '" + kind + "'");
    }

    private Node ReadComposite(JsonElement element, string where, BehaviorContext context, int depth, string id, bool sequence)
    {
        _json.Object(element, where, ["id", "kind", "resume", "children"]);
        var resume = _json.Bool(element, "resume", where);
        var children = _json.Array(element, "children", where, 1, 32).EnumerateArray().Select((c, i) => ReadNode(c, where + ".children[" + i + "]", context, depth + 1)).ToList();
        var slot = resume ? context.Claim(persistent: false, children.Count - 1) : -1;
        return sequence ? new SequenceNode(id, slot, children, resume) : new SelectorNode(id, slot, children, resume);
    }

    private LetNode ReadLet(JsonElement element, string where, BehaviorContext context, string id)
    {
        _json.Object(element, where, ["id", "kind", "key", "value"]);
        var key = OwnKey(_json.Text(element, "key", where, 128), where);
        var value = ReadExpr(element.GetProperty("value"), where + ".value", context.Scope);
        Fits(value, key.Value, where + ".value");
        return new LetNode(id, key, value);
    }

    /// <summary>A key the tree may write: one the agent owns. A key the host owns is what the host tells the agent, and the tree cannot change what it was told.</summary>
    private KeyDef OwnKey(string name, string where)
    {
        if (!_keyById.TryGetValue(name, out var key)) throw _json.Refuse(AiCodes.Reference, where + " names the unknown key '" + name + "'");
        return key.HostWrites ? throw _json.Refuse(AiCodes.WrongWriter, where + ": key '" + name + "' is written by the host, not by the tree") : key;
    }

    private ActNode ReadAct(JsonElement element, string where, BehaviorContext context, string id)
    {
        _json.Object(element, where, ["id", "kind", "intent", "args"], "until", "failWhen", "timeout");
        var intentId = _json.Text(element, "intent", where, 128);
        if (!_intents.TryGetValue(intentId, out var intent)) throw _json.Refuse(AiCodes.Reference, where + " names the unknown intent '" + intentId + "'");
        var argsElement = element.GetProperty("args");
        if (argsElement.ValueKind != JsonValueKind.Object) throw _json.Refuse(AiCodes.Format, where + ".args must be an object");
        foreach (var given in argsElement.EnumerateObject())
            if (!intent.TryArg(given.Name, out _)) throw _json.Refuse(AiCodes.UnknownField, where + ".args has '" + given.Name + "', which intent '" + intentId + "' does not take");
        var args = new Expr[intent.Args.Count];
        foreach (var arg in intent.Args)
        {
            if (!argsElement.TryGetProperty(arg.Id, out var written)) throw _json.Refuse(AiCodes.MissingField, where + ".args is missing '" + arg.Id + "' of intent '" + intentId + "'");
            args[arg.Index] = ReadExpr(written, where + ".args." + arg.Id, context.Scope);
            Fits(args[arg.Index], arg, where + ".args." + arg.Id);
        }
        Cond? until = null, failWhen = null;
        long timeout = 0;
        if (_json.Has(element, "until"))
        {
            until = ReadCond(element.GetProperty("until"), where + ".until", context.Scope);
            if (_json.Has(element, "failWhen")) failWhen = ReadCond(element.GetProperty("failWhen"), where + ".failWhen", context.Scope);
            if (_json.Has(element, "timeout")) timeout = _json.Long(element, "timeout", where, 1, ValueDef.Limit);
        }
        else if (_json.Has(element, "failWhen") || _json.Has(element, "timeout"))
            throw _json.Refuse(AiCodes.Format, where + " has failWhen or timeout but nothing to wait for (no until)");
        var slot = until is null ? -1 : context.Claim(persistent: false, AiRuntime.TickLimit + 1);
        return new ActNode(id, slot, intent, args, until, failWhen, timeout);
    }

    private UtilityNode ReadUtility(JsonElement element, string where, BehaviorContext context, int depth, string id)
    {
        _json.Object(element, where, ["id", "kind", "momentum", "options"]);
        var momentum = _json.Long(element, "momentum", where, 0, 1000);
        var options = new List<UtilityOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var option in _json.Array(element, "options", where, 1, UtilityNode.MaxOptions).EnumerateArray())
        {
            var optionWhere = where + ".options[" + n++ + "]";
            _json.Object(option, optionWhere, ["id", "weight", "considerations", "child"], "when");
            var optionId = _json.Id(option, "id", optionWhere);
            if (!seen.Add(optionId)) throw _json.Refuse(AiCodes.Duplicate, where + " has two options named '" + optionId + "'");
            optionWhere = where + " option '" + optionId + "'";
            var when = _json.Has(option, "when") ? ReadCond(option.GetProperty("when"), optionWhere + ".when", context.Scope) : null;
            var considerations = _json.Array(option, "considerations", optionWhere, 1, 16).EnumerateArray().Select((c, i) => ReadConsideration(c, optionWhere + ".considerations[" + i + "]", context.Scope)).ToList();
            var child = ReadNode(option.GetProperty("child"), optionWhere + ".child", context, depth + 1);
            options.Add(new UtilityOption(optionId, when, considerations, _json.Long(option, "weight", optionWhere, 1, 1000), child));
        }
        // Options are tried in id order, so a tie always goes to the lower id however the document lists them.
        return new UtilityNode(id, context.Claim(persistent: true, options.Count), momentum, options.OrderBy(o => o.Id, StringComparer.Ordinal).ToList());
    }

    private Consideration ReadConsideration(JsonElement element, string where, ReadScope scope)
    {
        _json.Object(element, where, ["input", "min", "max", "curve"]);
        var input = ReadExpr(element.GetProperty("input"), where + ".input", scope);
        if (input.Kind != FactKind.Integer) throw _json.Refuse(AiCodes.ExprType, where + ".input must be a whole number");
        var min = _json.Long(element, "min", where, -ValueDef.Limit, ValueDef.Limit);
        var max = _json.Long(element, "max", where, -ValueDef.Limit, ValueDef.Limit);
        if (min >= max) throw _json.Refuse(AiCodes.Range, where + " needs min below max");
        var curveElement = element.GetProperty("curve");
        var curveWhere = where + ".curve";
        _json.Object(curveElement, curveWhere, ["kind"], "invert", "at");
        var name = _json.Text(curveElement, "kind", curveWhere, 16);
        var curve = name switch
        {
            "linear" => CurveKind.Linear,
            "quadratic" => CurveKind.Quadratic,
            "cubic" => CurveKind.Cubic,
            "smooth" => CurveKind.Smooth,
            "step" => CurveKind.Step,
            _ => throw _json.Refuse(AiCodes.Format, curveWhere + ".kind '" + name + "' must be linear, quadratic, cubic, smooth or step"),
        };
        if ((curve == CurveKind.Step) != _json.Has(curveElement, "at")) throw _json.Refuse(AiCodes.Format, curveWhere + (curve == CurveKind.Step ? " needs 'at'" : " takes 'at' only for a step"));
        var at = curve == CurveKind.Step ? _json.Long(curveElement, "at", curveWhere, 1, 1000) : 0;
        var invert = _json.Has(curveElement, "invert") && _json.Bool(curveElement, "invert", curveWhere);
        return new Consideration(input, min, max, curve, invert, at);
    }

    private RandomNode ReadRandom(JsonElement element, string where, BehaviorContext context, int depth, string id)
    {
        _json.Object(element, where, ["id", "kind", "options"]);
        var options = new List<RandomOption>();
        var n = 0;
        foreach (var option in _json.Array(element, "options", where, 1, 16).EnumerateArray())
        {
            var optionWhere = where + ".options[" + n++ + "]";
            _json.Object(option, optionWhere, ["weight", "child"]);
            options.Add(new RandomOption(_json.Long(option, "weight", optionWhere, 1, 1000), ReadNode(option.GetProperty("child"), optionWhere + ".child", context, depth + 1)));
        }
        return new RandomNode(id, context.Claim(persistent: false, options.Count), options);
    }

    // ---- queries ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The <c>of</c> and <c>where</c> of a query, read in the scope the query itself is in; <c>where</c> sees the candidate as the item.</summary>
    private (Expr? Of, Cond? Where) ReadFilter(JsonElement element, string where, PerceptKindDef kind, BehaviorContext context)
    {
        Expr? of = null;
        if (_json.Has(element, "of"))
        {
            if (!kind.Owned) throw _json.Refuse(AiCodes.Reference, where + ": kind '" + kind.Id + "' has no owners");
            of = ReadExpr(element.GetProperty("of"), where + ".of", context.Scope);
            if (of.Kind != FactKind.String) throw _json.Refuse(AiCodes.ExprType, where + ".of must be a string");
        }
        Cond? filter = null;
        if (_json.Has(element, "where"))
        {
            context.Scope.Push(kind, hasIndex: false, limit: 1);
            filter = ReadCond(element.GetProperty("where"), where + ".where", context.Scope);
            context.Scope.Pop();
        }
        return (of, filter);
    }

    private ForEachNode ReadForEach(JsonElement element, string where, BehaviorContext context, int depth, string id)
    {
        _json.Object(element, where, ["id", "kind", "from", "limit", "child"], "of", "where");
        var kind = Kind(_json.Text(element, "from", where, 128), where + ".from");
        var limit = _json.Int(element, "limit", where, 1, kind.Capacity);
        var (of, filter) = ReadFilter(element, where, kind, context);
        context.Scope.Push(kind, hasIndex: true, limit);
        var child = ReadNode(element.GetProperty("child"), where + ".child", context, depth + 1);
        context.Scope.Pop();
        return child.MayRun ? throw _json.Refuse(AiCodes.CannotWait, where + ": the body of a loop can never wait") : new ForEachNode(id, kind, limit, child, of, filter);
    }

    private CountNode ReadCount(JsonElement element, string where, BehaviorContext context, string id)
    {
        _json.Object(element, where, ["id", "kind", "from", "into"], "of", "where");
        var kind = Kind(_json.Text(element, "from", where, 128), where + ".from");
        var into = OwnKey(_json.Text(element, "into", where, 128), where + ".into");
        if (into.Value.Kind != FactKind.Integer || into.Value.Minimum > 0 || into.Value.Maximum < kind.Capacity)
            throw _json.Refuse(AiCodes.Range, where + ": key '" + into.Id + "' must be a whole number holding at least 0 to " + kind.Capacity);
        var (of, filter) = ReadFilter(element, where, kind, context);
        return new CountNode(id, kind, into, of, filter);
    }

    private SelectNode ReadSelect(JsonElement element, string where, BehaviorContext context, string id)
    {
        _json.Object(element, where, ["id", "kind", "from", "pick", "into"], "of", "where");
        var kind = Kind(_json.Text(element, "from", where, 128), where + ".from");
        var into = OwnKey(_json.Text(element, "into", where, 128), where + ".into");
        if (into.Value.Kind != FactKind.String) throw _json.Refuse(AiCodes.ExprType, where + ": key '" + into.Id + "' must be a string to hold an id");
        var (of, filter) = ReadFilter(element, where, kind, context);
        var pick = element.GetProperty("pick");
        if (pick.ValueKind != JsonValueKind.Object || pick.EnumerateObject().Count() != 1) throw _json.Refuse(AiCodes.Format, where + ".pick must be an object with exactly one of min, max or nth");
        var how = pick.EnumerateObject().First();
        switch (how.Name)
        {
            case "min":
            case "max":
                var name = _json.Text(pick, how.Name, where + ".pick", 128);
                if (!kind.TryAttr(name, out var attr) || attr.Kind != FactKind.Integer) throw _json.Refuse(AiCodes.Reference, where + ".pick: '" + name + "' is not a whole-number attribute of '" + kind.Id + "'");
                return new SelectNode(id, kind, into, how.Name == "min" ? PickKind.Min : PickKind.Max, attr, null, of, filter);
            case "nth":
                var place = ReadExpr(how.Value, where + ".pick.nth", context.Scope);
                if (place.Kind != FactKind.Integer) throw _json.Refuse(AiCodes.ExprType, where + ".pick.nth must be a whole number");
                return new SelectNode(id, kind, into, PickKind.Nth, null, place, of, filter);
        }
        throw _json.Refuse(AiCodes.UnknownField, where + ".pick has '" + how.Name + "'; use min, max or nth");
    }

    private AdjustNode ReadAdjust(JsonElement element, string where, BehaviorContext context, string id)
    {
        _json.Object(element, where, ["id", "kind", "from", "target", "attr", "by"]);
        var kind = Kind(_json.Text(element, "from", where, 128), where + ".from");
        var target = ReadExpr(element.GetProperty("target"), where + ".target", context.Scope);
        if (target.Kind != FactKind.String) throw _json.Refuse(AiCodes.ExprType, where + ".target must be a string");
        var name = _json.Text(element, "attr", where, 128);
        if (!kind.TryAttr(name, out var attr) || attr.Kind != FactKind.Integer) throw _json.Refuse(AiCodes.Reference, where + ": '" + name + "' is not a whole-number attribute of '" + kind.Id + "'");
        var by = ReadExpr(element.GetProperty("by"), where + ".by", context.Scope);
        return by.Kind == FactKind.Integer ? new AdjustNode(id, kind, target, attr, by) : throw _json.Refuse(AiCodes.ExprType, where + ".by must be a whole number");
    }
}