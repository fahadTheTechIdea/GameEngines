namespace GameRuntime;

// Events of the tutorial component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. Names are ids, never text: the host renders them in its locale with TutorialNames.
//
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. One request gets one answer: from the component that serves its channel, or, from the
// TutorialChannels registry, a refusal with TUT_CHANNEL_UNKNOWN for a channel nobody serves. A request is one event with a kind (see TutorialRequestKind), a text and a number, so the set of requests is one list.
//
// Order of what one Update raises: the answers to the requests in arrival order (each followed by the changes it made), then what the time of this Update changed (steps, hints, tips, skills, the phase), at most
// the component's maximum per Update; the rest is owed and follows in the next Updates, in the same order, none lost. A change the player's progress must keep is followed by SaveNeeded: the host saves at once.
// Everything is deferred: it is delivered after the Update returns.

/// <summary>A request to the tutorial: <c>kind</c> is one of the names in <see cref="TutorialRequestKind"/>, <c>text</c> the id or word the kind takes and <c>number</c> its number. Only queued.</summary>
public readonly struct TutorialRequested
{
    public TutorialRequested(string requestId, string kind, string text = "", long number = 1, string channel = "") { RequestId = requestId; Kind = kind; Text = text; Number = number; Channel = channel; }
    public string RequestId { get; }
    public string Kind { get; }
    public string Text { get; }
    public long Number { get; }
    public string Channel { get; }
}

/// <summary>A request was applied; the changes it made follow as their own events.</summary>
public readonly struct TutorialApplied
{
    public TutorialApplied(string source, string channel, string requestId, long tick) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the tutorial is exactly as it was. A request for a channel nobody serves is refused by the registry with <see cref="TutorialChannels.UnknownChannel"/>.</summary>
public readonly struct TutorialRefused
{
    public TutorialRefused(string source, string channel, string requestId, long tick, string code) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }

    /// <summary>The id of the component that refused; the registry's id for an unserved channel.</summary>
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>The answer to a progress request: how much is behind the player (0 to 1000), the steps satisfied and all, the phase, the journey state, the steps being taught joined by ';', and the language.</summary>
public readonly struct TutorialProgressAnswered
{
    public TutorialProgressAnswered(string source, string channel, string requestId, long tick, int permille, int satisfied, int total, string phase, string journey, string active, string language)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Permille = permille; Satisfied = satisfied; Total = total; Phase = phase; Journey = journey; Active = active; Language = language; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public int Permille { get; }
    public int Satisfied { get; }
    public int Total { get; }
    public string Phase { get; }
    public string Journey { get; }
    public string Active { get; }
    public string Language { get; }
}

/// <summary>A step changed status (pending, active, done, skipped, mastered, bypassed).</summary>
public readonly struct TutorialStepChanged
{
    public TutorialStepChanged(string source, string channel, long tick, string step, string from, string to) { Source = source; Channel = channel; Tick = tick; Step = step; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Step { get; }
    public string From { get; }
    public string To { get; }
}

/// <summary>A hint of a step rose to <c>level</c> (0 took it away). Kind, key, target and motion are those to draw; the motion is already the one the player's settings allow.</summary>
public readonly struct TutorialHintChanged
{
    public TutorialHintChanged(string source, string channel, long tick, string step, int level, string kind, string key, string target, string motion)
    { Source = source; Channel = channel; Tick = tick; Step = step; Level = level; Kind = kind; Key = key; Target = target; Motion = motion; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Step { get; }
    public int Level { get; }
    public string Kind { get; }
    public string Key { get; }
    public string Target { get; }
    public string Motion { get; }
}

/// <summary>A tip began showing, or ended: <c>change</c> is shown, dismissed, expired or withdrawn. <c>until</c> is the step a shown tip ends at; the motion is the one the player's settings allow.</summary>
public readonly struct TutorialTipChanged
{
    public TutorialTipChanged(string source, string channel, long tick, string tip, string change, int shows, string key, string motion, long until)
    { Source = source; Channel = channel; Tick = tick; Tip = tip; Change = change; Shows = shows; Key = key; Motion = motion; Until = until; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Tip { get; }
    public string Change { get; }
    public int Shows { get; }
    public string Key { get; }
    public string Motion { get; }
    public long Until { get; }
}

/// <summary>The player reached the mastery of a skill: its steps are no longer taught and its tips no longer shown.</summary>
public readonly struct TutorialSkillMastered
{
    public TutorialSkillMastered(string source, string channel, long tick, string skill) { Source = source; Channel = channel; Tick = tick; Skill = skill; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Skill { get; }
}

/// <summary>The tutorial as a whole changed phase (idle, running, finished, skipped).</summary>
public readonly struct TutorialPhaseChanged
{
    public TutorialPhaseChanged(string source, string channel, long tick, string from, string to) { Source = source; Channel = channel; Tick = tick; From = from; To = to; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string From { get; }
    public string To { get; }
}

/// <summary>The first-run journey moved (language, story, entry, done), or the language or the intent changed, or the story page moved.</summary>
public readonly struct TutorialJourneyChanged
{
    public TutorialJourneyChanged(string source, string channel, long tick, string from, string to, int page, string language) { Source = source; Channel = channel; Tick = tick; From = from; To = to; Page = page; Language = language; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string From { get; }
    public string To { get; }
    public int Page { get; }
    public string Language { get; }
}

/// <summary>An accessibility or preference setting changed (reduced_motion, text_scale, tips); a switch is 0 or 1.</summary>
public readonly struct TutorialSettingChanged
{
    public TutorialSettingChanged(string source, string channel, long tick, string name, long value) { Source = source; Channel = channel; Tick = tick; Name = name; Value = value; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public string Name { get; }
    public long Value { get; }
}

/// <summary>Something the player's saved progress holds changed: the host takes a checkpoint now (progress is persisted at once, not at exit).</summary>
public readonly struct TutorialSaveNeeded
{
    public TutorialSaveNeeded(string source, string channel, long tick) { Source = source; Channel = channel; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
}
