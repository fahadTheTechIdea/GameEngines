using System.Globalization;
using System.Text;

namespace EngineCommon.Balance;

/// <summary>The values every seed gave, per metric, in seed order: <see cref="Values"/>[name][i] is the value for seed <c>FromSeed + i</c>.</summary>
public sealed record SweepResult(long FromSeed, SortedDictionary<string, long[]> Values)
{
    public int Seeds => Values.Count == 0 ? 0 : Values.First().Value.Length;

    /// <summary>The per-event values the engine reported beside its metrics (one value per onset, say), by name, each in seed order and then the order the engine reported it; null when the engine reports none.</summary>
    public SortedDictionary<string, List<(long Seed, long Value)>>? Events { get; init; }
}

/// <summary>What one seed gave: its named integer metrics (one value per seed) and named lists of values of which a seed may give any number (the magnitude of each onset that seed saw).</summary>
public sealed record SeedObservation(IReadOnlyDictionary<string, long> Metrics, IReadOnlyDictionary<string, IReadOnlyList<long>> Events);

/// <summary>
/// The one multi-seed sweep. An engine supplies a function from a seed to its named integer metrics (one independent headless run); this class runs the seeds one after the other in seed order (nothing is parallel, so the output never depends on
/// scheduling), checks that every seed reports the same metric names, keeps the metrics asked for, and writes the strict report: one line per metric in ordinal name order, optional per-seed rows, and a digest of the whole report.
/// </summary>
public static class SeedSweep
{
    public static SweepResult Run(SweepOptions options, Func<long, IReadOnlyDictionary<string, long>> perSeed)
    {
        ArgumentNullException.ThrowIfNull(perSeed);
        return RunWithEvents(options, seed => new SeedObservation(perSeed(seed), new Dictionary<string, IReadOnlyList<long>>())) with { Events = null };
    }

    /// <summary>The sweep of an engine that also reports lists of per-event values (see <see cref="SeedObservation"/>). The metrics are reduced as in <see cref="Run"/>; the events are kept whole, by name, for the report and for the offline tail tool.</summary>
    public static SweepResult RunWithEvents(SweepOptions options, Func<long, SeedObservation> perSeed)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(perSeed);
        SortedDictionary<string, long[]>? values = null;
        List<string>? available = null;
        var events = new SortedDictionary<string, List<(long Seed, long Value)>>(StringComparer.Ordinal);
        for (var i = 0; i < options.Seeds; i++)
        {
            var observation = perSeed(options.FromSeed + i);
            var metrics = observation.Metrics;
            foreach (var (eventName, list) in observation.Events.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (!events.TryGetValue(eventName, out var kept)) events[eventName] = kept = [];
                foreach (var value in list) kept.Add((options.FromSeed + i, value));
            }
            if (values is null)
            {
                available = metrics.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                var wanted = options.Metrics.Count == 0 ? available : options.Metrics;
                foreach (var name in wanted)
                    if (!metrics.ContainsKey(name)) throw new EngineRefusedException(BalanceCodes.MetricUnknown, "there is no metric '" + name + "'; the metrics are " + string.Join(", ", available));
                values = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
                foreach (var name in wanted) values[name] = new long[options.Seeds];
            }
            else if (metrics.Count != available!.Count || available.Any(name => !metrics.ContainsKey(name)))
            {
                throw new EngineRefusedException(BalanceCodes.MetricsDiffer, "seed " + (options.FromSeed + i).ToString(CultureInfo.InvariantCulture) + " reported other metric names than the first seed");
            }
            foreach (var (name, column) in values) column[i] = metrics[name];
        }
        return new SweepResult(options.FromSeed, values!) { Events = events };
    }

    /// <summary>The report: the heading line the engine gives, a line per metric, the rows when asked for, and the digest of everything before it. Lines end with a line feed.</summary>
    public static string Render(string heading, SweepOptions options, SweepResult result)
    {
        var text = new StringBuilder();
        text.Append(heading).Append('\n');
        foreach (var (name, column) in result.Values)
        {
            var sorted = SampleStats.Sorted(column);
            var positive = column.Count(v => v > 0);
            text.Append("metric ").Append(name).Append(" n ").Append(column.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" min ").Append(sorted[0].ToString(CultureInfo.InvariantCulture))
                .Append(" p10 ").Append(SampleStats.Percentile(sorted, 10).ToString(CultureInfo.InvariantCulture))
                .Append(" p50 ").Append(SampleStats.Percentile(sorted, 50).ToString(CultureInfo.InvariantCulture))
                .Append(" p90 ").Append(SampleStats.Percentile(sorted, 90).ToString(CultureInfo.InvariantCulture))
                .Append(" p99 ").Append(SampleStats.Percentile(sorted, 99).ToString(CultureInfo.InvariantCulture))
                .Append(" max ").Append(sorted[^1].ToString(CultureInfo.InvariantCulture))
                .Append(" mean ").Append(SampleStats.Fixed3(SampleStats.MeanMilli(column)))
                .Append(" positive ").Append(positive.ToString(CultureInfo.InvariantCulture));
            var interval = SampleStats.MedianInterval(sorted);
            text.Append(interval is null ? " median95 n/a" : " median95 " + interval.Value.Low.ToString(CultureInfo.InvariantCulture) + ".." + interval.Value.High.ToString(CultureInfo.InvariantCulture));
            if (positive == 0) text.Append(" zero-upper95-ppm ").Append(((3_000_000L + column.Length - 1) / column.Length).ToString(CultureInfo.InvariantCulture));
            text.Append('\n');
        }
        if (result.Events is { Count: > 0 })
            foreach (var (name, list) in result.Events)
            {
                var sorted = SampleStats.Sorted(list.Select(e => e.Value));
                text.Append("events ").Append(name).Append(" n ").Append(sorted.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(" seeds ").Append(list.Select(e => e.Seed).Distinct().Count().ToString(CultureInfo.InvariantCulture));
                if (sorted.Length > 0)
                    text.Append(" min ").Append(sorted[0].ToString(CultureInfo.InvariantCulture))
                        .Append(" p10 ").Append(SampleStats.Percentile(sorted, 10).ToString(CultureInfo.InvariantCulture))
                        .Append(" p50 ").Append(SampleStats.Percentile(sorted, 50).ToString(CultureInfo.InvariantCulture))
                        .Append(" p90 ").Append(SampleStats.Percentile(sorted, 90).ToString(CultureInfo.InvariantCulture))
                        .Append(" p99 ").Append(SampleStats.Percentile(sorted, 99).ToString(CultureInfo.InvariantCulture))
                        .Append(" max ").Append(sorted[^1].ToString(CultureInfo.InvariantCulture))
                        .Append(" mean ").Append(SampleStats.Fixed3(SampleStats.MeanMilli(sorted)));
                text.Append('\n');
            }
        if (options.Rows)
        {
            foreach (var (name, column) in result.Values)
                for (var i = 0; i < column.Length; i++)
                    text.Append("sample ").Append(name).Append(' ').Append((result.FromSeed + i).ToString(CultureInfo.InvariantCulture)).Append(' ').Append(column[i].ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (result.Events is { Count: > 0 })
                foreach (var (name, list) in result.Events)
                    foreach (var (seed, value) in list)
                        text.Append("onset ").Append(name).Append(' ').Append(seed.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        var digest = Digest.Of(text.ToString());
        text.Append("sweep-digest ").Append(digest).Append('\n');
        return text.ToString();
    }
}
