using EconomyEngine;

namespace GameRuntime;

public sealed partial class EconomyComponent
{
    /// <summary>What the changes of a call or a period become as events, in a fixed order: rates, shocks, arrivals, bands, shortages, launches, blocks, settlements, loans. Each is built now (the names are read now) and raised later.</summary>
    private List<Action<IEventPort>> EventsOf(long tick, EconomyChanges changes)
    {
        var set = Runtime.Set;
        var events = new List<Action<IEventPort>>();
        foreach (var c in changes.Rates)
        {
            var e = new EconomyRateChanged(Id, Channel, tick, set.Currencies[c.Currency].Id, c.From, c.To);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Shocks)
        {
            var e = new EconomyShockChanged(Id, Channel, tick, set.Shocks[c.Shock].Id, c.Market < 0 ? "" : set.Markets[c.Market].Id, c.Started);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Arrivals)
        {
            var e = new EconomyShipmentArrived(Id, Channel, tick, set.Routes[c.Route].Id, set.Goods[c.Good].Id, c.Quantity);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Bands)
        {
            var e = new EconomyBandChanged(Id, Channel, tick, set.Markets[c.Market].Id, set.Goods[c.Good].Id, set.Bands[c.From].Id, set.Bands[c.To].Id);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Shortages)
        {
            var e = new EconomyShortageChanged(Id, Channel, tick, set.Markets[c.Market].Id, set.Goods[c.Good].Id, c.Entered);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Launches)
        {
            var e = new EconomyShipmentLaunched(Id, Channel, tick, set.Routes[c.Route].Id, set.Goods[c.Good].Id, c.Quantity, c.Arrives);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Blocks)
        {
            var e = new EconomyRouteBlocked(Id, Channel, tick, set.Routes[c.Route].Id, set.Goods[c.Good].Id);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Settlements)
        {
            var e = new EconomySettled(Id, Channel, tick, set.Markets[c.Market].Id, c.Kind.ToString().ToLowerInvariant(), c.Id, c.Paid, c.Due);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Loans)
        {
            var e = new EconomyLoanChanged(Id, Channel, tick, c.Loan, c.Outcome.ToString().ToLowerInvariant(), c.Balance);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Limits)
        {
            var e = new EconomyProductionLimited(Id, Channel, tick, set.Markets[c.Market].Id, set.Recipes[c.Recipe].Id, c.From.ToString().ToLowerInvariant(), c.To.ToString().ToLowerInvariant(), c.Good < 0 ? "" : set.Goods[c.Good].Id);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Unlocks)
        {
            var e = new EconomyRecipeUnlocked(Id, Channel, tick, set.Recipes[c.Recipe].Id);
            events.Add(port => port.Emit(e));
        }
        return events;
    }

    /// <summary>The changes of a request are raised now, with the answer, in the Update that answered it.</summary>
    private void Raise(IEventPort events, long tick, EconomyChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) raise(events);
    }

    /// <summary>The changes of the periods are owed: raised at most <c>maxEvents</c> per Update, in order, oldest first.</summary>
    private void Owe(long tick, EconomyChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) _owed.Enqueue(raise);
    }
}
