namespace TutorialEngine;

/// <summary>Where the tutorial as a whole is: not started, running, finished (every step satisfied) or skipped by the player.</summary>
public enum TutorialPhase { Idle, Running, Finished, Skipped }

/// <summary>Where the first-run journey is: choosing a language, reading the story, choosing how to enter, or done (a returning player starts here).</summary>
public enum JourneyState { Language, Story, Entry, Done }

/// <summary>Why a tip stopped showing.</summary>
public enum TipEnd { Shown, Dismissed, Expired, Withdrawn }

/// <summary>One step's state: its status, when it began and ended, the hint level shown, and the counts of the events its completion reads at the moment it began.</summary>
public sealed class StepState
{
    public StepState(int since) { Baseline = new long[since]; }
    public StepStatus Status { get; set; }
    public long ActivatedAt { get; set; } = -1;
    public long DoneAt { get; set; } = -1;
    public int Hint { get; set; }
    public long[] Baseline { get; }
}

/// <summary>One tip's state: how often it showed, whether the player acknowledged it, whether it shows now (from <see cref="ShownAt"/> to <see cref="Until"/>), and when it last ended.</summary>
public sealed class TipState
{
    public int Shows { get; set; }
    public bool Acknowledged { get; set; }
    public bool Visible { get; set; }
    public long ShownAt { get; set; } = -1;
    public long Until { get; set; } = -1;
    public long EndedAt { get; set; } = -1;
}

/// <summary>The first-run journey of one player: the state, the chosen language, the story page, whether the story was skipped or is being replayed, how the player entered, and the intent that waits for the end.</summary>
public sealed class JourneyProgress
{
    public JourneyState State { get; set; }
    public string Language { get; set; } = "";
    public int Page { get; set; }
    public bool StorySkipped { get; set; }
    public bool Replaying { get; set; }
    public string Entry { get; set; } = "";
    public string Intent { get; set; } = "";
}

public readonly record struct StepChanged(int Step, StepStatus From, StepStatus To);
public readonly record struct HintChanged(int Step, int Level);
public readonly record struct TipChanged(int Tip, TipEnd Change, int Shows);
public readonly record struct SkillChanged(int Skill);
public readonly record struct PhaseChanged(TutorialPhase From, TutorialPhase To);
public readonly record struct JourneyChanged(JourneyState From, JourneyState To, int Page, string Language);
public readonly record struct SettingChanged(string Name, long Value);

/// <summary>Everything that changed during a call, in the order it happened.</summary>
public sealed class TutorialChanges
{
    public List<StepChanged> Steps { get; } = new();
    public List<HintChanged> Hints { get; } = new();
    public List<TipChanged> Tips { get; } = new();
    public List<SkillChanged> Skills { get; } = new();
    public List<PhaseChanged> Phases { get; } = new();
    public List<JourneyChanged> Journeys { get; } = new();
    public List<SettingChanged> Settings { get; } = new();

    public bool Any => Steps.Count + Hints.Count + Tips.Count + Skills.Count + Phases.Count + Journeys.Count + Settings.Count > 0;

    /// <summary>True when something the save holds changed other than the time-driven showing and ending of hints and tips; the host saves at once (progress is persisted immediately).</summary>
    public bool Persist => Steps.Count + Skills.Count + Phases.Count + Journeys.Count + Settings.Count > 0 || Tips.Any(t => t.Change != TipEnd.Expired);
}

/// <summary>A hint that shows now, for the host to draw: which step, how strong, the key to render, the highlight target and the motion that the player's settings allow.</summary>
public sealed record HintView(string Step, int Level, HintKind Kind, string Key, string Target, Motion Motion);

/// <summary>A tip that shows now, with the steps it lasts and the motion the player's settings allow.</summary>
public sealed record TipView(string Tip, string Key, long ShownAt, long Until, Motion Motion);
