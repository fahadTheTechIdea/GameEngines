namespace GameRuntime;

// Events of the alchemy-sim components. They are flat: primitive fields only.

/// <summary>A request to queue a transformation on a processor. Only queued; the order component submits it to the sim in its next Update.</summary>
public readonly struct OrderTransformationRequested
{
    public OrderTransformationRequested(string requestId, string transformation, string processor, string capability, int count)
    { RequestId = requestId; Transformation = transformation; Processor = processor; Capability = capability; Count = count; }
    public string RequestId { get; }
    /// <summary>The transformation (and alchemy recipe) id.</summary>
    public string Transformation { get; }
    /// <summary>The entity definition id of the processor that runs it.</summary>
    public string Processor { get; }
    /// <summary>The processor's operation (capability) id.</summary>
    public string Capability { get; }
    public int Count { get; }
}

/// <summary>The sim answered an order. The code is "APPLIED" or the engine's own refusal code (for example <c>technology.locked</c>).</summary>
public readonly struct TransformationOrderResolved
{
    public TransformationOrderResolved(string requestId, bool accepted, string code, long tick)
    { RequestId = requestId; Accepted = accepted; Code = code; Tick = tick; }
    public string RequestId { get; }
    public bool Accepted { get; }
    public string Code { get; }
    public long Tick { get; }
}
