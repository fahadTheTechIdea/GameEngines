using System.Globalization;
using TutorialEngine;

namespace GameRuntime;

/// <summary>
/// The player's hands on the tutorial, the way a Godot node polling <c>Input</c> or an XNA game polling <c>Keyboard.GetState</c> would do it: it polls the <see cref="InputState"/> inside its own Update and turns
/// each pressed action into the matching tutorial request event, in the order the presses were written. An action the document maps to an event (<c>inputs</c>) becomes an observation of that event; the controls
/// of the tutorial itself (<see cref="SkipStep"/>, <see cref="SkipTutorial"/>, <see cref="Replay"/>, <see cref="DismissTip"/> and the journey's <see cref="Next"/>, <see cref="Back"/>, <see cref="SkipStory"/>,
/// <see cref="Language"/>, <see cref="Entry"/>, <see cref="ReplayStory"/>) become their requests; any other press is plain activity (a touch: the player is not idle). It changes nothing in the tutorial; the tutorial
/// component answers the request in its own Update (the same tick when this component runs before it). A release is a state change of the input, not a request, and is ignored on purpose.
/// </summary>
public sealed class TutorialInputComponent(InputState input, TutorialSet set, string id = "tutorial-input", string channel = "") : IGameComponent
{
    public const string SkipStep = "tutorial.skip-step";
    public const string SkipTutorial = "tutorial.skip";
    public const string Replay = "tutorial.replay";
    public const string DismissTip = "tutorial.dismiss-tip";
    public const string Next = "journey.next";
    public const string Back = "journey.back";
    public const string SkipStory = "journey.skip";
    public const string Language = "journey.language";
    public const string Entry = "journey.entry";
    public const string ReplayStory = "journey.replay-story";

    private IEventPort? _events;

    public string Id { get; } = id;

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        var events = _events;
        if (events is null) throw new InvalidOperationException("Update before Init");
        var number = 0;
        foreach (var edge in input.Poll())
        {
            if (!edge.Pressed) continue;
            var requestId = string.Create(CultureInfo.InvariantCulture, $"t{context.Tick}-{number++}");
            var (kind, text) = Translate(edge);
            events.Emit(new TutorialRequested(requestId, TutorialRequestNames.NameOf(kind), text, 1, channel));
        }
    }

    private (TutorialRequestKind Kind, string Text) Translate(InputEdgeRaised edge)
    {
        if (set.InputByAction.TryGetValue(edge.Action, out var mapped)) return (TutorialRequestKind.Observe, set.Events[mapped.Event].Id);
        switch (edge.Action)
        {
            case SkipStep: return (TutorialRequestKind.SkipStep, edge.Argument);
            case SkipTutorial: return (TutorialRequestKind.SkipTutorial, "");
            case Replay: return (TutorialRequestKind.Replay, "");
            case DismissTip: return (TutorialRequestKind.DismissTip, edge.Argument);
            case Next: return (TutorialRequestKind.StoryNext, "");
            case Back: return (TutorialRequestKind.StoryBack, "");
            case SkipStory: return (TutorialRequestKind.StorySkip, "");
            case Language: return (TutorialRequestKind.Language, edge.Argument);
            case Entry: return (TutorialRequestKind.Entry, edge.Argument);
            case ReplayStory: return (TutorialRequestKind.ReplayStory, "");
        }
        return (TutorialRequestKind.Touch, "");
    }
}
