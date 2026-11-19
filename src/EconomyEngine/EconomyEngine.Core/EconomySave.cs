using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;

namespace EconomyEngine;

/// <summary>
/// The save of an economy runtime: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of
/// the state (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document (money is conserved, prices stay
/// inside their bounds). It never repairs or migrates; an old format is refused.
/// </summary>
public static class EconomySave
{
    public const int Format = 6;
    public const int MaxBytes = 128 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    public static byte[] Write(EconomyRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(EconomyImage.Of(runtime), Options);
    }

    /// <summary>
    /// The format number a save states, or -1 when it is not an object that has one. Read before the save is understood, so an old format can be refused by name, and read with a forward-only reader that skips every
    /// other property (a document model of a multi-megabyte save cost as much as reading the save). Bytes that are not JSON throw, as the full read does.
    /// </summary>
    private static long PeekFormat(ReadOnlyMemory<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return -1;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isFormat = reader.ValueTextEquals("Format"u8);
            reader.Read();
            if (isFormat) return reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number) ? number : -1;
            reader.Skip();
        }
        return -1;
    }

    public static EconomyRuntime Open(EconomySet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(EconomyCodes.SaveFormat, "an economy save is at most " + MaxBytes + " bytes");
        EconomyImage? image;
        try
        {
            var stated = PeekFormat(bytes);
            if (stated == 3) throw new EngineRefusedException(EconomyCodes.SaveFormat3, "this reader reads economy save format " + Format + " only; format 3 is no longer read (format 4 groups production batches by line and adds the margin and required-good line limits; format 5 adds tool wear)");
            if (stated == 4) throw new EngineRefusedException(EconomyCodes.SaveFormat4, "this reader reads economy save format " + Format + " only; format 4 is no longer read (format 5 saves, for each line, the runs done towards the next loss of each tool that wears out, and adds the worn-tool line limit)");
            if (stated == 5) throw new EngineRefusedException(EconomyCodes.SaveFormat5, "this reader reads economy save format " + Format + " only; format 5 is no longer read (format 6 holds a required good for one line at a time within a period, so a format 5 save would continue differently where lines share a tool)");
            image = JsonSerializer.Deserialize<EconomyImage>(bytes.Span, Options);
        }
        catch (JsonException exception) { throw new EngineRefusedException(EconomyCodes.SaveFormat, "not an economy save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(EconomyCodes.SaveFormat, "unsupported economy save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(EconomyCodes.SaveDocument, "the save was taken from another document");
        EconomyStateRules.CheckRanges(set, image);
        if (image.Digest != EngineCommon.Digest.Of(EconomyImage.Canonical(image))) throw new EngineRefusedException(EconomyCodes.SaveDigest, "the save digest does not match its state");
        var runtime = EconomyRuntime.Restore(set, image);
        EconomyStateRules.CheckAgainstDocument(runtime);
        return runtime;
    }
}
