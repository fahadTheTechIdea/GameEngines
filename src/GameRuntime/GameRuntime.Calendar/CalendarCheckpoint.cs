using CalendarEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// A checkpoint of the calendar component: the calendar save itself, which is already digest-checked and bound to its document. This class only adds the component's own rule: a request still waiting for
/// the next Update (the component's own, or a refusal waiting in its channel registry) means the calendar is not in a consistent place, so nothing is captured. It never repairs or migrates; the first failed
/// check refuses with its code. What is not in a checkpoint: the channel registry (fixed by how the game was assembled) and the component's memory of the minute and host facts it last announced for (the
/// first Update after a restore looks at every actor again, and an actor is only announced when its answer differs from the one the save holds, so nothing is announced twice).
/// </summary>
public static class CalendarCheckpoint
{
    public static byte[] Capture(CalendarComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.Channels.IsQuiet) throw new EngineRefusedException(CalendarComponent.NotQuiet, "a refusal for an unserved channel is still waiting for the next Update");
        if (!component.IsQuiet) throw new EngineRefusedException(CalendarComponent.NotQuiet, "a calendar request is still waiting for the next Update");
        return CalendarSave.Write(component.Runtime);
    }

    /// <summary>The runtime a verified checkpoint holds, to build a new component from.</summary>
    public static CalendarRuntime Open(CalendarSet set, ReadOnlyMemory<byte> bytes) => CalendarSave.Open(set, bytes);
}
