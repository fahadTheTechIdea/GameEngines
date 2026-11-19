using System.Text.RegularExpressions;
using EngineCommon;

namespace EconomyEngine;

/// <summary>
/// The rules a state must satisfy to belong to a document. The digest is not a secret, so a save passes through these as well; it cannot bring in a state the document could not have made. Ranges and shapes are
/// checked before the digest, what the state says about the document (money is conserved, every price is inside its bounds and its band is steady) after it.
/// </summary>
internal static class EconomyStateRules
{
    private static readonly Regex IdText = new("^[a-z0-9._-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static EngineRefusedException Refuse(string message) => new(EconomyCodes.SaveState, message);

    private static bool In(long value, long low, long high) => value >= low && value <= high;

    public static void CheckRanges(EconomySet set, EconomyImage image)
    {
        if (image.Step < 0) throw Refuse("the step is negative");
        var period = image.Step / set.PeriodSteps;
        if (image.Currencies.Count != set.Currencies.Count) throw Refuse("the save has " + image.Currencies.Count + " currencies, the document " + set.Currencies.Count);
        foreach (var (c, i) in image.Currencies.Select((c, i) => (c, i)))
        {
            var where = "currency '" + set.Currencies[i].Id + "'";
            if (!In(c.Level, 1, EconomyFacts.MaxLevel) || !In(c.Rate, 1, EconomyFacts.MaxRate)) throw Refuse(where + " has a price level or a rate outside its range");
            if (!In(c.Money, 0, EconomyFacts.MaxMoney) || !In(c.Growth, -500_000, 500_000)) throw Refuse(where + " has money or growth outside its range");
            if (!In(c.World, -EconomyFacts.MaxMoney * 1000, EconomyFacts.MaxMoney * 1000) || !In(c.Minted, -EconomyFacts.MaxMoney * 1000, EconomyFacts.MaxMoney * 1000)) throw Refuse(where + " has a world account outside its range");
        }
        if (image.Markets.Count != set.Markets.Count) throw Refuse("the save has " + image.Markets.Count + " markets, the document " + set.Markets.Count);
        for (var m = 0; m < set.Markets.Count; m++) CheckMarket(set, set.Markets[m], image.Markets[m]);
        if (image.Shipments.Count > set.Limits.MaxShipments) throw Refuse("more shipments than the document allows (" + set.Limits.MaxShipments + ")");
        foreach (var s in image.Shipments) CheckShipment(set, s, period);
        if (image.Loans.Count > set.Limits.MaxLoans) throw Refuse("more loans than the document allows (" + set.Limits.MaxLoans + ")");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in image.Loans) CheckLoan(set, l, ids);
        if (image.Shocks.Count > set.Limits.MaxShocks) throw Refuse("more shocks than the document allows (" + set.Limits.MaxShocks + ")");
        foreach (var s in image.Shocks) CheckShock(set, s, period);
        CheckProduction(set, image, period);
    }

