using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

public sealed partial class TutorialRuntime
{
    // What the player did, as the host saw it. An observation is a count and a time; what it completes is decided in Evaluate, from the facts.

    /// <summary>
    /// Records that the player did <paramref name="eventId"/> <paramref name="count"/> times at the current step: its count and time are updated, the player counts as active (idle time starts again), and
    /// every skill the event is evidence for gains its points (a skill that reaches its mastery is reported).
    /// </summary>
    public TutorialChanges Observe(string eventId, int count = 1)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        if (!Set.EventById.TryGetValue(eventId, out var evt)) throw new EngineRefusedException(TutorialCodes.UnknownEvent, "there is no event '" + eventId + "'");
        if (count < 1 || count > MaxObserve) throw new EngineRefusedException(TutorialCodes.Count, "an observation counts 1 to " + MaxObserve + " times, not " + count);
        var changes = new TutorialChanges();
        _count[evt.Index] = Saturating(_count[evt.Index] + count);
        _lastAt[evt.Index] = Step;
        LastActivity = Step;
        foreach (var (skill, points) in Set.EvidenceByEvent[evt.Index])
        {
            var before = _points[skill];
            var mastered = Set.Skills[skill].Mastered;
            _points[skill] = Math.Min(mastered, before + (long)points * count);
            if (before < mastered && _points[skill] >= mastered) changes.Skills.Add(new SkillChanged(skill));
        }
        return changes;
    }

    /// <summary>The player did something the document has no event for (any key, any movement): the player is not idle.</summary>
    public void Touch() => LastActivity = Step;

    /// <summary>The player came back to a tutorial that was saved: idle time starts again from now, so the time away is not taken for being stuck.</summary>
    public void Resume() => LastActivity = Step;
}
