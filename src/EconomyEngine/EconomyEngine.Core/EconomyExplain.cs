using System.Globalization;
using System.Text;

namespace EconomyEngine;

/// <summary>One line of an explanation: what contributed, its value, and the detail a reader needs to check it.</summary>
public sealed record ExplainTerm(string Kind, string Id, long Value, string Detail);

/// <summary>An explanation of a number: its terms and the number itself.</summary>
public sealed record Explanation(string Subject, IReadOnlyList<ExplainTerm> Terms, long Value, string State)
{
    public string Text()
    {
        var text = new StringBuilder();
        text.Append(Subject).Append(" = ").Append(Value.ToString(CultureInfo.InvariantCulture)).Append(" (").Append(State).Append(')');
        foreach (var term in Terms)
            text.Append("\n  ").Append(term.Kind).Append(' ').Append(term.Id).Append(": ").Append(term.Value.ToString(CultureInfo.InvariantCulture)).Append(term.Detail.Length > 0 ? " (" + term.Detail + ")" : "");
        return text.ToString();
    }
}

/// <summary>Says why a price, a loan or a currency is what it is, from the same arithmetic the runtime uses: a pure reading that changes nothing.</summary>
public static class EconomyExplain
{
    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The price of a good in a market: its reference, where it sits, last period's supply and demand, the stock against what the market keeps, the pressure that gives and the change the next period would make.</summary>
    public static Explanation Price(EconomyRuntime runtime, string marketId, string goodId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var market = runtime.MarketIndex(marketId);
        var goodIndex = runtime.GoodIndex(goodId);
        var slot = runtime.PairIndex(market, goodIndex);
        var def = set.Markets[market];
        var pairDef = def.Pairs[slot];
        var good = set.Goods[goodIndex];
        var state = runtime.Markets[market].Pairs[slot];
        var currency = set.Currencies[def.Currency];
        var reference = runtime.ReferencePrice(def.Currency, good);
        var gap = pairDef.Cover * pairDef.Consumption - state.Stock;
        var imbalance = runtime.Imbalance(state.Demand + state.SpotDemand, state.Supply + state.SpotSupply, gap);
        var change = EconomyRuntime.PriceChange(good, state.LastChange, imbalance);
        var terms = new List<ExplainTerm>
        {
            new("reference", good.Id, reference, "base " + N(good.Base) + " at price level " + N(runtime.Currencies[def.Currency].Level) + " ppm, in " + currency.Id + " (anchor " + N(currency.Anchor) + " ppm)"),
            new("bounds", good.Id, runtime.Ratio(market, slot), "per mille of the reference; floor " + N(good.Floor) + ", ceiling " + N(good.Ceiling)),
            new("supply", good.Id, state.Supply, "last period; production " + N(pairDef.Production)),
            new("demand", good.Id, state.Demand, "last period; consumption " + N(pairDef.Consumption) + ", met " + N(state.Met)),
            new("stock", good.Id, state.Stock, "keeps " + N(pairDef.Cover) + " periods of use = " + N(pairDef.Cover * pairDef.Consumption) + ", capacity " + N(pairDef.Capacity)),
            new("imbalance", good.Id, imbalance, "per mille; the gap to the wanted stock is spread over " + N(set.Price.SettlePeriods) + " periods"),
            new("next-change", good.Id, change, "per mille; speed " + N(good.Speed) + ", inertia " + N(good.Inertia) + " (last change " + N(state.LastChange) + "), largest step " + N(good.MaxStep)),
        };
        return new Explanation("price of " + good.Id + " in " + def.Id, terms, state.Price, set.Bands[state.Band].Id);
    }

