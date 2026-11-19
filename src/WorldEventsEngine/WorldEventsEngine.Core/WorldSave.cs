using System.Text.Json;
using EngineCommon;

namespace WorldEventsEngine;

/// <summary>
/// The save of a world runtime: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of the state
/// (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document. It never repairs or migrates; an old format is refused.
/// </summary>
public static class WorldSave
{
    /// <summary>Format 2 adds the excitation of every hazard in every region. A format 1 save is refused (<see cref="WorldCodes.SaveFormat"/>), never migrated.</summary>
    public const int Format = 2;
    public const int MaxBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    public static byte[] Write(WorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(WorldImage.Of(runtime), Options);
    }

    public static WorldRuntime Open(WorldSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(WorldCodes.SaveFormat, "a world save is at most " + MaxBytes + " bytes");
        WorldImage? image;
        try { image = JsonSerializer.Deserialize<WorldImage>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(WorldCodes.SaveFormat, "not a world save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(WorldCodes.SaveFormat, "unsupported world save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(WorldCodes.SaveDocument, "the save was taken from another document");
        WorldStateRules.CheckRanges(set, image);
        if (image.Digest != Digest.Of(WorldImage.Canonical(image))) throw new EngineRefusedException(WorldCodes.SaveDigest, "the save digest does not match its state");
        WorldStateRules.CheckAgainstDocument(set, image);
        return WorldRuntime.Restore(set, image);
    }

    /// <summary>The digest of the state of a runtime: equal for equal states, and what a save carries.</summary>
    public static string StateDigest(WorldRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return WorldImage.Of(runtime).Digest;
    }
}
