using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// The save of an agent set: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in that
/// order and then checks the state against the document; it never repairs or migrates.
/// </summary>
public static class AiSave
{
    public const int Format = 1;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    /// <summary>The digest of the state of a runtime: the same for the same state, whatever order things were done in.</summary>
    public static string StateDigest(AiRuntime runtime) => AiImage.Of(runtime).Digest;

    public static byte[] Write(AiRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return JsonSerializer.SerializeToUtf8Bytes(AiImage.Of(runtime), Options);
    }

    public static AiRuntime Open(AiSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        AiImage? image;
        try { image = JsonSerializer.Deserialize<AiImage>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(AiCodes.SaveFormat, "not an AI save: " + exception.Message); }
        if (image is null || image.Format != Format) throw new EngineRefusedException(AiCodes.SaveFormat, "unsupported AI save format");
        if (image.Document != set.Digest || image.Id != set.Id) throw new EngineRefusedException(AiCodes.SaveDocument, "the save was taken from another document");
        if (!ulong.TryParse(image.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) throw new EngineRefusedException(AiCodes.SaveFormat, "the seed is not a number");
        foreach (var agent in image.Agents)
            if (agent.Knowledge.Select(k => k.Kind + "/" + k.Id).Distinct(StringComparer.Ordinal).Count() != agent.Knowledge.Count)
                throw new EngineRefusedException(AiCodes.SaveFormat, "agent '" + agent.Id + "' knows something twice");
        // A save writes agents in id order, so an agent listed twice is out of order too.
        if (image.Agents.Zip(image.Agents.Skip(1)).Any(p => string.CompareOrdinal(p.First.Id, p.Second.Id) >= 0))
            throw new EngineRefusedException(AiCodes.SaveFormat, "agents are not in strictly increasing id order (an agent listed twice counts)");

        if (image.Digest != EngineCommon.Digest.Of(AiImage.Canonical(image))) throw new EngineRefusedException(AiCodes.SaveDigest, "the save digest does not match its state");
        AiStateRules.Check(set, image);
        return AiRuntime.Restore(set, seed, image);
    }
}