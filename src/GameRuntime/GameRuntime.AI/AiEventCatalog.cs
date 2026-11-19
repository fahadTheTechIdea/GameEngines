namespace GameRuntime;

/// <summary>The AI component's events, declared once with their owner before the session's Init.</summary>
public static class AiEventCatalog
{
    public static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<AiAgentAddRequested>("ai.agent-add-requested", 1);
        session.DeclareEvent<AiAgentRemoveRequested>("ai.agent-remove-requested", 1);
        session.DeclareEvent<AiPerceptPosted>("ai.percept-posted", 1);
        session.DeclareEvent<AiPerceptLost>("ai.percept-lost", 1);
        session.DeclareEvent<AiKeyPosted>("ai.key-posted", 1);
        session.DeclareEvent<AiIntentRaised>("ai.intent-raised", 1);
        session.DeclareEvent<AiStepDone>("ai.step-done", 1);
        session.DeclareEvent<AiRefused>("ai.refused", 1);
    }
}