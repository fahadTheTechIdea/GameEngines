using EngineCommon;
using TutorialEngine;

namespace GameRuntime;

public sealed partial class TutorialComponent
{
    /// <summary>Answers one request. Returns whether it changed something the player's saved progress holds.</summary>
    private bool Answer(IEventPort events, long tick, in TutorialPending request)
    {
        var persist = false;
        try
        {
            var kind = TutorialRequestNames.KindOf(request.Kind);
            if (kind is null) throw new EngineRefusedException(UnknownKind, "there is no request kind '" + request.Kind + "'");
            persist = Perform(events, tick, request, kind.Value);
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the tutorial has already been left as it was.
            events.Emit(new TutorialRefused(Id, Channel, request.RequestId, tick, refused.Code));
        }
        _answered++;
        return persist;
    }

    private static int ToInt(long value, string code) => value is < int.MinValue or > int.MaxValue ? throw new EngineRefusedException(code, "a number is " + value + ", beyond what the request can take") : (int)value;

    private bool Perform(IEventPort events, long tick, in TutorialPending request, TutorialRequestKind kind)
    {
        switch (kind)
        {
            case TutorialRequestKind.Observe: return Done(events, tick, request, Runtime.Observe(request.Text, ToInt(request.Number, TutorialCodes.Count)));
            case TutorialRequestKind.Touch: Runtime.Touch(); return Done(events, tick, request, new TutorialChanges());
            case TutorialRequestKind.Start: return Done(events, tick, request, Runtime.Start());
            case TutorialRequestKind.SkipStep: return Done(events, tick, request, Runtime.SkipStep(request.Text));
            case TutorialRequestKind.SkipTutorial: return Done(events, tick, request, Runtime.SkipTutorial());
            case TutorialRequestKind.Replay: return Done(events, tick, request, Runtime.Replay());
            case TutorialRequestKind.ResetTips: return Done(events, tick, request, Runtime.ResetTips());
            case TutorialRequestKind.Resume: Runtime.Resume(); return Done(events, tick, request, new TutorialChanges());
            case TutorialRequestKind.DismissTip: return Done(events, tick, request, Runtime.DismissTip(request.Text));
            case TutorialRequestKind.Language: return Done(events, tick, request, Runtime.SelectLanguage(request.Text));
            case TutorialRequestKind.StoryNext: return Done(events, tick, request, Runtime.StoryNext());
            case TutorialRequestKind.StoryBack: return Done(events, tick, request, Runtime.StoryBack());
            case TutorialRequestKind.StorySkip: return Done(events, tick, request, Runtime.StorySkip());
            case TutorialRequestKind.Entry: return Done(events, tick, request, Runtime.ChooseEntry(request.Text));
            case TutorialRequestKind.ReplayStory: return Done(events, tick, request, Runtime.ReplayStory());
            case TutorialRequestKind.Intent: return Done(events, tick, request, Runtime.SetIntent(request.Text));
            case TutorialRequestKind.ReducedMotion: return Done(events, tick, request, Runtime.SetReducedMotion(request.Number != 0));
            case TutorialRequestKind.TextScale: return Done(events, tick, request, Runtime.SetTextScale(ToInt(request.Number, TutorialCodes.Setting)));
            case TutorialRequestKind.Tips: return Done(events, tick, request, Runtime.SetTipsEnabled(request.Number != 0));
            case TutorialRequestKind.Progress: ProgressAnswer(events, tick, request); return false;
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    /// <summary>The request was applied: the answer, then the changes it made. Returns whether the player's saved progress changed.</summary>
    private bool Done(IEventPort events, long tick, in TutorialPending request, TutorialChanges changes)
    {
        events.Emit(new TutorialApplied(Id, Channel, request.RequestId, tick));
        Raise(events, tick, changes);
        return changes.Persist;
    }

    private void ProgressAnswer(IEventPort events, long tick, in TutorialPending request)
    {
        var active = string.Join(';', Runtime.ActiveSteps().Select(i => Runtime.Set.Steps[i].Id));
        events.Emit(new TutorialProgressAnswered(Id, Channel, request.RequestId, tick, Runtime.ProgressPermille(), Runtime.Satisfied(), Runtime.Steps.Count, TutorialFacts.PhaseName(Runtime.Phase),
            TutorialFacts.JourneyName(Runtime.Journey.State), active, Runtime.Journey.Language));
    }
}
