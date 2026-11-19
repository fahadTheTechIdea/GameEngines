namespace EconomyEngine;

public sealed partial class EconomyRuntime
{
    /// <summary>Fills a fresh runtime from an image whose ranges and shapes have been checked.</summary>
    private void Apply(EconomyImage image)
    {
        for (var c = 0; c < image.Currencies.Count; c++)
        {
            var saved = image.Currencies[c];
            Currencies[c] = new CurrencyState { Level = saved.Level, Rate = saved.Rate, Money = saved.Money, Growth = saved.Growth, World = saved.World, Minted = saved.Minted };
        }
        for (var m = 0; m < image.Markets.Count; m++)
        {
            var saved = image.Markets[m];
            var state = new MarketState(Set.Markets[m], Set.Limits.History) { Treasury = saved.Treasury };
            Markets[m] = state;
            for (var i = 0; i < saved.Accrued.Count; i++) state.Accrued[i] = saved.Accrued[i];
            for (var i = 0; i < saved.Arrears.Count; i++) state.Arrears[i] = saved.Arrears[i];
            for (var i = 0; i < saved.History.Count; i++) state.History[i] = saved.History[i];
            for (var i = 0; i < saved.Pairs.Count; i++)
            {
                var p = saved.Pairs[i];
                var pair = state.Pairs[i];
                pair.Price = p.Price; pair.Stock = p.Stock; pair.LastChange = p.LastChange; pair.Supply = p.Supply; pair.Demand = p.Demand; pair.Met = p.Met;
                pair.Band = p.Band; pair.Short = p.Short; pair.SpotDemand = p.SpotDemand; pair.SpotSupply = p.SpotSupply;
            }
        }
        foreach (var s in image.Shipments) _shipments.Add(new Shipment(s.Route, s.Good, s.Quantity, s.Arrives));
        foreach (var l in image.Loans)
            _loans.Add(new LoanState(l.Id, l.Lender, l.Borrower, l.Schedule, (LoanKind)l.Kind, l.RatePpm, l.Payment, l.Remaining, l.DefaultAfter, l.Balance) { Missed = l.Missed, Status = (LoanStatus)l.Status });
        foreach (var s in image.Shocks) _shocks.Add(new ShockInstance(s.Shock, s.Market, s.Start, s.Scale));
        for (var r = 0; r < image.Unlocked.Count; r++) _unlocked[r] = image.Unlocked[r];
        for (var m = 0; m < image.Markets.Count; m++)
            for (var i = 0; i < image.Markets[m].Lines.Count; i++)
            {
                var line = image.Markets[m].Lines[i];
                var state = Markets[m].Lines[i];
                state.LastRuns = line.LastRuns; state.Limit = (LineLimit)line.Limit; state.LimitGood = line.LimitGood;
                for (var r = 0; r < line.Wear.Count; r++) state.Wear[r] = line.Wear[r];
            }
        foreach (var group in image.Batches)
        {
            var recipe = Set.Recipes[group.Recipe];
            var line = Set.Markets[group.Market].Lines[Set.LineOf[group.Market][group.Recipe]];
            for (var k = 0; k < group.Completes.Count; k++)
            {
                _batches.Add(new Batch(group.Market, group.Recipe, group.Runs[k], group.Completes[k]));
                for (var o = 0; o < recipe.Outputs.Count; o++) Markets[group.Market].Pairs[line.OutputPairs[o]].Incoming += group.Runs[k] * recipe.Outputs[o].Amount;
            }
        }
    }

    /// <summary>The price a pair had in each of the last periods that were kept, oldest first (fewer when fewer periods have been lived).</summary>
    public IReadOnlyList<long> PriceHistory(int market, int pair)
    {
        var history = Set.Limits.History;
        var count = (int)Math.Min(Period, history);
        var prices = new List<long>(count);
        for (var back = count - 1; back >= 0; back--) prices.Add(Markets[market].History[pair * history + (int)((Period - back) % history)]);
        return prices;
    }
}
