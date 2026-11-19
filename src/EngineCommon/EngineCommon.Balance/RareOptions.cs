using System.Globalization;

namespace EngineCommon.Balance;

/// <summary>
/// What a <c>simulate ... --rare &lt;target&gt;</c> command line asks for: the engine's own target text (a hazard and a rung, say), the effort per stage, the number of replications, the root seed, the horizon (a positional number in the engine's
/// unit or <c>--days</c>, exactly one) and whether to print each replication (<c>--rows</c>). The target text is the engine's to read; an unreadable one is refused by the engine with <see cref="BalanceCodes.RareTarget"/>.
/// </summary>
public sealed record RareOptions(string Target, int Effort, int Replications, long FromSeed, long? Days, long? Horizon, bool Rows, IReadOnlyList<int>? Efforts = null, bool Predraw = false, bool Conditional = false, bool Auto = false, int PilotEffort = RareOptions.DefaultPilotEffort, int PilotReplications = RareOptions.DefaultPilotReplications)
{
    public const int DefaultEffort = 200;
    public const int DefaultReplications = 21;
    public const int DefaultPilotEffort = 100;
    public const int DefaultPilotReplications = 5;

    public static RareOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? target = null;
        int? effort = null;
        int? replications = null;
        long? from = null;
        long? days = null;
        long? positional = null;
        var rows = false;
        List<int>? efforts = null;
        var predraw = false;
        var conditional = false;
        var auto = false;
        int? pilotEffort = null;
        int? pilotReplications = null;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (positional is not null) throw new EngineRefusedException(BalanceCodes.HorizonTwice, "the horizon is given once; '" + arg + "' is a second positional number");
                positional = OptionReader.Whole("the horizon", arg, 1, long.MaxValue, BalanceCodes.OptionValue);
            }
            else if (arg == "--rare")
            {
                if (target is not null) throw OptionReader.Twice(arg);
                target = OptionReader.Value(args, ref i);
            }
            else if (arg == "--effort")
            {
                if (effort is not null) throw OptionReader.Twice(arg);
                effort = (int)OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, BalanceCodes.MaxEffort, BalanceCodes.EffortRange);
            }
            else if (arg == "--efforts")
            {
                if (efforts is not null || auto) throw OptionReader.Twice(arg);
                var list = OptionReader.Value(args, ref i);
                if (list == "auto") auto = true;
                else efforts = list.Split(',').Select(part => (int)OptionReader.Whole("an effort of --efforts", part, 1, BalanceCodes.MaxEffort, BalanceCodes.EffortRange)).ToList();
            }
            else if (arg == "--pilot-effort")
            {
                if (pilotEffort is not null) throw OptionReader.Twice(arg);
                pilotEffort = (int)OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, BalanceCodes.MaxEffort, BalanceCodes.EffortRange);
            }
            else if (arg == "--pilot-replications")
            {
                if (pilotReplications is not null) throw OptionReader.Twice(arg);
                pilotReplications = (int)OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, BalanceCodes.MaxReplications, BalanceCodes.ReplicationsRange);
            }
            else if (arg == "--predraw")
            {
                if (predraw) throw OptionReader.Twice(arg);
                predraw = true;
            }
            else if (arg == "--conditional")
            {
                if (conditional) throw OptionReader.Twice(arg);
                conditional = true;
            }
            else if (arg == "--replications")
            {
                if (replications is not null) throw OptionReader.Twice(arg);
                replications = (int)OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, BalanceCodes.MaxReplications, BalanceCodes.ReplicationsRange);
            }
            else if (arg == "--from-seed")
            {
                if (from is not null) throw OptionReader.Twice(arg);
                from = OptionReader.Whole(arg, OptionReader.Value(args, ref i), 0, BalanceCodes.MaxSeed, BalanceCodes.FromSeedRange);
            }
            else if (arg == "--days")
            {
                if (days is not null) throw OptionReader.Twice(arg);
                days = OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, 1_000_000, BalanceCodes.DaysRange);
            }
            else if (arg == "--rows")
            {
                if (rows) throw OptionReader.Twice(arg);
                rows = true;
            }
            else
            {
                throw new EngineRefusedException(BalanceCodes.OptionUnknown, "'" + arg + "' is not an option of a rare-event run (--rare, --effort, --efforts, --pilot-effort, --pilot-replications, --predraw, --conditional, --replications, --from-seed, --days, --rows)");
            }
        }
        if (efforts is not null && effort is not null) throw new EngineRefusedException(BalanceCodes.EffortsTwice, "give --effort (the same for every stage) or --efforts (one per stage), not both");
        if (conditional && (predraw || efforts is not null || auto)) throw new EngineRefusedException(BalanceCodes.ConditionalMix, "--conditional has no stages: it cannot be combined with --predraw or --efforts (--effort is the number of runs in a replication)");
        if (!auto && (pilotEffort is not null || pilotReplications is not null)) throw new EngineRefusedException(BalanceCodes.PilotWithoutAuto, "--pilot-effort and --pilot-replications belong to --efforts auto; give --efforts auto or remove them");
        if (target is null) throw new EngineRefusedException(BalanceCodes.RareMissing, "a rare-event run needs --rare <target>");
        if (positional is null && days is null) throw new EngineRefusedException(BalanceCodes.HorizonMissing, "give the horizon as a positional number or with --days, one of them");
        if (positional is not null && days is not null) throw new EngineRefusedException(BalanceCodes.HorizonTwice, "give the horizon as a positional number or with --days, not both");
        return new RareOptions(target, efforts?[0] ?? effort ?? DefaultEffort, replications ?? DefaultReplications, from ?? 0, days, positional, rows, efforts, predraw, conditional, auto, pilotEffort ?? DefaultPilotEffort, pilotReplications ?? DefaultPilotReplications);
    }



}
