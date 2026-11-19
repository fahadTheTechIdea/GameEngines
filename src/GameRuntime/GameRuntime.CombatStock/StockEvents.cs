namespace GameRuntime;

/// <summary>Every squad of the field has been raised and the roster answers for all of them: the stock layer issues their starting stock.</summary>
public readonly struct SquadsRaised
{
    public SquadsRaised(long tick) { Tick = tick; }
    public long Tick { get; }
}

/// <summary>The stock-flow layer's events, declared once with their owner before the session's Init.</summary>
public static class StockEventCatalog
{
    public static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<SquadsRaised>("stock.squads-raised", 1);
    }
}
