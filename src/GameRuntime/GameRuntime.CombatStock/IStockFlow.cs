using Beep.SimEngine;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// One kind of movement of a squad's stock, and the engine's name for it. Each kind is its own class; the shared effect knows only this
/// contract, so a new kind is a new class and a registration, never a branch in shared code.
/// </summary>
public interface IStockFlow
{
    /// <summary>The kind's name in a payload.</summary>
    string Name { get; }

    /// <summary>The engine's lifecycle kind the inventory records the flow as.</summary>
    ResourceFlowKind Kind { get; }

    /// <summary>The cause the inventory records with the flow.</summary>
    Code Cause { get; }

    /// <summary>True when the flow takes whatever the squad holds; its amount is then written as 0.</summary>
    bool Everything { get; }
}
