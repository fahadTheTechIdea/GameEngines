using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

/// <summary>Says why a step is being taught or waits, why a tip shows or does not, and where the first-run journey is, from the same rules the runtime uses: a pure reading that changes nothing.</summary>
public static class TutorialExplain
{
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, FactValue> Gather(TutorialRuntime runtime, IReadOnlyList<string> ids, int scope, IReadOnlyDictionary<string, FactValue> host)
    {
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (runtime.Set.Bindings[id].Source == FactSource.Host && !host.ContainsKey(id)) throw new EngineRefusedException(TutorialCodes.FactMissing, "the host gave no fact '" + id + "', which the condition reads");
            facts[id] = runtime.Resolve(id, scope, host);
        }
        return facts;
    }

    public static string Step(TutorialRuntime runtime, string stepId, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);
        if (!runtime.Set.StepById.TryGetValue(stepId, out var step)) throw new EngineRefusedException(TutorialCodes.UnknownStep, "there is no step '" + stepId + "'");
        var state = runtime.Steps[step.Index];
        var text = new StringBuilder("step " + step.Id + " is " + TutorialFacts.StatusName(state.Status));
        if (state.ActivatedAt >= 0) text.Append(", began at step ").Append(Number(state.ActivatedAt));
        if (state.DoneAt >= 0) text.Append(", ended at step ").Append(Number(state.DoneAt));
        foreach (var required in step.Requires)
            text.Append("\n  requires ").Append(runtime.Set.Steps[required].Id).Append(": ").Append(TutorialFacts.StatusName(runtime.Steps[required].Status));
        if (step.RequiresAny.Count > 0)
            text.Append("\n  requires one of ").Append(string.Join(", ", step.RequiresAny.Select(r => runtime.Set.Steps[r].Id + " (" + TutorialFacts.StatusName(runtime.Steps[r].Status) + ")")));
        if (step.Skill >= 0)
            text.Append("\n  teaches ").Append(runtime.Set.Skills[step.Skill].Id).Append(": ").Append(Number(runtime.SkillPoints[step.Skill])).Append(" of ").Append(Number(runtime.Set.Skills[step.Skill].Mastered)).Append(runtime.IsMastered(step.Skill) ? " (mastered)" : "");
        if (step.When is not null) text.Append("\n  branch ").Append(ConditionExplanation.Describe(step.When, Gather(runtime, step.WhenFacts, -1, host)));
        var scope = state.Status == StepStatus.Active ? step.Index : -1;
        var ids = step.CompleteFacts.Where(f => scope >= 0 || runtime.Set.Bindings[f].Source != FactSource.EventSince).ToList();
        if (ids.Count == step.CompleteFacts.Count) text.Append("\n  completes when ").Append(ConditionExplanation.Describe(step.Complete, Gather(runtime, ids, scope, host)));
        else text.Append("\n  completes when it reads events since it began (it is not being taught)");
        if (state.Status == StepStatus.Active && step.Hints.Count > 0)
        {
            var idle = runtime.Step - Math.Max(runtime.LastActivity, state.ActivatedAt);
            text.Append("\n  idle ").Append(Number(idle)).Append(" steps, hint level ").Append(Number(state.Hint)).Append(" of ").Append(Number(step.Hints.Count));
            if (state.Hint < step.Hints.Count) text.Append(", the next at ").Append(Number(step.Hints[state.Hint].Idle));
        }
        return text.ToString();
    }

    public static string Tip(TutorialRuntime runtime, string tipId, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);
        if (!runtime.Set.TipById.TryGetValue(tipId, out var tip)) throw new EngineRefusedException(TutorialCodes.UnknownTip, "there is no tip '" + tipId + "'");
        var state = runtime.Tips[tip.Index];
        var set = runtime.Set;
        var text = new StringBuilder("tip " + tip.Id + (state.Visible ? " shows from step " + Number(state.ShownAt) + " to " + Number(state.Until) : " does not show") + ", shown " + Number(state.Shows) + " of " + Number(tip.MaxShows) + " times");
        void Gate(bool ok, string what) => text.Append("\n  ").Append(ok ? "ok      " : "blocked ").Append(what);
        Gate(runtime.TipsEnabled, "tips are " + (runtime.TipsEnabled ? "on" : "off"));
        Gate(runtime.Journey.State == JourneyState.Done, "the first-run journey is " + TutorialFacts.JourneyName(runtime.Journey.State));
        Gate(!state.Acknowledged, state.Acknowledged ? "the player acknowledged it" : "the player has not acknowledged it");
        Gate(state.Shows < tip.MaxShows || state.Visible, "it has shown " + Number(state.Shows) + " of " + Number(tip.MaxShows) + " times");
        if (tip.Skill >= 0) Gate(!runtime.IsMastered(tip.Skill), "skill " + set.Skills[tip.Skill].Id + (runtime.IsMastered(tip.Skill) ? " is mastered" : " is not mastered"));
        if (tip.AfterStep >= 0) Gate(TutorialFacts.Satisfied(runtime.Steps[tip.AfterStep].Status), "step " + set.Steps[tip.AfterStep].Id + " is " + TutorialFacts.StatusName(runtime.Steps[tip.AfterStep].Status));
        if (state.EndedAt >= 0) Gate(state.Visible || runtime.Step - state.EndedAt >= tip.Cooldown, "it ended at step " + Number(state.EndedAt) + ", cooldown " + Number(tip.Cooldown));
        if (runtime.LastTipShown >= 0) Gate(runtime.Step - runtime.LastTipShown >= set.Limits.TipGap, "the latest tip began at step " + Number(runtime.LastTipShown) + ", gap " + Number(set.Limits.TipGap));
        text.Append("\n  context ").Append(ConditionExplanation.Describe(tip.When, Gather(runtime, tip.WhenFacts, -1, host)));
        return text.ToString();
    }

    public static string Journey(TutorialRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var j = runtime.Journey;
        var def = runtime.Set.Journey;
        var text = new StringBuilder("journey is at " + TutorialFacts.JourneyName(j.State));
        text.Append(j.Language.Length > 0 ? ", language " + j.Language : ", no language chosen");
        if (j.State == JourneyState.Story) text.Append(", page ").Append(Number(j.Page + 1)).Append(" of ").Append(Number(def.Pages.Count)).Append(j.Replaying ? " (replay)" : "");
        if (j.State == JourneyState.Done) text.Append(", entered by ").Append(j.Entry).Append(j.StorySkipped ? ", story skipped" : "");
        if (j.Intent.Length > 0) text.Append(", intent ").Append(j.Intent);
        text.Append("\n  languages ").Append(string.Join(", ", def.Languages)).Append(", entries ").Append(string.Join(", ", def.Entries));
        text.Append("\n  settings reduced motion ").Append(runtime.ReducedMotion ? "on" : "off").Append(", text scale ").Append(Number(runtime.TextScale)).Append(", tips ").Append(runtime.TipsEnabled ? "on" : "off");
        return text.ToString();
    }
}
