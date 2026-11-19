namespace GameRuntime;

// Events of the economy component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. Names are ids, never text: the host renders them in its locale with EconomyNames.
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. One request gets one answer: from the component that serves its channel, or, from the
// EconomyChannels registry, a refusal with ECON_CHANNEL_UNKNOWN for a channel nobody serves.
//
// Order of what one Update raises: the answers to the requests in arrival order (each followed by the changes it made), then Stepped when time moved, then what the periods changed (rates, shocks, arrivals, bands,
// shortages, launches, blocks, settlements, loans), at most the component's maximum per Update; the rest is owed and follows in the next Updates, in the same order, none lost. Everything is deferred.

/// <summary>A request that a trader buys from (positive quantity) or sells to (negative) a market. Only queued; the answer is an <see cref="EconomyTradeExecuted"/> or a refusal.</summary>
public readonly struct EconomyTradeRequested
{
    public EconomyTradeRequested(string requestId, string trader, string market, string good, long quantity, string channel = "")
    { RequestId = requestId; Trader = trader; Market = market; Good = good; Quantity = quantity; Channel = channel; }
    public string RequestId { get; }
    public string Trader { get; }
    public string Market { get; }
    public string Good { get; }
    public long Quantity { get; }
    public string Channel { get; }
}

/// <summary>A request to start a shock (a drought, a devaluation, a decree) on a market, or on every market when the market is empty. The hook an event system uses.</summary>
public readonly struct EconomyShockRequested
{
    public EconomyShockRequested(string requestId, string shock, string market = "", int scalePermille = 1000, string channel = "")
    { RequestId = requestId; Shock = shock; Market = market; ScalePermille = scalePermille; Channel = channel; }
    public string RequestId { get; }
    public string Shock { get; }
    public string Market { get; }
    public int ScalePermille { get; }
    public string Channel { get; }
}

/// <summary>A request to settle a manual schedule now: a pay day on the calendar's date, a tax decree.</summary>
public readonly struct EconomySettleRequested
{
    public EconomySettleRequested(string requestId, string schedule, string channel = "")
    { RequestId = requestId; Schedule = schedule; Channel = channel; }
    public string RequestId { get; }
    public string Schedule { get; }
    public string Channel { get; }
}

/// <summary>A request to set the money growth of a currency, in parts per million a period.</summary>
public readonly struct EconomyGrowthRequested
{
    public EconomyGrowthRequested(string requestId, string currency, long growthPpm, string channel = "")
    { RequestId = requestId; Currency = currency; GrowthPpm = growthPpm; Channel = channel; }
    public string RequestId { get; }
    public string Currency { get; }
    public long GrowthPpm { get; }
    public string Channel { get; }
}

/// <summary>A request to give a market's treasury money from the world (positive) or take it back (negative).</summary>
public readonly struct EconomyFundRequested
{
    public EconomyFundRequested(string requestId, string market, long delta, string channel = "")
    { RequestId = requestId; Market = market; Delta = delta; Channel = channel; }
    public string RequestId { get; }
    public string Market { get; }
    public long Delta { get; }
    public string Channel { get; }
}

/// <summary>A request to lend money: lender is a market id or <c>world</c>; kind is <c>annuity</c> or <c>bullet</c>.</summary>
public readonly struct EconomyLendRequested
{
    public EconomyLendRequested(string requestId, string loan, string lender, string borrower, long principal, long ratePpm, int term, string kind, string schedule, int defaultAfter, string channel = "")
    { RequestId = requestId; Loan = loan; Lender = lender; Borrower = borrower; Principal = principal; RatePpm = ratePpm; Term = term; Kind = kind; Schedule = schedule; DefaultAfter = defaultAfter; Channel = channel; }
    public string RequestId { get; }
    public string Loan { get; }
    public string Lender { get; }
    public string Borrower { get; }
    public long Principal { get; }
    public long RatePpm { get; }
    public int Term { get; }
    public string Kind { get; }
    public string Schedule { get; }
    public int DefaultAfter { get; }
    public string Channel { get; }
}

/// <summary>A request for what a trade would cost, without making it.</summary>
public readonly struct EconomyQuoteRequested
{
    public EconomyQuoteRequested(string requestId, string market, string good, long quantity, string channel = "")
    { RequestId = requestId; Market = market; Good = good; Quantity = quantity; Channel = channel; }
    public string RequestId { get; }
    public string Market { get; }
    public string Good { get; }
    public long Quantity { get; }
    public string Channel { get; }
}

