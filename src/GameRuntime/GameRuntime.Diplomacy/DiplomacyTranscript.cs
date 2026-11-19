namespace GameRuntime;

/// <summary>Every event the diplomacy component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class DiplomacyTranscript : EventTranscript
{
    public DiplomacyTranscript(RuntimeSession session) : base(session)
    {
        Listen<DiplomacyApplied>();
        Listen<DiplomacyRefused>();
        Listen<DiplomacyEvaluated>();
        Listen<DiplomacyStandingAnswered>();
        Listen<DiplomacyEstimated>();
        Listen<DiplomacySeasonChanged>();
        Listen<DiplomacyProposalChanged>();
        Listen<DiplomacyTreatyChanged>();
        Listen<DiplomacyObligationChanged>();
        Listen<DiplomacyWarChanged>();
        Listen<DiplomacyCoalitionChanged>();
        Listen<DiplomacyMissionChanged>();
        Listen<DiplomacyAgentChanged>();
        Listen<DiplomacyIntelChanged>();
        Listen<DiplomacyPoliticsChanged>();
        Listen<DiplomacyGrantChanged>();
        Listen<DiplomacyIncidentLogged>();
        Listen<DiplomacyEffectIssued>();
    }
}