    private static void CheckMarket(EconomySet set, MarketDef def, ImageMarket market)
    {
        var where = "market '" + def.Id + "'";
        if (!In(market.Treasury, 0, EconomyFacts.MaxMoney)) throw Refuse(where + " has a treasury outside 0 to " + EconomyFacts.MaxMoney);
        if (market.Accrued.Count != def.Policies.Count || market.Accrued.Any(a => !In(a, 0, EconomyFacts.MaxMoney))) throw Refuse(where + " has accruals that do not match its policies or are out of range");
        if (market.Arrears.Count != def.Budget.Count || market.Arrears.Any(a => !In(a, 0, EconomyFacts.MaxMoney))) throw Refuse(where + " has arrears that do not match its budget or are out of range");
        if (market.Pairs.Count != def.Pairs.Count) throw Refuse(where + " has " + market.Pairs.Count + " pairs, the document " + def.Pairs.Count);
        if (market.Lines.Count != def.Lines.Count) throw Refuse(where + " has " + market.Lines.Count + " production lines, the document " + def.Lines.Count);
        if (market.History.Count != def.Pairs.Count * set.Limits.History || market.History.Any(h => !In(h, 0, EconomyFacts.MaxPrice))) throw Refuse(where + " has a price history of the wrong size or out of range");
        for (var i = 0; i < def.Pairs.Count; i++)
        {
            var p = market.Pairs[i];
            var good = set.Goods[def.Pairs[i].Good].Id;
            if (!In(p.Price, 1, EconomyFacts.MaxPrice)) throw Refuse(where + " good '" + good + "' has a price outside its range");
            if (!In(p.Stock, 0, def.Pairs[i].Capacity)) throw Refuse(where + " good '" + good + "' holds more than its warehouse or less than nothing");
            if (!In(p.LastChange, -1000, 1000)) throw Refuse(where + " good '" + good + "' has a last change outside -1000 to 1000");
            if (!In(p.Band, 0, set.Bands.Count - 1)) throw Refuse(where + " good '" + good + "' is in a band the document does not have");
            if (!In(p.Supply, 0, EconomyFacts.MaxStock) || !In(p.Demand, 0, EconomyFacts.MaxStock) || !In(p.SpotDemand, 0, EconomyFacts.MaxStock) || !In(p.SpotSupply, 0, EconomyFacts.MaxStock))
                throw Refuse(where + " good '" + good + "' has a flow outside its range");
            if (!In(p.Met, 0, p.Demand)) throw Refuse(where + " good '" + good + "' met more than was wanted");
            if (p.Short != p.Met < p.Demand) throw Refuse(where + " good '" + good + "' is marked short but met its demand, or the other way round");
        }
    }

    /// <summary>
    /// Production: one flag per recipe (a recipe the document never locked is unlocked); each line's state a limit that fits its recipe (an input that ran short is one of the recipe's inputs, an output with no room one of
    /// its outputs) and no runs while the recipe is locked; each batch on a line that exists, of an unlocked recipe that takes time, with runs inside the line's capacity and a completion inside the recipe's duration
    /// (the same rule as a shipment's arrival), at most one batch for a line and completion period.
    /// </summary>
    private static void CheckProduction(EconomySet set, EconomyImage image, long period)
    {
        if (image.Unlocked.Count != set.Recipes.Count) throw Refuse("the save has " + image.Unlocked.Count + " recipe flags, the document " + set.Recipes.Count + " recipes");
        for (var r = 0; r < set.Recipes.Count; r++)
            if (!set.Recipes[r].Locked && !image.Unlocked[r]) throw Refuse("recipe '" + set.Recipes[r].Id + "' is locked in the save but the document never locked it");
        for (var m = 0; m < set.Markets.Count; m++)
            for (var i = 0; i < set.Markets[m].Lines.Count; i++)
            {
                var line = set.Markets[m].Lines[i];
                var saved = image.Markets[m].Lines[i];
                var recipe = set.Recipes[line.Recipe];
                var where = "market '" + set.Markets[m].Id + "' line for recipe '" + recipe.Id + "'";
                if (!In(saved.LastRuns, 0, line.Capacity) || !In(saved.Limit, 0, 6) || !In(saved.LimitGood, -1, set.Goods.Count - 1)) throw Refuse(where + " has runs or a limit outside its range");
                var limit = (LineLimit)saved.Limit;
                if (!image.Unlocked[line.Recipe] && (limit != LineLimit.Locked || saved.LastRuns != 0)) throw Refuse(where + " ran although its recipe is locked");
                var names = limit == LineLimit.Input || limit == LineLimit.Room || limit == LineLimit.Held || limit == LineLimit.Worn;
                var fits = names ? NamedBy(recipe, limit).Any(a => a.Good == saved.LimitGood) : saved.LimitGood == -1;
                if (!fits) throw Refuse(where + " names a limiting good that does not fit its limit");
                if (limit == LineLimit.Margin && line.Throttle is null) throw Refuse(where + " was held by a margin throttle although the line has none");
                if (limit == LineLimit.Held && recipe.Requires.Count == 0) throw Refuse(where + " was held for a required good although its recipe requires none");
                if (limit == LineLimit.Worn && !recipe.Requires.Any(a => a.Wear is not null)) throw Refuse(where + " was held short by a worn tool although its recipe requires none that wears out");
                CheckWear(where, recipe, saved);
            }
        var previous = (Market: -1, Recipe: -1);
        foreach (var b in image.Batches)
        {
            if (!In(b.Market, 0, set.Markets.Count - 1) || !In(b.Recipe, 0, set.Recipes.Count - 1)) throw Refuse("a production batch names a market or a recipe the document does not have");
            if ((b.Market, b.Recipe).CompareTo(previous) <= 0) throw Refuse("production batches are not grouped once per line in the order of market and recipe");
            previous = (b.Market, b.Recipe);
            var slot = set.LineOf[b.Market][b.Recipe];
            if (slot < 0) throw Refuse("a production batch runs a recipe its market has no line for");
            var recipe = set.Recipes[b.Recipe];
            if (recipe.Duration < 1 || !image.Unlocked[b.Recipe]) throw Refuse("a production batch is of a recipe that takes no time or is locked");
            if (b.Completes.Count == 0 || b.Completes.Count != b.Runs.Count) throw Refuse("a group of production batches is empty or has as many completion periods as runs missing");
            var capacity = set.Markets[b.Market].Lines[slot].Capacity;
            var last = period;
            for (var k = 0; k < b.Completes.Count; k++)
            {
                if (!In(b.Runs[k], 1, capacity)) throw Refuse("a production batch has runs outside its line's capacity");
                if (!In(b.Completes[k], period + 1, period + recipe.Duration)) throw Refuse("a production batch completes outside the duration of its recipe");
                if (b.Completes[k] <= last) throw Refuse("production batches of one line are not in ascending order of completion, or two complete in the same period");
                last = b.Completes[k];
            }
        }
    }

