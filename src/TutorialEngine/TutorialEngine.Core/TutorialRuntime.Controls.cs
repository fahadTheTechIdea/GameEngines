using EngineCommon;

namespace TutorialEngine;

public sealed partial class TutorialRuntime
{
    // What the player can ask of the tutorial itself: begin it, skip a step or all of it, replay it, dismiss a tip. Each is refused whole, with its own code, when it does not fit where the tutorial is.

    private static EngineRefusedException WrongPhase(string message) => new(TutorialCodes.Phase, message);

    /// <summary>Begins teaching. Allowed once, from idle; the first step becomes available at the next evaluation.</summary>
    public TutorialChanges Start()
    {
        if (Phase != TutorialPhase.Idle) throw WrongPhase("the tutorial can only be started from idle, it is " + TutorialFacts.PhaseName(Phase));
        var changes = new TutorialChanges();
        SetPhase(TutorialPhase.Running, changes);
        LastActivity = Step;
        return changes;
    }

    /// <summary>The player skips the whole tutorial: every step that was not satisfied is skipped, hints go, and tips (which are separate) go on.</summary>
    public TutorialChanges SkipTutorial()
    {
        if (Phase is not (TutorialPhase.Idle or TutorialPhase.Running)) throw WrongPhase("only a tutorial that is idle or running can be skipped, it is " + TutorialFacts.PhaseName(Phase));
        var changes = new TutorialChanges();
        for (var i = 0; i < _steps.Length; i++)
            if (!TutorialFacts.Satisfied(_steps[i].Status)) SetStatus(i, StepStatus.Skipped, changes);
        SetPhase(TutorialPhase.Skipped, changes);
        LastActivity = Step;
        return changes;
    }

    /// <summary>
    /// The player asks to see the tutorial again: every step starts over, the whole tutorial is taught (a skill the player has no longer skips a step), hints start from none. What the player did before
    /// (event counts, skill points) and the tips' memory stay; steps that should count only new doing read <c>since_step</c>.
    /// </summary>
    public TutorialChanges Replay()
    {
        if (Phase == TutorialPhase.Idle) throw WrongPhase("a tutorial that was never started cannot be replayed");
        var changes = new TutorialChanges();
        for (var i = 0; i < _steps.Length; i++)
        {
            var state = _steps[i];
            if (state.Status != StepStatus.Pending) changes.Steps.Add(new StepChanged(i, state.Status, StepStatus.Pending));
            if (state.Hint > 0) changes.Hints.Add(new HintChanged(i, 0));
            state.Status = StepStatus.Pending; state.ActivatedAt = -1; state.DoneAt = -1; state.Hint = 0;
            Array.Clear(state.Baseline);
        }
        IgnoreMastery = true;
        SetPhase(TutorialPhase.Running, changes);
        LastActivity = Step;
        return changes;
    }

    /// <summary>The player skips the step being taught. Only a step that is being taught and that the document allows to be skipped.</summary>
    public TutorialChanges SkipStep(string stepId)
    {
        var index = StepIndex(stepId);
        var state = _steps[index];
        if (state.Status != StepStatus.Active) throw new EngineRefusedException(TutorialCodes.StepState, "step '" + stepId + "' is " + TutorialFacts.StatusName(state.Status) + ", only a step being taught can be skipped");
        if (!Set.Steps[index].Skippable) throw new EngineRefusedException(TutorialCodes.NotSkippable, "step '" + stepId + "' cannot be skipped");
        var changes = new TutorialChanges();
        SetStatus(index, StepStatus.Skipped, changes);
        LastActivity = Step;
        return changes;
    }

    /// <summary>The player says "got it" to a tip that shows: it ends and is never shown again.</summary>
    public TutorialChanges DismissTip(string tipId)
    {
        var index = TipIndex(tipId);
        if (!_tips[index].Visible) throw new EngineRefusedException(TutorialCodes.TipState, "tip '" + tipId + "' is not showing");
        var changes = new TutorialChanges();
        _tips[index].Acknowledged = true;
        EndTip(index, TipEnd.Dismissed, changes);
        LastActivity = Step;
        return changes;
    }

    /// <summary>Forgets what the tips showed (the player asked to see the hints again): every tip is withdrawn and starts over.</summary>
    public TutorialChanges ResetTips()
    {
        var changes = new TutorialChanges();
        for (var t = 0; t < _tips.Length; t++)
        {
            if (_tips[t].Visible) EndTip(t, TipEnd.Withdrawn, changes);
            _tips[t].Shows = 0; _tips[t].Acknowledged = false; _tips[t].EndedAt = -1;
        }
        LastTipShown = -1;
        return changes;
    }

    // ---- accessibility and preferences ------------------------------------------------------------------------------------------------

    private static void Setting(TutorialChanges changes, string name, long value) => changes.Settings.Add(new SettingChanged(name, value));

    public TutorialChanges SetReducedMotion(bool on)
    {
        var changes = new TutorialChanges();
        if (ReducedMotion != on) { ReducedMotion = on; Setting(changes, "reduced_motion", on ? 1 : 0); }
        return changes;
    }

    /// <summary>Enlarged text, in permille of normal (1000 to 3000): tips stay longer so they can still be read.</summary>
    public TutorialChanges SetTextScale(int permille)
    {
        if (permille < MinTextScale || permille > MaxTextScale) throw new EngineRefusedException(TutorialCodes.Setting, "a text scale is " + MinTextScale + " to " + MaxTextScale + " permille, not " + permille);
        var changes = new TutorialChanges();
        if (TextScale != permille) { TextScale = permille; Setting(changes, "text_scale", permille); }
        return changes;
    }

    public TutorialChanges SetTipsEnabled(bool on)
    {
        var changes = new TutorialChanges();
        if (TipsEnabled != on)
        {
            TipsEnabled = on;
            Setting(changes, "tips", on ? 1 : 0);
            if (!on) WithdrawVisibleTips(changes);
        }
        return changes;
    }

    /// <summary>Ends every tip that shows, at once: tips turned off, or the story shown again, take them away without waiting for the next evaluation.</summary>
    private void WithdrawVisibleTips(TutorialChanges changes)
    {
        for (var t = 0; t < _tips.Length; t++)
            if (_tips[t].Visible) EndTip(t, TipEnd.Withdrawn, changes);
    }
}
