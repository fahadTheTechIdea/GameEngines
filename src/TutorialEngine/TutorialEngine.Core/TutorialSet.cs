using ScenarioEngine.Contracts;

namespace TutorialEngine;

/// <summary>
/// A loaded tutorial document: every declaration plus the digest of the exact text it was read from. It is immutable and holds no player; the runtime holds that. Every query is a pure function of it.
/// </summary>
public sealed class TutorialSet
{
    public TutorialSet(string id, string digest, int stepsPerDay, LimitsDef limits, IReadOnlyList<EventDef> events, IReadOnlyList<InputDef> inputs, IReadOnlyList<SkillDef> skills,
        IReadOnlyList<StepDef> steps, IReadOnlyList<TipDef> tips, JourneyDef journey, IReadOnlyDictionary<string, FactDefinition> hostFacts,
        IReadOnlyDictionary<string, FactDefinition> facts, IReadOnlyDictionary<string, FactBinding> bindings)
    {
        Id = id; Digest = digest; StepsPerDay = stepsPerDay; Limits = limits; Events = events; Inputs = inputs; Skills = skills; Steps = steps; Tips = tips; Journey = journey; HostFacts = hostFacts;
        Facts = facts; Bindings = bindings;
        EventById = events.ToDictionary(e => e.Id, StringComparer.Ordinal);
        SkillById = skills.ToDictionary(s => s.Id, StringComparer.Ordinal);
        StepById = steps.ToDictionary(s => s.Id, StringComparer.Ordinal);
        TipById = tips.ToDictionary(t => t.Id, StringComparer.Ordinal);
        InputByAction = inputs.ToDictionary(i => i.Action, StringComparer.Ordinal);
        var evidence = new List<(int Skill, int Points)>[events.Count];
        for (var e = 0; e < evidence.Length; e++) evidence[e] = new List<(int, int)>();
        foreach (var skill in skills)
            foreach (var item in skill.Evidence) evidence[item.Event].Add((skill.Index, item.Points));
        EvidenceByEvent = evidence.Select(l => (IReadOnlyList<(int Skill, int Points)>)l).ToList();
        TipsByPriority = tips.OrderByDescending(t => t.Priority).ThenBy(t => t.Index).Select(t => t.Index).ToList();
    }

    public string Id { get; }

    /// <summary>The SHA-256 of the exact text of the document.</summary>
    public string Digest { get; }

    /// <summary>How many steps make a day of the game: the contract with the calendar (a step is a whole number of its ticks).</summary>
    public int StepsPerDay { get; }

    public LimitsDef Limits { get; }
    public IReadOnlyList<EventDef> Events { get; }
    public IReadOnlyList<InputDef> Inputs { get; }
    public IReadOnlyList<SkillDef> Skills { get; }
    public IReadOnlyList<StepDef> Steps { get; }
    public IReadOnlyList<TipDef> Tips { get; }
    public JourneyDef Journey { get; }

    /// <summary>The facts the host supplies, with their types (the engine's own facts are in <see cref="Facts"/>).</summary>
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }

    /// <summary>Every fact a condition of this document may read: the engine's own and the host's.</summary>
    public IReadOnlyDictionary<string, FactDefinition> Facts { get; }

    public IReadOnlyDictionary<string, FactBinding> Bindings { get; }
    public IReadOnlyDictionary<string, EventDef> EventById { get; }
    public IReadOnlyDictionary<string, SkillDef> SkillById { get; }
    public IReadOnlyDictionary<string, StepDef> StepById { get; }
    public IReadOnlyDictionary<string, TipDef> TipById { get; }
    public IReadOnlyDictionary<string, InputDef> InputByAction { get; }

    /// <summary>For each event, the skills it is evidence for and the points it is worth.</summary>
    public IReadOnlyList<IReadOnlyList<(int Skill, int Points)>> EvidenceByEvent { get; }

    /// <summary>The tips in the order they are offered a place: highest priority first, document order among equals.</summary>
    public IReadOnlyList<int> TipsByPriority { get; }
}
