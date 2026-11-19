namespace GameRuntime;

/// <summary>The diplomacy component's events, declared once, by <see cref="DiplomacyChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class DiplomacyEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<DiplomacyRequested>("diplomacy.requested", 1);
        session.DeclareEvent<DiplomacyApplied>("diplomacy.applied", 1);
        session.DeclareEvent<DiplomacyRefused>("diplomacy.refused", 1);
        session.DeclareEvent<DiplomacyEvaluated>("diplomacy.evaluated", 1);
        session.DeclareEvent<DiplomacyStandingAnswered>("diplomacy.standing-answered", 1);
        session.DeclareEvent<DiplomacyEstimated>("diplomacy.estimated", 1);
        session.DeclareEvent<DiplomacySeasonChanged>("diplomacy.season-changed", 1);
        session.DeclareEvent<DiplomacyProposalChanged>("diplomacy.proposal-changed", 1);
        session.DeclareEvent<DiplomacyTreatyChanged>("diplomacy.treaty-changed", 1);
        session.DeclareEvent<DiplomacyObligationChanged>("diplomacy.obligation-changed", 1);
        session.DeclareEvent<DiplomacyWarChanged>("diplomacy.war-changed", 1);
        session.DeclareEvent<DiplomacyCoalitionChanged>("diplomacy.coalition-changed", 1);
        session.DeclareEvent<DiplomacyMissionChanged>("diplomacy.mission-changed", 1);
        session.DeclareEvent<DiplomacyAgentChanged>("diplomacy.agent-changed", 1);
        session.DeclareEvent<DiplomacyIntelChanged>("diplomacy.intel-changed", 1);
        session.DeclareEvent<DiplomacyPoliticsChanged>("diplomacy.politics-changed", 1);
        session.DeclareEvent<DiplomacyGrantChanged>("diplomacy.grant-changed", 1);
        session.DeclareEvent<DiplomacyIncidentLogged>("diplomacy.incident-logged", 1);
        session.DeclareEvent<DiplomacyEffectIssued>("diplomacy.effect-issued", 1);
    }
}
