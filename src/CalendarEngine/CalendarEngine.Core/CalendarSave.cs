using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;

namespace CalendarEngine;

/// <summary>
/// The save of a calendar: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of the
/// state (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document (schedules and interrupts that exist).
/// It never repairs or migrates; an old format is refused.
/// </summary>
public static class CalendarSave
{
    public const int Format = 1;
    public const int MaxBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    /// <summary>The digest of the state of a runtime: the same for the same state, whatever order things were done in.</summary>
    public static string StateDigest(CalendarRuntime runtime) => CalendarImage.Of(runtime).Digest;

    public static byte[] Write(CalendarRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(CalendarImage.Of(runtime), Options);
    }

    public static CalendarRuntime Open(CalendarSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(CalendarCodes.SaveFormat, "a calendar save is at most " + MaxBytes + " bytes");
        CalendarImage? image;
        try { image = JsonSerializer.Deserialize<CalendarImage>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(CalendarCodes.SaveFormat, "not a calendar save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(CalendarCodes.SaveFormat, "unsupported calendar save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(CalendarCodes.SaveDocument, "the save was taken from another document");
        CalendarStateRules.CheckRanges(set, image);
        if (image.Digest != EngineCommon.Digest.Of(CalendarImage.Canonical(image))) throw new EngineRefusedException(CalendarCodes.SaveDigest, "the save digest does not match its state");
        CalendarStateRules.CheckAgainstDocument(set, image);
        return CalendarRuntime.Restore(set, image);
    }
}
