namespace EngineCommon.Balance;

/// <summary>What one stage run came to: the checkpoint of the first moment it reached the stage's level, or null when the run ended without reaching it, and optionally the work it cost (the engine's own unit, for the model that counts it; 0 when it does not).</summary>
public sealed record StageOutcome<TState>(TState? Checkpoint, long Work = 0) where TState : class
{
    public bool Reached => Checkpoint is not null;
}

/// <summary>
/// What an engine supplies to the fixed-effort splitting sampler: a rare event cut into <see cref="Stages"/> nested levels (reaching level k implies having reached level k-1) and the two ways to run toward the next level. A checkpoint
/// is the complete state of a run at the first moment it reached a level; running from it under another seed must be an independent future of that state (the engines' counter-based random numbers make that true: a state holds no
/// generator, only the step and draw counts).
/// </summary>
public interface ISplittingModel<TState> where TState : class
{
    int Stages { get; }

    /// <summary>Stage 1: a fresh run from the start under <paramref name="seed"/>, stopped at the first moment level 1 is reached (or at the horizon).</summary>
    StageOutcome<TState> First(long seed);

    /// <summary>Stage 2 to <see cref="Stages"/>: continues from a checkpoint that reached level <paramref name="stage"/> - 1, under <paramref name="seed"/>, stopped at the first moment level <paramref name="stage"/> is reached (at once when the checkpoint is already there) or at the horizon.</summary>
    StageOutcome<TState> Next(TState checkpoint, int stage, long seed);
}
