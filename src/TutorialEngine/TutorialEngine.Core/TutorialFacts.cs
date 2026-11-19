using ScenarioEngine.Contracts;

namespace TutorialEngine;

/// <summary>Where a fact of the tutorial engine gets its value from.</summary>
public enum FactSource { Host, EventCount, EventAgo, EventSince, StepStatus, StepDone, TipShows, TipAcknowledged, TipVisible, SkillPoints, SkillMastered, Phase, Progress, JourneyState, JourneyLanguage, ReducedMotion, TextScale, TipsEnabled }

/// <summary>A fact id bound to the thing it reads: the source and the index of the event, step, tip or skill (0 when it reads none).</summary>
public sealed record FactBinding(FactSource Source, int Index);

/// <summary>
/// What the tutorial engine says about the player's progress, as facts in the shared fact model (boolean, integer, string). The same facts feed step completion, branches and tip conditions in the document
/// and, as external facts, dialogue conditions and AI sensors. The names are fixed: <c>event.&lt;id&gt;.count</c> / <c>.ago</c> / <c>.since_step</c>, <c>step.&lt;id&gt;.status</c> / <c>.done</c>,
/// <c>tip.&lt;id&gt;.shows</c> / <c>.acknowledged</c> / <c>.visible</c>, <c>skill.&lt;id&gt;.points</c> / <c>.mastered</c>, <c>tutorial.phase</c>, <c>tutorial.progress</c>, <c>journey.state</c>,
/// <c>journey.language</c> and <c>setting.reduced_motion</c> / <c>.text_scale</c> / <c>.tips</c>. Any other id is the host's. <c>since_step</c> (the count since the step began) is only meaningful inside the
/// completion condition of a step, and the document may only use it there.
/// </summary>
public static class TutorialFacts
{
    public const string EventPrefix = "event.";
    public const string StepPrefix = "step.";
    public const string TipPrefix = "tip.";
    public const string SkillPrefix = "skill.";
    public const string TutorialPrefix = "tutorial.";
    public const string JourneyPrefix = "journey.";
    public const string SettingPrefix = "setting.";
    public const string SinceSuffix = ".since_step";

    /// <summary>The largest value a count, a time or a point total reaches; a bigger one saturates here (the shared fact model's limit).</summary>
    public const long Saturate = 1_000_000_000;

    private static readonly string[] Prefixes = [EventPrefix, StepPrefix, TipPrefix, SkillPrefix, TutorialPrefix, JourneyPrefix, SettingPrefix];

