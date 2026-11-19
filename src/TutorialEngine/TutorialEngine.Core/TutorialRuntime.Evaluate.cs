using ScenarioEngine.Contracts;

namespace TutorialEngine;

public sealed partial class TutorialRuntime
{
    /// <summary>
    /// Looks at the tutorial as it is now and moves it: steps whose prerequisites are met become available (and are bypassed when their branch is not taken, mastered when the player already has the skill, or
    /// taught), taught steps complete when their condition holds, hints escalate by idle time, and tips show, expire or are withdrawn. The host facts are checked against the document first; a wrong one is
    /// refused and nothing moves. Nothing happens in the first-run journey before it is done (no tip competes with the story). The work is bounded by the numbers of steps and tips.
    /// </summary>
    public TutorialChanges Evaluate(IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        CheckHost(hostFacts);
        var changes = new TutorialChanges();
        if (Phase == TutorialPhase.Running)
        {
            RunSteps(hostFacts, changes);
            Escalate(changes);
        }
        RunTips(hostFacts, changes);
        return changes;
    }

    private void SetStatus(int step, StepStatus to, TutorialChanges changes)
    {
        var state = _steps[step];
        changes.Steps.Add(new StepChanged(step, state.Status, to));
        state.Status = to;
        if (TutorialFacts.Satisfied(to))
        {
            state.DoneAt = Step;
            if (state.Hint > 0) { state.Hint = 0; changes.Hints.Add(new HintChanged(step, 0)); }
        }
    }

    private bool PrerequisitesMet(StepDef step)
    {
        foreach (var required in step.Requires)
            if (!TutorialFacts.Satisfied(_steps[required].Status)) return false;
        return TutorialFacts.AlternativesMet(step.RequiresAny.Select(a => _steps[a].Status));
    }

    private bool Known(StepDef step) => !IgnoreMastery && step.Skill >= 0 && IsMastered(step.Skill);

    private void Activate(int index, TutorialChanges changes)
    {
        var def = Set.Steps[index];
        var state = _steps[index];
        for (var slot = 0; slot < def.Since.Count; slot++) state.Baseline[slot] = _count[def.Since[slot]];
        state.ActivatedAt = Step;
        state.DoneAt = -1;
        state.Hint = 0;
        SetStatus(index, StepStatus.Active, changes);
    }

    /// <summary>
    /// Settles the steps: completing may make another step available and a step that is already satisfied may complete at once, so it goes on until nothing changes. Each pass changes at least one
    /// step for good (a step never goes back to pending here), so it ends within the number of steps and one more.
    /// </summary>
    private void RunSteps(IReadOnlyDictionary<string, FactValue> host, TutorialChanges changes)
    {
        for (var pass = 0; pass <= _steps.Length; pass++)
        {
            var moved = false;
            var active = 0;
            for (var i = 0; i < _steps.Length; i++)
            {
                if (_steps[i].Status != StepStatus.Active) continue;
                var def = Set.Steps[i];
                if (Known(def)) { SetStatus(i, StepStatus.Mastered, changes); moved = true; }
                else if (Holds(def.Complete, def.CompleteFacts, i, host)) { SetStatus(i, StepStatus.Done, changes); moved = true; }
                else active++;
            }
            for (var i = 0; i < _steps.Length; i++)
            {
                if (_steps[i].Status != StepStatus.Pending) continue;
                var def = Set.Steps[i];
                if (!PrerequisitesMet(def)) continue;
                if (def.When is not null && !Holds(def.When, def.WhenFacts, -1, host)) { SetStatus(i, StepStatus.Bypassed, changes); moved = true; continue; }
                if (Known(def)) { SetStatus(i, StepStatus.Mastered, changes); moved = true; continue; }
                if (active >= Set.Limits.MaxActiveSteps) continue;
                Activate(i, changes);
                active++;
                moved = true;
            }
            if (!moved) break;
        }
        if (_steps.All(s => TutorialFacts.Satisfied(s.Status))) SetPhase(TutorialPhase.Finished, changes);
    }

    private void SetPhase(TutorialPhase to, TutorialChanges changes)
    {
        if (Phase == to) return;
        changes.Phases.Add(new PhaseChanged(Phase, to));
        Phase = to;
    }

    /// <summary>
    /// The hint of a taught step is the strongest one whose idle time has passed, counted from the later of the player's latest activity and the step's beginning. A hint level only rises while the step is
    /// taught; the player acting again starts the count again for the next level, it does not take the hint away.
    /// </summary>
    private void Escalate(TutorialChanges changes)
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            var state = _steps[i];
            if (state.Status != StepStatus.Active) continue;
            var hints = Set.Steps[i].Hints;
            if (hints.Count == 0) continue;
            var idle = Step - Math.Max(LastActivity, state.ActivatedAt);
            var level = 0;
            while (level < hints.Count && hints[level].Idle <= idle) level++;
            if (level > state.Hint)
            {
                state.Hint = level;
                changes.Hints.Add(new HintChanged(i, level));
            }
        }
    }

    private void EndTip(int tip, TipEnd reason, TutorialChanges changes)
    {
        var state = _tips[tip];
        state.Visible = false;
        state.ShownAt = -1;
        state.Until = -1;
        state.EndedAt = Step;
        changes.Tips.Add(new TipChanged(tip, reason, state.Shows));
    }

    private bool TipKnown(TipDef tip) => tip.Skill >= 0 && IsMastered(tip.Skill);

    /// <summary>
    /// Tips are contextual and one-shot: a tip shows while its context holds, for its duration, at most its number of times, a cooldown apart, never after the player acknowledged it, never for a skill the
    /// player has, and not before the step it follows. At most the document's number show at once and two begin at least the tip gap apart; the highest priority has the place. A tip whose context ended is
    /// withdrawn. With tips off, or before the first-run journey is done, none shows.
    /// </summary>
    private void RunTips(IReadOnlyDictionary<string, FactValue> host, TutorialChanges changes)
    {
        var allowed = TipsEnabled && Journey.State == JourneyState.Done;
        var visible = 0;
        for (var t = 0; t < _tips.Length; t++)
        {
            if (!_tips[t].Visible) continue;
            var def = Set.Tips[t];
            if (!allowed || TipKnown(def) || !Holds(def.When, def.WhenFacts, -1, host)) EndTip(t, TipEnd.Withdrawn, changes);
            else if (Step >= _tips[t].Until) EndTip(t, TipEnd.Expired, changes);
            else visible++;
        }
        if (!allowed) return;
        foreach (var t in Set.TipsByPriority)
        {
            if (visible >= Set.Limits.MaxVisibleTips) break;
            if (LastTipShown >= 0 && Step - LastTipShown < Set.Limits.TipGap) break;
            var state = _tips[t];
            var def = Set.Tips[t];
            if (state.Visible || state.Acknowledged || state.Shows >= def.MaxShows || TipKnown(def)) continue;
            if (def.AfterStep >= 0 && !TutorialFacts.Satisfied(_steps[def.AfterStep].Status)) continue;
            if (state.EndedAt >= 0 && Step - state.EndedAt < def.Cooldown) continue;
            if (!Holds(def.When, def.WhenFacts, -1, host)) continue;
            state.Shows++;
            state.Visible = true;
            state.ShownAt = Step;
            state.Until = Step + TutorialPresentation.TipDuration(def.Duration, TextScale);
            LastTipShown = Step;
            visible++;
            changes.Tips.Add(new TipChanged(t, TipEnd.Shown, state.Shows));
        }
    }
}
