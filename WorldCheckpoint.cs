using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// A checkpoint of the world-events component: the world save itself, which is already digest-checked and bound to its document. This class only adds the component own rule: a request still waiting for the next
/// Update, a refusal waiting in its channel registry, or a change event still owed means the world is not in a consistent place, so nothing is captured. It never repairs or migrates; the first failed check
/// refuses with its code. What is not in a checkpoint: the channel registry (fixed by how the game was assembled) and the tick it was taken at, which the caller keeps (<see cref="WorldComponent.Tick"/>).
/// </summary>
public static class WorldCheckpoint
{
    public static byte[] Capture(WorldComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.Channels.IsQuiet) throw new EngineRefusedException(WorldComponent.NotQuiet, "a refusal for an unserved channel is still waiting for the next Update");
        if (!component.IsQuiet) throw new EngineRefusedException(WorldComponent.NotQuiet, "a world request or a change event is still waiting for the next Update");
        return WorldSave.Write(component.Runtime);
    }

    /// <summary>The runtime a verified checkpoint holds, to build a new component from.</summary>
    public static WorldRuntime Open(WorldSet set, ReadOnlyMemory<byte> bytes) => WorldSave.Open(set, bytes);
}
