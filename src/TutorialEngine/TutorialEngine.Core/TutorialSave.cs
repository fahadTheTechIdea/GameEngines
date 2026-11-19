using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;

namespace TutorialEngine;

/// <summary>
/// The save of a tutorial runtime: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of
/// the state (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document. It never repairs or migrates;
/// an old format is refused.
/// </summary>
public static class TutorialSave
{
    public const int Format = 1;
    public const int MaxBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    public static byte[] Write(TutorialRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(TutorialImage.Of(runtime), Options);
    }

    public static TutorialRuntime Open(TutorialSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(TutorialCodes.SaveFormat, "a tutorial save is at most " + MaxBytes + " bytes");
        TutorialImage? image;
        try { image = JsonSerializer.Deserialize<TutorialImage>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(TutorialCodes.SaveFormat, "not a tutorial save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(TutorialCodes.SaveFormat, "unsupported tutorial save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(TutorialCodes.SaveDocument, "the save was taken from another document");
        TutorialStateRules.CheckRanges(set, image);
        if (image.Digest != EngineCommon.Digest.Of(TutorialImage.Canonical(image))) throw new EngineRefusedException(TutorialCodes.SaveDigest, "the save digest does not match its state");
        TutorialStateRules.CheckAgainstDocument(set, image);
        return TutorialRuntime.Restore(set, image);
    }
}
