using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>Rounds a squad fired: used up by doing what they are for.</summary>
public sealed class ConsumedFlow : IStockFlow
{
    public string Name => "consumed";
    public ResourceFlowKind Kind => ResourceFlowKind.Consumed;
    public Code Cause { get; } = new("stock.fired");
    public bool Everything => false;
}
