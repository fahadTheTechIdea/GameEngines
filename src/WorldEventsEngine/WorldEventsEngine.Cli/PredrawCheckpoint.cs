using ScenarioEngine.Contracts;
using WorldEventsEngine;

/// <summary>
/// A run just before the severity of an onset is drawn: the world as it stood after step <c>Step - 1</c> (never advanced again, only branched), the host facts and the scripted calls made by then, the step and region of the natural onset
/// whose trial passes. The level the onset has been conditioned on is the stage that restarts it (a checkpoint that reached level L is handed to stage L + 1): the onset is then known to be among the worst <c>Ladder[L - 1].TailPpm</c> parts per million of the ladder, so a restart draws its severity again below that bound.
/// </summary>
internal sealed record PredrawCheckpoint(WorldRuntime Before, IReadOnlyDictionary<string, FactValue> Facts, int Next, long Step, int Region);
