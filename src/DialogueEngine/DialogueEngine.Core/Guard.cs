using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>A condition with the facts it reads worked out once at load, so a step gathers exactly those.</summary>
public sealed record Guard(Condition Condition, IReadOnlyList<string> Facts);