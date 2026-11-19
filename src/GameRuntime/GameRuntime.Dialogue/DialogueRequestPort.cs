namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests and the registry that refuses the unserved ones cannot disagree about which requests exist.</summary>
internal enum RequestKind { Start, Advance, Choose, QuestStart, QuestsUpdate, Reload, ReloadRestart, Interrupt, Timeout, Skip, History }

/// <summary>One queued request. <see cref="Argument"/> is the conversation, option, quest or document, whichever the kind takes; <see cref="Number"/> and <see cref="Flag"/> belong to the history request.</summary>
internal readonly record struct Pending(RequestKind Kind, string RequestId, string Argument, string Map = "", int Number = 0, bool Flag = false);

/// <summary>The one place every dialogue request event is connected: the handler gets the request's channel and the request as a <see cref="Pending"/>.</summary>
internal static class DialogueRequestPort
{
    public static void Connect(IEventPort events, Action<string, Pending> handler)
    {
        events.Connect<DialogueStartRequested>(r => handler(r.Channel, new Pending(RequestKind.Start, r.RequestId, r.Conversation)));
        events.Connect<DialogueAdvanceRequested>(r => handler(r.Channel, new Pending(RequestKind.Advance, r.RequestId, "")));
        events.Connect<DialogueChooseRequested>(r => handler(r.Channel, new Pending(RequestKind.Choose, r.RequestId, r.Option)));
        events.Connect<DialogueQuestStartRequested>(r => handler(r.Channel, new Pending(RequestKind.QuestStart, r.RequestId, r.Quest)));
        events.Connect<DialogueQuestsUpdateRequested>(r => handler(r.Channel, new Pending(RequestKind.QuestsUpdate, r.RequestId, "")));
        events.Connect<DialogueReloadRequested>(r => handler(r.Channel, new Pending(r.Restart ? RequestKind.ReloadRestart : RequestKind.Reload, r.RequestId, r.Document, r.Map)));
        events.Connect<DialogueInterruptRequested>(r => handler(r.Channel, new Pending(RequestKind.Interrupt, r.RequestId, r.Conversation)));
        events.Connect<DialogueTimeoutRequested>(r => handler(r.Channel, new Pending(RequestKind.Timeout, r.RequestId, "")));
        events.Connect<DialogueSkipRequested>(r => handler(r.Channel, new Pending(RequestKind.Skip, r.RequestId, "")));
        events.Connect<DialogueHistoryRequested>(r => handler(r.Channel, new Pending(RequestKind.History, r.RequestId, "", "", r.Count, r.NewOnly)));
    }
}
