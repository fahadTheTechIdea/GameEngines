using System.Globalization;

namespace GameRuntime.Field.Headless;

/// <summary>What one run of the field game was asked to do.</summary>
public sealed record FieldOptions
{
    public const string Usage =
        "usage: GameRuntime.Field.Headless --pack-a <id> --formation-a <id> --squads-a <n> --pack-b <id> --formation-b <id> --squads-b <n>\n" +
        "                                  [--seconds <n>] [--seed <n>] [--frame-ms <1..1000>] [--distance <m>] [--round-seconds <n>] [--terrain <id>] [--stock-lines <n>] [--depot-percent <n>]\n" +
        "                                  [--packs <dir>] [--rules <file>] [--content <dir>] [--save <file>] [--load <file>] [--journal <file>] [--replay <file>] [--audit <file>]\n" +
        "  --pack-a / --formation-a / --squads-a   the catalog pack and formation of side 1 and how many squads of it take the field (side b likewise)\n" +
        "  --seconds   time to run in host seconds: real seconds in real-time mode, virtual seconds with --frame-ms (default 60)\n" +
        "  --seed      seed of the run: character names and traits, casualty order and combat rolls (default 1)\n" +
        "  --frame-ms  run the main loop on fixed frames of virtual time (reproducible); without it the loop reads the real clock\n" +
        "  --distance  distance between the lines in metres, held for the whole fight (default 300)\n" +
        "  --round-seconds  length of each round of fire in seconds (default 5)\n" +
        "  --stock-lines  units of stock work the world does per tick (a ledger line is 1, a move or a reinforcement 8); the rest wait for a later tick (default 40)\n" +
        "  --depot-percent  the reserve each side keeps in its depot, as a percent of the people and rounds it took onto the field; 0 gives no depot stock, so no resupply or reinforcement (default 25)\n" +
        "  --terrain   the ground the fight is on, a terrain id of the combat rules (default open)\n" +
        "  --packs     the folder that holds the catalog packs (default Packs beside the program)\n" +
        "  --rules     combat rules file (default Content/combat-rules.json beside the program)\n" +
        "  --content   the folder of the world content (default FieldContent beside the program)\n" +
        "  --save      write a checkpoint when the run stops (waits for every request to be answered)\n" +
        "  --load      resume from a checkpoint instead of raising the field\n" +
        "  --journal   record the state fingerprint of every tick (not with --load)\n" +
        "  --replay    re-run a journal against a fresh game and verify every tick\n" +
        "  --audit     audit file (default artifacts/field/audit.jsonl)\n" +
        "exit: 0 ended or time reached | 1 runtime fault | 2 usage/content/file refusal | 3 checkpoint refused | 4 journal refused or replay diverged";

    public decimal Seconds { get; init; } = 60m;
    public ulong Seed { get; init; } = 1;
    public int? FrameMilliseconds { get; init; }
    public int DistanceMetres { get; init; } = 300;
    public int RoundSeconds { get; init; } = 5;
    public int StockLinesPerTick { get; init; } = 40;
    public int DepotPercent { get; init; } = 25;
    public string Terrain { get; init; } = "open";
    public string? PackA { get; init; }
    public string? FormationA { get; init; }
    public int? SquadsA { get; init; }
    public string? PackB { get; init; }
    public string? FormationB { get; init; }
    public int? SquadsB { get; init; }
    public string? PacksPath { get; init; }
    public string? RulesPath { get; init; }
    public string? ContentPath { get; init; }
    public string? SavePath { get; init; }
    public string? LoadPath { get; init; }
    public string? JournalPath { get; init; }
    public string? ReplayPath { get; init; }
    public string AuditPath { get; init; } = Path.Combine("artifacts", "field", "audit.jsonl");
    public bool Help { get; init; }

    public bool RealTime => FrameMilliseconds is null;
    public long FrameMicroseconds => (FrameMilliseconds ?? throw new InvalidOperationException("real-time mode has no fixed frame")) * 1000L;
    public long TotalMicroseconds => (long)(Seconds * 1_000_000m);

    public static FieldOptions Parse(IReadOnlyList<string> args)
    {
        var options = new FieldOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--help") { options = options with { Help = true }; continue; }
            if (i + 1 >= args.Count) throw new FieldUsageException(name + " needs a value");
            var value = args[++i];
            options = name switch
            {
                "--seconds" => options with { Seconds = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var s) && s > 0 && s <= 86_400m ? s : throw new FieldUsageException("--seconds must be a number in (0, 86400]") },
                "--seed" => options with { Seed = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ? seed : throw new FieldUsageException("--seed must be a non-negative integer") },
                "--frame-ms" => options with { FrameMilliseconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 1000 ? f : throw new FieldUsageException("--frame-ms must be an integer in 1..1000") },
                "--distance" => options with { DistanceMetres = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var d) && d is >= 1 and <= 100_000 ? d : throw new FieldUsageException("--distance must be an integer in 1..100000 metres") },
                "--round-seconds" => options with { RoundSeconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var r) && r is >= 1 and <= 3600 ? r : throw new FieldUsageException("--round-seconds must be an integer in 1..3600") },
                "--stock-lines" => options with { StockLinesPerTick = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var lines) && lines is >= 1 and <= 1_000_000 ? lines : throw new FieldUsageException("--stock-lines must be an integer in 1..1000000") },
                "--depot-percent" => options with { DepotPercent = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var percent) && percent <= 1000 ? percent : throw new FieldUsageException("--depot-percent must be an integer in 0..1000") },
                "--terrain" => options with { Terrain = value },
                "--pack-a" => options with { PackA = value },
                "--formation-a" => options with { FormationA = value },
                "--squads-a" => options with { SquadsA = Squads(value, name) },
                "--pack-b" => options with { PackB = value },
                "--formation-b" => options with { FormationB = value },
                "--squads-b" => options with { SquadsB = Squads(value, name) },
                "--packs" => options with { PacksPath = value },
                "--rules" => options with { RulesPath = value },
                "--content" => options with { ContentPath = value },
                "--save" => options with { SavePath = value },
                "--load" => options with { LoadPath = value },
                "--journal" => options with { JournalPath = value },
                "--replay" => options with { ReplayPath = value },
                "--audit" => options with { AuditPath = value },
                _ => throw new FieldUsageException("unknown argument " + name),
            };
        }
        if (options.Help) return options;
        foreach (var (given, name) in new[] { (options.PackA, "--pack-a"), (options.FormationA, "--formation-a"), (options.PackB, "--pack-b"), (options.FormationB, "--formation-b") })
            if (given is null) throw new FieldUsageException(name + " is required");
        if (options.SquadsA is null || options.SquadsB is null) throw new FieldUsageException("--squads-a and --squads-b are required");
        if (options.LoadPath is not null && options.JournalPath is not null) throw new FieldUsageException("--journal records from tick 1 and cannot be combined with --load");
        if (options.ReplayPath is not null && (options.LoadPath is not null || options.SavePath is not null || options.JournalPath is not null))
            throw new FieldUsageException("--replay cannot be combined with --load, --save or --journal");
        return options;
    }

    private static int Squads(string value, string name) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 1000 ? n : throw new FieldUsageException(name + " must be an integer in 1..1000");
}
