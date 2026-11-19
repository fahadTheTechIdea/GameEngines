namespace EconomyEngine;

/// <summary>One good in one market: its price (thousandths of a minor unit of the market's currency), its stock, the last change of price (per mille, which the inertia carries), what was made and wanted last period, its band and whether demand went unmet.</summary>
public sealed class PairState
{
    public long Price { get; set; }
    public long Stock { get; set; }
    public long LastChange { get; set; }
    public long Supply { get; set; }
    public long Demand { get; set; }
    public long Met { get; set; }
    public int Band { get; set; }
    public bool Short { get; set; }

    /// <summary>Units bought from and sold to the market by a trade since the period closed; they count as demand and supply in the next period.</summary>
    public long SpotDemand { get; set; }

    public long SpotSupply { get; set; }

    /// <summary>Units of this good that production batches on their way will put into the pair. Derived from the batches, never saved; production leaves room for them.</summary>
    internal long Incoming { get; set; }
}

/// <summary>
/// Why a production line ran as many times as it did last period, or did not run. <see cref="Held"/>: a good the recipe requires in stock (a tool) was missing. <see cref="Margin"/>: the line's throttle held it under
/// its capacity because the outputs were worth little against the cost of a run. <see cref="Worn"/>: a tool the line holds could do no more runs before wearing out, so the line stopped short of what it wanted.
/// The numbers are saved, so new members go last.
/// </summary>
public enum LineLimit { Locked, Capacity, Input, Room, Held, Margin, Worn }

/// <summary>
/// A production line as it stands: the runs it started last period, and what held it to that many (<see cref="LimitGood"/> is the input that ran short, the output with no room, the required good that was missing or
/// worn out, -1 otherwise). <see cref="Wear"/> holds, for each good the recipe requires (in the recipe's order), the runs done towards the next loss of that good (0 up to its wear's runs less one; always 0 for a
/// requirement that does not wear out).
/// </summary>
public sealed class LineState(int requirements)
{
    public long LastRuns { get; set; }
    public LineLimit Limit { get; set; }
    public int LimitGood { get; set; } = -1;
    public long[] Wear { get; } = new long[requirements];
}

/// <summary>Runs of a recipe in progress in a market: they put their outputs into its stock at period <see cref="Completes"/>.</summary>
public sealed class Batch
{
    public Batch(int market, int recipe, long runs, long completes) { Market = market; Recipe = recipe; Runs = runs; Completes = completes; }
    public int Market { get; }
    public int Recipe { get; }
    public long Runs { get; }
    public long Completes { get; }
}

/// <summary>A market: its treasury, what its policies have accrued and not yet remitted, what its budget lines owe, its pairs and its price history (a ring of the last periods, per pair).</summary>
public sealed class MarketState
{
    public MarketState(MarketDef def, int history)
    {
        Accrued = new long[def.Policies.Count];
        Arrears = new long[def.Budget.Count];
        Pairs = new PairState[def.Pairs.Count];
        for (var i = 0; i < Pairs.Length; i++) Pairs[i] = new PairState();
        History = new long[def.Pairs.Count * history];
        Lines = new LineState[def.Lines.Count];
        for (var i = 0; i < Lines.Length; i++) Lines[i] = new LineState(def.Lines[i].RequirePairs.Count);
    }

    public long Treasury { get; set; }
    public long[] Accrued { get; }
    public long[] Arrears { get; }
    public PairState[] Pairs { get; }
    public long[] History { get; }
    public LineState[] Lines { get; }
}

/// <summary>A currency: the price level (parts per million of the start), its rate to the reference (ppm), the money supply growth is minted against, the growth per period, and the world account (the counter party of every flow of money).</summary>
public sealed class CurrencyState
{
    public long Level { get; set; }
    public long Rate { get; set; }
    public long Money { get; set; }
    public long Growth { get; set; }
    public long World { get; set; }
    public long Minted { get; set; }
}

/// <summary>Goods on the way: launched on a route, they join the destination's stock at period <see cref="Arrives"/>.</summary>
public sealed class Shipment
{
    public Shipment(int route, int good, long quantity, long arrives) { Route = route; Good = good; Quantity = quantity; Arrives = arrives; }
    public int Route { get; }
    public int Good { get; }
    public long Quantity { get; }
    public long Arrives { get; }
}

