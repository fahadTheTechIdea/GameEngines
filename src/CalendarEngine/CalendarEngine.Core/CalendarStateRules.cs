using EngineCommon;

namespace CalendarEngine;

/// <summary>
/// The rules a state must satisfy to belong to a document. The digest is not a secret, so a save passes through these as well; it cannot bring in a state the document could not have made.
/// Ranges are checked before the digest, what the state says about the document after it.
/// </summary>
internal static class CalendarStateRules
{
    private static EngineRefusedException Refuse(string message) => new(CalendarCodes.SaveState, message);

    public static void CheckRanges(CalendarSet set, CalendarImage image)
    {
        var system = set.System;
        if (image.Instant < system.MinInstant || image.Instant > system.MaxInstant) throw Refuse("the instant " + image.Instant + " is outside the supported years");
        if (image.ScaleDenominator < 1 || image.ScaleDenominator > CalendarRuntime.MaxScaleDenominator) throw Refuse("the scale denominator " + image.ScaleDenominator + " is outside 1 to " + CalendarRuntime.MaxScaleDenominator);
        if (image.ScaleNumerator < 1 || image.ScaleNumerator > CalendarRuntime.MaxScaleDays * set.Time.TicksPerDay * image.ScaleDenominator) throw Refuse("the scale numerator " + image.ScaleNumerator + " is outside what the document allows");
        if (image.ScaleRemainder < 0 || image.ScaleRemainder >= image.ScaleDenominator) throw Refuse("the scale remainder " + image.ScaleRemainder + " is not below the denominator");
        if (image.LastTick < 0) throw Refuse("the latest tick is negative");
        if (image.Backlog < 0 || image.Backlog > system.MaxInstant - image.Instant) throw Refuse("the backlog " + image.Backlog + " is outside what remains of the supported years");
        if (image.Actors.Count > CalendarRuntime.MaxActors) throw Refuse("more actors than " + CalendarRuntime.MaxActors);
        // A save writes actors in id order, so an actor listed twice is out of order too.
        if (image.Actors.Zip(image.Actors.Skip(1)).Any(p => string.CompareOrdinal(p.First.Id, p.Second.Id) >= 0)) throw Refuse("actors are not in strictly increasing id order (an actor listed twice counts)");
        foreach (var actor in image.Actors)
        {
            var where = "actor '" + actor.Id + "'";
            if (actor.Id.Length == 0 || actor.Id.Length > 128) throw Refuse("an actor id is the wrong length");
            if (actor.Announced.Length > 1024) throw Refuse(where + " has an announced answer longer than 1024");
            if ((actor.Interrupt is null) != (actor.InterruptUntil == 0)) throw Refuse(where + " has an interrupt without an end, or an end without an interrupt");
            if (actor.Interrupt is not null && (actor.InterruptUntil < 0 || actor.InterruptUntil > image.Instant + CalendarRuntime.MaxSkipDays * set.Time.TicksPerDay))
                throw Refuse(where + " has an interrupt ending at " + actor.InterruptUntil + ", beyond what an interrupt may last");
        }
    }

    public static void CheckAgainstDocument(CalendarSet set, CalendarImage image)
    {
        foreach (var actor in image.Actors)
        {
            var where = "actor '" + actor.Id + "'";
            if (!set.ScheduleById.ContainsKey(actor.Schedule)) throw Refuse(where + " follows the unknown schedule '" + actor.Schedule + "'");
            if (actor.Interrupt is not null && !set.InterruptById.ContainsKey(actor.Interrupt)) throw Refuse(where + " has the unknown interrupt '" + actor.Interrupt + "'");
        }
    }
}
