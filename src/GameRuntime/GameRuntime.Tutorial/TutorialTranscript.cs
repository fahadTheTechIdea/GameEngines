namespace GameRuntime;

/// <summary>Every event the tutorial component raised on a session, as text and as one hash (see <see cref="EventTranscript"/>): a run, a resumed run and a replay can be compared by one value.</summary>
public sealed class TutorialTranscript : EventTranscript
{
    public TutorialTranscript(RuntimeSession session) : base(session)
    {
        Listen<TutorialApplied>();
        Listen<TutorialRefused>();
        Listen<TutorialProgressAnswered>();
        Listen<TutorialStepChanged>();
        Listen<TutorialHintChanged>();
        Listen<TutorialTipChanged>();
        Listen<TutorialSkillMastered>();
        Listen<TutorialPhaseChanged>();
        Listen<TutorialJourneyChanged>();
        Listen<TutorialSettingChanged>();
        Listen<TutorialSaveNeeded>();
    }
}