    /// <summary>
    /// How a route's capacity would be shared if the next period closed now: each good both ends trade, in the order merchants fill the capacity (most gain a unit first, then the higher gain ratio, then the
    /// lower good id), with what it asked for, what it got and why; the goods that are not worth loading follow, by good id. The same arithmetic as the loading rule, which reads nothing else.
    /// </summary>
    public static Explanation Route(EconomyRuntime runtime, string routeId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var index = runtime.RouteIndex(routeId);
        var route = set.Routes[index];
        var capacity = runtime.CapacityNow(index);
        var left = capacity;
        var terms = new List<ExplainTerm>();
        long shipped = 0;
        foreach (var line in runtime.PlanRoute(index))
        {
            var good = set.Goods[line.Good];
            var kind = line.Outcome is LineOutcome.Shipped or LineOutcome.Trimmed ? "load" : line.Rank > 0 ? "wait" : "skip";
            var detail = line.Rank > 0
                ? "rank " + N(line.Rank) + "; gain " + N(line.Gain) + " a unit (" + N(line.Ratio) + " per mille), destination worth " + N(line.Landed) + (line.Via >= 0 ? " landed through route " + set.Routes[line.Via].Id : " at its own price")
                    + "; surplus " + N(line.Surplus) + ", asks " + N(line.Wanted) + ", capacity left " + N(left) + ": " + Outcome(line.Outcome)
                : "gain " + N(line.Gain) + " a unit" + (line.Ratio > 0 ? " (" + N(line.Ratio) + " per mille, the route's margin is " + N(route.Margin) + ")" : "") + ", destination worth " + N(line.Landed) + (line.Via >= 0 ? " landed through route " + set.Routes[line.Via].Id : " at its own price")
                    + "; surplus " + N(line.Surplus) + ": " + Outcome(line.Outcome);
            terms.Add(new ExplainTerm(kind, good.Id, line.Shipped, detail));
            left -= line.Shipped;
            shipped += line.Shipped;
        }
        return new Explanation("route " + route.Id + " (" + set.Markets[route.From].Id + " -> " + set.Markets[route.To].Id + ")", terms, shipped, set.Routing.ToString().ToLowerInvariant() + " routing, capacity " + N(capacity));
    }

    private static string Outcome(LineOutcome outcome)
    {
        switch (outcome)
        {
            case LineOutcome.Shipped: return "loaded in full";
            case LineOutcome.Trimmed: return "loaded in part, the capacity ran out";
            case LineOutcome.NoCapacity: return "profitable but the capacity went to goods that pay more a unit";
            case LineOutcome.Blocked: return "profitable but the shipments on their way are at the document's limit";
            case LineOutcome.BelowMargin: return "under the route's margin";
            case LineOutcome.NoSurplus: return "no surplus over a period's use to send";
            case LineOutcome.NoGain: return "not worth carrying";
        }
        throw new InvalidOperationException("unhandled line outcome " + outcome);
    }

    /// <summary>The best a unit of a good can fetch from a market: where it sells and the routes it takes to get there, with the price it fetches there and what the way costs.</summary>
    public static Explanation Reach(EconomyRuntime runtime, string marketId, string goodId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var market = runtime.MarketIndex(marketId);
        var good = runtime.GoodIndex(goodId);
        var slot = runtime.PairIndex(market, good);
        var def = set.Markets[market];
        var reach = runtime.Reach(market, slot);
        var terms = new List<ExplainTerm> { new("here", set.Goods[good].Id, runtime.Markets[market].Pairs[slot].Price, "price in " + def.Id + ", in " + set.Currencies[def.Currency].Id + " thousandths") };
        var at = market;
        foreach (var index in runtime.WayOn(market, slot))
        {
            var route = set.Routes[index];
            terms.Add(new ExplainTerm("via", route.Id, route.Delay, set.Markets[route.From].Id + " -> " + set.Markets[route.To].Id + ", " + N(route.Delay) + " periods, freight " + N(set.Goods[good].Weight * route.CostPerWeight) + " in " + set.Currencies[set.Markets[route.From].Currency].Id));
            at = route.To;
        }
        terms.Add(new ExplainTerm("sells-at", set.Goods[good].Id, runtime.Markets[at].Pairs[set.PairOf[at][good]].Price, "price in " + set.Markets[at].Id));
        return new Explanation("best value of " + set.Goods[good].Id + " from " + def.Id, terms, reach.Value, reach.Route < 0 ? "sell here" : N(reach.Hops) + (reach.Hops == 1 ? " route" : " routes") + " on, " + set.Routing.ToString().ToLowerInvariant() + " routing");
    }

