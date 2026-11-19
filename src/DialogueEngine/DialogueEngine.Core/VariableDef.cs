using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>A dialogue variable: a typed value with an initial value, and for numbers a range that arithmetic stays inside (it saturates), for text the values it may hold.</summary>
public sealed record VariableDef(string Id, FactKind Kind, FactValue Initial, long Minimum, long Maximum, IReadOnlyList<string> Allowed);