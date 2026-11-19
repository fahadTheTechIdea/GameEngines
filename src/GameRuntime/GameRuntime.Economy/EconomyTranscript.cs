namespace GameRuntime;

/// <summary>Every event the economy component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class EconomyTranscript : EventTranscript
{
    public EconomyTranscript(RuntimeSession session) : base(session)
    {
        Listen<EconomyApplied>();
        Listen<EconomyRefused>();
        Listen<EconomyTradeExecuted>();
        Listen<EconomyQuoted>();
        Listen<EconomyPriceAnswered>();
        Listen<EconomyStepped>();
        Listen<EconomyRateChanged>();
        Listen<EconomyShockChanged>();
        Listen<EconomyShipmentArrived>();
        Listen<EconomyBandChanged>();
        Listen<EconomyShortageChanged>();
        Listen<EconomyShipmentLaunched>();
        Listen<EconomyRouteBlocked>();
        Listen<EconomySettled>();
        Listen<EconomyLoanChanged>();
        Listen<EconomyProductionLimited>();
        Listen<EconomyRecipeUnlocked>();
    }
}
