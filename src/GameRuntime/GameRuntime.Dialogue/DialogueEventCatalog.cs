namespace GameRuntime;

/// <summary>The dialogue component's events, declared once, by <see cref="DialogueChannels.Install"/> only, before the session's Init. A version is bumped when an event's fields change.</summary>
internal static class DialogueEventCatalog
{
    internal static void Declare(RuntimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.DeclareEvent<DialogueStartRequested>("dialogue.start-requested", 2);
        session.DeclareEvent<DialogueAdvanceRequested>("dialogue.advance-requested", 2);
        session.DeclareEvent<DialogueChooseRequested>("dialogue.choose-requested", 2);
        session.DeclareEvent<DialogueQuestStartRequested>("dialogue.quest-start-requested", 2);
        session.DeclareEvent<DialogueQuestsUpdateRequested>("dialogue.quests-update-requested", 2);
        session.DeclareEvent<DialogueReloadRequested>("dialogue.reload-requested", 2);
        session.DeclareEvent<DialogueInterruptRequested>("dialogue.interrupt-requested", 2);
        session.DeclareEvent<DialogueTimeoutRequested>("dialogue.timeout-requested", 2);
        session.DeclareEvent<DialogueSkipRequested>("dialogue.skip-requested", 1);
        session.DeclareEvent<DialogueHistoryRequested>("dialogue.history-requested", 1);
        session.DeclareEvent<DialogueLineShown>("dialogue.line-shown", 3);
        session.DeclareEvent<DialogueChoicesShown>("dialogue.choices-shown", 3);
        session.DeclareEvent<DialogueEnded>("dialogue.ended", 2);
        session.DeclareEvent<DialogueSignalRaised>("dialogue.signal-raised", 3);
        session.DeclareEvent<DialogueQuestChanged>("dialogue.quest-changed", 2);
        session.DeclareEvent<DialogueVariableChanged>("dialogue.variable-changed", 2);
        session.DeclareEvent<DialogueReloaded>("dialogue.reloaded", 3);
        session.DeclareEvent<DialogueRefused>("dialogue.refused", 2);
        session.DeclareEvent<DialogueSkipped>("dialogue.skipped", 1);
        session.DeclareEvent<DialogueConversationChanged>("dialogue.conversation-changed", 1);
        session.DeclareEvent<DialogueHistoryEntryListed>("dialogue.history-entry-listed", 1);
        session.DeclareEvent<DialogueHistoryListed>("dialogue.history-listed", 1);
    }
}
