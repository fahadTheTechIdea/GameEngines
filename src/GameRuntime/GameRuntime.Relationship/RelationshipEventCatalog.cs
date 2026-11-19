namespace GameRuntime;

/// <summary>The relationship component's events, declared once, by <see cref="RelationshipChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class RelationshipEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<RelationshipActorAddRequested>("relationship.actor-add-requested", 1);
        session.DeclareEvent<RelationshipActorRemoveRequested>("relationship.actor-remove-requested", 1);
        session.DeclareEvent<RelationshipActivityRequested>("relationship.activity-requested", 1);
        session.DeclareEvent<RelationshipInteractRequested>("relationship.interact-requested", 1);
        session.DeclareEvent<RelationshipGrantRequested>("relationship.grant-requested", 1);
        session.DeclareEvent<RelationshipRememberRequested>("relationship.remember-requested", 1);
        session.DeclareEvent<RelationshipNeedRequested>("relationship.need-requested", 1);
        session.DeclareEvent<RelationshipAxisRequested>("relationship.axis-requested", 1);
        session.DeclareEvent<RelationshipBondRequested>("relationship.bond-requested", 1);
        session.DeclareEvent<RelationshipMoodRequested>("relationship.mood-requested", 1);
        session.DeclareEvent<RelationshipOpinionRequested>("relationship.opinion-requested", 1);
        session.DeclareEvent<RelationshipApplied>("relationship.applied", 1);
        session.DeclareEvent<RelationshipRefused>("relationship.refused", 1);
        session.DeclareEvent<RelationshipInteractionPerformed>("relationship.interaction-performed", 1);
        session.DeclareEvent<RelationshipMoodAnswered>("relationship.mood-answered", 1);
        session.DeclareEvent<RelationshipOpinionAnswered>("relationship.opinion-answered", 1);
        session.DeclareEvent<RelationshipStepped>("relationship.stepped", 1);
        session.DeclareEvent<RelationshipMoodChanged>("relationship.mood-changed", 1);
        session.DeclareEvent<RelationshipNeedCritical>("relationship.need-critical", 1);
        session.DeclareEvent<RelationshipStateChanged>("relationship.state-changed", 1);
        session.DeclareEvent<RelationshipBondChanged>("relationship.bond-changed", 1);
        session.DeclareEvent<RelationshipMemoryAdded>("relationship.memory-added", 1);
        session.DeclareEvent<RelationshipMoodletAdded>("relationship.moodlet-added", 1);
        session.DeclareEvent<RelationshipEvicted>("relationship.evicted", 1);
        session.DeclareEvent<RelationshipCommunityChanged>("relationship.community-changed", 1);
        session.DeclareEvent<RelationshipStandingChanged>("relationship.standing-changed", 1);
        session.DeclareEvent<RelationshipBalanceShifted>("relationship.balance-shifted", 1);
    }
}
