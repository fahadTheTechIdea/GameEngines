using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalendarEngine;

internal sealed class ImageActor
{
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public string Schedule { get; set; } = "";
    [JsonRequired] public string? Interrupt { get; set; }
    [JsonRequired] public long InterruptUntil { get; set; }
    [JsonRequired] public string Announced { get; set; } = "";
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and
/// nothing else, so it can be checked before the data is understood, and the data is checked against the document afterwards.
/// </summary>
internal sealed class CalendarImage
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Instant { get; set; }
    [JsonRequired] public long ScaleNumerator { get; set; }
    [JsonRequired] public long ScaleDenominator { get; set; }
    [JsonRequired] public long ScaleRemainder { get; set; }
    [JsonRequired] public bool Paused { get; set; }
    [JsonRequired] public long LastTick { get; set; }
    [JsonRequired] public long Backlog { get; set; }
    [JsonRequired] public List<ImageActor> Actors { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";

    public static CalendarImage Of(CalendarRuntime runtime)
    {
        var image = new CalendarImage
        {
            Format = CalendarSave.Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Instant = runtime.Instant, ScaleNumerator = runtime.ScaleNumerator, ScaleDenominator = runtime.ScaleDenominator,
            ScaleRemainder = runtime.ScaleRemainder, Paused = runtime.Paused, LastTick = runtime.LastTick, Backlog = runtime.Backlog,
        };
        foreach (var actor in runtime.Actors.Values)
            image.Actors.Add(new ImageActor { Id = actor.Id, Schedule = actor.Schedule, Interrupt = actor.Interrupt, InterruptUntil = actor.InterruptUntil, Announced = actor.Announced });
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped.</summary>
    public static string Canonical(CalendarImage image)
    {
        var text = new StringBuilder("calendar-state-1");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={image.Id}\ninstant={image.Instant}\nscale={image.ScaleNumerator}/{image.ScaleDenominator}+{image.ScaleRemainder}\npaused={image.Paused}\ntick={image.LastTick}\nbacklog={image.Backlog}");
        foreach (var actor in image.Actors)
            text.Append(CultureInfo.InvariantCulture, $"\nactor {JsonSerializer.Serialize(actor.Id)}|{JsonSerializer.Serialize(actor.Schedule)}|{JsonSerializer.Serialize(actor.Interrupt)}|{actor.InterruptUntil}|{JsonSerializer.Serialize(actor.Announced)}");
        return text.ToString();
    }
}
