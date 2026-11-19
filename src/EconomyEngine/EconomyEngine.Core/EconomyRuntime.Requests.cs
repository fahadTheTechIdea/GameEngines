using EngineCommon;

namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    // What the host (a player, a dialogue outcome, a story event, a decree) can do without waiting for a period: each by the document's own names, each refused with its own code and nothing changed.

    /// <summary>
    /// What a trade of <paramref name="quantity"/> units would come to (positive: the trader buys from the market; negative: sells to it), without making it. A buy pays the sell price (the price plus the market's
    /// spread) and the sales tax on top; a sale receives the buy price (the price less the spread).
    /// </summary>
    public TradeResult Quote(string marketId, string goodId, long quantity) => Price(marketId, goodId, quantity, out _, out _);

    private TradeResult Price(string marketId, string goodId, long quantity, out int market, out int pair)
    {
        market = MarketIndex(marketId);
        var good = GoodIndex(goodId);
        pair = PairIndex(market, good);
        if (quantity == 0 || quantity < -EconomyReader.MaxQuantity || quantity > EconomyReader.MaxQuantity)
            throw new EngineRefusedException(EconomyCodes.Range, "a trade is 1 to " + EconomyReader.MaxQuantity + " units, bought (positive) or sold (negative), not " + quantity);
        var def = Set.Markets[market];
        var goodDef = Set.Goods[good];
        var state = Markets[market].Pairs[pair];
        long unit;
        long value;
        long tax = 0;
        if (quantity > 0)
        {
            if (state.Stock < quantity) throw new EngineRefusedException(EconomyCodes.Stock, "market '" + def.Id + "' holds " + state.Stock + " of '" + goodDef.Id + "', not " + quantity);
            unit = EconomyMath.Nearest(state.Price, EconomyMath.Permille + def.Spread, EconomyMath.Permille);
            value = EconomyMath.Nearest(quantity, unit, EconomyMath.Permille);
            foreach (var index in def.Policies)
            {
                var policy = Set.Policies[index];
                if (policy.Kind == PolicyKind.Sales && Matches(policy.Tags, goodDef)) tax += EconomyMath.Nearest(value, policy.Rate, EconomyMath.Permille);
            }
        }
        else
        {
            if (state.Stock - quantity > def.Pairs[pair].Capacity) throw new EngineRefusedException(EconomyCodes.Capacity, "market '" + def.Id + "' holds at most " + def.Pairs[pair].Capacity + " of '" + goodDef.Id + "'");
            unit = EconomyMath.Nearest(state.Price, EconomyMath.Permille - def.Spread, EconomyMath.Permille);
            value = EconomyMath.Nearest(-quantity, unit, EconomyMath.Permille);
        }
        return new TradeResult(def.Id, goodDef.Id, quantity, unit, value, tax, quantity > 0 ? value + tax : value, new EconomyChanges());
    }

    /// <summary>
    /// A trader buys from or sells to a market now. The goods leave or join the market's stock at once and count as demand or supply in the next period's price; the sales tax of a buy accrues to the market's policies. The
    /// money is the trader's own and is not held here: the host (the SimEngine bridge) records it in its ledger from the result.
    /// </summary>
    public TradeResult Trade(string marketId, string goodId, long quantity)
    {
        var quote = Price(marketId, goodId, quantity, out var market, out var pair);
        var state = Markets[market].Pairs[pair];
        var def = Set.Markets[market];
        var good = Set.Goods[Set.Markets[market].Pairs[pair].Good];
        if (quantity > 0)
        {
            state.Stock -= quantity;
            state.SpotDemand += quantity;
            Accrue(def, PolicyKind.Sales, good, quote.Value);
        }
        else
        {
            state.Stock -= quantity;
            state.SpotSupply -= quantity;
        }
        return quote;
    }

    /// <summary>Gives a market's treasury money from the world (positive) or takes it back (negative): an aid grant, a fine. The treasury never goes below zero.</summary>
    public void Fund(string marketId, long delta)
    {
        var market = MarketIndex(marketId);
        if (delta == 0 || delta < -EconomyFacts.MaxMoney || delta > EconomyFacts.MaxMoney) throw new EngineRefusedException(EconomyCodes.Range, "a grant is a non-zero amount of at most " + EconomyFacts.MaxMoney + ", not " + delta);
        var state = Markets[market];
        if (state.Treasury + delta < 0 || state.Treasury + delta > EconomyFacts.MaxMoney) throw new EngineRefusedException(EconomyCodes.Funds, "the treasury of '" + marketId + "' holds " + state.Treasury + ", so " + delta + " leaves it outside 0 to " + EconomyFacts.MaxMoney);
        state.Treasury += delta;
        Currencies[Set.Markets[market].Currency].World -= delta;
    }

    /// <summary>Sets the money growth of a currency, in ppm a period: the central bank's decision. Prices follow the price level it produces.</summary>
    public void SetGrowth(string currencyId, long growthPpm)
    {
        var currency = CurrencyIndex(currencyId);
        if (growthPpm < -500_000 || growthPpm > 500_000) throw new EngineRefusedException(EconomyCodes.Range, "money growth is -500000 to 500000 ppm a period, not " + growthPpm);
        Currencies[currency].Growth = growthPpm;
    }

    /// <summary>Settles a manual schedule now (a pay day on the calendar's date, a tax decree). A schedule that runs by itself is refused, so a period cannot be settled twice.</summary>
    public EconomyChanges Settle(string scheduleId)
    {
        if (!Set.ScheduleById.TryGetValue(CheckId(scheduleId, "schedule"), out var schedule)) throw new EngineRefusedException(EconomyCodes.UnknownSchedule, "there is no schedule '" + scheduleId + "'");
        if (!schedule.Manual) throw new EngineRefusedException(EconomyCodes.Schedule, "schedule '" + scheduleId + "' runs by itself every " + schedule.Every + " periods and is not settled by hand");
        var changes = new EconomyChanges();
        SettleSchedule(schedule.Index, changes);
        return changes;
    }

    /// <summary>
    /// Lends money now: the principal leaves the lender (the world, or a market's treasury) for the borrower's treasury, and the loan is serviced on its schedule. A loan between markets is in their one currency.
    /// </summary>
    public void Lend(string id, string lenderId, string borrowerId, long principal, long ratePpm, int term, LoanKind kind, string scheduleId, int defaultAfter)
    {
        CheckId(id, "loan");
        if (_loans.Any(l => l.Id == id)) throw new EngineRefusedException(EconomyCodes.DuplicateLoan, "there is already a loan '" + id + "'");
        if (_loans.Count >= Set.Limits.MaxLoans) throw new EngineRefusedException(EconomyCodes.Capacity, "there are already " + Set.Limits.MaxLoans + " loans");
        var lender = lenderId == "world" ? -1 : MarketIndex(lenderId);
        var borrower = MarketIndex(borrowerId);
        if (!Set.ScheduleById.TryGetValue(CheckId(scheduleId, "schedule"), out var schedule)) throw new EngineRefusedException(EconomyCodes.UnknownSchedule, "there is no schedule '" + scheduleId + "'");
        if (principal < 1 || principal > EconomyFacts.MaxMoney || ratePpm < 0 || ratePpm > EconomyMath.Ppm || term < 1 || term > 1200 || defaultAfter < 1 || defaultAfter > 100)
            throw new EngineRefusedException(EconomyCodes.Range, "a loan has a principal of 1 to " + EconomyFacts.MaxMoney + ", a rate of 0 to 1000000 ppm, a term of 1 to 1200 and a default limit of 1 to 100");
        if (lender == borrower) throw new EngineRefusedException(EconomyCodes.Loan, "a market does not lend to itself");
        if (lender >= 0 && Set.Markets[lender].Currency != Set.Markets[borrower].Currency) throw new EngineRefusedException(EconomyCodes.Loan, "a loan between markets is in one currency");
        if (lender >= 0 && Markets[lender].Treasury < principal) throw new EngineRefusedException(EconomyCodes.Funds, "the treasury of '" + lenderId + "' holds " + Markets[lender].Treasury + ", less than the " + principal + " lent");
        if (Markets[borrower].Treasury + principal > EconomyFacts.MaxMoney) throw new EngineRefusedException(EconomyCodes.Funds, "the treasury of '" + borrowerId + "' would hold more than " + EconomyFacts.MaxMoney);
        var payment = kind == LoanKind.Annuity ? EconomyMath.Annuity(principal, ratePpm, term) : 0;
        if (lender >= 0) Markets[lender].Treasury -= principal;
        else Currencies[Set.Markets[borrower].Currency].World -= principal;
        Markets[borrower].Treasury += principal;
        _loans.Add(new LoanState(id, lender, borrower, schedule.Index, kind, ratePpm, payment, term, defaultAfter, principal));
    }
}