    /// <summary>The goods a limit can name: an input that ran short, an output with no room, a required good that was missing, or a required good that wears out.</summary>
    private static IEnumerable<RecipeAmount> NamedBy(RecipeDef recipe, LineLimit limit)
    {
        switch (limit)
        {
            case LineLimit.Input: return recipe.Inputs;
            case LineLimit.Room: return recipe.Outputs;
            case LineLimit.Held: return recipe.Requires;
            case LineLimit.Worn: return recipe.Requires.Where(a => a.Wear is not null);
            case LineLimit.Locked:
            case LineLimit.Capacity:
            case LineLimit.Margin:
                return [];
        }
        throw new InvalidOperationException("unhandled line limit " + limit);   // a value outside the enum was range checked away before this is reached
    }

    /// <summary>The runs a line has done towards the next loss of each required good: one number per requirement, 0 for one that does not wear out, under the wear's runs for one that does.</summary>
    private static void CheckWear(string where, RecipeDef recipe, ImageLine saved)
    {
        if (saved.Wear.Count != recipe.Requires.Count) throw Refuse(where + " keeps " + saved.Wear.Count + " tool wear counts, its recipe requires " + recipe.Requires.Count + " goods");
        for (var r = 0; r < recipe.Requires.Count; r++)
        {
            var runs = recipe.Requires[r].Wear?.Runs ?? 1;
            if (!In(saved.Wear[r], 0, runs - 1)) throw Refuse(where + " has done a number of runs towards a tool loss outside 0 to " + (runs - 1));
        }
    }

    private static void CheckShipment(EconomySet set, ImageShipment s, long period)
    {
        if (!In(s.Route, 0, set.Routes.Count - 1) || !In(s.Good, 0, set.Goods.Count - 1)) throw Refuse("a shipment names a route or a good the document does not have");
        var route = set.Routes[s.Route];
        if (set.PairOf[route.From][s.Good] < 0 || set.PairOf[route.To][s.Good] < 0) throw Refuse("a shipment carries a good one end of its route does not trade");
        if (!In(s.Quantity, 1, EconomyFacts.MaxStock)) throw Refuse("a shipment carries a quantity outside its range");
        if (!In(s.Arrives, period + 1, period + route.Delay)) throw Refuse("a shipment arrives outside the delay of its route");
    }

