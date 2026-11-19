using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>A fact the host supplies each time a step needs it (an objective status, a count): declared with its type so a missing or mistyped one is a refusal, not false.</summary>
public sealed record ExternalFactDef(string Id, FactKind Kind, long Minimum, long Maximum);