using System.Text.RegularExpressions;
using EngineCommon;

namespace TutorialEngine;

/// <summary>
/// The rules a state must satisfy to belong to a document. The digest is not a secret, so a save passes through these as well; it cannot bring in a state the document could not have made. Ranges are checked
/// before the digest, what the state says about the document (which steps may be taught, which tips show) after it.
/// </summary>
internal static class TutorialStateRules
{
    private static readonly Regex IntentText = new("^[a-z0-9._:/-]{0,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static EngineRefusedException Refuse(string message) => new(TutorialCodes.SaveState, message);

    private static bool Time(long value, long step) => value >= 0 && value <= step;

    private static bool TimeOrNone(long value, long step) => value == -1 || Time(value, step);

    public static void CheckRanges(TutorialSet set, TutorialImage image)
    {
        var step = image.Step;
        if (step < 0) throw Refuse("the step is negative");
        if (!Time(image.LastActivity, step)) throw Refuse("the latest activity is outside the time lived");
        if (!TimeOrNone(image.LastTipShown, step)) throw Refuse("the latest tip is outside the time lived");
        if (!Enum.IsDefined((TutorialPhase)image.Phase)) throw Refuse("the phase is not one of the engine's");
        if (image.TextScale < TutorialRuntime.MinTextScale || image.TextScale > TutorialRuntime.MaxTextScale) throw Refuse("the text scale is outside " + TutorialRuntime.MinTextScale + " to " + TutorialRuntime.MaxTextScale);
        if (image.IgnoreMastery && (TutorialPhase)image.Phase == TutorialPhase.Idle) throw Refuse("a tutorial that was never started cannot have been replayed");
        CheckJourney(set, image.Journey);
        if (image.Events.Count != set.Events.Count) throw Refuse("the save has " + image.Events.Count + " events, the document " + set.Events.Count);
        for (var e = 0; e < image.Events.Count; e++)
        {
            var saved = image.Events[e];
            var where = "event '" + set.Events[e].Id + "'";
            if (saved.Count < 0 || saved.Count > TutorialFacts.Saturate) throw Refuse(where + " has a count outside 0 to " + TutorialFacts.Saturate);
            if (!TimeOrNone(saved.LastAt, step)) throw Refuse(where + " last happened outside the time lived");
            if ((saved.Count == 0) != (saved.LastAt == -1)) throw Refuse(where + " has a count and a time that disagree about whether it happened");
        }
        if (image.Skills.Count != set.Skills.Count) throw Refuse("the save has " + image.Skills.Count + " skills, the document " + set.Skills.Count);
        for (var k = 0; k < image.Skills.Count; k++)
            if (image.Skills[k] < 0 || image.Skills[k] > set.Skills[k].Mastered) throw Refuse("skill '" + set.Skills[k].Id + "' has points outside 0 to its mastery");
        if (image.Steps.Count != set.Steps.Count) throw Refuse("the save has " + image.Steps.Count + " steps, the document " + set.Steps.Count);
        for (var s = 0; s < image.Steps.Count; s++) CheckStep(set, image, s);
        if (image.Tips.Count != set.Tips.Count) throw Refuse("the save has " + image.Tips.Count + " tips, the document " + set.Tips.Count);
        for (var t = 0; t < image.Tips.Count; t++) CheckTip(set, image, t);
    }

    private static void CheckJourney(TutorialSet set, ImageJourney journey)
    {
        var def = set.Journey;
        if (!Enum.IsDefined((JourneyState)journey.State)) throw Refuse("the journey state is not one of the engine's");
        var state = (JourneyState)journey.State;
        if (journey.Language.Length > 0 && !def.Languages.Contains(journey.Language)) throw Refuse("the journey language is not one the document offers");
        if (state != JourneyState.Language && journey.Language.Length == 0) throw Refuse("the journey is past the language but no language was chosen");
        if (journey.Page < 0 || journey.Page >= def.Pages.Count) throw Refuse("the story page is outside the document's " + def.Pages.Count);
        if (state is JourneyState.Language && journey.Page != 0) throw Refuse("the journey is at the language but a story page was reached");
        if (journey.Entry.Length > 0 && !def.Entries.Contains(journey.Entry)) throw Refuse("the entry is not one the document offers");
        if (journey.Replaying && state != JourneyState.Story) throw Refuse("the story is replaying but the journey is not at the story");
        if (state == JourneyState.Done && journey.Entry.Length == 0) throw Refuse("the journey is done but no entry was chosen");
        if (!journey.Replaying && state is JourneyState.Language or JourneyState.Story && journey.Entry.Length > 0) throw Refuse("an entry was chosen before the journey reached it");
        if (journey.StorySkipped && state is JourneyState.Language or JourneyState.Story) throw Refuse("the story is marked skipped but is not over");
        if (!IntentText.IsMatch(journey.Intent)) throw Refuse("the intent is not an intent");
    }

    private static void CheckStep(TutorialSet set, TutorialImage image, int index)
    {
        var step = image.Step;
        var saved = image.Steps[index];
        var def = set.Steps[index];
        var where = "step '" + def.Id + "'";
        if (!Enum.IsDefined((StepStatus)saved.Status)) throw Refuse(where + " has a status that is not one of the engine's");
        if (!TimeOrNone(saved.ActivatedAt, step) || !TimeOrNone(saved.DoneAt, step)) throw Refuse(where + " began or ended outside the time lived");
        if (saved.Hint < 0 || saved.Hint > def.Hints.Count) throw Refuse(where + " shows a hint the document does not have");
        if (saved.Baseline.Count != def.Since.Count || saved.Baseline.Any(b => b < 0 || b > TutorialFacts.Saturate)) throw Refuse(where + " has a baseline that does not fit the events its completion reads");
        var status = (StepStatus)saved.Status;
        if (status == StepStatus.Pending && (saved.ActivatedAt != -1 || saved.DoneAt != -1 || saved.Hint != 0)) throw Refuse(where + " is pending but has begun");
        if (status == StepStatus.Pending && saved.Baseline.Any(b => b != 0)) throw Refuse(where + " is pending but remembers counts from a beginning it never had");
        if (status == StepStatus.Active && (saved.ActivatedAt == -1 || saved.DoneAt != -1)) throw Refuse(where + " is being taught but has no beginning, or already ended");
        if (status != StepStatus.Active && saved.Hint != 0) throw Refuse(where + " shows a hint but is not being taught");
        if (TutorialFacts.Satisfied(status) && saved.DoneAt == -1) throw Refuse(where + " is satisfied but has no end");
        if (status == StepStatus.Done && saved.ActivatedAt == -1) throw Refuse(where + " is done but never began");
        if (saved.ActivatedAt != -1 && saved.DoneAt != -1 && saved.DoneAt < saved.ActivatedAt) throw Refuse(where + " ended before it began");
        if (status == StepStatus.Mastered && def.Skill < 0) throw Refuse(where + " is mastered but teaches no skill");
    }

    private static void CheckTip(TutorialSet set, TutorialImage image, int index)
    {
        var step = image.Step;
        var saved = image.Tips[index];
        var def = set.Tips[index];
        var where = "tip '" + def.Id + "'";
        if (saved.Shows < 0 || saved.Shows > def.MaxShows) throw Refuse(where + " showed more often than the document allows");
        if (!TimeOrNone(saved.EndedAt, step)) throw Refuse(where + " ended outside the time lived");
        if (saved.Visible)
        {
            if (!Time(saved.ShownAt, step)) throw Refuse(where + " shows but began outside the time lived");
            if (saved.Shows < 1) throw Refuse(where + " shows but never showed");
            if (saved.Until < saved.ShownAt + def.Duration || saved.Until > saved.ShownAt + (long)def.Duration * TutorialRuntime.MaxTextScale / 1000 + 1) throw Refuse(where + " lasts a time its duration cannot give");
            if (saved.Acknowledged) throw Refuse(where + " shows although the player acknowledged it");
        }
        else if (saved.ShownAt != -1 || saved.Until != -1) throw Refuse(where + " does not show but has times of showing");
    }

    public static void CheckAgainstDocument(TutorialSet set, TutorialImage image)
    {
        var phase = (TutorialPhase)image.Phase;
        var statuses = image.Steps.Select(s => (StepStatus)s.Status).ToList();
        var active = statuses.Count(s => s == StepStatus.Active);
        if (active > set.Limits.MaxActiveSteps) throw Refuse("more steps are being taught than the document allows (" + set.Limits.MaxActiveSteps + ")");
        if (phase == TutorialPhase.Idle && statuses.Any(s => s != StepStatus.Pending)) throw Refuse("the tutorial was never started but a step has moved");
        if (phase == TutorialPhase.Skipped && statuses.Any(s => !TutorialFacts.Satisfied(s))) throw Refuse("the tutorial was skipped but a step still waits");
        if (phase == TutorialPhase.Finished && statuses.Any(s => !TutorialFacts.Satisfied(s))) throw Refuse("the tutorial is finished but a step still waits");
        if (active > 0 && phase != TutorialPhase.Running) throw Refuse("a step is being taught but the tutorial is not running");
        if (image.IgnoreMastery && statuses.Any(s => s == StepStatus.Mastered)) throw Refuse("a step is mastered in a tutorial that was replayed in full");
        if (phase != TutorialPhase.Skipped)
        {
            // A step the player (or a branch) moved on had its prerequisites met; only a skipped tutorial moves steps that were never available.
            for (var s = 0; s < set.Steps.Count; s++)
            {
                if (statuses[s] == StepStatus.Pending) continue;
                var def = set.Steps[s];
                if (def.Requires.Any(r => !TutorialFacts.Satisfied(statuses[r])) || !TutorialFacts.AlternativesMet(def.RequiresAny.Select(r => statuses[r])))
                    throw Refuse("step '" + def.Id + "' has begun although its prerequisites are not met");
            }
        }
        if (image.Tips.Count(t => t.Visible) > set.Limits.MaxVisibleTips) throw Refuse("more tips show than the document allows (" + set.Limits.MaxVisibleTips + ")");
        if (!image.TipsEnabled && image.Tips.Any(t => t.Visible)) throw Refuse("a tip shows although tips are off");
        if ((JourneyState)image.Journey.State != JourneyState.Done && image.Tips.Any(t => t.Visible)) throw Refuse("a tip shows before the first-run journey is done");
        for (var k = 0; k < set.Skills.Count; k++)
        {
            var reachable = 0L;
            foreach (var evidence in set.Skills[k].Evidence) reachable = Math.Min(set.Skills[k].Mastered, reachable + image.Events[evidence.Event].Count * evidence.Points);
            if (image.Skills[k] > reachable) throw Refuse("skill '" + set.Skills[k].Id + "' has more points than the events it is evidence for can have given");
        }
    }
}
