using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>Stock a squad starts with: its people and the rounds its weapons carry.</summary>
public sealed class IssuedFlow : IStockFlow
{
    public string Name => "issued";
    public ResourceFlowKind Kind => ResourceFlowKind.Initialized;
    public Code Cause { get; } = new("stock.issued");
    public bool Everything => false;
}
