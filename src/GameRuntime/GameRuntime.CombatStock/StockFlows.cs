namespace GameRuntime;

/// <summary>The registry of stock-flow kinds: the one list the effect and the bridge look kinds up in.</summary>
public static class StockFlows
{
    public static readonly IssuedFlow Issued = new();
    public static readonly ConsumedFlow Consumed = new();
    public static readonly DestroyedFlow Destroyed = new();
    public static readonly LostFlow Lost = new();
    public static readonly AbandonedFlow Abandoned = new();
    public static readonly WithdrawnFlow Withdrawn = new();

    private static readonly IStockFlow[] All = [Issued, Consumed, Destroyed, Lost, Abandoned, Withdrawn];

    public static bool TryGet(string name, out IStockFlow flow)
    {
        foreach (var candidate in All)
            if (candidate.Name == name) { flow = candidate; return true; }
        flow = null!;
        return false;
    }
}