public enum LoanStatus { Active, Closed, Defaulted }

/// <summary>A loan as it stands: what is owed, the payment, the periods left and how many payments in a row were missed.</summary>
public sealed class LoanState
{
    public LoanState(string id, int lender, int borrower, int schedule, LoanKind kind, long ratePpm, long payment, int remaining, int defaultAfter, long balance)
    {
        Id = id; Lender = lender; Borrower = borrower; Schedule = schedule; Kind = kind; RatePpm = ratePpm; Payment = payment; Remaining = remaining; DefaultAfter = defaultAfter; Balance = balance;
    }

    public string Id { get; }
    public int Lender { get; }
    public int Borrower { get; }
    public int Schedule { get; }
    public LoanKind Kind { get; }
    public long RatePpm { get; }
    public long Payment { get; }
    public int DefaultAfter { get; }
    public long Balance { get; set; }
    public int Remaining { get; set; }
    public int Missed { get; set; }
    public LoanStatus Status { get; set; }
}

/// <summary>A shock that is on: which one, whose (a market, or -1 for every market), the period it began and how hard (per mille of its amounts).</summary>
public sealed class ShockInstance
{
    public ShockInstance(int shock, int market, long start, int scale) { Shock = shock; Market = market; Start = start; Scale = scale; }
    public int Shock { get; }
    public int Market { get; }
    public long Start { get; }
    public int Scale { get; }
}

public readonly record struct BandChanged(int Market, int Good, int From, int To);
public readonly record struct ShortageChanged(int Market, int Good, bool Entered);
public readonly record struct ShipmentLaunched(int Route, int Good, long Quantity, long Arrives);
public readonly record struct ShipmentArrived(int Route, int Good, long Quantity);

/// <summary>A route that wanted to ship and could not because the shipments in transit are at the document's limit.</summary>
public readonly record struct RouteBlocked(int Route, int Good);

public enum SettleKind { Tax, Wage, Upkeep, Subsidy, Loan }

/// <summary>A settlement: what a market's treasury received or paid (of what was due), for a policy, a budget line, a subsidy or a loan (<paramref name="Id"/> names it).</summary>
public readonly record struct Settled(int Market, SettleKind Kind, string Id, long Paid, long Due);

public enum LoanOutcome { Missed, Closed, Defaulted }
public readonly record struct LoanChanged(string Loan, LoanOutcome Outcome, long Balance);
public readonly record struct ShockChanged(int Shock, int Market, bool Started);
public readonly record struct RateChanged(int Currency, long From, long To);

/// <summary>A production line is held back by something new (or no longer): <paramref name="Good"/> is the input that ran short, the output with no room or the tool that is missing or wearing out, -1 for capacity, the margin or a locked recipe.</summary>
public readonly record struct ProductionLimitChanged(int Market, int Recipe, LineLimit From, LineLimit To, int Good);

/// <summary>A recipe was unlocked (discovered): its lines may run from the next period.</summary>
public readonly record struct RecipeUnlocked(int Recipe);

/// <summary>Everything that changed during a call, in the order it happened.</summary>
public sealed class EconomyChanges
{
    public List<RateChanged> Rates { get; } = new();
    public List<ShockChanged> Shocks { get; } = new();
    public List<ShipmentArrived> Arrivals { get; } = new();
    public List<BandChanged> Bands { get; } = new();
    public List<ShortageChanged> Shortages { get; } = new();
    public List<ShipmentLaunched> Launches { get; } = new();
    public List<RouteBlocked> Blocks { get; } = new();
    public List<Settled> Settlements { get; } = new();
    public List<LoanChanged> Loans { get; } = new();
    public List<ProductionLimitChanged> Limits { get; } = new();
    public List<RecipeUnlocked> Unlocks { get; } = new();

    public bool Any => Rates.Count + Shocks.Count + Arrivals.Count + Bands.Count + Shortages.Count + Launches.Count + Blocks.Count + Settlements.Count + Loans.Count + Limits.Count + Unlocks.Count > 0;
}

/// <summary>What a trade came to: the unit price (thousandths of a minor unit), the value of the goods, the sales tax on top of a buy, and what the trader pays (buy) or receives (sell) in minor units.</summary>
public sealed record TradeResult(string Market, string Good, long Quantity, long UnitPrice, long Value, long Tax, long Total, EconomyChanges Changes);
