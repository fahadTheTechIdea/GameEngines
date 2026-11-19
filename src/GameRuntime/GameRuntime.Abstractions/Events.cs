namespace GameRuntime;

// Events that reach a host are flat: primitive fields plus at most one JSON string.

/// <summary>A happening or value change from a source component, to be fed to the mission as a world observation.</summary>
public readonly struct ObservationPosted
{
    public ObservationPosted(string kind, string payloadJson, long tick) { Kind = kind; PayloadJson = payloadJson; Tick = tick; }
    public string Kind { get; }
    public string PayloadJson { get; }
    public long Tick { get; }
}

/// <summary>A host or component command for a mission operation.</summary>
public readonly struct MissionCommandRequested
{
    public MissionCommandRequested(string commandId, string controllerId, string operation, string payloadJson)
    { CommandId = commandId; ControllerId = controllerId; Operation = operation; PayloadJson = payloadJson; }
    public string CommandId { get; }
    public string ControllerId { get; }
    public string Operation { get; }
    public string PayloadJson { get; }
}

/// <summary>An event the mission engine committed (objective change, ending, ...).</summary>
public readonly struct MissionEvent
{
    public MissionEvent(string kind, string payloadJson, long tick, long sequence) { Kind = kind; PayloadJson = payloadJson; Tick = tick; Sequence = sequence; }
    public string Kind { get; }
    public string PayloadJson { get; }
    public long Tick { get; }
    public long Sequence { get; }
}

/// <summary>A request the mission engine issued; it reaches its target on the next tick.</summary>
public readonly struct EffectRequested
{
    public EffectRequested(string requestId, string kind, string payloadJson, long tick) { RequestId = requestId; Kind = kind; PayloadJson = payloadJson; Tick = tick; }
    public string RequestId { get; }
    public string Kind { get; }
    public string PayloadJson { get; }
    public long Tick { get; }
}

/// <summary>The outcome of a mission command.</summary>
public readonly struct CommandResolved
{
    public CommandResolved(string commandId, string engine, bool applied, string code) { CommandId = commandId; Engine = engine; Applied = applied; Code = code; }
    public string CommandId { get; }
    public string Engine { get; }
    public bool Applied { get; }
    public string Code { get; }
}

/// <summary>Raised by the session when a tick has fully settled.</summary>
public readonly struct TickSettled
{
    public TickSettled(long tick) { Tick = tick; }
    public long Tick { get; }
}

/// <summary>Raised once when the session stops on a failure.</summary>
public readonly struct SessionFaulted
{
    public SessionFaulted(long tick, string component, string message) { Tick = tick; Component = component; Message = message; }
    public long Tick { get; }
    public string Component { get; }
    public string Message { get; }
}

/// <summary>Raised once when the mission reaches an ending.</summary>
public readonly struct SessionEnded
{
    public SessionEnded(long tick, string endingId) { Tick = tick; EndingId = endingId; }
    public long Tick { get; }
    public string EndingId { get; }
}

/// <summary>
/// A change of an input action's state from outside the game (a key, a button, a script). It is data only: nothing in
/// the game changes until a component polls the input state in a later Update.
/// </summary>
public readonly struct InputEdgeRaised
{
    public InputEdgeRaised(string action, string argument, bool pressed) { Action = action; Argument = argument; Pressed = pressed; }
    public string Action { get; }
    public string Argument { get; }
    public bool Pressed { get; }
}
