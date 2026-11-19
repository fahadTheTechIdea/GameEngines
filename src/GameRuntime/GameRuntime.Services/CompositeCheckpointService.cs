using System.Security.Cryptography;
using System.Text.Json;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using ScenarioEngine.Contracts;
using ScenarioEngine.Persistence;

namespace GameRuntime;

/// <summary>A checkpoint was refused. The code names the first check that failed; nothing is repaired or migrated.</summary>
public sealed class CheckpointRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>What a verified checkpoint holds: the session tick, SimEngine's own save bytes and the mission snapshot.</summary>
public sealed record CheckpointParts(long Tick, byte[] Sim, SessionSnapshot Mission);

/// <summary>
/// One checkpoint for the whole session. It embeds each engine's native bytes unchanged (SimEngine's save, ScenarioEngine's
/// save document) beside the identities that must match on restore and a SHA-256 over everything. It never repairs,
/// migrates or guesses: any mismatch is a refusal with its own code.
/// </summary>
public sealed class CompositeCheckpointService(IScenarioSaveCodec missionCodec)
{
    private const int Format = 1;

    public byte[] Capture(long tick, SimGame sim, IScenarioSimulation mission)
    {
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(mission);
        if (sim.Tick != tick || mission.Snapshot.Tick != tick)
            throw new CheckpointRefusedException("CHECKPOINT_TICK", $"session tick {tick}, sim tick {sim.Tick}, mission tick {mission.Snapshot.Tick} must agree");
        var simBytes = sim.Save();
        var missionBytes = missionCodec.Encode(new SaveDocument(2, ScenarioSaveCodec.BuildId, DateTimeOffset.UnixEpoch, mission.Snapshot));
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope
        {
            Format = Format,
            Tick = tick,
            SimBuild = EngineInfo.Build,
            SimSaveFormat = EngineInfo.SaveFormat,
            Mission = mission.Snapshot.Identity,
            Sim = Convert.ToBase64String(simBytes),
            MissionSave = Convert.ToBase64String(missionBytes),
            Digest = Digest(tick, simBytes, missionBytes),
        });
    }

    public CheckpointParts Open(ReadOnlyMemory<byte> bytes, ScenarioIdentity expectedMission)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span); }
        catch (JsonException exception) { throw new CheckpointRefusedException("CHECKPOINT_FORMAT", "not a checkpoint: " + exception.Message); }
        if (envelope is null || envelope.Format != Format) throw new CheckpointRefusedException("CHECKPOINT_FORMAT", "unsupported checkpoint format");
        if (envelope.SimBuild != EngineInfo.Build || envelope.SimSaveFormat != EngineInfo.SaveFormat)
            throw new CheckpointRefusedException("CHECKPOINT_SIM_BUILD", $"checkpoint is from {envelope.SimBuild}/{envelope.SimSaveFormat}; this is {EngineInfo.Build}/{EngineInfo.SaveFormat}");
        if (envelope.Mission != expectedMission)
            throw new CheckpointRefusedException("CHECKPOINT_MISSION_IDENTITY", "mission content, layer or version differ from the checkpoint");
        byte[] simBytes, missionBytes;
        try { simBytes = Convert.FromBase64String(envelope.Sim); missionBytes = Convert.FromBase64String(envelope.MissionSave); }
        catch (FormatException exception) { throw new CheckpointRefusedException("CHECKPOINT_FORMAT", "part encoding is invalid: " + exception.Message); }
        if (envelope.Digest != Digest(envelope.Tick, simBytes, missionBytes))
            throw new CheckpointRefusedException("CHECKPOINT_DIGEST", "checkpoint digest does not match its parts");
        var decoded = missionCodec.Decode(missionBytes);
        if (!decoded.Report.IsValid || decoded.Save is null)
            throw new CheckpointRefusedException("CHECKPOINT_MISSION_SAVE", string.Join("; ", decoded.Report.Diagnostics.Select(d => d.Code)));
        if (decoded.Save.Snapshot.Tick != envelope.Tick)
            throw new CheckpointRefusedException("CHECKPOINT_TICK", "mission snapshot tick differs from the checkpoint tick");
        return new CheckpointParts(envelope.Tick, simBytes, decoded.Save.Snapshot);
    }

    private static string Digest(long tick, byte[] sim, byte[] mission)
    {
        using var hash = SHA256.Create();
        hash.TransformBlock(BitConverter.GetBytes(tick), 0, 8, null, 0);
        hash.TransformBlock(sim, 0, sim.Length, null, 0);
        hash.TransformFinalBlock(mission, 0, mission.Length);
        return Convert.ToHexString(hash.Hash!);
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public long Tick { get; set; }
        public string SimBuild { get; set; } = "";
        public int SimSaveFormat { get; set; }
        public ScenarioIdentity Mission { get; set; } = null!;
        public string Sim { get; set; } = "";
        public string MissionSave { get; set; } = "";
        public string Digest { get; set; } = "";
    }
}
