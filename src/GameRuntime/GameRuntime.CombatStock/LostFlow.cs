using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>A stated quantity that left the squad's control without being used or destroyed.</summary>
public sealed class LostFlow : IStockFlow
{
    public string Name => "lost";
    public ResourceFlowKind Kind => ResourceFlowKind.Lost;
    public Code Cause { get; } = new("stock.lost");
    public bool Everything => false;
}
