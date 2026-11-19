using AIEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// A checkpoint of the AI component: the AI save itself, which is already digest-checked and bound to its document. This class only adds the component rule: a
/// request still waiting for the next Update means the agents are not in a consistent place, so nothing is captured. It never repairs or migrates.
/// </summary>
public static class AiCheckpoint
{
    public static byte[] Capture(AiComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.IsQuiet) throw new EngineRefusedException(AiComponent.NotQuiet, "an AI request is still waiting for the next Update");
        return AiSave.Write(component.Runtime);
    }

    /// <summary>The runtime a verified checkpoint holds, to build a new component from.</summary>
    public static AiRuntime Open(AiSet set, ReadOnlyMemory<byte> bytes) => AiSave.Open(set, bytes);
}