using ScenarioEngine.Contracts;

namespace TutorialEngine;

// The declarations of a tutorial document, as plain immutable data. Every named thing has an id (stable, language free) and, when a player can see it, a localisation key. Indexes are positions in the document's lists.

/// <summary>At most <paramref name="MaxActiveSteps"/> steps are taught at once (one main task per step: usually 1), at most <paramref name="MaxVisibleTips"/> tips show at once, and two tips start at least <paramref name="TipGap"/> steps apart.</summary>
public sealed record LimitsDef(int MaxActiveSteps, int MaxVisibleTips, int TipGap);

/// <summary>Something the player can do that the host observes (built a structure, opened the menu). The engine counts it and remembers when it last happened.</summary>
public sealed record EventDef(int Index, string Id);

/// <summary>A pressed input action that counts as the event <paramref name="Event"/> (the input component reads this; any other pressed action is plain activity).</summary>
public sealed record InputDef(string Action, int Event);

/// <summary>Doing <paramref name="Event"/> is worth <paramref name="Points"/> towards a skill.</summary>
public sealed record EvidenceDef(int Event, int Points);

/// <summary>A skill the player may already have. At <paramref name="Mastered"/> points the player is taken to know it: its steps are not taught and its tips are not shown.</summary>
public sealed record SkillDef(int Index, string Id, string Key, int Mastered, IReadOnlyList<EvidenceDef> Evidence);

/// <summary>How strongly a hint speaks: a nudge, an explanation, or showing the target.</summary>
public enum HintKind { Nudge, Explain, Show }

/// <summary>After <paramref name="Idle"/> steps without any activity the hint is shown.</summary>
public sealed record HintDef(int Idle, HintKind Kind, string Key);

/// <summary>The authored movement of a highlight. Reduced motion turns every one of them into <see cref="None"/>.</summary>
public enum Motion { None, Pulse, Slide }

public enum StepStatus { Pending, Active, Done, Skipped, Mastered, Bypassed }

/// <summary>
/// One thing to teach. It is available when all of <paramref name="Requires"/> and one of <paramref name="RequiresAny"/> (when given) are met: the requirements are satisfied (done, skipped, mastered or bypassed), an alternative counts when it was really done (a bypassed one is the branch not taken, and only when every alternative was bypassed does the step go on); then, when
/// <paramref name="When"/> is false it is bypassed (a branch not taken); when its skill is mastered it is mastered; otherwise it becomes active until <paramref name="Complete"/> holds. <paramref name="Since"/> lists the
/// events whose count since the step began the completion reads.
/// </summary>
public sealed record StepDef(int Index, string Id, string Key, string BodyKey, string Target, Motion Motion, bool Skippable, int Skill, IReadOnlyList<int> Requires, IReadOnlyList<int> RequiresAny,
    Condition? When, IReadOnlyList<string> WhenFacts, Condition Complete, IReadOnlyList<string> CompleteFacts, IReadOnlyList<int> Since, IReadOnlyList<HintDef> Hints);

/// <summary>A contextual one-shot tip: shown while <paramref name="When"/> holds, at most <paramref name="MaxShows"/> times, <paramref name="Cooldown"/> steps apart, for <paramref name="Duration"/> steps.</summary>
public sealed record TipDef(int Index, string Id, string Key, Condition When, IReadOnlyList<string> WhenFacts, int Cooldown, int MaxShows, int Duration, int Priority, int Skill, int AfterStep, Motion Motion);

/// <summary>One page of the first-run story: a title, a body and an illustration, all keys.</summary>
public sealed record PageDef(string Id, string Key, string BodyKey, string ArtKey);

/// <summary>The first-run journey: language first, then the story, then an entry (sign-in, register, host, join, guest).</summary>
public sealed record JourneyDef(IReadOnlyList<string> Languages, string DefaultLanguage, IReadOnlyList<PageDef> Pages, IReadOnlyList<string> Entries);
