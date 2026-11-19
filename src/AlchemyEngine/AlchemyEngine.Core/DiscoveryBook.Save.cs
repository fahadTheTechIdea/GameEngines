using System.Security.Cryptography;
using System.Text.Json;

namespace AlchemyEngine;

public sealed partial class DiscoveryBook
{
    private const int SaveFormat = 1;

    /// <summary>The book as bytes: the content it belongs to, a digest and the state. Loading refuses anything that does not match.</summary>
    public byte[] Save()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new Body
        {
            Attempts = Attempts,
            Elements = _elements.ToList(),
            Recipes = _recipes.ToList(),
            Tried = _tried.ToList(),
        });
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope
        {
            Format = SaveFormat,
            Content = Content.Digest,
            Digest = Convert.ToHexString(SHA256.HashData(body)),
            Body = Convert.ToBase64String(body),
        });
    }

    /// <summary>Opens saved bytes against the content they must belong to. Nothing is migrated or repaired: the first failed check refuses.</summary>
    public static DiscoveryBook Load(AlchemyContent content, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(content);
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes); }
        catch (JsonException exception) { throw Refuse(AlchemyCodes.BookFormat, "not a discovery book: " + exception.Message); }
        if (envelope is null || envelope.Format != SaveFormat) throw Refuse(AlchemyCodes.BookFormat, "unsupported discovery book format");
        byte[] bodyBytes;
        try { bodyBytes = Convert.FromBase64String(envelope.Body); }
        catch (FormatException exception) { throw Refuse(AlchemyCodes.BookFormat, "body encoding is invalid: " + exception.Message); }
        if (Convert.ToHexString(SHA256.HashData(bodyBytes)) != envelope.Digest) throw Refuse(AlchemyCodes.BookDigest, "discovery book digest does not match its body");
        if (envelope.Content != content.Digest) throw Refuse(AlchemyCodes.BookContent, "the book was saved against different alchemy content");
        Body? body;
        try { body = JsonSerializer.Deserialize<Body>(bodyBytes); }
        catch (JsonException exception) { throw Refuse(AlchemyCodes.BookFormat, "body is invalid: " + exception.Message); }
        if (body is null || body.Attempts < 0) throw Refuse(AlchemyCodes.BookFormat, "empty or invalid book body");

        var book = new DiscoveryBook(content) { Attempts = body.Attempts };
        foreach (var id in body.Elements)
        {
            if (!content.TryElement(id, out _)) throw Refuse(AlchemyCodes.BookUnknownElement, "the book names unknown element '" + id + "'");
            book._elements.Add(id);
        }
        foreach (var element in content.Elements)
            if (element.Starting && !body.Elements.Contains(element.Id, StringComparer.Ordinal)) throw Refuse(AlchemyCodes.BookFormat, "the book lacks starting element '" + element.Id + "'");
        foreach (var id in body.Recipes)
        {
            if (!content.TryRecipe(id, out var recipe)) throw Refuse(AlchemyCodes.BookUnknownRecipe, "the book names unknown recipe '" + id + "'");
            foreach (var produced in recipe.Outputs)
                if (!book._elements.Contains(produced)) throw Refuse(AlchemyCodes.BookFormat, "the book knows recipe '" + id + "' but not its output '" + produced + "'");
            book._recipes.Add(id);
        }
        foreach (var key in body.Tried) book._tried.Add(key);
        return book;
    }

    private static AlchemyRefusedException Refuse(string code, string message) => new(code, message);

    private sealed class Body
    {
        public long Attempts { get; set; }
        public List<string> Elements { get; set; } = new();
        public List<string> Recipes { get; set; } = new();
        public List<string> Tried { get; set; } = new();
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public string Content { get; set; } = "";
        public string Digest { get; set; } = "";
        public string Body { get; set; } = "";
    }
}
