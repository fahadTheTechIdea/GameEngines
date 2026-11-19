using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// The one owner of moving stock from one holder to another through the inventory: a check of a whole set of moves against running balances and
/// the inventory own admission, and the commit of the set. The supply effect and the reinforce effect both go through it.
/// </summary>
public static class StockMoves
{
    private static ResourceMovementIntent Intent(StockMove move, StockTarget from, StockTarget to) =>
        new(new ReservationOwner("stock.moves", move.To), from.Holder.Owner, from.Stock.Endpoint, to.Stock.Endpoint,
            new Bill(new[] { new ResourceAmount(from.Resource, Fix.FromInt(move.Amount)) }),
            Array.Empty<ResourceReservation>(), Array.Empty<ResourceReservation>(), Array.Empty<ResourceReservation>(), ResourceMovementParticipantDisposition.Retain);

    private static Code Resolve(World world, StockMove move, out StockTarget from, out StockTarget to)
    {
        to = default;
        var refusal = StockTarget.Find(world, move.From, move.Resource, out from);
        if (!refusal.IsNone) return refusal;
        refusal = StockTarget.Find(world, move.To, move.Resource, out to);
        if (!refusal.IsNone) return refusal;
        return from.Holder.Owner == to.Holder.Owner ? Code.None : Codes.NotOwned;
    }

    /// <summary>Pure. Every move must fit what its source holds after the moves before it, and the inventory must admit it.</summary>
    public static Code Check(World world, IReadOnlyList<StockMove> moves)
    {
        var transactions = world.Services.Get<IResourceTransaction>();
        var taken = new Dictionary<(int Holder, ResourceId Resource), Fix>();
        foreach (var move in moves)
        {
            var refusal = Resolve(world, move, out var from, out var to);
            if (!refusal.IsNone) return refusal;
            var key = (move.From, from.Resource);
            taken.TryGetValue(key, out var already);
            var amount = Fix.FromInt(move.Amount);
            if (from.Stock.Amount(from.Resource) - already < amount) return StockCodes.Short;
            var check = transactions.EvaluateMovement(Intent(move, from, to));
            if (!check.Ready) return StockCodes.Refused(check.Status.ToString());
            taken[key] = already + amount;
        }
        return Code.None;
    }

    /// <summary>Commits a set that <see cref="Check"/> passed; a refusal here is a broken invariant, not a normal answer.</summary>
    public static void Apply(World world, IReadOnlyList<StockMove> moves)
    {
        var transactions = world.Services.Get<IResourceTransaction>();
        foreach (var move in moves)
        {
            var refusal = Resolve(world, move, out var from, out var to);
            if (!refusal.IsNone) throw new InvalidOperationException("a checked stock move no longer resolves: " + refusal);
            var result = transactions.CommitMovement(Intent(move, from, to));
            if (!result.Committed) throw new InvalidOperationException("a checked stock move was refused by the inventory: " + result.Status);
        }
    }
}
