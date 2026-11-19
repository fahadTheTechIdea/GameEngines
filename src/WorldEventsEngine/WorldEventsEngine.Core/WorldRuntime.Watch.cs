namespace WorldEventsEngine;

/// <summary>
/// What an observer of one runtime sees of the hazards' own onset trials, in the order the engine takes them. <see cref="Trial"/> is called for every trial the step is about to take (a region exposed to the hazard, with no such hazard going
/// on there and not cooling down), with the chance the engine itself will use for it: <see cref="WorldRuntime.OnsetChance"/> of the state at that moment, or 0 when the hazard's condition, an exclusion or the director's pacing would
/// veto the onset anyway. <see cref="Started"/> is called when a trial's onset did start, with the rung its severity drew. Neither call can change a draw or the state; an observer that throws stops the step.
/// </summary>
public interface IHazardWatcher
{
    void Trial(long step, int hazard, int region, long chancePpm);

    void Started(long step, int hazard, int region, int rung);
}

public sealed partial class WorldRuntime
{
    private IHazardWatcher? _watcher;

    /// <summary>
    /// Lets <paramref name="watcher"/> see the onset trials of the unforced steps lived from now on (null stops it). The watcher belongs to this runtime only: a branch, a restore or a save does not carry it, and a forced step
    /// (<see cref="BranchForcingOnset"/>) is not reported, because its trial is not a chance.
    /// </summary>
    public void Watch(IHazardWatcher? watcher) => _watcher = watcher;
}
