using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// What the SimEngine says about a transformation that alchemy has no place for: amounts, ports, duration, cost and quality. These are
/// the engine's own value objects, read from its content and never copied into alchemy content or edited here.
/// </summary>
public sealed record SimRecipeFacts(
    string TransformationId,
    string Category,
    Bill Inputs,
    Bill Tools,
    Bill ToolConsumption,
    Bill Outputs,
    GameDuration Duration,
    Fix Cash,
    IReadOnlyList<ResourcePortRoute> InputPorts,
    IReadOnlyList<ResourcePortRoute> ToolPorts,
    IReadOnlyList<ResourcePortRoute> OutputPorts,
    int OutputQualityRules,
    int OperatingInputs);
