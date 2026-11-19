using System.Globalization;

namespace GameRuntime.Headless;

/// <summary>The command line was wrong. The message names the argument; nothing is guessed or defaulted around it.</summary>
public sealed class UsageException(string message) : Exception(message);

/// <summary>What one run was asked to do.</summary>
public sealed record HeadlessOptions
{
    public const string Usage =
        "usage: GameRuntime.Headless [--seconds <n>] [--seed <n>] [--frame-ms <1..1000>] [--player none|greedy|cautious]\n" +
        "                            [--script <file>] [--save <file>] [--load <file>] [--journal <file>] [--replay <file>] [--audit <file>]\n" +
        "  --seconds   time to run in host seconds: real seconds in real-time mode, virtual seconds with --frame-ms (default 30)\n" +
        "  --seed      seed of the run and of the player's policy (default 1)\n" +
        "  --frame-ms  run the main loop on fixed frames of virtual time (reproducible); without it the loop reads the real clock\n" +
        "  --player    a bot that plays through the input state (default none)\n" +
        "  --script    extra input from a human: 'at <seconds> sell <1..10>' | 'autosell <1..10>|off' | 'speed <n>/<d>' | 'pause' | 'resume'\n" +
        "  --save      write a composite checkpoint when the run stops (waits for the input state to be quiet)\n" +
        "  --load      resume from a checkpoint instead of starting a new game (not with --player)\n" +
        "  --journal   record input edges with their delivery tick and per-tick state hashes (not with --load)\n" +
        "  --replay    re-run a journal against a fresh game with --seed and verify every tick hash\n" +
        "  --audit     audit file (default artifacts/headless/audit.jsonl)\n" +
        "exit: 0 ended or time reached | 1 runtime fault | 2 usage/content/file refusal | 3 checkpoint refused | 4 journal refused or replay diverged";

    public decimal Seconds { get; init; } = 30m;
    public ulong Seed { get; init; } = 1;
    /// <summary>Null runs the main loop on the real clock (Stopwatch); a value runs fixed frames of virtual time, which is reproducible.</summary>
    public int? FrameMilliseconds { get; init; }
    public string Player { get; init; } = "none";
    public string? ScriptPath { get; init; }
    public string? SavePath { get; init; }
    public string? LoadPath { get; init; }
    public string? JournalPath { get; init; }
    public string? ReplayPath { get; init; }
    public string AuditPath { get; init; } = Path.Combine("artifacts", "headless", "audit.jsonl");
    public bool Help { get; init; }

    public bool RealTime => FrameMilliseconds is null;
    public long FrameMicroseconds => (FrameMilliseconds ?? throw new InvalidOperationException("real-time mode has no fixed frame")) * 1000L;
    public long TotalMicroseconds => (long)(Seconds * 1_000_000m);

    public static HeadlessOptions Parse(IReadOnlyList<string> args)
    {
        var options = new HeadlessOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--help") { options = options with { Help = true }; continue; }
            if (i + 1 >= args.Count) throw new UsageException(name + " needs a value");
            var value = args[++i];
            options = name switch
            {
                "--seconds" => options with { Seconds = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var s) && s > 0 && s <= 86_400m ? s : throw new UsageException("--seconds must be a number in (0, 86400]") },
                "--seed" => options with { Seed = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ? seed : throw new UsageException("--seed must be a non-negative integer") },
                "--frame-ms" => options with { FrameMilliseconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 1000 ? f : throw new UsageException("--frame-ms must be an integer in 1..1000") },
                "--player" => options with { Player = TycoonBots.Names.Split('|').Contains(value) ? value : throw new UsageException("--player must be one of " + TycoonBots.Names) },
                "--script" => options with { ScriptPath = value },
                "--save" => options with { SavePath = value },
                "--load" => options with { LoadPath = value },
                "--journal" => options with { JournalPath = value },
                "--replay" => options with { ReplayPath = value },
                "--audit" => options with { AuditPath = value },
                _ => throw new UsageException("unknown argument " + name),
            };
        }
        if (options.LoadPath is not null && options.JournalPath is not null)
            throw new UsageException("--journal records from tick 1 and cannot be combined with --load");
        if (options.LoadPath is not null && options.Player != "none")
            throw new UsageException("a player policy keeps its own state, which a checkpoint does not contain; --load cannot be combined with --player");
        if (options.ReplayPath is not null && (options.Player != "none" || options.LoadPath is not null || options.SavePath is not null || options.JournalPath is not null || options.ScriptPath is not null))
            throw new UsageException("--replay uses only the journal's inputs and cannot be combined with --load, --save, --journal, --script or --player");
        return options;
    }
}
