using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// A squad takes in people from a depot: the people move in the ledger and the character factory adds the same number of members to the squad,
/// as one step that happens completely or not at all. The members join the roster entries of the squad formation in its proportions. Payload: <c>{"depot":3,"squad":7,"count":12}</c>.
/// </summary>
public sealed class StockReinforceEffect : IDirectorEffect
{
    public const string EffectName = "stock.reinforce";
    public string Name => EffectName;

    private static StockMove? Read(string payloadJson)
    {
        if (EffectPayload.Parse(payloadJson, "depot", "squad", "count") is not { } root
            || !EffectPayload.Int(root, "depot", out var depot) || !EffectPayload.Int(root, "squad", out var squad)
            || !EffectPayload.Int(root, "count", out var count)) return null;
        if (depot <= 0 || squad <= 0 || depot == squad || count <= 0) return null;
        return new StockMove(depot, squad, StockResources.People, count);
    }

    public Code Check(World world, string payloadJson)
    {
        if (Read(payloadJson) is not { } read) return Codes.InvalidArgument;
        var refusal = world.Services.Get<ICharacterFactory>().CheckReinforceByRoster(new EntityId(read.To), read.Amount);
        return refusal.IsNone ? StockMoves.Check(world, [read]) : refusal;
    }

    public void Apply(World world, string payloadJson)
    {
        var read = Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson);
        StockMoves.Apply(world, [read]);
        world.Services.Get<ICharacterFactory>().ReinforceByRoster(new EntityId(read.To), read.Amount);
    }
}
