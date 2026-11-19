using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

public sealed partial class TutorialRuntime
{
    /// <summary>
    /// Checks the facts the host gave against the document's declarations before anything is evaluated: every declared fact is present, of the declared type and inside its range. A wrong or missing fact is the
    /// host's mistake and is refused whole, so a tutorial never decides on a guess.
    /// </summary>
    private void CheckHost(IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(host);
        foreach (var (id, definition) in Set.HostFacts)
        {
            if (!host.TryGetValue(id, out var value)) throw new EngineRefusedException(TutorialCodes.FactMissing, "the host gave no fact '" + id + "'");
            if (value.Kind != definition.Kind) throw new EngineRefusedException(TutorialCodes.FactInvalid, "fact '" + id + "' is " + value.Kind + ", the document declares " + definition.Kind);
            if (value.Kind == FactKind.Integer && ((definition.Minimum is { } min && value.Integer < min) || (definition.Maximum is { } max && value.Integer > max)))
                throw new EngineRefusedException(TutorialCodes.FactInvalid, "fact '" + id + "' is " + value.Integer + ", outside " + definition.Minimum + " to " + definition.Maximum);
            if (value.Kind == FactKind.String && (value.Text is null || value.Text.Length > 4096)) throw new EngineRefusedException(TutorialCodes.FactInvalid, "fact '" + id + "' is a text longer than 4096");
        }
    }

    /// <summary>The value of one fact now. <paramref name="scope"/> is the step whose completion is being read (-1 outside a step): the only place <c>since_step</c> has a meaning.</summary>
    internal FactValue Resolve(string id, int scope, IReadOnlyDictionary<string, FactValue> host)
    {
        var binding = Set.Bindings[id];
        var i = binding.Index;
        switch (binding.Source)
        {
            case FactSource.Host: return host[id];
            case FactSource.EventCount: return FactValue.From(_count[i]);
            case FactSource.EventAgo: return FactValue.From(_lastAt[i] < 0 ? -1 : Saturating(Step - _lastAt[i]));
            case FactSource.EventSince: return FactValue.From(_count[i] - _steps[scope].Baseline[SinceSlot(scope, i)]);
            case FactSource.StepStatus: return FactValue.From(TutorialFacts.StatusName(_steps[i].Status));
            case FactSource.StepDone: return FactValue.From(TutorialFacts.Satisfied(_steps[i].Status));
            case FactSource.TipShows: return FactValue.From(_tips[i].Shows);
            case FactSource.TipAcknowledged: return FactValue.From(_tips[i].Acknowledged);
            case FactSource.TipVisible: return FactValue.From(_tips[i].Visible);
            case FactSource.SkillPoints: return FactValue.From(_points[i]);
            case FactSource.SkillMastered: return FactValue.From(IsMastered(i));
            case FactSource.Phase: return FactValue.From(TutorialFacts.PhaseName(Phase));
            case FactSource.Progress: return FactValue.From(ProgressPermille());
            case FactSource.JourneyState: return FactValue.From(TutorialFacts.JourneyName(Journey.State));
            case FactSource.JourneyLanguage: return FactValue.From(Journey.Language);
            case FactSource.ReducedMotion: return FactValue.From(ReducedMotion);
            case FactSource.TextScale: return FactValue.From(TextScale);
            case FactSource.TipsEnabled: return FactValue.From(TipsEnabled);
        }
        throw new InvalidOperationException("unknown fact source " + binding.Source);
    }

    /// <summary>The place of an event in the list of events a step's completion reads since it began (the document lists every one it reads, so it is always there).</summary>
    private int SinceSlot(int step, int evt)
    {
        var since = Set.Steps[step].Since;
        for (var slot = 0; slot < since.Count; slot++)
            if (since[slot] == evt) return slot;
        return -1;
    }

    /// <summary>Evaluates a condition against the facts it reads, gathered now.</summary>
    private bool Holds(Condition condition, IReadOnlyList<string> facts, int scope, IReadOnlyDictionary<string, FactValue> host)
    {
        _scratch.Clear();
        foreach (var id in facts) _scratch[id] = Resolve(id, scope, host);
        return Conditions.Evaluate(condition, _scratch);
    }

    /// <summary>Every fact the engine itself offers (the document's own and the engine's), for a dialogue's external facts or an AI sensor; each id begins with <paramref name="prefix"/>. The host's facts are not included.</summary>
    public Dictionary<string, FactValue> ReadFacts(string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var none = new Dictionary<string, FactValue>();
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var (id, binding) in Set.Bindings)
        {
            if (binding.Source is FactSource.Host or FactSource.EventSince) continue;
            facts[prefix + id] = Resolve(id, -1, none);
        }
        return facts;
    }
}
