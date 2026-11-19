using System.Globalization;

namespace GameRuntime;

/// <summary>
/// The player's hands on a conversation, the way a Godot node polling <c>Input</c> or an XNA game polling <c>Keyboard.GetState</c> would do it: it polls the <see cref="InputState"/> inside
/// its own Update and turns each pressed action into the matching dialogue request event, in the order the presses were written. It changes nothing in the conversation; the dialogue component
/// answers the request in its own Update (the same tick when this component runs before it). A press the dialogue does not know is answered with a <see cref="DialogueRefused"/> carrying
/// <see cref="UnknownAction"/> instead of being dropped. A release is a state change of the input, not a request, and is ignored on purpose. Polling takes every edge written to the state, so
/// this component and any other player component need an input state each.
/// </summary>
public sealed class DialogueInputComponent(InputState input, string id = "dialogue-input", string channel = "") : IGameComponent
{
    /// <summary>Go on after the line that is showing.</summary>
    public const string Advance = "dialogue.advance";

    /// <summary>Take an answer; the press carries the answer's id as its argument.</summary>
    public const string Choose = "dialogue.choose";

    /// <summary>The player's time ran out on the open choice.</summary>
    public const string Timeout = "dialogue.timeout";

    /// <summary>The refusal code for a pressed action that none of the three above names.</summary>
    public const string UnknownAction = "DLG_INPUT_ACTION";

    private IEventPort? _events;

    public string Id { get; } = id;

    public void Init(in ComponentContext context) => _events = context.Events;

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        var number = 0;
        foreach (var edge in input.Poll())
        {
            if (!edge.Pressed) continue;
            var requestId = string.Create(CultureInfo.InvariantCulture, $"i{context.Tick}-{number++}");
            switch (edge.Action)
            {
                case Advance: events.Emit(new DialogueAdvanceRequested(requestId, channel)); break;
                case Choose: events.Emit(new DialogueChooseRequested(requestId, edge.Argument, channel)); break;
                case Timeout: events.Emit(new DialogueTimeoutRequested(requestId, channel)); break;
                default: events.Emit(new DialogueRefused(requestId, context.Tick, UnknownAction, channel)); break;
            }
        }
    }
}
