namespace GameRuntime;

/// <summary>The alchemy-sim components' events, declared once with their owner before the session's Init.</summary>
public static class AlchemySimEventCatalog
{
    public static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<OrderTransformationRequested>("alchemysim.order-requested", 1);
        session.DeclareEvent<TransformationOrderResolved>("alchemysim.order-resolved", 1);
    }
}
