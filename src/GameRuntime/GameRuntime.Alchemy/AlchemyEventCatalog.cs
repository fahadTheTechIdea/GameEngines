namespace GameRuntime;

/// <summary>The alchemy component's events, declared once with their owner before the session's Init.</summary>
public static class AlchemyEventCatalog
{
    public static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<CombineRequested>("alchemy.combine-requested", 1);
        session.DeclareEvent<RecipeLearnedExternally>("alchemy.recipe-learned-externally", 1);
        session.DeclareEvent<Combined>("alchemy.combined", 1);
        session.DeclareEvent<CombineRefused>("alchemy.combine-refused", 1);
        session.DeclareEvent<DiscoveryMade>("alchemy.discovery-made", 1);
        session.DeclareEvent<KnowledgeRestored>("alchemy.knowledge-restored", 1);
    }
}
