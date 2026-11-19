using System.Security.Cryptography;
using System.Text.Json;
using AlchemyEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>What a verified workshop checkpoint holds: the tick, the sim's own save, the alchemy checkpoint and the recipes already ordered.</summary>
public sealed record WorkshopCheckpointParts(long Tick, byte[] Sim, byte[] Alchemy, IReadOnlyList<string> Ordered);

/// <summary>
/// One checkpoint for the whole workshop. It embeds the sim's save and the alchemy checkpoint unchanged, plus the recipes the player has
/// ordered (the one thing no event replays), with a SHA-256 over everything. It refuses to capture while anything is in flight or while the
/// book and the sim disagree about a recipe, and never repairs or migrates on open: the first failed check refuses with its code.
/// </summary>
public static class WorkshopCheckpoint
{
    public const string FormatCode = "ALCHEMY_CHECKPOINT_FORMAT";
    public const string DigestCode = "ALCHEMY_CHECKPOINT_DIGEST";
    public const string SimBuildCode = "ALCHEMY_CHECKPOINT_SIM_BUILD";
    public const string DriftCode = "ALCHEMY_CHECKPOINT_DRIFT";
    private const int Format = 1;

    public static byte[] Capture(WorkshopSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsSettled) throw new AlchemyRefusedException(AlchemyCheckpoint.NotQuietCode, "a request, order, effect or input is still in flight");
        var drift = session.Drift();
        if (drift.Count != 0)
            throw new AlchemyRefusedException(DriftCode, "the book and the sim disagree about: " + string.Join(", ", drift.Select(d => d.RecipeId)));
        var tick = session.Clock.Tick;
        var sim = session.Game.Save();
        var alchemy = AlchemyCheckpoint.Capture(tick, session.Alchemy);
        var ordered = session.Player.View.Ordered.ToList();
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope
        {
            Format = Format,
            Tick = tick,
            SimBuild = EngineInfo.Build,
            SimSaveFormat = EngineInfo.SaveFormat,
            Sim = Convert.ToBase64String(sim),
            Alchemy = Convert.ToBase64String(alchemy),
            Ordered = ordered,
            Digest = Digest(tick, sim, alchemy, ordered),
        });
    }

    public static WorkshopCheckpointParts Open(ReadOnlyMemory<byte> bytes)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span); }
        catch (JsonException exception) { throw new AlchemyRefusedException(FormatCode, "not a checkpoint: " + exception.Message); }
        if (envelope is null || envelope.Format != Format || envelope.Tick < 0) throw new AlchemyRefusedException(FormatCode, "unsupported checkpoint format");
        if (envelope.SimBuild != EngineInfo.Build || envelope.SimSaveFormat != EngineInfo.SaveFormat)
            throw new AlchemyRefusedException(SimBuildCode, $"checkpoint is from {envelope.SimBuild}/{envelope.SimSaveFormat}; this is {EngineInfo.Build}/{EngineInfo.SaveFormat}");
        byte[] sim, alchemy;
        try { sim = Convert.FromBase64String(envelope.Sim); alchemy = Convert.FromBase64String(envelope.Alchemy); }
        catch (FormatException exception) { throw new AlchemyRefusedException(FormatCode, "part encoding is invalid: " + exception.Message); }
        if (envelope.Digest != Digest(envelope.Tick, sim, alchemy, envelope.Ordered))
            throw new AlchemyRefusedException(DigestCode, "checkpoint digest does not match its parts");
        return new WorkshopCheckpointParts(envelope.Tick, sim, alchemy, envelope.Ordered);
    }

    private static string Digest(long tick, byte[] sim, byte[] alchemy, IReadOnlyList<string> ordered)
    {
        using var hash = SHA256.Create();
        hash.TransformBlock(BitConverter.GetBytes(tick), 0, 8, null, 0);
        hash.TransformBlock(sim, 0, sim.Length, null, 0);
        hash.TransformBlock(alchemy, 0, alchemy.Length, null, 0);
        var names = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', ordered));
        hash.TransformFinalBlock(names, 0, names.Length);
        return Convert.ToHexString(hash.Hash!);
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public long Tick { get; set; }
        public string SimBuild { get; set; } = "";
        public int SimSaveFormat { get; set; }
        public string Sim { get; set; } = "";
        public string Alchemy { get; set; } = "";
        public List<string> Ordered { get; set; } = new();
        public string Digest { get; set; } = "";
    }
}
