using EngineCommon;

namespace WorldEventsEngine;

public sealed partial class WorldRuntime
{
    /// <summary>
    /// A copy of this world, at this very step, that goes on under another seed: the continuation of a checkpoint with fresh luck. Every random number of the engine is a pure function of the document's seed, a stream name and a step
    /// or a draw count (no generator state exists), and the state holds the step and the draw count, so a state restored under a different seed lives its future from that seed alone while its past stays as it was. This is what a
    /// rare-event sampler needs to restart many independent futures from one state that has just reached an intermediate level.
    /// <paramref name="reseeded"/> must be the same document with another seed: the same <c>id</c> (refused with <see cref="WorldCodes.BranchDocument"/> otherwise) and the same shape, which the save's own state rules check (a mismatch
    /// of counts or an impossible state is refused with <see cref="WorldCodes.SaveState"/>). A document that differs in a rate or a rule but not in shape cannot be told from a reseeded one here; telling them apart is the caller's, who
    /// makes the set from the same text with the seed replaced. This runtime is not changed, so it can be branched again and again. With the same seed as this runtime's, the copy lives exactly the future this one would.
    /// </summary>
    public WorldRuntime Branch(WorldSet reseeded)
    {
        ArgumentNullException.ThrowIfNull(reseeded);
        if (reseeded.Id != Set.Id) throw new EngineRefusedException(WorldCodes.BranchDocument, "a branch continues the same world under another seed: '" + reseeded.Id + "' is not '" + Set.Id + "'");
        var image = WorldImage.Of(this);
        image.Document = reseeded.Digest;
        WorldStateRules.CheckRanges(reseeded, image);
        WorldStateRules.CheckAgainstDocument(reseeded, image);
        return Restore(reseeded, image);
    }
}
