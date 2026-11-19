namespace GameRuntime;

/// <summary>The tutorial component's events, declared once, by <see cref="TutorialChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class TutorialEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<TutorialRequested>("tutorial.requested", 1);
        session.DeclareEvent<TutorialApplied>("tutorial.applied", 1);
        session.DeclareEvent<TutorialRefused>("tutorial.refused", 1);
        session.DeclareEvent<TutorialProgressAnswered>("tutorial.progress-answered", 1);
        session.DeclareEvent<TutorialStepChanged>("tutorial.step-changed", 1);
        session.DeclareEvent<TutorialHintChanged>("tutorial.hint-changed", 1);
        session.DeclareEvent<TutorialTipChanged>("tutorial.tip-changed", 1);
        session.DeclareEvent<TutorialSkillMastered>("tutorial.skill-mastered", 1);
        session.DeclareEvent<TutorialPhaseChanged>("tutorial.phase-changed", 1);
        session.DeclareEvent<TutorialJourneyChanged>("tutorial.journey-changed", 1);
        session.DeclareEvent<TutorialSettingChanged>("tutorial.setting-changed", 1);
        session.DeclareEvent<TutorialSaveNeeded>("tutorial.save-needed", 1);
    }
}
