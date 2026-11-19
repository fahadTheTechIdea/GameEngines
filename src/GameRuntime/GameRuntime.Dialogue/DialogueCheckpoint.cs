using DialogueEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// A checkpoint of a dialogue session: the dialogue save itself, which is already digest-checked and bound to its document. This class
/// only adds the component's own rule: a request still waiting for the next Update (the component's own, or a refusal waiting in its channel registry) means the session is not in a consistent place, so
/// nothing is captured. It never repairs or migrates; the first failed check refuses with its code.
/// </summary>
public static class DialogueCheckpoint
{
    public static byte[] Capture(DialogueComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.Channels.IsQuiet) throw new EngineRefusedException(DialogueComponent.NotQuiet, "a refusal for an unserved channel is still waiting for the next Update");
        if (!component.IsQuiet) throw new EngineRefusedException(DialogueComponent.NotQuiet, "a dialogue request is still waiting for the next Update");
        return DialogueSave.Write(component.Runtime);
    }

    /// <summary>The runtime a verified checkpoint holds, to build a new component from.</summary>
    public static DialogueRuntime Open(DialogueSet set, ReadOnlyMemory<byte> bytes) => DialogueSave.Open(set, bytes);
}
