namespace GameRuntime;

// Events of the relationship component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. Names are ids, never text: the host renders them in its locale with RelationshipNames.
// Lists are joined by ';' (traits, witnesses, tastes as tag=level).
//
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. One request gets one answer: from the component that serves its channel, or, from the
// RelationshipChannels registry, a refusal with REL_CHANNEL_UNKNOWN for a channel nobody serves.
//
// Order of what one Update raises: the answers to the requests in arrival order (each followed by the changes it made), then Stepped when time moved, then what the steps changed (moods, needs, states, bonds,
// memories, moodlets, evictions, then at a day boundary community changes, standing changes and balance pushes), at most the component's maximum per Update; the rest is owed and follows in the next Updates, in the same order, none lost. Everything is deferred: it is delivered after the Update returns.

/// <summary>A request to add an actor with its trait ids and its gift tastes (<c>tag=level</c>, level -2 hate to 2 love). Only queued.</summary>
public readonly struct RelationshipActorAddRequested
{
    public RelationshipActorAddRequested(string requestId, string actor, string traits, string tastes = "", string channel = "") { RequestId = requestId; Actor = actor; Traits = traits; Tastes = tastes; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Traits { get; }
    public string Tastes { get; }
    public string Channel { get; }
}

/// <summary>A request to remove an actor and every opinion of it.</summary>
public readonly struct RelationshipActorRemoveRequested
{
    public RelationshipActorRemoveRequested(string requestId, string actor, string channel = "") { RequestId = requestId; Actor = actor; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Channel { get; }
}

/// <summary>A request to say what an actor is doing now (the activity of its schedule block); empty for nothing.</summary>
public readonly struct RelationshipActivityRequested
{
    public RelationshipActivityRequested(string requestId, string actor, string activity, string channel = "") { RequestId = requestId; Actor = actor; Activity = activity; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Activity { get; }
    public string Channel { get; }
}

/// <summary>A request that an actor does an interaction to a target, with witnesses and a gift item when the interaction is a gift.</summary>
public readonly struct RelationshipInteractRequested
{
    public RelationshipInteractRequested(string requestId, string interaction, string actor, string target, string witnesses = "", string gift = "", string channel = "")
    { RequestId = requestId; Interaction = interaction; Actor = actor; Target = target; Witnesses = witnesses; Gift = gift; Channel = channel; }
    public string RequestId { get; }
    public string Interaction { get; }
    public string Actor { get; }
    public string Target { get; }
    public string Witnesses { get; }
    public string Gift { get; }
    public string Channel { get; }
}

/// <summary>A request to give an actor a mood modifier (a story event: the festival begins).</summary>
public readonly struct RelationshipGrantRequested
{
    public RelationshipGrantRequested(string requestId, string actor, string moodlet, int scalePermille = 1000, string source = "", string channel = "")
    { RequestId = requestId; Actor = actor; Moodlet = moodlet; ScalePermille = scalePermille; Source = source; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Moodlet { get; }
    public int ScalePermille { get; }
    public string Source { get; }
    public string Channel { get; }
}

/// <summary>A request that the holder remembers something about the subject.</summary>
public readonly struct RelationshipRememberRequested
{
    public RelationshipRememberRequested(string requestId, string holder, string subject, string memory, int scalePermille = 1000, string channel = "")
    { RequestId = requestId; Holder = holder; Subject = subject; Memory = memory; ScalePermille = scalePermille; Channel = channel; }
    public string RequestId { get; }
    public string Holder { get; }
    public string Subject { get; }
    public string Memory { get; }
    public int ScalePermille { get; }
    public string Channel { get; }
}

/// <summary>A request to change a need's level by a delta.</summary>
public readonly struct RelationshipNeedRequested
{
    public RelationshipNeedRequested(string requestId, string actor, string need, long delta, string channel = "") { RequestId = requestId; Actor = actor; Need = need; Delta = delta; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Need { get; }
    public long Delta { get; }
    public string Channel { get; }
}

/// <summary>A request to move an axis of the holder's opinion of another actor.</summary>
public readonly struct RelationshipAxisRequested
{
    public RelationshipAxisRequested(string requestId, string holder, string other, string axis, long delta, string channel = "") { RequestId = requestId; Holder = holder; Other = other; Axis = axis; Delta = delta; Channel = channel; }
    public string RequestId { get; }
    public string Holder { get; }
    public string Other { get; }
    public string Axis { get; }
    public long Delta { get; }
    public string Channel { get; }
}

/// <summary>A request to set or clear a bond between two actors.</summary>
public readonly struct RelationshipBondRequested
{
    public RelationshipBondRequested(string requestId, string holder, string other, string bond, bool set, string channel = "") { RequestId = requestId; Holder = holder; Other = other; Bond = bond; Set = set; Channel = channel; }
    public string RequestId { get; }
    public string Holder { get; }
    public string Other { get; }
    public string Bond { get; }
    public bool Set { get; }
    public string Channel { get; }
}

/// <summary>A request for an actor's mood and why.</summary>
public readonly struct RelationshipMoodRequested
{
    public RelationshipMoodRequested(string requestId, string actor, string channel = "") { RequestId = requestId; Actor = actor; Channel = channel; }
    public string RequestId { get; }
    public string Actor { get; }
    public string Channel { get; }
}

/// <summary>A request for one actor's opinion of another on an axis, and why.</summary>
public readonly struct RelationshipOpinionRequested
{
    public RelationshipOpinionRequested(string requestId, string from, string to, string axis, string channel = "") { RequestId = requestId; From = from; To = to; Axis = axis; Channel = channel; }
    public string RequestId { get; }
    public string From { get; }
    public string To { get; }
    public string Axis { get; }
    public string Channel { get; }
}

/// <summary>A request was carried out. Raised for every request that changes the relationships, so a requester never has to guess that silence meant success.</summary>
public readonly struct RelationshipApplied
{
    public RelationshipApplied(string source, string channel, string requestId, long tick) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the relationships are exactly as they were. A request for a channel nobody serves is refused by the registry with <see cref="RelationshipChannels.UnknownChannel"/>.</summary>
public readonly struct RelationshipRefused
{
    public RelationshipRefused(string source, string channel, string requestId, long tick, string code) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }

    /// <summary>The id of the component that refused; the registry's id for an unserved channel.</summary>
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>An interaction was performed; <paramref name="accepted"/> says whether the target went along. The changes it made follow as their own events.</summary>
public readonly struct RelationshipInteractionPerformed
{
    public RelationshipInteractionPerformed(string source, string channel, string requestId, long tick, string interaction, string actor, string target, bool accepted, long score, long threshold, long noise)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Interaction = interaction; Actor = actor; Target = target; Accepted = accepted; Score = score; Threshold = threshold; Noise = noise; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Interaction { get; }
    public string Actor { get; }
    public string Target { get; }
    public bool Accepted { get; }
    public long Score { get; }
    public long Threshold { get; }
    public long Noise { get; }
}

/// <summary>The answer to a mood request: the mood, its state, the sum before the clamp, and the terms as <c>kind|id|value</c> joined by ';'.</summary>
public readonly struct RelationshipMoodAnswered
{
    public RelationshipMoodAnswered(string source, string channel, string requestId, long tick, string actor, long mood, string state, long sum, string terms)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Actor = actor; Mood = mood; State = state; Sum = sum; Terms = terms; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Actor { get; }
    public long Mood { get; }
    public string State { get; }
    public long Sum { get; }
    public string Terms { get; }
}

/// <summary>The answer to an opinion request: the opinion, the states it puts the edge in, the sum before the clamp, and the terms as <c>kind|id|value</c> joined by ';'.</summary>
public readonly struct RelationshipOpinionAnswered
{
    public RelationshipOpinionAnswered(string source, string channel, string requestId, long tick, string from, string to, string axis, long opinion, string state, long sum, string terms)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; From = from; To = to; Axis = axis; Opinion = opinion; State = state; Sum = sum; Terms = terms; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string From { get; }
    public string To { get; }
    public string Axis { get; }
    public long Opinion { get; }
    public string State { get; }
    public long Sum { get; }
    public string Terms { get; }
}

/// <summary>Time moved: the relationships are now at <paramref name="step"/>; <paramref name="owed"/> steps remain to be lived in the next Updates.</summary>
public readonly struct RelationshipStepped
{
    public RelationshipStepped(string source, string channel, long tick, long fromStep, long step, long owed) { Source = source; Channel = channel; Tick = tick; FromStep = fromStep; Step = step; Owed = owed; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long FromStep { get; }
    public long Step { get; }
    public long Owed { get; }
}

/// <summary>An actor's mood state changed.</summary>
public readonly struct RelationshipMoodChanged
{
    public RelationshipMoodChanged(string source, string channel, long tick, string actor, string from, string to, long mood) { Source = source; Channel = channel; Tick = tick; Actor = actor; From = from; To = to; Mood = mood; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string From { get; }
    public string To { get; }
    public long Mood { get; }
}

/// <summary>A need fell to its critical line (<paramref name="entered"/>) or recovered to its re-arm line.</summary>
public readonly struct RelationshipNeedCritical
{
    public RelationshipNeedCritical(string source, string channel, long tick, string actor, string need, long level, bool entered) { Source = source; Channel = channel; Tick = tick; Actor = actor; Need = need; Level = level; Entered = entered; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Need { get; }
    public long Level { get; }
    public bool Entered { get; }
}

/// <summary>The named state of one actor's opinion of another changed on a ladder (friend to rival).</summary>
public readonly struct RelationshipStateChanged
{
    public RelationshipStateChanged(string source, string channel, long tick, string actor, string target, string ladder, string from, string to)
    { Source = source; Channel = channel; Tick = tick; Actor = actor; Target = target; Ladder = ladder; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Target { get; }
    public string Ladder { get; }
    public string From { get; }
    public string To { get; }
}

/// <summary>A bond was set or cleared on one actor's side of a pair.</summary>
public readonly struct RelationshipBondChanged
{
    public RelationshipBondChanged(string source, string channel, long tick, string actor, string target, string bond, bool set) { Source = source; Channel = channel; Tick = tick; Actor = actor; Target = target; Bond = bond; Set = set; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Target { get; }
    public string Bond { get; }
    public bool Set { get; }
}

/// <summary>An actor gained a memory of another; <paramref name="origin"/> is who told it (empty when the holder saw it).</summary>
public readonly struct RelationshipMemoryAdded
{
    public RelationshipMemoryAdded(string source, string channel, long tick, string holder, string subject, string memory, string origin) { Source = source; Channel = channel; Tick = tick; Holder = holder; Subject = subject; Memory = memory; Origin = origin; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Holder { get; }
    public string Subject { get; }
    public string Memory { get; }
    public string Origin { get; }
}

/// <summary>An actor gained a mood modifier.</summary>
public readonly struct RelationshipMoodletAdded
{
    public RelationshipMoodletAdded(string source, string channel, long tick, string actor, string moodlet, string from) { Source = source; Channel = channel; Tick = tick; Actor = actor; Moodlet = moodlet; From = from; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string Moodlet { get; }
    public string From { get; }
}

/// <summary>Something was dropped to stay inside a limit of the document: a <c>moodlet</c>, a <c>memory</c> or an <c>edge</c>, of this actor, and which.</summary>
public readonly struct RelationshipEvicted
{
    public RelationshipEvicted(string source, string channel, long tick, string kind, string actor, string what) { Source = source; Channel = channel; Tick = tick; Kind = kind; Actor = actor; What = what; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Kind { get; }
    public string Actor { get; }
    public string What { get; }
}

/// <summary>At a day boundary an actor changed community; the names are community ids (the lowest member of each).</summary>
public readonly struct RelationshipCommunityChanged
{
    public RelationshipCommunityChanged(string source, string channel, long tick, string actor, string from, string to) { Source = source; Channel = channel; Tick = tick; Actor = actor; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public string From { get; }
    public string To { get; }
}

/// <summary>At a day boundary an actor's standing moved by at least the document's event delta (parts per thousand of the village average).</summary>
public readonly struct RelationshipStandingChanged
{
    public RelationshipStandingChanged(string source, string channel, long tick, string actor, long from, long to) { Source = source; Channel = channel; Tick = tick; Actor = actor; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Actor { get; }
    public long From { get; }
    public long To { get; }
}

/// <summary>
/// Balance pressure was put on the holder's opinion of the subject through <paramref name="via"/> (a triad: the holder's opinion of Via, <paramref name="holderVia"/>, and Via's of the subject,
/// <paramref name="viaSubject"/>). <paramref name="scale"/> is the permille the pressure memory was given at; its sign says which way it pushes. Raised for a new or changed push, not for a daily renewal.
/// </summary>
public readonly struct RelationshipBalanceShifted
{
    public RelationshipBalanceShifted(string source, string channel, long tick, string holder, string subject, string via, long holderVia, long viaSubject, int scale)
    { Source = source; Channel = channel; Tick = tick; Holder = holder; Subject = subject; Via = via; HolderVia = holderVia; ViaSubject = viaSubject; Scale = scale; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Holder { get; }
    public string Subject { get; }
    public string Via { get; }
    public long HolderVia { get; }
    public long ViaSubject { get; }
    public int Scale { get; }
}
