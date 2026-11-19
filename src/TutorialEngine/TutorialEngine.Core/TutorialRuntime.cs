using System.Text.RegularExpressions;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

/// <summary>
/// The state of one player's tutorial and first-run journey: which steps are done, which are being taught, how long the player has been idle, which tips showed, which skills the player already has, the
/// chosen language and story page, and the accessibility settings. It has no clock of its own. The caller gives it a step number (the game layer converts the tick or the calendar instant) and the facts
/// the host owns; every change of time, every observation of what the player did and every control (skip, replay, next, back) is one call, refused whole with a code when it is wrong. Everything it holds is
/// bounded by the document. Nothing here reads a clock, a float or a random source: the same document, calls and facts give the same state.
/// </summary>
public sealed partial class TutorialRuntime
{
    public const int MinTextScale = 1000;
    public const int MaxTextScale = 3000;
    public const int MaxObserve = 1000;
    public const int MaxIntentLength = 128;

    private static readonly Regex IntentText = new("^[a-z0-9._:/-]{0,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly StepState[] _steps;
    private readonly TipState[] _tips;
    private readonly long[] _count;
    private readonly long[] _lastAt;
    private readonly long[] _points;
    private readonly Dictionary<string, FactValue> _scratch = new(StringComparer.Ordinal);

    private TutorialRuntime(TutorialSet set, long step)
    {
        Set = set; Step = step; LastActivity = step;
        _steps = set.Steps.Select(s => new StepState(s.Since.Count)).ToArray();
        _tips = set.Tips.Select(_ => new TipState()).ToArray();
        _count = new long[set.Events.Count];
        _lastAt = Enumerable.Repeat(-1L, set.Events.Count).ToArray();
        _points = new long[set.Skills.Count];
    }

    public TutorialSet Set { get; }

    /// <summary>The step the state is at.</summary>
    public long Step { get; private set; }

    /// <summary>The step of the player's latest activity (an observation, a touch, a control); idle time is counted from it.</summary>
    public long LastActivity { get; private set; }

    /// <summary>The step the latest tip began showing at, or -1; the tip gap is counted from it.</summary>
    public long LastTipShown { get; private set; } = -1;

    public TutorialPhase Phase { get; private set; }

    /// <summary>True once the tutorial was replayed: steps are taught even for skills the player has (the player asked for the whole tutorial).</summary>
    public bool IgnoreMastery { get; private set; }

    public JourneyProgress Journey { get; } = new();
    public bool ReducedMotion { get; private set; }
    public int TextScale { get; private set; } = MinTextScale;
    public bool TipsEnabled { get; private set; } = true;

    public IReadOnlyList<StepState> Steps => _steps;
    public IReadOnlyList<TipState> Tips => _tips;
    public IReadOnlyList<long> EventCounts => _count;
    public IReadOnlyList<long> EventLastAt => _lastAt;
    public IReadOnlyList<long> SkillPoints => _points;

    public static TutorialRuntime Create(TutorialSet set, long startStep = 0)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (startStep < 0) throw new EngineRefusedException(TutorialCodes.StepOrder, "the start step is not negative");
        return new TutorialRuntime(set, startStep);
    }

    /// <summary>Rebuilds a runtime from checked save data; only the save reader calls it.</summary>
    internal static TutorialRuntime Restore(TutorialSet set, TutorialImage image)
    {
        var runtime = new TutorialRuntime(set, image.Step)
        {
            LastActivity = image.LastActivity, LastTipShown = image.LastTipShown, Phase = (TutorialPhase)image.Phase, IgnoreMastery = image.IgnoreMastery,
            ReducedMotion = image.ReducedMotion, TextScale = image.TextScale, TipsEnabled = image.TipsEnabled,
        };
        runtime.Journey.State = (JourneyState)image.Journey.State;
        runtime.Journey.Language = image.Journey.Language;
        runtime.Journey.Page = image.Journey.Page;
        runtime.Journey.StorySkipped = image.Journey.StorySkipped;
        runtime.Journey.Replaying = image.Journey.Replaying;
        runtime.Journey.Entry = image.Journey.Entry;
        runtime.Journey.Intent = image.Journey.Intent;
        for (var e = 0; e < image.Events.Count; e++) { runtime._count[e] = image.Events[e].Count; runtime._lastAt[e] = image.Events[e].LastAt; }
        for (var k = 0; k < image.Skills.Count; k++) runtime._points[k] = image.Skills[k];
        for (var s = 0; s < image.Steps.Count; s++)
        {
            var saved = image.Steps[s];
            var state = runtime._steps[s];
            state.Status = (StepStatus)saved.Status; state.ActivatedAt = saved.ActivatedAt; state.DoneAt = saved.DoneAt; state.Hint = saved.Hint;
            for (var b = 0; b < saved.Baseline.Count; b++) state.Baseline[b] = saved.Baseline[b];
        }
        for (var t = 0; t < image.Tips.Count; t++)
        {
            var saved = image.Tips[t];
            var state = runtime._tips[t];
            state.Shows = saved.Shows; state.Acknowledged = saved.Acknowledged; state.Visible = saved.Visible; state.ShownAt = saved.ShownAt; state.Until = saved.Until; state.EndedAt = saved.EndedAt;
        }
        return runtime;
    }

