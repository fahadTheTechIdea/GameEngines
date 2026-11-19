namespace GameRuntime;

/// <summary>The combat component's events, declared once with their owner before the session's Init.</summary>
public static class CombatEventCatalog
{
    public static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<EngagementRequested>("combat.engagement-requested", 2);
        session.DeclareEvent<RoundRequested>("combat.round-requested", 1);
        session.DeclareEvent<MoveRequested>("combat.move-requested", 1);
        session.DeclareEvent<ResupplyRequested>("combat.resupply-requested", 1);
        session.DeclareEvent<Engagement>("combat.engagement", 1);
        session.DeclareEvent<Casualty>("combat.casualty", 1);
        session.DeclareEvent<AmmoSpent>("combat.ammo-spent", 1);
        session.DeclareEvent<PlatformsLost>("combat.platforms-lost", 1);
        session.DeclareEvent<Moved>("combat.moved", 1);
        session.DeclareEvent<Resupplied>("combat.resupplied", 1);
        session.DeclareEvent<ReinforceRequested>("combat.reinforce-requested", 1);
        session.DeclareEvent<Reinforced>("combat.reinforced", 1);
        session.DeclareEvent<RefuelRequested>("combat.refuel-requested", 1);
        session.DeclareEvent<Refuelled>("combat.refuelled", 1);
        session.DeclareEvent<FuelBurned>("combat.fuel-burned", 1);
        session.DeclareEvent<EngagementRefused>("combat.engagement-refused", 1);
        session.DeclareEvent<Standing>("combat.standing", 2);
        session.DeclareEvent<ForcesRestored>("combat.forces-restored", 2);
    }
}
