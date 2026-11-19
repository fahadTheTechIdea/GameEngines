using System.Globalization;

namespace GameRuntime.Combat.Headless;

/// <summary>One fighting group of a run: its slot letter (which is its group id), the pack its formation comes from, the formation and its side.</summary>
public sealed record SkirmishSlot(char Letter, string Pack, string Formation, int Side)
{
    public string GroupId => Letter.ToString();
}

/// <summary>What one run was asked to do.</summary>
public sealed record SkirmishOptions
{
    public const string Usage =
        "usage: GameRuntime.Combat.Headless (--era <ancient|medieval|modern|future|fantasy> | --pack <id> | --pack-a <id> --pack-b <id> ...) --side-a <formation> --side-b <formation> [--side-c <formation> [--side-d <formation>]]\n" +
        "                                   [--teams <n,n,...>] [--seconds <n>] [--seed <n>] [--frame-ms <1..1000>] [--player none|aggressor|skirmisher|defender|melee]\n" +
        "                                   [--distance <m>] [--round-seconds <n>] [--terrain <id>] [--packs <dir>] [--rules <file>] [--script <file>]\n" +
        "                                   [--save <file>] [--load <file>] [--journal <file>] [--replay <file>] [--audit <file>]\n" +
        "  --era       every group comes from the catalog pack arms-<era>\n" +
        "  --pack      every group comes from this catalog pack; --pack-a to --pack-d choose each group's pack, so eras can meet\n" +
        "  --side-a    formation id (for example arms-ancient.iron-warband) of group a; --side-b to --side-d likewise for groups b to d\n" +
        "  --teams     the side of each group in order, for allies: 1,1,2 puts a and b on side 1 against c (default: every group its own side)\n" +
        "  --seconds   time to run in host seconds: real seconds in real-time mode, virtual seconds with --frame-ms (default 30)\n" +
        "  --seed      seed of the run (chance rolls) (default 1)\n" +
        "  --frame-ms  run the main loop on fixed frames of virtual time (reproducible); without it the loop reads the real clock\n" +
        "  --player    a commander bot that plays through the input state (default none); aggressor, skirmisher and defender play a duel, melee any number\n" +
        "  --distance  starting distance between the sides in metres (default 300)\n" +
        "  --round-seconds  length of each march or engagement the bot orders, in seconds (default 10)\n" +
        "  --terrain   the ground the bot fights and marches over, a terrain id of the rules (default open)\n" +
        "  --packs     the folder that holds the packs (default Packs beside the program)\n" +
        "  --rules     combat rules file (default Content/combat-rules.json beside the program)\n" +
        "  --script    extra input from a human: 'at <seconds> engage|march|round|resupply <argument>' (see the actions)\n" +
        "  --save      write a checkpoint when the run stops (waits for the commander and input to be settled)\n" +
        "  --load      resume from a checkpoint instead of starting a new game\n" +
        "  --journal   record input edges with their delivery tick and per-tick state fingerprints (not with --load)\n" +
        "  --replay    re-run a journal against a fresh game with the same forces and --seed and verify every tick\n" +
        "  --audit     audit file (default artifacts/combat/audit.jsonl)\n" +
        "exit: 0 ended or time reached | 1 runtime fault | 2 usage/content/file refusal | 3 checkpoint refused | 4 journal refused or replay diverged";

    public const string Letters = "abcd";

    public decimal Seconds { get; init; } = 30m;
    public ulong Seed { get; init; } = 1;
    public int? FrameMilliseconds { get; init; }
    public string Player { get; init; } = "none";
    public int DistanceMetres { get; init; } = 300;
    public int RoundSeconds { get; init; } = 10;
    public string Terrain { get; init; } = "open";
    public string? Era { get; init; }
    public string? Pack { get; init; }
    public IReadOnlyDictionary<char, string> PackOf { get; init; } = new Dictionary<char, string>();
    public IReadOnlyDictionary<char, string> FormationOf { get; init; } = new Dictionary<char, string>();
    public string? Teams { get; init; }
    public string? PacksPath { get; init; }
    public string? RulesPath { get; init; }
    public string? ScriptPath { get; init; }
    public string? SavePath { get; init; }
    public string? LoadPath { get; init; }
    public string? JournalPath { get; init; }
    public string? ReplayPath { get; init; }
    public string AuditPath { get; init; } = Path.Combine("artifacts", "combat", "audit.jsonl");
    public bool Help { get; init; }

    public bool RealTime => FrameMilliseconds is null;
    public long FrameMicroseconds => (FrameMilliseconds ?? throw new InvalidOperationException("real-time mode has no fixed frame")) * 1000L;
    public long TotalMicroseconds => (long)(Seconds * 1_000_000m);

    public static readonly string[] Eras = ["ancient", "medieval", "modern", "future", "fantasy"];

    public SkirmishPlan Plan => new(DistanceMetres, RoundSeconds, Terrain);

