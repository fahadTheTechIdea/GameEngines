namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests, the input that makes them and the journal that records them cannot disagree about which requests exist.</summary>
public enum TutorialRequestKind
{
    Observe, Touch, Start, SkipStep, SkipTutorial, Replay, ResetTips, Resume, DismissTip,
    Language, StoryNext, StoryBack, StorySkip, Entry, ReplayStory, Intent, ReducedMotion, TextScale, Tips, Progress,
}

/// <summary>The names of the request kinds on the wire and in the journal: one place to turn a kind into its name and a name into its kind.</summary>
public static class TutorialRequestNames
{
    public static string NameOf(TutorialRequestKind kind)
    {
        switch (kind)
        {
            case TutorialRequestKind.Observe: return "observe";
            case TutorialRequestKind.Touch: return "touch";
            case TutorialRequestKind.Start: return "start";
            case TutorialRequestKind.SkipStep: return "skip-step";
            case TutorialRequestKind.SkipTutorial: return "skip-tutorial";
            case TutorialRequestKind.Replay: return "replay";
            case TutorialRequestKind.ResetTips: return "reset-tips";
            case TutorialRequestKind.Resume: return "resume";
            case TutorialRequestKind.DismissTip: return "dismiss-tip";
            case TutorialRequestKind.Language: return "language";
            case TutorialRequestKind.StoryNext: return "story-next";
            case TutorialRequestKind.StoryBack: return "story-back";
            case TutorialRequestKind.StorySkip: return "story-skip";
            case TutorialRequestKind.Entry: return "entry";
            case TutorialRequestKind.ReplayStory: return "replay-story";
            case TutorialRequestKind.Intent: return "intent";
            case TutorialRequestKind.ReducedMotion: return "reduced-motion";
            case TutorialRequestKind.TextScale: return "text-scale";
            case TutorialRequestKind.Tips: return "tips";
            case TutorialRequestKind.Progress: return "progress";
        }
        throw new InvalidOperationException("unknown request kind " + kind);
    }

    /// <summary>The kind a name stands for, or null when the name is none of them (the component answers that with a refusal, it does not drop it).</summary>
    public static TutorialRequestKind? KindOf(string name) => Enum.GetValues<TutorialRequestKind>().Cast<TutorialRequestKind?>().FirstOrDefault(k => NameOf(k!.Value) == name);
}

/// <summary>One queued request: the kind as the sender wrote it (it may be none the component knows), the id, the text and the number.</summary>
internal readonly record struct TutorialPending(string Kind, string RequestId, string Text, long Number);

/// <summary>The one place the tutorial request event is connected: the handler gets the request channel and the request as a <see cref="TutorialPending"/>.</summary>
internal static class TutorialRequestPort
{
    public static void Connect(IEventPort events, Action<string, TutorialPending> handler) =>
        events.Connect<TutorialRequested>(r => handler(r.Channel, new TutorialPending(r.Kind, r.RequestId, r.Text, r.Number)));
}
