using System.Text.RegularExpressions;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace EconomyEngine;

/// <summary>
/// The market-level economy of a game world: the prices and stocks of goods in markets, the shipments between them, taxes, tariffs and subsidies, the money of each currency and what it is worth in the others, loans
/// and wages. It has no clock of its own. The caller gives it a step number (the game layer converts the calendar instant), and it closes one period each time the step crosses a multiple of the document's
/// <c>periodSteps</c>, at most <see cref="MaxPeriodsPerAdvance"/> in one call, so the work of one call is bounded however far behind the game is. Nothing here reads a clock, a float or a random source other
/// than the document's seeded roll, and every number is a whole number. Everything it holds is bounded by the document; money moves only between treasuries and the world account of its currency, so what is
/// held in treasuries and by the world is always what the document started with plus what was minted (<see cref="Conservation"/>).
/// </summary>
public sealed partial class EconomyRuntime
{
    public const int MaxPeriodsPerAdvance = 64;

    private static readonly Regex IdText = new("^[a-z0-9._-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<Shipment> _shipments = new();
    private readonly List<LoanState> _loans = new();
    private readonly List<ShockInstance> _shocks = new();

    private EconomyRuntime(EconomySet set, long step)
    {
        Set = set;
        Step = step;
        Markets = new MarketState[set.Markets.Count];
        Currencies = new CurrencyState[set.Currencies.Count];
        _unlocked = new bool[set.Recipes.Count];
    }

    public EconomySet Set { get; }

    /// <summary>The step the state is at. Periods up to <c>Step / periodSteps</c> have been lived through.</summary>
    public long Step { get; private set; }

    /// <summary>The number of periods lived through.</summary>
    public long Period => Step / Set.PeriodSteps;

    public MarketState[] Markets { get; }
    public CurrencyState[] Currencies { get; }
    public IReadOnlyList<Shipment> Shipments => _shipments;
    public IReadOnlyList<LoanState> Loans => _loans;
    public IReadOnlyList<ShockInstance> ActiveShocks => _shocks;

    public static EconomyRuntime Create(EconomySet set, long startStep = 0)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (startStep < 0) throw new EngineRefusedException(EconomyCodes.StepOrder, "the start step is not negative");
        var runtime = new EconomyRuntime(set, startStep);
        foreach (var currency in set.Currencies)
            runtime.Currencies[currency.Index] = new CurrencyState { Level = EconomyMath.Ppm, Rate = currency.Anchor, Money = currency.Money, Growth = currency.GrowthPpm };
        foreach (var market in set.Markets)
        {
            var state = new MarketState(market, set.Limits.History) { Treasury = market.Treasury };
            runtime.Markets[market.Index] = state;
            foreach (var pair in market.Pairs)
            {
                var price = runtime.ReferencePrice(market.Currency, set.Goods[pair.Good]);
                state.Pairs[pair.Index].Price = price;
                state.Pairs[pair.Index].Stock = pair.Stock;
                state.Pairs[pair.Index].Band = runtime.BandOf(EconomyMath.Trunc(price, EconomyMath.Permille, price), 0);
            }
        }
        foreach (var recipe in set.Recipes) runtime._unlocked[recipe.Index] = !recipe.Locked;
        foreach (var market in set.Markets)
            foreach (var line in market.Lines)
                runtime.Markets[market.Index].Lines[runtime.Set.LineOf[market.Index][line.Recipe]].Limit = runtime._unlocked[line.Recipe] ? LineLimit.Capacity : LineLimit.Locked;
        foreach (var loan in set.Loans)
        {
            var payment = loan.Kind == LoanKind.Annuity ? EconomyMath.Annuity(loan.Principal, loan.RatePpm, loan.Term) : 0;
            runtime._loans.Add(new LoanState(loan.Id, loan.Lender, loan.Borrower, loan.Schedule, loan.Kind, loan.RatePpm, payment, loan.Term, loan.DefaultAfter, loan.Principal));
        }
        return runtime;
    }

    /// <summary>Rebuilds a runtime from checked save data; only the save reader calls it.</summary>
    internal static EconomyRuntime Restore(EconomySet set, EconomyImage image)
    {
        var runtime = new EconomyRuntime(set, image.Step);
        runtime.Apply(image);
        return runtime;
    }

    // ---- lookups ---------------------------------------------------------------------------------------------------------------------

