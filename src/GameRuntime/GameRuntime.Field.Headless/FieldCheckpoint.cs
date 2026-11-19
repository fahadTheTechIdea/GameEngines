using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CombatEngine;

namespace GameRuntime.Field.Headless;

/// <summary>What a verified field checkpoint holds: the tick, the saved world, the combat checkpoint and the commander's memory.</summary>
public sealed record FieldCheckpointParts(long Tick, byte[] Sim, byte[] Combat, FieldCommanderState Commander);

/// <summary>
/// One checkpoint for a field game: the world's own save, the combat checkpoint and the commander's state, with a SHA-256 over all of them.
/// Like every checkpoint in the game layer it never repairs or migrates; the first failed check refuses with its code. It is only taken
/// when nothing is waiting for an answer.
/// </summary>
public static class FieldCheckpoint
{
    public const string FormatCode = "FIELD_CHECKPOINT_FORMAT";
    public const string DigestCode = "FIELD_CHECKPOINT_DIGEST";
    public const string NotQuietCode = "FIELD_CHECKPOINT_NOT_QUIET";
    // 3: the ledger also holds platforms and fuel (2 added depots and the ledger), so older checkpoints are refused, never migrated.
    private const int Format = 3;

    public static byte[] Capture(FieldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsSettled) throw new CombatRefusedException(NotQuietCode, "a request is still waiting for an answer");
        var tick = session.Runtime.Clock.Tick;
        var sim = session.World.Game.Save();
        var combat = CombatCheckpoint.Capture(tick, session.Combat);
        var commander = JsonSerializer.Serialize(session.Commander.State);
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope
        {
            Format = Format, Tick = tick, Sim = Convert.ToBase64String(sim), Combat = Convert.ToBase64String(combat), Commander = commander,
            Digest = Digest(tick, sim, combat, commander),
        });
    }

    public static FieldCheckpointParts Open(ReadOnlyMemory<byte> bytes)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span); }
        catch (JsonException exception) { throw new CombatRefusedException(FormatCode, "not a field checkpoint: " + exception.Message); }
        if (envelope is null || envelope.Format != Format || envelope.Tick < 0) throw new CombatRefusedException(FormatCode, "unsupported field checkpoint format");
        byte[] sim, combat;
        try { sim = Convert.FromBase64String(envelope.Sim); combat = Convert.FromBase64String(envelope.Combat); }
        catch (FormatException exception) { throw new CombatRefusedException(FormatCode, "a part is not encoded correctly: " + exception.Message); }
        if (envelope.Digest != Digest(envelope.Tick, sim, combat, envelope.Commander)) throw new CombatRefusedException(DigestCode, "checkpoint digest does not match its parts");
        FieldCommanderState? commander;
        try { commander = JsonSerializer.Deserialize<FieldCommanderState>(envelope.Commander); }
        catch (JsonException exception) { throw new CombatRefusedException(FormatCode, "the commander part is not valid: " + exception.Message); }
        return new FieldCheckpointParts(envelope.Tick, sim, combat, commander ?? throw new CombatRefusedException(FormatCode, "the commander part is empty"));
    }

    private static string Digest(long tick, byte[] sim, byte[] combat, string commander)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("field-checkpoint-3|" + tick.ToString(CultureInfo.InvariantCulture) + "|"));
        hash.AppendData(sim);
        hash.AppendData(combat);
        hash.AppendData(Encoding.UTF8.GetBytes(commander));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public long Tick { get; set; }
        public string Sim { get; set; } = "";
        public string Combat { get; set; } = "";
        public string Commander { get; set; } = "";
        public string Digest { get; set; } = "";
    }
}
