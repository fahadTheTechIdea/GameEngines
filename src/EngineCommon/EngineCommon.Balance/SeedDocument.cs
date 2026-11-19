using System.Text.Json;
using System.Text.Json.Nodes;

namespace EngineCommon.Balance;

/// <summary>Gives an engine document another seed: the one place the sweep changes a document. Everything else in it is carried over as it is.</summary>
public static class SeedDocument
{
    public static string WithSeed(string document, long seed)
    {
        ArgumentNullException.ThrowIfNull(document);
        JsonNode? parsed;
        try { parsed = JsonNode.Parse(document); }
        catch (JsonException exception) { throw new EngineRefusedException(BalanceCodes.DocumentSeed, "the document is not JSON: " + exception.Message); }
        if (parsed is not JsonObject root || !root.ContainsKey("seed")) throw new EngineRefusedException(BalanceCodes.DocumentSeed, "a document with a top-level 'seed' is needed to sweep seeds");
        root["seed"] = JsonValue.Create(seed);
        return root.ToJsonString();
    }
}
