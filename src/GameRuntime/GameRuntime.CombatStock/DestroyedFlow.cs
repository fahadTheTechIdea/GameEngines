using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>People a squad lost to enemy fire: destroyed by an outside force.</summary>
public sealed class DestroyedFlow : IStockFlow
{
    public string Name => "destroyed";
    public ResourceFlowKind Kind => ResourceFlowKind.Destroyed;
    public Code Cause { get; } = new("stock.casualty");
    public bool Everything => false;
}
