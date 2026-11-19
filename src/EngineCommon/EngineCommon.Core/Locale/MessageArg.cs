using ScenarioEngine.Contracts;

namespace EngineCommon;

/// <summary>A value a message shows, taken from a fact at the moment the text was produced, so a replay shows the same. Dialogue lines and calendar date formats both render through it.</summary>
public sealed record MessageArg(string Name, FactValue Value);
