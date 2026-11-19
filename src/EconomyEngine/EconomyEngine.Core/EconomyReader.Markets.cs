using System.Text.Json;

namespace EconomyEngine;

internal sealed partial class EconomyReader
{
    private static readonly IReadOnlyDictionary<string, BudgetKind> BudgetKinds = new Dictionary<string, BudgetKind>(StringComparer.Ordinal)
    {
        ["wage"] = BudgetKind.Wage, ["upkeep"] = BudgetKind.Upkeep,
    };

    private IReadOnlyList<MarketDef> ReadMarkets(JsonElement array)
    {
        var list = new List<MarketDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var lineCount = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "markets[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "currency", "treasury", "staff", "spread", "pairs", "policies", "budget", "lines"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "market");
            var currency = Ref(element, "currency", where, _currencyIndex, "currency");
            var pairs = ReadPairs(_json.Array(element, "pairs", where, 0, MaxGoods), currency, where);
            var policies = ReadMarketPolicies(_json.Array(element, "policies", where, 0, 32), where);
            var budget = ReadBudget(_json.Array(element, "budget", where, 0, 32), where);
            var lines = ReadLines(_json.Array(element, "lines", where, 0, MaxRecipes), list.Count, pairs, where, ref lineCount);
            list.Add(new MarketDef(list.Count, id, _json.Id(element, "key", where), currency, _json.Long(element, "treasury", where, 0, EconomyFacts.MaxMoney), _json.Long(element, "staff", where, 0, 1_000_000),
                _json.Int(element, "spread", where, 0, 500), pairs, policies, budget, lines));
        }
        _marketIndex = Index(list, m => m.Id);
        return list;
    }

    private IReadOnlyList<PairDef> ReadPairs(JsonElement array, int currency, string owner)
    {
        var list = new List<PairDef>();
        var seen = new HashSet<int>();
        foreach (var element in array.EnumerateArray())
        {
            var where = owner + ".pairs[" + list.Count + "]";
            _json.Object(element, where, ["good", "production", "consumption", "stock", "cover", "capacity"]);
            var good = Ref(element, "good", where, _goodIndex, "good");
            if (!seen.Add(good)) throw _json.Refuse(EconomyCodes.Duplicate, where + " trades good '" + _goods[good].Id + "' twice in one market");
            var capacity = _json.Long(element, "capacity", where, 1, MaxCapacity);
            var stock = _json.Long(element, "stock", where, 0, MaxCapacity);
            if (stock > capacity) throw _json.Refuse(EconomyCodes.Range, where + " starts with " + stock + " in stock but holds at most " + capacity);
            if (EconomyMath.Trunc(_goods[good].Base, EconomyMath.Ppm, _currencies[currency].Anchor) < 1)
                throw _json.Refuse(EconomyCodes.Price, where + ": good '" + _goods[good].Id + "' is worth less than a thousandth of a minor unit of currency '" + _currencies[currency].Id + "'");
            list.Add(new PairDef(list.Count, good, _json.Long(element, "production", where, 0, MaxQuantity), _json.Long(element, "consumption", where, 0, MaxQuantity), stock, _json.Int(element, "cover", where, 0, 1000), capacity));
        }
        return list;
    }

    private IReadOnlyList<int> ReadMarketPolicies(JsonElement array, string owner)
    {
        var list = new List<int>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String) throw _json.Refuse(EconomyCodes.Format, owner + ".policies must hold texts");
            var id = _json.CheckId(element.GetString()!, owner + ".policies");
            if (!_policyIndex.TryGetValue(id, out var index)) throw _json.Refuse(EconomyCodes.Reference, owner + ".policies '" + id + "' is not a declared policy");
            if (list.Contains(index)) throw _json.Refuse(EconomyCodes.Duplicate, owner + ".policies repeats '" + id + "'");
            list.Add(index);
        }
        return list;
    }

    private IReadOnlyList<BudgetDef> ReadBudget(JsonElement array, string owner)
    {
        var list = new List<BudgetDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = owner + ".budget[" + list.Count + "]";
            _json.Object(element, where, ["id", "kind", "schedule", "amount", "perHead", "indexed"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "budget line");
            var amount = _json.Long(element, "amount", where, 0, EconomyFacts.MaxMoney);
            var perHead = _json.Long(element, "perHead", where, 0, 1_000_000_000_000);
            if (amount == 0 && perHead == 0) throw _json.Refuse(EconomyCodes.Budget, where + " pays nothing: it has no amount and no amount per head");
            list.Add(new BudgetDef(list.Count, id, Choice(element, "kind", where, BudgetKinds), Ref(element, "schedule", where, _scheduleIndex, "schedule"), amount, perHead, _json.Bool(element, "indexed", where)));
        }
        return list;
    }

    private IReadOnlyList<RouteDef> ReadRoutes(JsonElement array)
    {
        var list = new List<RouteDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "routes[" + list.Count + "]";
            _json.Object(element, where, ["id", "from", "to", "delay", "capacity", "costPerWeight", "margin", "share"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "route");
            var from = Ref(element, "from", where, _marketIndex, "market");
            var to = Ref(element, "to", where, _marketIndex, "market");
            if (from == to) throw _json.Refuse(EconomyCodes.Route, where + " leaves and arrives at the same market");
            list.Add(new RouteDef(list.Count, id, from, to, _json.Int(element, "delay", where, 1, 64), _json.Long(element, "capacity", where, 0, MaxQuantity), _json.Long(element, "costPerWeight", where, 0, 1_000_000_000),
                _json.Int(element, "margin", where, 0, 10_000), _json.Int(element, "share", where, 1, 1000)));
        }
        _routeIndex = Index(list, r => r.Id);
        return list;
    }

    private static readonly IReadOnlyDictionary<string, EffectKind> EffectKinds = new Dictionary<string, EffectKind>(StringComparer.Ordinal)
    {
        ["supply"] = EffectKind.Supply, ["demand"] = EffectKind.Demand, ["price"] = EffectKind.Price, ["rate"] = EffectKind.Rate, ["growth"] = EffectKind.Growth, ["route"] = EffectKind.Route,
    };

    private IReadOnlyList<ShockDef> ReadShocks(JsonElement array)
    {
        var list = new List<ShockDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "shocks[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "duration", "effects"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "shock");
            var effects = new List<EffectDef>();
            foreach (var effect in _json.Array(element, "effects", where, 1, 16).EnumerateArray()) effects.Add(ReadEffect(effect, where + ".effects[" + effects.Count + "]"));
            list.Add(new ShockDef(list.Count, id, _json.Id(element, "key", where), _json.Int(element, "duration", where, 1, 100_000), effects));
        }
        return list;
    }

    private EffectDef ReadEffect(JsonElement element, string where)
    {
        _json.Object(element, where, ["kind", "good", "tag", "route", "amount"]);
        var kind = Choice(element, "kind", where, EffectKinds);
        var good = OptionalRef(element, "good", where, _goodIndex, "good");
        var tag = _json.Text(element, "tag", where, 128);
        if (tag.Length > 0) _json.CheckId(tag, where + ".tag");
        var route = OptionalRef(element, "route", where, _routeIndex, "route");
        if (good != -1 && tag.Length > 0) throw _json.Refuse(EconomyCodes.Shock, where + " names a good and a tag; use one");
        switch (kind)
        {
            case EffectKind.Supply:
            case EffectKind.Demand:
            case EffectKind.Price:
                if (route != -1) throw _json.Refuse(EconomyCodes.Shock, where + " is about goods and names a route");
                return new EffectDef(kind, good, tag, -1, _json.Long(element, "amount", where, -1000, 10_000));
            case EffectKind.Route:
                if (good != -1 || tag.Length > 0) throw _json.Refuse(EconomyCodes.Shock, where + " is about a route and names a good");
                return new EffectDef(kind, -1, "", route, _json.Long(element, "amount", where, -1000, 10_000));
            case EffectKind.Rate:
                if (good != -1 || tag.Length > 0 || route != -1) throw _json.Refuse(EconomyCodes.Shock, where + " is about a currency and names a good or a route");
                return new EffectDef(kind, -1, "", -1, _json.Long(element, "amount", where, -900, 10_000));
            case EffectKind.Growth:
                if (good != -1 || tag.Length > 0 || route != -1) throw _json.Refuse(EconomyCodes.Shock, where + " is about a currency and names a good or a route");
                return new EffectDef(kind, -1, "", -1, _json.Long(element, "amount", where, -500_000, 500_000));
        }
        throw new InvalidOperationException("unhandled effect kind " + kind);
    }

    private static readonly IReadOnlyDictionary<string, LoanKind> LoanKinds = new Dictionary<string, LoanKind>(StringComparer.Ordinal)
    {
        ["annuity"] = LoanKind.Annuity, ["bullet"] = LoanKind.Bullet,
    };

    private IReadOnlyList<LoanDef> ReadLoans(JsonElement array, LimitsDef limits)
    {
        if (array.GetArrayLength() > limits.MaxLoans) throw _json.Refuse(EconomyCodes.Range, "loans holds " + array.GetArrayLength() + " entries but limits.maxLoans is " + limits.MaxLoans);
        var list = new List<LoanDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "loans[" + list.Count + "]";
            _json.Object(element, where, ["id", "lender", "borrower", "principal", "ratePpm", "term", "kind", "schedule", "defaultAfter"]);
            var id = _json.Id(element, "id", where);
            Unique(ids, id, "loan");
            var lenderText = _json.Text(element, "lender", where, 128);
            var lender = -1;
            if (lenderText != "world")
            {
                _json.CheckId(lenderText, where + ".lender");
                if (!_marketIndex.TryGetValue(lenderText, out lender)) throw _json.Refuse(EconomyCodes.Reference, where + ".lender '" + lenderText + "' is neither 'world' nor a declared market");
            }
            var borrower = Ref(element, "borrower", where, _marketIndex, "market");
            if (lender == borrower) throw _json.Refuse(EconomyCodes.Loan, where + " lends a market money to itself");
            if (lender >= 0 && _markets[lender].Currency != _markets[borrower].Currency) throw _json.Refuse(EconomyCodes.Loan, where + " is between markets of different currencies");
            list.Add(new LoanDef(id, lender, borrower, _json.Long(element, "principal", where, 1, EconomyFacts.MaxMoney), _json.Long(element, "ratePpm", where, 0, EconomyMath.Ppm), _json.Int(element, "term", where, 1, 1200),
                Choice(element, "kind", where, LoanKinds), Ref(element, "schedule", where, _scheduleIndex, "schedule"), _json.Int(element, "defaultAfter", where, 1, 100)));
        }
        return list;
    }
}
