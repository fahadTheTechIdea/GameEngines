using System.Globalization;

namespace EngineCommon.Balance;

/// <summary>
/// What a <c>simulate ... --seeds N</c> command line asks for: how many seeds from which first seed, the horizon (a positional number in the engine's own unit, or <c>--days</c>), the metrics to report (all when none are named) and
/// whether to print every per-seed value (<c>--rows</c>). Exactly one of the positional horizon and <c>--days</c> is given.
/// </summary>
public sealed record SweepOptions(int Seeds, long FromSeed, long? Days, long? Horizon, IReadOnlyList<string> Metrics, bool Rows)
{
    /// <summary>The engine's own flags (for example <c>--onsets</c>) that the command line carried; only flags the engine named when it called <see cref="Parse"/> can be here.</summary>
    public IReadOnlyList<string> Flags { get; init; } = [];

    public bool Has(string flag) => Flags.Contains(flag);

    /// <summary>
    /// Reads the arguments after the document and scenario paths. A refusal is an <see cref="EngineRefusedException"/> with a <see cref="BalanceCodes"/> code; nothing has run. <paramref name="engineFlags"/> are the extra on/off flags this engine
    /// reads itself (an engine that has none passes nothing, and any other flag is refused): they are kept in <see cref="Flags"/> in the order given.
    /// </summary>
    public static SweepOptions Parse(IReadOnlyList<string> args, IReadOnlyList<string>? engineFlags = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        var extra = engineFlags ?? [];
        var flags = new List<string>();
        int? seeds = null;
        long? from = null;
        long? days = null;
        long? positional = null;
        List<string>? metrics = null;
        var rows = false;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (positional is not null) throw new EngineRefusedException(BalanceCodes.HorizonTwice, "the horizon is given once; '" + arg + "' is a second positional number");
                positional = OptionReader.Whole("the horizon", arg, 1, long.MaxValue, BalanceCodes.OptionValue);
            }
            else if (arg == "--seeds")
            {
                if (seeds is not null) throw OptionReader.Twice(arg);
                seeds = (int)OptionReader.Whole(arg, OptionReader.Value(args, ref i), 1, BalanceCodes.MaxSeeds, BalanceCodes.SeedsRange);
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
            else if (arg == "--metrics")
            {
                if (metrics is not null) throw OptionReader.Twice(arg);
                metrics = Names(OptionReader.Value(args, ref i));
            }
            else if (arg == "--rows")
            {
                if (rows) throw OptionReader.Twice(arg);
                rows = true;
            }
            else if (extra.Contains(arg))
            {
                if (flags.Contains(arg)) throw OptionReader.Twice(arg);
                flags.Add(arg);
            }
            else
            {
                throw new EngineRefusedException(BalanceCodes.OptionUnknown, "'" + arg + "' is not an option of a seed sweep (--seeds, --from-seed, --days, --metrics, --rows" + string.Concat(extra.Select(f => ", " + f)) + ")");
            }
        }
        if (seeds is null) throw new EngineRefusedException(BalanceCodes.SeedsMissing, "a seed sweep needs --seeds <count>");
        if (positional is null && days is null) throw new EngineRefusedException(BalanceCodes.HorizonMissing, "give the horizon as a positional number or with --days, one of them");
        if (positional is not null && days is not null) throw new EngineRefusedException(BalanceCodes.HorizonTwice, "give the horizon as a positional number or with --days, not both");
        var first = from ?? 0;
        var last = first + seeds.Value - 1;
        if (last > BalanceCodes.MaxSeed) throw new EngineRefusedException(BalanceCodes.FromSeedRange, "the last seed " + last.ToString(CultureInfo.InvariantCulture) + " is above " + BalanceCodes.MaxSeed.ToString(CultureInfo.InvariantCulture));
        return new SweepOptions(seeds.Value, first, days, positional, metrics ?? [], rows) { Flags = flags };
    }




    private static List<string> Names(string text)
    {
        var names = new List<string>();
        foreach (var name in text.Split(','))
        {
            if (name.Length == 0 || name.Any(c => c <= ' ' || c > '~')) throw new EngineRefusedException(BalanceCodes.MetricName, "a metric name is printable ASCII without spaces, not '" + name + "'");
            if (names.Contains(name)) throw new EngineRefusedException(BalanceCodes.MetricName, "metric '" + name + "' is named twice");
            names.Add(name);
        }
        return names;
    }
}