    /// <summary>
    /// Why a market's line for a recipe would start the runs it would if the next period closed now, from the same arithmetic the period uses (<see cref="EconomyRuntime.PlanLine"/>): the recipe and whether it may run,
    /// the line's capacity, for each input how many runs its stock allows at the line's turn (after the lines before it in the order have taken theirs), for each output how many runs the room in the warehouse
    /// allows, which of them sets the limit, what the line did last period and the batches still on their way.
    /// </summary>
    public static Explanation Production(EconomyRuntime runtime, string marketId, string recipeId, IReadOnlyDictionary<string, ScenarioEngine.Contracts.FactValue>? hostFacts = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var set = runtime.Set;
        var market = runtime.MarketIndex(marketId);
        ArgumentNullException.ThrowIfNull(recipeId);
        if (!set.RecipeById.TryGetValue(recipeId, out var recipe)) throw new EngineCommon.EngineRefusedException(EconomyCodes.UnknownRecipe, "there is no recipe '" + recipeId + "'");
        var def = set.Markets[market];
        var slot = set.LineOf[market][recipe.Index];
        if (slot < 0) throw new EngineCommon.EngineRefusedException(EconomyCodes.NoLine, "market '" + def.Id + "' has no production line for '" + recipe.Id + "'");
        var line = def.Lines[slot];
        var ordered = set.LinesInOrder[market];
        var position = Array.FindIndex(ordered, l => l.Index == line.Index);
        var period = runtime.Period + 1;
        var modifiers = runtime.ModifiersForPlan(hostFacts);
        var (stock, incoming) = runtime.StartPlan(market);
        for (var i = 0; i < position; i++) runtime.PlanLine(market, ordered[i], stock, incoming, period, modifiers);
        var terms = new List<ExplainTerm>
        {
            new("recipe", recipe.Id, recipe.Duration, "periods per run" + (recipe.Duration == 0 ? " (the outputs join the stock at once)" : "") + "; " + (recipe.Locked ? runtime.IsUnlocked(recipe.Index) ? "unlocked" : "locked until discovered" : "open from the start")),
            new("order", recipe.Id, position + 1, "of " + N(ordered.Length) + " lines of " + def.Id + (position == 0 ? "" : "; after " + string.Join(", ", ordered.Take(position).Select(l => set.Recipes[l.Recipe].Id)))),
        };
        var penalty = runtime.InArrears(market) ? set.Price.ArrearsPenalty : 0;
        terms.Add(new ExplainTerm("capacity", recipe.Id, line.Capacity, "runs a period" + (penalty > 0 ? "; the market is in arrears, so " + N(penalty) + " per mille of its output is lost" : "")));
        for (var r = 0; r < recipe.Requires.Count; r++)
        {
            var good = set.Goods[recipe.Requires[r].Good];
            var need = recipe.Requires[r];
            var held = runtime.Reserved[line.RequirePairs[r]];
            var have = runtime.Free(stock, line.RequirePairs[r]);
            terms.Add(new ExplainTerm("requires", good.Id, have, "needs " + N(need.Amount) + " free at this line's turn, never used up: stock " + N(stock[line.RequirePairs[r]]) + ", held by earlier lines this period " + N(held) + (have >= need.Amount ? "; held" : held > 0 && stock[line.RequirePairs[r]] >= need.Amount ? "; missing: the earlier lines hold it" : "; missing")));
            if (need.Wear is not { } wear) continue;
            var progress = runtime.Markets[market].Lines[slot].Wear[r];
            terms.Add(new ExplainTerm("wear", good.Id, progress, "runs done towards the next loss: " + N(wear.Units) + " lost for every " + N(wear.Runs) + " runs"
                + (have >= need.Amount ? "; the stock lasts " + N(EconomyRuntime.RunsBeforeWornOut(need, have, progress)) + " more runs before it falls under " + N(need.Amount) : "; the tool is worn out until the market holds " + N(need.Amount) + " again")));
        }
        if (line.Throttle is not null)
        {
            var margin = runtime.MarginOf(market, line, period, modifiers);
            var against = line.Throttle.Costs is null ? "inputs that cost " + N(margin.InputCost) : "a cost of " + N(margin.InputCost + margin.Wage + margin.Overhead) + " (inputs " + N(margin.InputCost) + " + wage " + N(margin.Wage) + " + overhead " + N(margin.Overhead) + ")";
            terms.Add(new ExplainTerm("margin", recipe.Id, margin.Ratio, "per mille: one run's outputs are worth " + N(margin.OutputValue) + " against " + against + " (thousandths of a minor unit); the line stops at " + N(line.Throttle.Stop)
                + ", runs in full from " + N(line.Throttle.Full) + ", so it runs " + N(margin.Fraction) + " per mille of its capacity"));
            if (line.Throttle.Costs is { } costs)
            {
                terms.Add(new ExplainTerm("wage", recipe.Id, margin.Wage, "a run's labour, declared " + N(costs.Wage) + (costs.Indexed ? ", following the price level of " + set.Currencies[def.Currency].Id : ", nominal")));
                terms.Add(new ExplainTerm("overhead", recipe.Id, margin.Overhead, "a run's other variable cost, declared " + N(costs.Overhead) + (costs.Indexed ? ", following the price level" : ", nominal") + "; costs the line pays whether it runs or not are not counted"));
                foreach (var output in recipe.Outputs)
                {
                    var made = set.Goods[output.Good];
                    terms.Add(new ExplainTerm("sales-tax", made.Id, runtime.SalesRate(def, made), "per mille buyers pay on top of the price: it lowers what they ask for, so the price already carries it, and it is not a cost of the line; subsidies pay a pair's own production, not what a recipe makes"));
                }
            }
        }
        for (var i = 0; i < recipe.Inputs.Count; i++)
        {
            var good = set.Goods[recipe.Inputs[i].Good];
            var have = runtime.Free(stock, line.InputPairs[i]);
            terms.Add(new ExplainTerm("input", good.Id, have / recipe.Inputs[i].Amount, "needs " + N(recipe.Inputs[i].Amount) + " a run; stock " + N(have) + " at this line's turn"));
        }
        for (var o = 0; o < recipe.Outputs.Count; o++)
        {
            var good = set.Goods[recipe.Outputs[o].Good];
            var pair = line.OutputPairs[o];
            var capacity = def.Pairs[pair].Capacity;
            var room = Math.Max(0, capacity - stock[pair] - incoming[pair]);
            terms.Add(new ExplainTerm("room", good.Id, room / recipe.Outputs[o].Amount, "makes " + N(recipe.Outputs[o].Amount) + " a run; warehouse " + N(capacity) + ", stock " + N(stock[pair]) + ", on its way " + N(incoming[pair])));
            var (modifier, shock) = runtime.SupplyFactors(market, good, period, modifiers);
            terms.Add(new ExplainTerm("yield", good.Id, runtime.Yield(market, good, recipe.Outputs[o].Amount * 1000, period, modifiers), "thousandths of a unit a run actually joins the stock: supply modifiers " + N(modifier) + " per mille, shocks " + N(shock) + " per mille, applied when the outputs arrive"
                + (hostFacts is null ? " (modifiers not applied: no host facts were given)" : "")));
        }
        var plan = runtime.PlanLine(market, line, stock, incoming, period, modifiers);
        terms.Add(new ExplainTerm("runs", recipe.Id, plan.Runs, "of " + N(plan.Wanted) + " wanted: " + LimitText(set, plan.Limit, plan.Good)));
        var last = runtime.Markets[market].Lines[slot];
        terms.Add(new ExplainTerm("last-period", recipe.Id, last.LastRuns, LimitText(set, last.Limit, last.LimitGood)));
        foreach (var batch in runtime.Batches)
            if (batch.Market == market && batch.Recipe == recipe.Index) terms.Add(new ExplainTerm("batch", recipe.Id, batch.Runs, "runs, outputs due at period " + N(batch.Completes)));
        return new Explanation("production of " + recipe.Id + " in " + def.Id, terms, plan.Runs, plan.Limit.ToString().ToLowerInvariant());
    }

