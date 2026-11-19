using System.Security.Cryptography;
using System.Text.Json;

namespace GameRuntime;

/// <summary>
/// The record of a session: the input events the host submitted (with the tick each was delivered at) and the state
/// hash after every tick. It holds no wall-clock data, so it reproduces a run exactly.
/// </summary>
public sealed class SessionJournal
{
    public sealed record Input(long Tick, string Type, string Json);
    public sealed record Hash(long Tick, string Value);

    public List<Input> Inputs { get; } = new();
    public List<Hash> Hashes { get; } = new();

    public byte[] Serialize()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new Payload { Inputs = Inputs, Hashes = Hashes });
        var digest = Convert.ToHexString(SHA256.HashData(body));
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope { Format = 1, Digest = digest, Body = Convert.ToBase64String(body) });
    }

    public static SessionJournal Deserialize(ReadOnlySpan<byte> bytes)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes); }
        catch (JsonException exception) { throw new JournalRefusedException("JOURNAL_FORMAT", exception.Message); }
        if (envelope is null || envelope.Format != 1) throw new JournalRefusedException("JOURNAL_FORMAT", "unsupported journal format");
        byte[] body;
        try { body = Convert.FromBase64String(envelope.Body); }
        catch (FormatException exception) { throw new JournalRefusedException("JOURNAL_FORMAT", exception.Message); }
        if (Convert.ToHexString(SHA256.HashData(body)) != envelope.Digest) throw new JournalRefusedException("JOURNAL_DIGEST", "journal digest does not match its body");
        var payload = JsonSerializer.Deserialize<Payload>(body) ?? throw new JournalRefusedException("JOURNAL_FORMAT", "empty journal body");
        var journal = new SessionJournal();
        journal.Inputs.AddRange(payload.Inputs);
        journal.Hashes.AddRange(payload.Hashes);
        return journal;
    }

    private sealed class Payload { public List<Input> Inputs { get; set; } = new(); public List<Hash> Hashes { get; set; } = new(); }
    private sealed class Envelope { public int Format { get; set; } public string Digest { get; set; } = ""; public string Body { get; set; } = ""; }
}

public sealed class JournalRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Records the state hash after every tick. Registered last so it sees the settled tick.</summary>
public sealed class JournalComponent(SessionJournal journal, Func<string> stateHash, string id = "journal") : IGameComponent
{
    public string Id { get; } = id;
    public void Init(in ComponentContext context) { }
    public void Update(in UpdateContext context) => journal.Hashes.Add(new SessionJournal.Hash(context.Tick, stateHash()));
}

/// <summary>Maps the input event types a journal may contain to names, so a journal never names an arbitrary CLR type.</summary>
public sealed class JournalCodec
{
    private readonly Dictionary<string, Func<string, Action<IGameRuntime>>> _decoders = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _names = new();

    public JournalCodec Register<T>(string name, Func<JsonElement, T> decode) where T : struct
    {
        _names.Add(typeof(T), name);
        _decoders.Add(name, json =>
        {
            using var document = JsonDocument.Parse(json);
            var value = decode(document.RootElement);
            return runtime => runtime.Submit(value);
        });
        return this;
    }

    /// <summary>
    /// Journals an input that was written outside <c>Submit</c> (for example into an <see cref="InputState"/>). On replay it is
    /// submitted at the start of <paramref name="deliveryTick"/>, the tick whose Update first sees it.
    /// </summary>
    public void Record<T>(SessionJournal journal, long deliveryTick, in T inputEvent) where T : struct =>
        journal.Inputs.Add(new SessionJournal.Input(deliveryTick, NameOf<T>(), JsonSerializer.Serialize(inputEvent)));

    internal string NameOf<T>() => _names.TryGetValue(typeof(T), out var name)
        ? name : throw new InvalidOperationException("input event " + typeof(T).Name + " is not registered with the journal codec");

    internal Action<IGameRuntime> Decode(string name, string json) => _decoders.TryGetValue(name, out var decoder)
        ? decoder(json) : throw new JournalRefusedException("JOURNAL_INPUT_TYPE", "journal names an unregistered input type '" + name + "'");
}

/// <summary>Wraps a runtime so every submitted input is journaled with the tick it will be delivered at.</summary>
public sealed class JournalingRuntime(IGameRuntime inner, SessionJournal journal, JournalCodec codec) : IGameRuntime
{
    public RuntimeState State => inner.State;
    public IRuntimeClock Clock => inner.Clock;
    public void Init() => inner.Init();
    public int Update(long elapsedMicroseconds) => inner.Update(elapsedMicroseconds);
    public void Subscribe<T>(Action<T> handler) where T : struct => inner.Subscribe(handler);
    public void Dispose() => inner.Dispose();

    public void Submit<T>(in T inputEvent) where T : struct
    {
        var name = codec.NameOf<T>();
        inner.Submit(inputEvent);
        journal.Inputs.Add(new SessionJournal.Input(inner.Clock.Tick + 1, name, JsonSerializer.Serialize(inputEvent)));
    }
}

public sealed record ReplayResult(bool Matches, long? DivergedAtTick, string Code);

/// <summary>Re-runs a journal against a fresh session and names the first tick whose state differs.</summary>
public static class JournalReplay
{
    /// <param name="build">Builds a fresh runtime and the journal it fills while running (it must include a <see cref="JournalComponent"/>).</param>
    public static ReplayResult Verify(SessionJournal recorded, JournalCodec codec, Func<(IGameRuntime Runtime, SessionJournal Journal)> build)
    {
        var (runtime, replayed) = build();
        runtime.Init();
        var rate = runtime.Clock.TicksPerSecond;
        var last = recorded.Hashes.Count == 0 ? 0 : recorded.Hashes[^1].Tick;
        long previousMicros = 0;
        for (long tick = 1; tick <= last; tick++)
        {
            foreach (var input in recorded.Inputs.Where(i => i.Tick == tick)) codec.Decode(input.Type, input.Json)(runtime);
            // Cumulative ceiling: each call earns exactly one whole tick, with no rounding drift.
            var micros = (tick * RuntimeClock.MicrosecondsPerSecond + rate - 1) / rate;
            runtime.Update(micros - previousMicros);
            previousMicros = micros;
            if (runtime.Clock.Tick != tick) return new ReplayResult(false, tick, "REPLAY_TICK");
        }
        foreach (var expected in recorded.Hashes)
        {
            var actual = replayed.Hashes.FirstOrDefault(h => h.Tick == expected.Tick);
            if (actual is null || actual.Value != expected.Value) return new ReplayResult(false, expected.Tick, "REPLAY_STATE");
        }
        return new ReplayResult(true, null, "REPLAY_MATCHES");
    }
}
