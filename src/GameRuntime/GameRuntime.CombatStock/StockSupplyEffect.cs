using Beep.SimEngine;
using Beep.SimEngine.Kernel;

namespace GameRuntime;

/// <summary>Stock goes from a depot to the squads that took it in (rounds a battery accepted). Payload: see <see cref="StockMovePayload"/>.</summary>
public sealed class StockSupplyEffect : IDirectorEffect
{
    public const string EffectName = "stock.supply";
    public string Name => EffectName;

    public Code Check(World world, string payloadJson) =>
        StockMovePayload.Read(payloadJson) is { } moves ? StockMoves.Check(world, moves) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson) =>
        StockMoves.Apply(world, StockMovePayload.Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson));
}
