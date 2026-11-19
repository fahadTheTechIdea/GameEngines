using Beep.SimEngine;

namespace GameRuntime;

/// <summary>The refusals of the stock-flow layer, beside the engine's own codes.</summary>
public static class StockCodes
{
    /// <summary>A flow would take more than the squad's ledger holds.</summary>
    public static readonly Code Short = new("stock.short");

    /// <summary>The inventory refused a flow that was otherwise well formed; the status is appended.</summary>
    public static Code Refused(string status) => new("stock.refused." + status.ToLowerInvariant());
}
