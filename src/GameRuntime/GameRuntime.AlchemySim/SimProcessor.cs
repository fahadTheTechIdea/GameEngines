namespace GameRuntime;

/// <summary>An entity definition that can run a transformation, and the operation (capability) that queues it.</summary>
public sealed record SimProcessor(string EntityDefinition, string Capability);
