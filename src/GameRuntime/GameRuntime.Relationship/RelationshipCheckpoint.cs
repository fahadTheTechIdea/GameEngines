using EngineCommon;
using RelationshipEngine;

namespace GameRuntime;

/// <summary>
/// A checkpoint of the relationship component: the relationship save itself, which is already digest-checked and bound to its document. This class only adds the component own rule: a request still waiting for
/// the next Update, a refusal waiting in its channel registry, or a change event still owed means the relationships are not in a consistent place, so nothing is captured. It never repairs or migrates; the first
/// failed check refuses with its code. What is not in a checkpoint: the channel registry (fixed by how the game was assembled) and the tick it was taken at, which the caller keeps (<see cref="RelationshipComponent.Tick"/>).
/// </summary>
public static class RelationshipCheckpoint
{
    public static byte[] Capture(RelationshipComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.Channels.IsQuiet) throw new EngineRefusedException(RelationshipComponent.NotQuiet, "a refusal for an unserved channel is still waiting for the next Update");
        if (!component.IsQuiet) throw new EngineRefusedException(RelationshipComponent.NotQuiet, "a relationship request or a change event is still waiting for the next Update");
        return RelationshipSave.Write(component.Runtime);
    }

    /// <summary>The runtime a verified checkpoint holds, to build a new component from.</summary>
    public static RelationshipRuntime Open(RelationshipSet set, ReadOnlyMemory<byte> bytes) => RelationshipSave.Open(set, bytes);
}
