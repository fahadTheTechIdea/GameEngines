using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EngineCommon.TailTool;

/// <summary>A tool refusal: the code says why, nothing was proposed.</summary>
public sealed class TailRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The codes the tail tool refuses with.</summary>
public static class TailCodes
{
    public const string SampleFormat = "TAIL_SAMPLE_FORMAT";
    public const string SampleDuplicate = "TAIL_SAMPLE_DUPLICATE";
    public const string SampleAmbiguous = "TAIL_SAMPLE_AMBIGUOUS";
    public const string MetricMissing = "TAIL_METRIC_MISSING";
    public const string Option = "TAIL_OPTION";
    public const string Levels = "TAIL_LEVELS";
    public const string FewExceedances = "TAIL_FEW_EXCEEDANCES";
    public const string FitFailed = "TAIL_FIT_FAILED";
    public const string CannotFall = "TAIL_CANNOT_FALL";
    public const string Multimodal = "TAIL_MULTIMODAL";
    public const string Bounded = "TAIL_BOUNDED";
}

/// <summary>
/// One metric's values read from a balance sweep written with <c>--rows</c>: either the per-seed rows <c>sample &lt;metric&gt; &lt;seed&gt; &lt;value&gt;</c> (one per seed) or the per-onset rows <c>onset &lt;metric&gt; &lt;seed&gt; &lt;value&gt;</c> (any
/// number per seed: the magnitude of each onset the engine reported). A name that has both kinds of row is refused, since the sample to fit would be a guess; every other line is ignored.
/// </summary>
public sealed record TailSample(string Metric, long[] Values, string Digest, bool PerOnset)
{
    public static TailSample Read(string text, string metric)
    {
        var seen = new SortedSet<long>();
        var samples = new List<(long Seed, long Value)>();
        var onsets = new List<(long Seed, long Value)>();
        foreach (var raw in text.Split((char)10))
        {
            var line = raw.TrimEnd((char)13);
            var perOnset = line.StartsWith("onset ", StringComparison.Ordinal);
            if (!perOnset && !line.StartsWith("sample ", StringComparison.Ordinal)) continue;
            var parts = line.Split(' ');
            if (parts.Length != 4 || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seed) || !long.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new TailRefusedException(TailCodes.SampleFormat, "'" + line + "' is not '" + (perOnset ? "onset" : "sample") + " <metric> <seed> <whole value>'");
            if (parts[1] != metric) continue;
            if (perOnset) onsets.Add((seed, value));
            else if (!seen.Add(seed)) throw new TailRefusedException(TailCodes.SampleDuplicate, "seed " + seed.ToString(CultureInfo.InvariantCulture) + " is listed twice for " + metric);
            else samples.Add((seed, value));
        }
        if (samples.Count > 0 && onsets.Count > 0) throw new TailRefusedException(TailCodes.SampleAmbiguous, metric + " has both 'sample' and 'onset' rows; sweep it as one or the other");
        var rows = onsets.Count > 0 ? onsets : samples;
        if (rows.Count == 0) throw new TailRefusedException(TailCodes.MetricMissing, "no 'sample " + metric + " ...' or 'onset " + metric + " ...' rows; run the sweep with --rows (and --metrics " + metric + ", or --onsets for per-onset values)");
        var ordered = rows.Select((row, index) => (row.Seed, row.Value, Index: index)).OrderBy(r => r.Seed).ThenBy(r => r.Index).ToList();
        var canonical = new StringBuilder();
        foreach (var (seed, value, _) in ordered)
            canonical.Append(onsets.Count > 0 ? "onset " : "").Append(metric).Append(' ').Append(seed.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append((char)10);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        return new TailSample(metric, ordered.Select(r => r.Value).ToArray(), digest, onsets.Count > 0);
    }
}
