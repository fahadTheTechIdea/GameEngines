using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>A read-only copy of one thing an agent knows, for the host and for tests.</summary>
public sealed record PerceptInfo(string Kind, string Id, string Owner, long Seen, IReadOnlyDictionary<string, FactValue> Attrs);