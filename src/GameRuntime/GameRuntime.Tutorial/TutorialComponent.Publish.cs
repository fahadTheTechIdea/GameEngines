using TutorialEngine;

namespace GameRuntime;

public sealed partial class TutorialComponent
{
    /// <summary>
    /// What the changes of a call or an evaluation become as events, in a fixed order: journey, settings, skills, steps, hints, tips, phases. Each is built now (the names, keys and motions are read now) and raised later.
    /// </summary>
    private List<Action<IEventPort>> EventsOf(long tick, TutorialChanges changes)
    {
        var set = Runtime.Set;
        var events = new List<Action<IEventPort>>(changes.Journeys.Count + changes.Settings.Count + changes.Skills.Count + changes.Steps.Count + changes.Hints.Count + changes.Tips.Count + changes.Phases.Count);
        foreach (var c in changes.Journeys)
        {
            var e = new TutorialJourneyChanged(Id, Channel, tick, TutorialFacts.JourneyName(c.From), TutorialFacts.JourneyName(c.To), c.Page, c.Language);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Settings)
        {
            var e = new TutorialSettingChanged(Id, Channel, tick, c.Name, c.Value);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Skills)
        {
            var e = new TutorialSkillMastered(Id, Channel, tick, set.Skills[c.Skill].Id);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Steps)
        {
            var e = new TutorialStepChanged(Id, Channel, tick, set.Steps[c.Step].Id, TutorialFacts.StatusName(c.From), TutorialFacts.StatusName(c.To));
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Hints)
        {
            var step = set.Steps[c.Step];
            var e = c.Level == 0
                ? new TutorialHintChanged(Id, Channel, tick, step.Id, 0, "", "", "", "none")
                : new TutorialHintChanged(Id, Channel, tick, step.Id, c.Level, Lower(step.Hints[c.Level - 1].Kind), step.Hints[c.Level - 1].Key, step.Target, Lower(TutorialPresentation.Effective(step.Motion, Runtime.ReducedMotion)));
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Tips)
        {
            var tip = set.Tips[c.Tip];
            var e = new TutorialTipChanged(Id, Channel, tick, tip.Id, Lower(c.Change), c.Shows, tip.Key, Lower(TutorialPresentation.Effective(tip.Motion, Runtime.ReducedMotion)), c.Change == TipEnd.Shown ? Runtime.Tips[c.Tip].Until : 0);
            events.Add(port => port.Emit(e));
        }
        foreach (var c in changes.Phases)
        {
            var e = new TutorialPhaseChanged(Id, Channel, tick, TutorialFacts.PhaseName(c.From), TutorialFacts.PhaseName(c.To));
            events.Add(port => port.Emit(e));
        }
        return events;
    }

    private static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    /// <summary>The changes of a request are raised now, with the answer, in the Update that answered it.</summary>
    private void Raise(IEventPort events, long tick, TutorialChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) raise(events);
    }

    /// <summary>The changes of the evaluation are owed: raised at most <c>maxEvents</c> per Update, in order, oldest first.</summary>
    private void Owe(long tick, TutorialChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) _owed.Enqueue(raise);
    }
}