    private static string LimitText(EconomySet set, LineLimit limit, int good)
    {
        switch (limit)
        {
            case LineLimit.Locked: return "the recipe is locked";
            case LineLimit.Capacity: return "the line's capacity";
            case LineLimit.Input: return "short of " + set.Goods[good].Id;
            case LineLimit.Room: return "no room for " + set.Goods[good].Id;
            case LineLimit.Held: return "missing " + set.Goods[good].Id + ", which the recipe requires free (in stock and not held by a line earlier in the order this period)";
            case LineLimit.Margin: return "the margin throttle: the outputs are worth little against the cost of a run";
            case LineLimit.Worn: return "the tool " + set.Goods[good].Id + " lasts no more runs before it wears out";
        }
        throw new InvalidOperationException("unhandled line limit " + limit);
    }

    /// <summary>A loan: what is owed, what the next payment is, the periods left and the misses.</summary>
    public static Explanation Loan(EconomyRuntime runtime, string loanId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var loan = runtime.Loans.FirstOrDefault(l => l.Id == loanId);
        if (loan is null) throw new EngineCommon.EngineRefusedException(EconomyCodes.UnknownLoan, "there is no loan '" + loanId + "'");
        var interest = EconomyMath.Nearest(loan.Balance, loan.RatePpm, EconomyMath.Ppm);
        var terms = new List<ExplainTerm>
        {
            new("balance", loan.Id, loan.Balance, "owed now"),
            new("interest", loan.Id, interest, "at " + N(loan.RatePpm) + " ppm a period on the balance, rounded to the nearest minor unit"),
            new("payment", loan.Id, loan.Payment, loan.Kind.ToString().ToLowerInvariant() + "; the last period pays the whole balance"),
            new("remaining", loan.Id, loan.Remaining, "periods of payment left"),
            new("missed", loan.Id, loan.Missed, "in a row; default after " + N(loan.DefaultAfter)),
        };
        return new Explanation("loan " + loan.Id, terms, loan.Balance, loan.Status.ToString().ToLowerInvariant());
    }

    /// <summary>A currency: its price level, its rate and the rate that purchasing power parity points to.</summary>
    public static Explanation Currency(EconomyRuntime runtime, string currencyId)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var index = runtime.CurrencyIndex(currencyId);
        var def = runtime.Set.Currencies[index];
        var state = runtime.Currencies[index];
        var target = EconomyMath.Nearest(def.Anchor, runtime.Currencies[runtime.Set.ReferenceCurrency].Level, state.Level);
        var terms = new List<ExplainTerm>
        {
            new("level", def.Id, state.Level, "ppm of the price level at the start"),
            new("parity", def.Id, target, "rate purchasing power parity points to: anchor " + N(def.Anchor) + " x reference level / this level"),
            new("step", def.Id, def.MaxRateStepPpm, "ppm of the rate the rate may move in a period"),
            new("money", def.Id, state.Money, "supply growth is minted against; growth " + N(state.Growth) + " ppm a period"),
            new("world", def.Id, state.World, "the account opposite every treasury; minted so far " + N(state.Minted)),
        };
        return new Explanation("currency " + def.Id, terms, state.Rate, def.Reference ? "reference" : "floating");
    }
}
