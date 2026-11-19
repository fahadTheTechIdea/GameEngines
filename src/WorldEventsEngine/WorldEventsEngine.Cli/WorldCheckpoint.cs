using ScenarioEngine.Contracts;
using WorldEventsEngine;

/// <summary>
/// A run at the first moment it reached a level of a rare-event ladder: the world as it stands (never advanced again, only branched), the host facts as the scenario had set them by then, how many scripted calls had been made, and the
/// highest rung (plus one) any natural onset of the target hazard had reached. The score is history, not world state, so it travels with the checkpoint.
/// </summary>
internal sealed record WorldCheckpoint(WorldRuntime Runtime, IReadOnlyDictionary<string, FactValue> Facts, int Next, int Score);