    private static void CheckLoan(EconomySet set, ImageLoan l, HashSet<string> ids)
    {
        if (!IdText.IsMatch(l.Id) || !ids.Add(l.Id)) throw Refuse("a loan id is not an id, or appears twice");
        var where = "loan '" + l.Id + "'";
        if (!In(l.Borrower, 0, set.Markets.Count - 1) || !In(l.Lender, -1, set.Markets.Count - 1) || l.Lender == l.Borrower) throw Refuse(where + " names a lender or borrower the document does not have");
        if (l.Lender >= 0 && set.Markets[l.Lender].Currency != set.Markets[l.Borrower].Currency) throw Refuse(where + " is between markets of different currencies");
        if (!In(l.Schedule, 0, set.Schedules.Count - 1) || !In(l.Kind, 0, 1) || !In(l.Status, 0, 2)) throw Refuse(where + " has a schedule, kind or status the document does not have");
        if (!In(l.RatePpm, 0, EconomyMath.Ppm) || !In(l.Payment, 0, EconomyFacts.MaxMoney) || !In(l.Balance, 0, EconomyFacts.MaxMoney)) throw Refuse(where + " has a rate, payment or balance outside its range");
        if (!In(l.DefaultAfter, 1, 100) || !In(l.Remaining, 0, 1200) || !In(l.Missed, 0, l.DefaultAfter)) throw Refuse(where + " has a limit, a count of periods left or a count of misses outside its range");
        var status = (LoanStatus)l.Status;
        if (status == LoanStatus.Active && (l.Remaining < 1 || l.Missed >= l.DefaultAfter)) throw Refuse(where + " is active but has no periods left or has missed its limit");
        if (status == LoanStatus.Defaulted && l.Missed != l.DefaultAfter) throw Refuse(where + " is in default without having missed its limit");
    }

    private static void CheckShock(EconomySet set, ImageShock s, long period)
    {
        if (!In(s.Shock, 0, set.Shocks.Count - 1) || !In(s.Market, -1, set.Markets.Count - 1)) throw Refuse("a shock names a shock or a market the document does not have");
        if (!In(s.Scale, 1, EconomyRuntime.MaxShockScale)) throw Refuse("a shock scale is outside 1 to " + EconomyRuntime.MaxShockScale);
        if (!In(s.Start, 0, period) || s.Start + set.Shocks[s.Shock].Duration <= period) throw Refuse("a shock began in the future or has already ended");
    }

    /// <summary>What the state says about the document: money is conserved, each price is inside its bounds, each band is the one the ratio gives or the one it is held in by the margin.</summary>
    public static void CheckAgainstDocument(EconomyRuntime runtime)
    {
        var set = runtime.Set;
        var conservation = runtime.Conservation();
        for (var c = 0; c < conservation.Count; c++)
            if (conservation[c] != 0) throw Refuse("currency '" + set.Currencies[c].Id + "' does not conserve money: treasuries and the world account differ by " + conservation[c] + " from the start plus what was minted");
        foreach (var market in set.Markets)
            foreach (var pair in market.Pairs)
            {
                var good = set.Goods[pair.Good];
                var state = runtime.Markets[market.Index].Pairs[pair.Index];
                var reference = runtime.ReferencePrice(market.Currency, good);
                var low = Math.Max(1, EconomyMath.Trunc(reference, good.Floor, EconomyMath.Permille));
                var high = Math.Max(low, Math.Min(EconomyFacts.MaxPrice, EconomyMath.Trunc(reference, good.Ceiling, EconomyMath.Permille)));
                if (state.Price < low || state.Price > high) throw Refuse("market '" + market.Id + "' good '" + good.Id + "' has a price outside its floor and ceiling");
                if (runtime.BandOf(EconomyMath.Trunc(state.Price, EconomyMath.Permille, reference), state.Band) != state.Band) throw Refuse("market '" + market.Id + "' good '" + good.Id + "' is in a band its price does not give");
            }
    }
}
