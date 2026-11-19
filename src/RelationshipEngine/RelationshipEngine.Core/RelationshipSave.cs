using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;

namespace RelationshipEngine;

/// <summary>
/// The save of a relationship runtime: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of
/// the state (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document. It never repairs or migrates; an old
/// format is refused with its own code (format 1 had no communities or standing).
/// </summary>
public static class RelationshipSave
{
    public const int Format = 2;
    public const int MaxBytes = 128 * 1024 * 1024;

    private const int FormatBeforeSocial = 1;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    public static byte[] Write(RelationshipRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(RelationshipImage.Of(runtime), Options);
    }

    public static RelationshipRuntime Open(RelationshipSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(RelationshipCodes.SaveFormat, "a relationship save is at most " + MaxBytes + " bytes");
        RelationshipImage? image;
        try
        {
            if (IsFormatBeforeSocial(bytes.Span)) throw new EngineRefusedException(RelationshipCodes.SaveOldFormat, "a format " + FormatBeforeSocial + " relationship save has no communities or standing; this reader reads format " + Format + " only");
            image = JsonSerializer.Deserialize<RelationshipImage>(bytes.Span, Options);
        }
        catch (JsonException exception) { throw new EngineRefusedException(RelationshipCodes.SaveFormat, "not a relationship save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(RelationshipCodes.SaveFormat, "unsupported relationship save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(RelationshipCodes.SaveDocument, "the save was taken from another document");
        RelationshipStateRules.CheckRanges(set, image);
        if (image.Digest != EngineCommon.Digest.Of(RelationshipImage.Canonical(image))) throw new EngineRefusedException(RelationshipCodes.SaveDigest, "the save digest does not match its state");
        RelationshipStateRules.CheckAgainstDocument(set, image);
        return RelationshipRuntime.Restore(set, image);
    }

    /// <summary>True when the top-level <c>Format</c> of the save is the one before this reader's. A malformed text throws <see cref="JsonException"/>, which the caller turns into the format refusal.</summary>
    private static bool IsFormatBeforeSocial(ReadOnlySpan<byte> bytes)
    {
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isFormat = reader.ValueTextEquals("Format"u8);
            if (!reader.Read()) return false;
            if (isFormat) return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) && number == FormatBeforeSocial;
            reader.Skip();
        }
        return false;
    }
}