    /// <summary>The digest of the whole state: the same for the same state, whatever order things were done in.</summary>
    public string StateDigest() => TutorialImage.Of(this).Digest;

    private long Saturating(long value) => Math.Min(TutorialFacts.Saturate, value);

    // ---- time -------------------------------------------------------------------------------------------------------------------------

    /// <summary>Moves the state to a later step. Time never goes back; the same step is allowed. Nothing is evaluated: <see cref="Evaluate"/> does that.</summary>
    public void MoveTo(long step)
    {
        if (step < Step) throw new EngineRefusedException(TutorialCodes.StepOrder, "the step is " + Step + " and cannot go back to " + step);
        Step = step;
    }

    /// <summary>Moves to <paramref name="step"/> and evaluates, in one call.</summary>
    public TutorialChanges Advance(long step, IReadOnlyDictionary<string, FactValue> hostFacts)
    {
        MoveTo(step);
        return Evaluate(hostFacts);
    }

    // ---- reading ----------------------------------------------------------------------------------------------------------------------

    public bool IsMastered(int skill) => _points[skill] >= Set.Skills[skill].Mastered;

    /// <summary>The number of steps that no longer wait for the player: done, skipped, mastered or bypassed.</summary>
    public int Satisfied() => _steps.Count(s => TutorialFacts.Satisfied(s.Status));

    /// <summary>How much of the tutorial is behind the player, 0 to 1000, for the progress bar.</summary>
    public int ProgressPermille() => (int)((long)Satisfied() * 1000 / _steps.Length);

    public IEnumerable<int> ActiveSteps()
    {
        for (var i = 0; i < _steps.Length; i++)
            if (_steps[i].Status == StepStatus.Active) yield return i;
    }

    public StepState StepState(string id) => _steps[StepIndex(id)];

    private int StepIndex(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!Set.StepById.TryGetValue(id, out var step)) throw new EngineRefusedException(TutorialCodes.UnknownStep, "there is no step '" + id + "'");
        return step.Index;
    }

    private int TipIndex(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!Set.TipById.TryGetValue(id, out var tip)) throw new EngineRefusedException(TutorialCodes.UnknownTip, "there is no tip '" + id + "'");
        return tip.Index;
    }

    /// <summary>The hints showing now, one per step being taught that has one, in document order, with the motion the player's settings allow.</summary>
    public IReadOnlyList<HintView> Hints()
    {
        var views = new List<HintView>();
        foreach (var i in ActiveSteps())
        {
            var state = _steps[i];
            if (state.Hint == 0) continue;
            var step = Set.Steps[i];
            var hint = step.Hints[state.Hint - 1];
            views.Add(new HintView(step.Id, state.Hint, hint.Kind, hint.Key, step.Target, TutorialPresentation.Effective(step.Motion, ReducedMotion)));
        }
        return views;
    }

    /// <summary>The tips showing now, in document order.</summary>
    public IReadOnlyList<TipView> VisibleTips()
    {
        var views = new List<TipView>();
        for (var t = 0; t < _tips.Length; t++)
        {
            if (!_tips[t].Visible) continue;
            var tip = Set.Tips[t];
            views.Add(new TipView(tip.Id, tip.Key, _tips[t].ShownAt, _tips[t].Until, TutorialPresentation.Effective(tip.Motion, ReducedMotion)));
        }
        return views;
    }
}