/// <summary>A request for a price and why.</summary>
public readonly struct EconomyPriceRequested
{
    public EconomyPriceRequested(string requestId, string market, string good, string channel = "")
    { RequestId = requestId; Market = market; Good = good; Channel = channel; }
    public string RequestId { get; }
    public string Market { get; }
    public string Good { get; }
    public string Channel { get; }
}

/// <summary>A request was carried out. Raised for every request that changes the economy, so a requester never has to guess that silence meant success.</summary>
public readonly struct EconomyApplied
{
    public EconomyApplied(string source, string channel, string requestId, long tick)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the economy is exactly as it was. A request for a channel nobody serves is refused by the registry with <see cref="EconomyChannels.UnknownChannel"/>.</summary>
public readonly struct EconomyRefused
{
    public EconomyRefused(string source, string channel, string requestId, long tick, string code)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>A trade was made. A positive quantity is bought from the market: the trader pays <c>Total</c> (value plus tax) in the market's currency; a negative one is sold to it: the trader receives <c>Total</c>. The unit price is in thousandths of a minor unit.</summary>
public readonly struct EconomyTradeExecuted
{
    public EconomyTradeExecuted(string source, string channel, string requestId, long tick, string trader, string market, string good, string currency, long quantity, long unitPrice, long value, long tax, long total)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Trader = trader; Market = market; Good = good; Currency = currency; Quantity = quantity; UnitPrice = unitPrice; Value = value; Tax = tax; Total = total; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Trader { get; }
    public string Market { get; }
    public string Good { get; }
    public string Currency { get; }
    public long Quantity { get; }
    public long UnitPrice { get; }
    public long Value { get; }
    public long Tax { get; }
    public long Total { get; }
}

/// <summary>The answer to a quote request: what the trade would come to. Nothing changed.</summary>
public readonly struct EconomyQuoted
{
    public EconomyQuoted(string source, string channel, string requestId, long tick, string trader, string market, string good, string currency, long quantity, long unitPrice, long value, long tax, long total)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Trader = trader; Market = market; Good = good; Currency = currency; Quantity = quantity; UnitPrice = unitPrice; Value = value; Tax = tax; Total = total; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Trader { get; }
    public string Market { get; }
    public string Good { get; }
    public string Currency { get; }
    public long Quantity { get; }
    public long UnitPrice { get; }
    public long Value { get; }
    public long Tax { get; }
    public long Total { get; }
}

/// <summary>The answer to a price request: the price, its band, and the terms as <c>kind|id|value</c> joined by ';'.</summary>
public readonly struct EconomyPriceAnswered
{
    public EconomyPriceAnswered(string source, string channel, string requestId, long tick, string market, string good, long price, string band, string terms)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Market = market; Good = good; Price = price; Band = band; Terms = terms; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Market { get; }
    public string Good { get; }
    public long Price { get; }
    public string Band { get; }
    public string Terms { get; }
}

/// <summary>Time moved: the economy is now at <paramref name="step"/>; <paramref name="owed"/> periods remain to be lived in the next Updates.</summary>
public readonly struct EconomyStepped
{
    public EconomyStepped(string source, string channel, long tick, long fromStep, long step, long owed)
    { Source = source; Channel = channel; Tick = tick; FromStep = fromStep; Step = step; Owed = owed; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long FromStep { get; }
    public long Step { get; }
    public long Owed { get; }
}

/// <summary>A currency's rate to the reference changed.</summary>
public readonly struct EconomyRateChanged
{
    public EconomyRateChanged(string source, string channel, long tick, string currency, long from, long to)
    { Source = source; Channel = channel; Tick = tick; Currency = currency; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Currency { get; }
    public long From { get; }
    public long To { get; }
}

/// <summary>A shock began or ended.</summary>
public readonly struct EconomyShockChanged
{
    public EconomyShockChanged(string source, string channel, long tick, string shock, string market, bool started)
    { Source = source; Channel = channel; Tick = tick; Shock = shock; Market = market; Started = started; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Shock { get; }
    public string Market { get; }
    public bool Started { get; }
}

/// <summary>Goods arrived at the end of a route.</summary>
public readonly struct EconomyShipmentArrived
{
    public EconomyShipmentArrived(string source, string channel, long tick, string route, string good, long quantity)
    { Source = source; Channel = channel; Tick = tick; Route = route; Good = good; Quantity = quantity; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Route { get; }
    public string Good { get; }
    public long Quantity { get; }
}

/// <summary>The price of a good in a market moved to another band.</summary>
public readonly struct EconomyBandChanged
{
    public EconomyBandChanged(string source, string channel, long tick, string market, string good, string from, string to)
    { Source = source; Channel = channel; Tick = tick; Market = market; Good = good; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Market { get; }
    public string Good { get; }
    public string From { get; }
    public string To { get; }
}

/// <summary>A market ran short of a good (demand went unmet) or was supplied again.</summary>
public readonly struct EconomyShortageChanged
{
    public EconomyShortageChanged(string source, string channel, long tick, string market, string good, bool entered)
    { Source = source; Channel = channel; Tick = tick; Market = market; Good = good; Entered = entered; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Market { get; }
    public string Good { get; }
    public bool Entered { get; }
}

/// <summary>Merchants loaded goods on a route; they are due at period <c>Arrives</c>.</summary>
public readonly struct EconomyShipmentLaunched
{
    public EconomyShipmentLaunched(string source, string channel, long tick, string route, string good, long quantity, long arrives)
    { Source = source; Channel = channel; Tick = tick; Route = route; Good = good; Quantity = quantity; Arrives = arrives; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Route { get; }
    public string Good { get; }
    public long Quantity { get; }
    public long Arrives { get; }
}

/// <summary>A route could not ship a good because the shipments on the way are at the document's limit.</summary>
public readonly struct EconomyRouteBlocked
{
    public EconomyRouteBlocked(string source, string channel, long tick, string route, string good)
    { Source = source; Channel = channel; Tick = tick; Route = route; Good = good; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Route { get; }
    public string Good { get; }
}

/// <summary>A settlement: what a market's treasury received or paid, of what was due, for a tax, a wage, an upkeep line, a subsidy or a loan (<c>Kind</c> says which, <c>Name</c> names it).</summary>
public readonly struct EconomySettled
{
    public EconomySettled(string source, string channel, long tick, string market, string kind, string name, long paid, long due)
    { Source = source; Channel = channel; Tick = tick; Market = market; Kind = kind; Name = name; Paid = paid; Due = due; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Market { get; }
    public string Kind { get; }
    public string Name { get; }
    public long Paid { get; }
    public long Due { get; }
}

/// <summary>A loan missed a payment, was repaid, or went into default.</summary>
public readonly struct EconomyLoanChanged
{
    public EconomyLoanChanged(string source, string channel, long tick, string loan, string outcome, long balance)
    { Source = source; Channel = channel; Tick = tick; Loan = loan; Outcome = outcome; Balance = balance; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Loan { get; }
    public string Outcome { get; }
    public long Balance { get; }
}

/// <summary>A request to unlock a recipe the document locked: the hook a discovery (an alchemy book, a research) uses to let production start. Only queued; the answer is an <see cref="EconomyApplied"/> or a refusal.</summary>
public readonly struct EconomyRecipeUnlockRequested
{
    public EconomyRecipeUnlockRequested(string requestId, string recipe, string channel = "")
    { RequestId = requestId; Recipe = recipe; Channel = channel; }
    public string RequestId { get; }
    public string Recipe { get; }
    public string Channel { get; }
}

/// <summary>A recipe was unlocked: its production lines may run from the next period.</summary>
public readonly struct EconomyRecipeUnlocked
{
    public EconomyRecipeUnlocked(string source, string channel, long tick, string recipe)
    { Source = source; Channel = channel; Tick = tick; Recipe = recipe; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Recipe { get; }
}

/// <summary>A production line is held back by something new, or no longer: <c>From</c> and <c>To</c> are locked, capacity, input or room; <c>Good</c> is the input that ran short or the output with no room (empty for capacity or a locked recipe).</summary>
public readonly struct EconomyProductionLimited
{
    public EconomyProductionLimited(string source, string channel, long tick, string market, string recipe, string from, string to, string good)
    { Source = source; Channel = channel; Tick = tick; Market = market; Recipe = recipe; From = from; To = to; Good = good; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Market { get; }
    public string Recipe { get; }
    public string From { get; }
    public string To { get; }
    public string Good { get; }
}
