namespace GameRuntime;

/// <summary>The economy component's events, declared once, by <see cref="EconomyChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class EconomyEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<EconomyTradeRequested>("economy.trade-requested", 1);
        session.DeclareEvent<EconomyShockRequested>("economy.shock-requested", 1);
        session.DeclareEvent<EconomySettleRequested>("economy.settle-requested", 1);
        session.DeclareEvent<EconomyGrowthRequested>("economy.growth-requested", 1);
        session.DeclareEvent<EconomyFundRequested>("economy.fund-requested", 1);
        session.DeclareEvent<EconomyLendRequested>("economy.lend-requested", 1);
        session.DeclareEvent<EconomyQuoteRequested>("economy.quote-requested", 1);
        session.DeclareEvent<EconomyPriceRequested>("economy.price-requested", 1);
        session.DeclareEvent<EconomyRecipeUnlockRequested>("economy.recipe-unlock-requested", 1);
        session.DeclareEvent<EconomyApplied>("economy.applied", 1);
        session.DeclareEvent<EconomyRefused>("economy.refused", 1);
        session.DeclareEvent<EconomyTradeExecuted>("economy.trade-executed", 1);
        session.DeclareEvent<EconomyQuoted>("economy.quoted", 1);
        session.DeclareEvent<EconomyPriceAnswered>("economy.price-answered", 1);
        session.DeclareEvent<EconomyStepped>("economy.stepped", 1);
        session.DeclareEvent<EconomyRateChanged>("economy.rate-changed", 1);
        session.DeclareEvent<EconomyShockChanged>("economy.shock-changed", 1);
        session.DeclareEvent<EconomyShipmentArrived>("economy.shipment-arrived", 1);
        session.DeclareEvent<EconomyBandChanged>("economy.band-changed", 1);
        session.DeclareEvent<EconomyShortageChanged>("economy.shortage-changed", 1);
        session.DeclareEvent<EconomyShipmentLaunched>("economy.shipment-launched", 1);
        session.DeclareEvent<EconomyRouteBlocked>("economy.route-blocked", 1);
        session.DeclareEvent<EconomySettled>("economy.settled", 1);
        session.DeclareEvent<EconomyLoanChanged>("economy.loan-changed", 1);
        session.DeclareEvent<EconomyProductionLimited>("economy.production-limited", 1);
        session.DeclareEvent<EconomyRecipeUnlocked>("economy.recipe-unlocked", 1);
    }
}
