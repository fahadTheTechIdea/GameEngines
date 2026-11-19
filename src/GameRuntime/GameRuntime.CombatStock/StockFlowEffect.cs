using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// The game layer reports what happened to the stock of squads and depots (rounds fired, people lost, a group wiped out or routed) and the world
/// records each as the inventory own lifecycle flow on the holder storage. All lines of one request are checked first against a running balance, so
/// a request either applies completely or changes nothing. Payload: see <see cref="StockPayload"/>.
/// </summary>
public sealed class StockFlowEffect : IDirectorEffect
{
    public const string EffectName = "stock.flows";
    public string Name => EffectName;

    private static ResourceInventoryIntent Intent(StockLine line, StockTarget target, Fix amount) =>
        new(new ReservationOwner("stock.flows", line.Squad), target.Holder.Owner, target.Holder.Id,
            new Bill(new[] { new ResourceAmount(target.Resource, amount) }), line.Flow.Kind, line.Flow.Cause);

    public Code Check(World world, string payloadJson)
    {
        if (StockPayload.Read(payloadJson) is not { } lines) return Codes.InvalidArgument;
        var transactions = world.Services.Get<IResourceTransaction>();
        var held = new Dictionary<(int Squad, ResourceId Resource), Fix>();
        foreach (var line in lines)
        {
            var refusal = StockTarget.Find(world, line.Squad, line.Resource, out var target);
            if (!refusal.IsNone) return refusal;
            var key = (line.Squad, target.Resource);
            if (!held.TryGetValue(key, out var balance)) balance = target.Stock.Amount(target.Resource);
            var amount = line.Flow.Everything ? balance : Fix.FromInt(line.Amount);
            if (!amount.IsPositive) continue;
            var intent = Intent(line, target, amount);
            if (!intent.AddsStock && balance < amount) return StockCodes.Short;
            var check = transactions.EvaluateInventory(intent);
            if (!check.Ready) return StockCodes.Refused(check.Status.ToString());
            held[key] = intent.AddsStock ? balance + amount : balance - amount;
        }
        return Code.None;
    }

    public void Apply(World world, string payloadJson)
    {
        var lines = StockPayload.Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson);
        var transactions = world.Services.Get<IResourceTransaction>();
        foreach (var line in lines)
        {
            var refusal = StockTarget.Find(world, line.Squad, line.Resource, out var target);
            if (!refusal.IsNone) throw new InvalidOperationException("a checked stock flow no longer resolves: " + refusal);
            var amount = line.Flow.Everything ? target.Stock.Amount(target.Resource) : Fix.FromInt(line.Amount);
            if (!amount.IsPositive) continue;
            var result = transactions.CommitInventory(Intent(line, target, amount));
            if (!result.Committed) throw new InvalidOperationException("a checked stock flow was refused by the inventory: " + result.Status);
        }
    }
}