    /// <summary>The groups of the run in slot order, each with its pack (own pack, then --pack, then --era) and its side (from --teams, else its own).</summary>
    public IReadOnlyList<SkirmishSlot> Slots()
    {
        var letters = Letters.Where(FormationOf.ContainsKey).ToList();
        for (var i = 0; i < letters.Count; i++)
            if (letters[i] != Letters[i]) throw new SkirmishUsageException("groups are given in order: --side-" + Letters[i] + " is missing before --side-" + letters[i]);
        if (letters.Count < 2) throw new SkirmishUsageException("--side-a and --side-b name the two formations that fight");
        var teams = Teams is null ? Enumerable.Range(1, letters.Count).ToArray() : ParseTeams(Teams, letters.Count);
        var slots = new List<SkirmishSlot>();
        for (var i = 0; i < letters.Count; i++)
        {
            var letter = letters[i];
            var pack = PackOf.TryGetValue(letter, out var own) ? own : Pack ?? (Era is null ? null : "arms-" + Era);
            slots.Add(new SkirmishSlot(letter, pack ?? throw new SkirmishUsageException("group " + letter + " has no pack: give --era, --pack or --pack-" + letter), FormationOf[letter], teams[i]));
        }
        foreach (var letter in PackOf.Keys)
            if (!FormationOf.ContainsKey(letter)) throw new SkirmishUsageException("--pack-" + letter + " names a pack for a group that has no --side-" + letter);
        return slots;
    }

    private static int[] ParseTeams(string text, int groups)
    {
        var parts = text.Split(',');
        if (parts.Length != groups) throw new SkirmishUsageException("--teams needs one side per group (" + groups + ") but got '" + text + "'");
        return parts.Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var side) && side >= 1 ? side : throw new SkirmishUsageException("--teams sides must be whole numbers from 1 but got '" + p + "'")).ToArray();
    }

    public static SkirmishOptions Parse(IReadOnlyList<string> args)
    {
        var options = new SkirmishOptions();
        var packs = new Dictionary<char, string>();
        var formations = new Dictionary<char, string>();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--help") { options = options with { Help = true }; continue; }
            if (i + 1 >= args.Count) throw new SkirmishUsageException(name + " needs a value");
            var value = args[++i];
            if (name.Length == 8 && name.StartsWith("--side-", StringComparison.Ordinal) && Letters.Contains(name[7])) { formations[name[7]] = value; continue; }
            if (name.Length == 8 && name.StartsWith("--pack-", StringComparison.Ordinal) && Letters.Contains(name[7])) { packs[name[7]] = value; continue; }
            options = name switch
            {
                "--seconds" => options with { Seconds = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var s) && s > 0 && s <= 86_400m ? s : throw new SkirmishUsageException("--seconds must be a number in (0, 86400]") },
                "--seed" => options with { Seed = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ? seed : throw new SkirmishUsageException("--seed must be a non-negative integer") },
                "--frame-ms" => options with { FrameMilliseconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 1000 ? f : throw new SkirmishUsageException("--frame-ms must be an integer in 1..1000") },
                "--player" => options with { Player = SkirmishBots.Names.Split('|').Contains(value) ? value : throw new SkirmishUsageException("--player must be one of " + SkirmishBots.Names) },
                "--distance" => options with { DistanceMetres = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var d) && d is >= 1 and <= 100_000 ? d : throw new SkirmishUsageException("--distance must be an integer in 1..100000 metres") },
                "--round-seconds" => options with { RoundSeconds = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var r) && r is >= 1 and <= 3600 ? r : throw new SkirmishUsageException("--round-seconds must be an integer in 1..3600") },
                "--terrain" => options with { Terrain = value },
                "--era" => options with { Era = Eras.Contains(value) ? value : throw new SkirmishUsageException("--era must be one of " + string.Join('|', Eras)) },
                "--pack" => options with { Pack = value },
                "--teams" => options with { Teams = value },
                "--packs" => options with { PacksPath = value },
                "--rules" => options with { RulesPath = value },
                "--script" => options with { ScriptPath = value },
                "--save" => options with { SavePath = value },
                "--load" => options with { LoadPath = value },
                "--journal" => options with { JournalPath = value },
                "--replay" => options with { ReplayPath = value },
                "--audit" => options with { AuditPath = value },
                _ => throw new SkirmishUsageException("unknown argument " + name),
            };
        }
        options = options with { PackOf = packs, FormationOf = formations };
        if (options.Help) return options;
        var slots = options.Slots();
        if (SkirmishBots.Duelists.Contains(options.Player) && slots.Count != 2) throw new SkirmishUsageException("--player " + options.Player + " plays a duel between two groups; use melee for " + slots.Count);
        if (options.LoadPath is not null && options.JournalPath is not null)
            throw new SkirmishUsageException("--journal records from tick 1 and cannot be combined with --load");
        if (options.ReplayPath is not null && (options.Player != "none" || options.LoadPath is not null || options.SavePath is not null || options.JournalPath is not null || options.ScriptPath is not null))
            throw new SkirmishUsageException("--replay uses only the journal's inputs and cannot be combined with --load, --save, --journal, --script or --player");
        return options;
    }
}
