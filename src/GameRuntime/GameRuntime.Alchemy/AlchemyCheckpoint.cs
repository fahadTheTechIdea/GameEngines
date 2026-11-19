using System.Security.Cryptography;
using System.Text.Json;
using AlchemyEngine;

namespace GameRuntime;

/// <summary>What a verified alchemy checkpoint holds: the session tick and the discovery book.</summary>
public sealed record AlchemyCheckpointParts(long Tick, DiscoveryBook Book);

/// <summary>
/// One checkpoint for an alchemy session: the tick beside the book's own saved bytes, with a SHA-256 over both. Like the
/// composite checkpoint of the mission and sim, it never repairs or migrates; the first failed check refuses with its code.
/// </summary>
public static class AlchemyCheckpoint
{
    public const string FormatCode = "ALCHEMY_CHECKPOINT_FORMAT";
    public const string DigestCode = "ALCHEMY_CHECKPOINT_DIGEST";
    public const string NotQuietCode = "ALCHEMY_CHECKPOINT_NOT_QUIET";
    private const int Format = 1;

    /// <summary>Captures the book at a settled tick. The component must have no request waiting, or the saved state would lose it.</summary>
    public static byte[] Capture(long tick, AlchemyComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.IsQuiet) throw new AlchemyRefusedException(NotQuietCode, "a combine request is still waiting for the next Update");
        var book = component.Book.Save();
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope { Format = Format, Tick = tick, Book = Convert.ToBase64String(book), Digest = Digest(tick, book) });
    }

    public static AlchemyCheckpointParts Open(AlchemyContent content, ReadOnlyMemory<byte> bytes)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span); }
        catch (JsonException exception) { throw new AlchemyRefusedException(FormatCode, "not a checkpoint: " + exception.Message); }
        if (envelope is null || envelope.Format != Format || envelope.Tick < 0) throw new AlchemyRefusedException(FormatCode, "unsupported checkpoint format");
        byte[] book;
        try { book = Convert.FromBase64String(envelope.Book); }
        catch (FormatException exception) { throw new AlchemyRefusedException(FormatCode, "book encoding is invalid: " + exception.Message); }
        if (envelope.Digest != Digest(envelope.Tick, book)) throw new AlchemyRefusedException(DigestCode, "checkpoint digest does not match its parts");
        return new AlchemyCheckpointParts(envelope.Tick, DiscoveryBook.Load(content, book));
    }

    private static string Digest(long tick, byte[] book)
    {
        using var hash = SHA256.Create();
        hash.TransformBlock(BitConverter.GetBytes(tick), 0, 8, null, 0);
        hash.TransformFinalBlock(book, 0, book.Length);
        return Convert.ToHexString(hash.Hash!);
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public long Tick { get; set; }
        public string Book { get; set; } = "";
        public string Digest { get; set; } = "";
    }
}