    /// <summary>True when the id begins with one of the prefixes the engine owns.</summary>
    public static bool IsReserved(string id) => Prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// Every fact a document with these declarations offers, each with its type and range, plus the host's own, and the binding of each. Duplicate ids cannot arise from ids that were already checked to be
    /// unique within their kind, and the prefixes are different, so nothing is overwritten.
    /// </summary>
    public static (IReadOnlyDictionary<string, FactDefinition> Facts, IReadOnlyDictionary<string, FactBinding> Bindings) Build(IReadOnlyList<string> events, IReadOnlyList<(string Id, int Mastered)> skills,
        IReadOnlyList<string> steps, IReadOnlyList<(string Id, int MaxShows)> tips, IReadOnlyDictionary<string, FactDefinition> hostFacts)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        var bindings = new Dictionary<string, FactBinding>(StringComparer.Ordinal);
        void Add(string id, FactDefinition definition, FactSource source, int index)
        {
            facts[id] = definition;
            bindings[id] = new FactBinding(source, index);
        }
        for (var e = 0; e < events.Count; e++)
        {
            Add(EventPrefix + events[e] + ".count", new FactDefinition(FactKind.Integer, 0, Saturate), FactSource.EventCount, e);
            Add(EventPrefix + events[e] + ".ago", new FactDefinition(FactKind.Integer, -1, Saturate), FactSource.EventAgo, e);
            Add(EventPrefix + events[e] + SinceSuffix, new FactDefinition(FactKind.Integer, 0, Saturate), FactSource.EventSince, e);
        }
        for (var s = 0; s < steps.Count; s++)
        {
            Add(StepPrefix + steps[s] + ".status", new FactDefinition(FactKind.String), FactSource.StepStatus, s);
            Add(StepPrefix + steps[s] + ".done", new FactDefinition(FactKind.Boolean), FactSource.StepDone, s);
        }
        for (var t = 0; t < tips.Count; t++)
        {
            Add(TipPrefix + tips[t].Id + ".shows", new FactDefinition(FactKind.Integer, 0, tips[t].MaxShows), FactSource.TipShows, t);
            Add(TipPrefix + tips[t].Id + ".acknowledged", new FactDefinition(FactKind.Boolean), FactSource.TipAcknowledged, t);
            Add(TipPrefix + tips[t].Id + ".visible", new FactDefinition(FactKind.Boolean), FactSource.TipVisible, t);
        }
        for (var k = 0; k < skills.Count; k++)
        {
            Add(SkillPrefix + skills[k].Id + ".points", new FactDefinition(FactKind.Integer, 0, skills[k].Mastered), FactSource.SkillPoints, k);
            Add(SkillPrefix + skills[k].Id + ".mastered", new FactDefinition(FactKind.Boolean), FactSource.SkillMastered, k);
        }
        Add("tutorial.phase", new FactDefinition(FactKind.String), FactSource.Phase, 0);
        Add("tutorial.progress", new FactDefinition(FactKind.Integer, 0, 1000), FactSource.Progress, 0);
        Add("journey.state", new FactDefinition(FactKind.String), FactSource.JourneyState, 0);
        Add("journey.language", new FactDefinition(FactKind.String), FactSource.JourneyLanguage, 0);
        Add("setting.reduced_motion", new FactDefinition(FactKind.Boolean), FactSource.ReducedMotion, 0);
        Add("setting.text_scale", new FactDefinition(FactKind.Integer, TutorialRuntime.MinTextScale, TutorialRuntime.MaxTextScale), FactSource.TextScale, 0);
        Add("setting.tips", new FactDefinition(FactKind.Boolean), FactSource.TipsEnabled, 0);
        foreach (var (id, definition) in hostFacts)
        {
            facts[id] = definition;
            bindings[id] = new FactBinding(FactSource.Host, 0);
        }
        return (facts, bindings);
    }

    // Every member of each enum is listed and there is no catch-all arm: a member left out is a build error (CS8509), and a value outside the enum fails the app.

    /// <summary>The name a status has in facts and in the journal.</summary>
    public static string StatusName(StepStatus status)
    {
        switch (status)
        {
            case StepStatus.Pending: return "pending";
            case StepStatus.Active: return "active";
            case StepStatus.Done: return "done";
            case StepStatus.Skipped: return "skipped";
            case StepStatus.Mastered: return "mastered";
            case StepStatus.Bypassed: return "bypassed";
        }
        throw new InvalidOperationException("unknown step status " + status);
    }

    public static string PhaseName(TutorialPhase phase)
    {
        switch (phase)
        {
            case TutorialPhase.Idle: return "idle";
            case TutorialPhase.Running: return "running";
            case TutorialPhase.Finished: return "finished";
            case TutorialPhase.Skipped: return "skipped";
        }
        throw new InvalidOperationException("unknown tutorial phase " + phase);
    }

    public static string JourneyName(JourneyState state)
    {
        switch (state)
        {
            case JourneyState.Language: return "language";
            case JourneyState.Story: return "story";
            case JourneyState.Entry: return "entry";
            case JourneyState.Done: return "done";
        }
        throw new InvalidOperationException("unknown journey state " + state);
    }

    /// <summary>
    /// Whether a step that waits for one of several alternatives may begin: none is listed, or one of them was really done (done, skipped or mastered), or every one was bypassed (a branch not taken,
    /// so none can ever happen and waiting would be for ever). A bypassed alternative alone does not count: it is the branch the player did not take.
    /// </summary>
    public static bool AlternativesMet(IEnumerable<StepStatus> alternatives)
    {
        var any = false;
        var allBypassed = true;
        foreach (var status in alternatives)
        {
            any = true;
            if (status is StepStatus.Done or StepStatus.Skipped or StepStatus.Mastered) return true;
            if (status != StepStatus.Bypassed) allBypassed = false;
        }
        return !any || allBypassed;
    }

    /// <summary>A step is satisfied (later steps may go on) when it is done, skipped, mastered or bypassed.</summary>
    public static bool Satisfied(StepStatus status) => status != StepStatus.Pending && status != StepStatus.Active;
}
