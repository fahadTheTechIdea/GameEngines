namespace GameRuntime;

// Events of the dialogue component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged.
// Lists are joined by ';' and their parts by '|'. A line's text is never in an event: the key is, and the host renders it in its locale.
//
// Every event names a channel (empty by default). A game that runs more than one conversation at once, such as the player's and two characters talking in the background, gives each its own
// dialogue component with its own channel: a component answers the requests of its channel only and stamps its channel on everything it raises. One request gets one answer: from the component that serves its channel, or, when
// the game gave its components a DialogueChannels registry, a refusal with DLG_CHANNEL_UNKNOWN for a channel nobody serves.

/// <summary>A request to start a conversation. Only queued; the dialogue component answers it in its next Update.</summary>
public readonly struct DialogueStartRequested
{
    public DialogueStartRequested(string requestId, string conversation, string channel = "") { RequestId = requestId; Conversation = conversation; Channel = channel; }
    public string RequestId { get; }
    public string Conversation { get; }
    public string Channel { get; }
}

/// <summary>A request to go on after the line that is showing.</summary>
public readonly struct DialogueAdvanceRequested
{
    public DialogueAdvanceRequested(string requestId, string channel = "") { RequestId = requestId; Channel = channel; }
    public string RequestId { get; }
    public string Channel { get; }
}

/// <summary>A request to take one of the offered answers.</summary>
public readonly struct DialogueChooseRequested
{
    public DialogueChooseRequested(string requestId, string option, string channel = "") { RequestId = requestId; Option = option; Channel = channel; }
    public string RequestId { get; }
    public string Option { get; }
    public string Channel { get; }
}

/// <summary>A request to start a quest by id, outside any conversation.</summary>
public readonly struct DialogueQuestStartRequested
{
    public DialogueQuestStartRequested(string requestId, string quest, string channel = "") { RequestId = requestId; Quest = quest; Channel = channel; }
    public string RequestId { get; }
    public string Quest { get; }
    public string Channel { get; }
}

/// <summary>A request to let every active quest look at the facts the host holds now and move if its rules say so.</summary>
public readonly struct DialogueQuestsUpdateRequested
{
    public DialogueQuestsUpdateRequested(string requestId, string channel = "") { RequestId = requestId; Channel = channel; }
    public string RequestId { get; }
    public string Channel { get; }
}

/// <summary>
/// A request to put an edited dialogue document in place under the running session. With <paramref name="restart"/> a conversation that cannot go on in the new document is closed
/// instead of the reload being refused.
/// </summary>
public readonly struct DialogueReloadRequested
{
    public DialogueReloadRequested(string requestId, string document, bool restart = false, string map = "", string channel = "")
    { RequestId = requestId; Document = document; Restart = restart; Map = map; Channel = channel; }
    public string RequestId { get; }
    public string Document { get; }
    public bool Restart { get; }
    /// <summary>A <see cref="DialogueEngine.ReloadMap"/> document naming what the edit renamed, so the running state follows it; empty for none.</summary>
    public string Map { get; }
    public string Channel { get; }
}

/// <summary>A request to break into the open conversation, at its line or choice, with another conversation (it returns to where it was).</summary>
public readonly struct DialogueInterruptRequested
{
    public DialogueInterruptRequested(string requestId, string conversation, string channel = "") { RequestId = requestId; Conversation = conversation; Channel = channel; }
    public string RequestId { get; }
    public string Conversation { get; }
    public string Channel { get; }
}

/// <summary>A request that the open choice takes its timeout answer: the host's timer ran out.</summary>
public readonly struct DialogueTimeoutRequested
{
    public DialogueTimeoutRequested(string requestId, string channel = "") { RequestId = requestId; Channel = channel; }
    public string RequestId { get; }
    public string Channel { get; }
}

