using System.Globalization;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>What one run was asked to do.</summary>
public sealed record WorkshopOptions
{
    public const string Usage =
        "usage: GameRuntime.AlchemySim.Headless [--seconds <n>] [--seed <n>] [--frame-ms <1..1000>] [--player none|alchemist]\n" +
        "                                       [--script <file>] [--save <file>] [--load <file>] [--journal <file>] [--replay <file>] [--audit <file>]\n" +
        "  --seconds   time to run in host seconds: real seconds in real-time mode, virtual seconds with --frame-ms (default 60)\n" +
        "  --seed      seed of the run (chance rolls and the sim) (default 1)\n" +
        "  --frame-ms  run the main loop on fixed frames of virtual time (reproducible); without it the loop reads the real clock\n" +
        "  --player    a bot that plays through the input state (default none)\n" +
        "  --script    extra input from a human: 'at <seconds> combine <items>[|<environment>]' | 'at <seconds> order <recipe>[|<count>]'\n" +
        "  --save      write a checkpoint when the run stops (waits for everything in flight to settle)\n" +
        "  --load      resume from a checkpoint instead of starting a new game\n" +
        "  --journal   record input edges with their delivery tick and per-tick state fingerprints (not with --load)\n" +
        "  --replay    re-run a journal against a fresh game with --seed and verify every tick\n" +
        "  --audit     audit file (default artifacts/workshop/audit.jsonl)\n" +
        "exit: 0 ended or time reached | 1 runtime fault | 2 usage/content/file refusal | 3 checkpoint refused | 4 journal refused or replay diverged";

    public decimal Seconds { get; init; } = 60m;
    public ulong Seed { get; init; } = 1;
    /// <summary>Null runs the main loop on the real clock (Stopwatch); a value runs fixed frames of virtual time, which is reproducible.</summary>
    public int? FrameMilliseconds { get; init; }
    public string Player { get; init; } = "none";
    public string? ScriptPath { get; init; }
    public string? SavePath { get; init; }
    public string? LoadPath { get; init; }
    public string? JournalPath { get; init; }
    public string? ReplayPath { get; init; }
    public string AuditPath { get; init; } = Path.Combine("artifacts", "workshop", "audit.jsonl");
    public bool Help { get; init; }

    public bool RealTime => FrameMilliseconds is null;
    public long FrameMicroseconds => (FrameMilliseconds ?? throw new InvalidOperationException("real-time mode has no fixed frame")) * 1000L;
    public long TotalMicroseconds => (long)(Seconds * 1_000_000m);

    public static WorkshopOptions Parse(IReadOnlyList<string> args)
    {
        var options = new WorkshopOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--help") { options = options with { Help = true }; continue; }
            if (i + 1 >= args.Count) throw new WorkshopUsageException(name + " needs a value");
            var value = args[++i];
            options = name switch
            {
                "--seconds" => options with { Seconds = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var s) && s > 0 && s <= 86_400m ? s : throw new WorkshopUsageException("--seconds must be a number in (0, 86400]") },
                "--seed" => options with { Seed = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ? seed : throw new WorkshopUsageException("--seed must be a non-negative integer") },
                "--frame-ms" => options with { FrameMilliseconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 1000 ? f : throw new WorkshopUsageException("--frame-ms must be an integer in 1..1000") },
                "--player" => options with { Player = value is "none" or "alchemist" ? value : throw new WorkshopUsageException("--player must be none or alchemist") },
                "--script" => options with { ScriptPath = value },
                "--save" => options with { SavePath = value },
                "--load" => options with { LoadPath = value },
                "--journal" => options with { JournalPath = value },
                "--replay" => options with { ReplayPath = value },
                "--audit" => options with { AuditPath = value },
                _ => throw new WorkshopUsageException("unknown argument " + name),
            };
        }
        if (options.LoadPath is not null && options.JournalPath is not null)
            throw new WorkshopUsageException("--journal records from tick 1 and cannot be combined with --load");
        if (options.ReplayPath is not null && (options.Player != "none" || options.LoadPath is not null || options.SavePath is not null || options.JournalPath is not null || options.ScriptPath is not null))
            throw new WorkshopUsageException("--replay uses only the journal's inputs and cannot be combined with --load, --save, --journal, --script or --player");
        return options;
    }
}
