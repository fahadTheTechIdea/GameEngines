using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>Everything a squad still held when its group was destroyed: nobody is left to carry it, so it is lost.</summary>
public sealed class AbandonedFlow : IStockFlow
{
    public string Name => "abandoned";
    public ResourceFlowKind Kind => ResourceFlowKind.Lost;
    public Code Cause { get; } = new("stock.abandoned");
    public bool Everything => true;
}