/// <summary>
/// A line to show. <see cref="Args"/> is <c>name=value</c> entries joined by ';' (a value is percent-encoded for '%', ';' and '='), the values taken when the line was reached. <see cref="Tags"/> are the
/// author's tags joined by ','; <see cref="Voice"/> and <see cref="Emotion"/> are ids (empty when the line has none); <see cref="AutoAdvanceMs"/> is how long a host that moves on by itself waits
/// (0: it does not); <see cref="Seen"/> is true when the player has been shown this line before (in this session or one saved and resumed), for a host that offers to skip what was read.
/// </summary>
public readonly struct DialogueLineShown
{
    public DialogueLineShown(string requestId, long tick, string conversation, string node, string speaker, string portrait, string key, string args,
        string tags = "", string voice = "", string emotion = "", int autoAdvanceMs = 0, bool seen = false, string channel = "", bool glue = false, bool replaced = false)
    {
        RequestId = requestId; Tick = tick; Conversation = conversation; Node = node; Speaker = speaker; Portrait = portrait; Key = key; Args = args;
        Tags = tags; Voice = voice; Emotion = emotion; AutoAdvanceMs = autoAdvanceMs; Seen = seen; Channel = channel; Glue = glue; Replaced = replaced;
    }
    public string Tags { get; }
    public string Voice { get; }
    public string Emotion { get; }
    public int AutoAdvanceMs { get; }
    public bool Seen { get; }
    /// <summary>The next line continues this one on the same output line.</summary>
    public bool Glue { get; }
    /// <summary>The key is the line's content-warning alternative, not its own text.</summary>
    public bool Replaced { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Conversation { get; }
    public string Node { get; }
    public string Speaker { get; }
    public string Portrait { get; }
    public string Key { get; }
    public string Args { get; }
}

/// <summary>
/// Answers to offer. <see cref="Options"/> is <c>id|key|enabled|tags|replaced</c> entries (<c>replaced</c> is 1 when the key is a content-warning alternative) joined by ';' (tags joined by ','). A choice with a timeout names it: the host starts a timer of
/// <see cref="TimeoutMs"/> and, if it runs out, requests the timeout; <see cref="TimeoutOption"/> is the answer that will be taken (empty: none, 0 ms).
/// </summary>
public readonly struct DialogueChoicesShown
{
    public DialogueChoicesShown(string requestId, long tick, string conversation, string node, string options, int timeoutMs = 0, string timeoutOption = "", string channel = "")
    { RequestId = requestId; Tick = tick; Conversation = conversation; Node = node; Options = options; TimeoutMs = timeoutMs; TimeoutOption = timeoutOption; Channel = channel; }
    public int TimeoutMs { get; }
    public string TimeoutOption { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Conversation { get; }
    public string Node { get; }
    public string Options { get; }
}

/// <summary>The conversation ended with this result.</summary>
public readonly struct DialogueEnded
{
    public DialogueEnded(string requestId, long tick, string conversation, string result, string channel = "") { RequestId = requestId; Tick = tick; Conversation = conversation; Result = result; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Conversation { get; }
    public string Result { get; }
    public string Channel { get; }
}

/// <summary>A signal a conversation raised for the host to act on.</summary>
public readonly struct DialogueSignalRaised
{
    public DialogueSignalRaised(string requestId, long tick, string name, string channel = "", string args = "") { RequestId = requestId; Tick = tick; Name = name; Channel = channel; Args = args; }
    /// <summary>The signal's arguments in order, joined by ';', each <c>i:5</c>, <c>b:true</c> or <c>s:text</c> (text percent-encoded for '%' and ';'); empty for none.</summary>
    public string Args { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Name { get; }
    public string Channel { get; }
}

/// <summary>A quest started, changed stage or ended.</summary>
public readonly struct DialogueQuestChanged
{
    public DialogueQuestChanged(string requestId, long tick, string quest, string status, string stage, string channel = "") { RequestId = requestId; Tick = tick; Quest = quest; Status = status; Stage = stage; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Quest { get; }
    public string Status { get; }
    public string Stage { get; }
    public string Channel { get; }
}

/// <summary>A dialogue variable changed value. <see cref="Value"/> is written in the invariant culture.</summary>
public readonly struct DialogueVariableChanged
{
    public DialogueVariableChanged(string requestId, long tick, string id, string value, string channel = "") { RequestId = requestId; Tick = tick; Id = id; Value = value; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Id { get; }
    public string Value { get; }
    public string Channel { get; }
}

/// <summary>An edited document was accepted under the running session.</summary>
public readonly struct DialogueReloaded
{
    public DialogueReloaded(string requestId, long tick, string addedVariables, string addedQuests, int droppedVisits, string closedConversation = "", int renamedVisits = 0, string channel = "", int droppedHistory = 0, int droppedQueued = 0)
    { RequestId = requestId; Tick = tick; AddedVariables = addedVariables; AddedQuests = addedQuests; DroppedVisits = droppedVisits; ClosedConversation = closedConversation; RenamedVisits = renamedVisits; Channel = channel; DroppedHistory = droppedHistory; DroppedQueued = droppedQueued; }
    public int DroppedHistory { get; }
    public int DroppedQueued { get; }
    /// <summary>How many visit counts the reload map moved to a new node name.</summary>
    public int RenamedVisits { get; }
    /// <summary>Where the open conversation was (<c>conversation/node</c>) when the reload had to close it; empty when it went on.</summary>
    public string ClosedConversation { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string AddedVariables { get; }
    public string AddedQuests { get; }
    public int DroppedVisits { get; }
    public string Channel { get; }
}

/// <summary>A request was refused with this code; the dialogue state is exactly as it was.</summary>
public readonly struct DialogueRefused
{
    public DialogueRefused(string requestId, long tick, string code, string channel = "") { RequestId = requestId; Tick = tick; Code = code; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
    public string Channel { get; }
}

/// <summary>A request that the lines the player has already seen are passed, from the line showing, up to the first line not yet seen, a choice or the end.</summary>
public readonly struct DialogueSkipRequested
{
    public DialogueSkipRequested(string requestId, string channel = "") { RequestId = requestId; Channel = channel; }
    public string RequestId { get; }
    public string Channel { get; }
}

/// <summary>A request to list the kept history: the newest <see cref="Count"/> entries (0: all), only the ones that were new to the player when <see cref="NewOnly"/>.</summary>
public readonly struct DialogueHistoryRequested
{
    public DialogueHistoryRequested(string requestId, int count = 0, bool newOnly = false, string channel = "") { RequestId = requestId; Count = count; NewOnly = newOnly; Channel = channel; }
    public string RequestId { get; }
    public int Count { get; }
    public bool NewOnly { get; }
    public string Channel { get; }
}

/// <summary>The skip rule passed this many already-seen lines.</summary>
public readonly struct DialogueSkipped
{
    public DialogueSkipped(string requestId, long tick, int count, string channel = "") { RequestId = requestId; Tick = tick; Count = count; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public int Count { get; }
    public string Channel { get; }
}

/// <summary>
/// A conversation was queued (<see cref="Change"/> <c>queued</c>, <see cref="Position"/> its place), started from the queue (<c>started</c>), broken into by <see cref="Other"/> (<c>preempted</c>),
/// put back after it (<c>resumed</c>), or finished while something followed it (<c>ended</c>; <see cref="Other"/> is its result).
/// </summary>
public readonly struct DialogueConversationChanged
{
    public DialogueConversationChanged(string requestId, long tick, string change, string conversation, string other, int position, string channel = "")
    { RequestId = requestId; Tick = tick; Change = change; Conversation = conversation; Other = other; Position = position; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Change { get; }
    public string Conversation { get; }
    public string Other { get; }
    public int Position { get; }
    public string Channel { get; }
}

/// <summary>
/// One entry of the kept history, listed in answer to a <see cref="DialogueHistoryRequested"/> (oldest first, <see cref="Index"/> from 0). <see cref="Kind"/> is <c>line</c> or <c>answer</c>;
/// <see cref="Args"/> are written like those of a line; <see cref="Chosen"/> is the answer taken.
/// </summary>
public readonly struct DialogueHistoryEntryListed
{
    public DialogueHistoryEntryListed(string requestId, long tick, int index, long turn, long shownTick, string kind, string conversation, string node, string speaker, string key, string args, string chosen, bool seen, bool skipped, bool replaced, string channel = "")
    {
        RequestId = requestId; Tick = tick; Index = index; Turn = turn; ShownTick = shownTick; Kind = kind; Conversation = conversation; Node = node; Speaker = speaker; Key = key; Args = args;
        Chosen = chosen; Seen = seen; Skipped = skipped; Replaced = replaced; Channel = channel;
    }
    public string RequestId { get; }
    public long Tick { get; }
    public int Index { get; }
    public long Turn { get; }
    public long ShownTick { get; }
    public string Kind { get; }
    public string Conversation { get; }
    public string Node { get; }
    public string Speaker { get; }
    public string Key { get; }
    public string Args { get; }
    public string Chosen { get; }
    public bool Seen { get; }
    public bool Skipped { get; }
    public bool Replaced { get; }
    public string Channel { get; }
}

/// <summary>The end of a history listing: how many entries were listed and how many the session keeps in all.</summary>
public readonly struct DialogueHistoryListed
{
    public DialogueHistoryListed(string requestId, long tick, int listed, int kept, string channel = "") { RequestId = requestId; Tick = tick; Listed = listed; Kept = kept; Channel = channel; }
    public string RequestId { get; }
    public long Tick { get; }
    public int Listed { get; }
    public int Kept { get; }
    public string Channel { get; }
}
