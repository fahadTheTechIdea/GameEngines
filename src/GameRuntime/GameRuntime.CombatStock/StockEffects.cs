namespace GameRuntime;

/// <summary>The director effects of the stock-flow layer, as the one list a game registers with its director bridge.</summary>
public static class StockEffects
{
    public static IDirectorEffect[] All() => [new StockFlowEffect(), new StockSupplyEffect(), new StockReinforceEffect()];
}