    private static string CheckId(string id, string what)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (IdText.IsMatch(id)) return id;
        throw new EngineRefusedException(EconomyCodes.UnknownName, what + " '" + id + "' may only use a-z, 0-9, hyphen, dot and underscore, 1 to 128 characters");
    }

    public int MarketIndex(string id)
    {
        if (Set.MarketById.TryGetValue(CheckId(id, "market"), out var market)) return market.Index;
        throw new EngineRefusedException(EconomyCodes.UnknownMarket, "there is no market '" + id + "'");
    }

    public int GoodIndex(string id)
    {
        if (Set.GoodById.TryGetValue(CheckId(id, "good"), out var good)) return good.Index;
        throw new EngineRefusedException(EconomyCodes.UnknownGood, "there is no good '" + id + "'");
    }

    public int RouteIndex(string id)
    {
        if (Set.RouteById.TryGetValue(CheckId(id, "route"), out var route)) return route.Index;
        throw new EngineRefusedException(EconomyCodes.UnknownRoute, "there is no route '" + id + "'");
    }

    public int CurrencyIndex(string id)
    {
        if (Set.CurrencyById.TryGetValue(CheckId(id, "currency"), out var currency)) return currency.Index;
        throw new EngineRefusedException(EconomyCodes.UnknownCurrency, "there is no currency '" + id + "'");
    }

    /// <summary>The pair of a market and a good, or a refusal when the market does not trade the good.</summary>
    public int PairIndex(int market, int good)
    {
        var slot = Set.PairOf[market][good];
        if (slot >= 0) return slot;
        throw new EngineRefusedException(EconomyCodes.NotTraded, "market '" + Set.Markets[market].Id + "' does not trade '" + Set.Goods[good].Id + "'");
    }

    /// <summary>The price a good has in a currency when supply meets demand: its base price in the reference currency, at the currency's price level, in the currency's own units; never below one thousandth of a minor unit, so a deflated currency still has a price to divide by.</summary>
    public long ReferencePrice(int currency, GoodDef good) => Math.Max(1, EconomyMath.Trunc(good.Base, Currencies[currency].Level, Set.Currencies[currency].Anchor));

    /// <summary>The price of a pair over its reference price, per mille.</summary>
    public long Ratio(int market, int pair)
    {
        var def = Set.Markets[market];
        var reference = ReferencePrice(def.Currency, Set.Goods[def.Pairs[pair].Good]);
        return EconomyMath.Trunc(Markets[market].Pairs[pair].Price, EconomyMath.Permille, reference);
    }

    /// <summary>True when any wage or upkeep line of the market is owed.</summary>
    public bool InArrears(int market) => Markets[market].Arrears.Any(a => a > 0);

    /// <summary>The band for a ratio, given the band the pair is in: up at once when the ratio reaches a band, down only when it is under the band's start by the document's margin.</summary>
    internal int BandOf(long ratio, int current)
    {
        var bands = Set.Bands;
        var plain = 0;
        for (var b = 0; b < bands.Count; b++) if (ratio >= bands[b].From) plain = b;
        if (plain >= current) return plain;
        return ratio < bands[current].From - Set.Price.BandMargin ? plain : current;
    }

    /// <summary>Money in treasuries plus the world account, less what the document started with and what was minted, per currency: always zero, because money moves only between those accounts.</summary>
    public IReadOnlyList<long> Conservation()
    {
        var result = new long[Set.Currencies.Count];
        for (var c = 0; c < result.Length; c++) result[c] = Currencies[c].World - Currencies[c].Minted;
        foreach (var market in Set.Markets) result[market.Currency] += Markets[market.Index].Treasury - market.Treasury;
        return result;
    }

    public string StateDigest() => EconomyImage.Of(this).Digest;

    /// <summary>How many periods remain to live to reach <paramref name="target"/> step after a limited <see cref="Advance"/>.</summary>
    public long Owed(long targetStep) => Math.Max(0, targetStep / Set.PeriodSteps - Period);

    // ---- time ------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves the economy to <paramref name="toStep"/>, closing each period on the way (at most <see cref="MaxPeriodsPerAdvance"/>; if more are owed the step stops at the last one closed and <see cref="Owed"/>
    /// says how many remain). A step before the present is refused, and so is a missing or wrongly typed host fact a modifier needs; the economy has not moved in either case.
    /// </summary>
    public EconomyChanges Advance(long toStep, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        ArgumentNullException.ThrowIfNull(hostFacts);
        if (toStep < Step) throw new EngineRefusedException(EconomyCodes.StepOrder, "the economy is at step " + Step + " and does not go back to " + toStep);
        var changes = new EconomyChanges();
        var from = Period;
        var target = toStep / Set.PeriodSteps;
        if (target == from)
        {
            Step = toStep;
            return changes;
        }
        var modifiers = ModifiersFor(hostFacts);
        var count = Math.Min(target - from, MaxPeriodsPerAdvance);
        for (var i = 1; i <= count; i++) ClosePeriod(from + i, modifiers, changes);
        Step = count == target - from ? toStep : (from + count) * Set.PeriodSteps;
        return changes;
    }

    private void ClosePeriod(long period, Modifiers modifiers, EconomyChanges changes)
    {
        var before = StartCurrencies(period, changes);
        Arrive(period, changes);
        Deliver(period, modifiers);
        Produce(period, modifiers, before, changes);
        for (var m = 0; m < Set.Markets.Count; m++) UpdateMarket(m, period, before, modifiers, changes);
        if (Set.Routing == RoutingMode.Landed) ComputeLandedField();
        foreach (var index in Set.RoutesByRank) Launch(Set.Routes[index], period, changes);
        foreach (var schedule in Set.Schedules)
            if (!schedule.Manual && period % schedule.Every == schedule.Offset) SettleSchedule(schedule.Index, changes);
        EndShocks(period, changes);
    }
}
