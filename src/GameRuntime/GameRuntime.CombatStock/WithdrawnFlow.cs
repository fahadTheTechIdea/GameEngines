using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>Everything a squad still held when its group routed: the group is out of the fight for good and no longer under command, so its people and rounds are lost to the army.</summary>
public sealed class WithdrawnFlow : IStockFlow
{
    public string Name => "withdrawn";
    public ResourceFlowKind Kind => ResourceFlowKind.Lost;
    public Code Cause { get; } = new("stock.withdrawn");
    public bool Everything => true;
}
