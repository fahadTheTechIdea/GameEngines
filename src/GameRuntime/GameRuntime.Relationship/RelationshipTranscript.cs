namespace GameRuntime;

/// <summary>Every event the relationship component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class RelationshipTranscript : EventTranscript
{
    public RelationshipTranscript(RuntimeSession session) : base(session)
    {
        Listen<RelationshipApplied>();
        Listen<RelationshipRefused>();
        Listen<RelationshipInteractionPerformed>();
        Listen<RelationshipMoodAnswered>();
        Listen<RelationshipOpinionAnswered>();
        Listen<RelationshipStepped>();
        Listen<RelationshipMoodChanged>();
        Listen<RelationshipNeedCritical>();
        Listen<RelationshipStateChanged>();
        Listen<RelationshipBondChanged>();
        Listen<RelationshipMemoryAdded>();
        Listen<RelationshipMoodletAdded>();
        Listen<RelationshipEvicted>();
        Listen<RelationshipCommunityChanged>();
        Listen<RelationshipStandingChanged>();
        Listen<RelationshipBalanceShifted>();
    }
}
