using System.Globalization;
using System.Text;

namespace EngineCommon.TailTool;

/// <summary>
/// The text of <c>modality &lt;sweep.txt&gt; &lt;metric&gt; [--threshold-percent P]</c>: what the three unimodality tests of the tool say about a sample, side by side, for all its values and for the values above the percentile threshold the fit would use. It
/// decides nothing: <c>propose</c> still refuses on the gap test (<see cref="ModalityCheck"/>) alone, and this report is how a person sees what Hartigan's dip test (<see cref="DipTest"/>) and Silverman's critical-bandwidth test
/// (<see cref="SilvermanTest"/>) say about the same numbers.
/// </summary>
public static class ModalityReport
{
    /// <summary>The fewest values a part of the sample needs for the report to test it.</summary>
    public const int MinimumValues = 20;

    public static string Of(TailSample sample, int percent)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var sorted = sample.Values.OrderBy(v => v).ToArray();
        var rank = ((long)percent * sorted.Length + 99) / 100;
        var threshold = sorted[(int)rank - 1];
        var text = new StringBuilder();
        text.Append("tail-tool modality metric ").Append(sample.Metric).Append(" samples ").Append(sorted.Length).Append(" min ").Append(sorted[0]).Append(" max ").Append(sorted[^1]).Append(" sample-digest ").Append(sample.Digest).Append(sample.PerOnset ? " per-onset" : "").Append('\n');
        text.Append(Line("all-values", sorted));
        text.Append(Line("above-threshold-" + percent.ToString(CultureInfo.InvariantCulture) + "% (threshold " + threshold.ToString(CultureInfo.InvariantCulture) + ")", sorted.Where(v => v > threshold).ToArray()));
        text.Append("modality-note p-values: the gap test has none (it finds a stretch with no values, ratio over ").Append(ModalityCheck.RatioLimit.ToString("F0", CultureInfo.InvariantCulture)).Append(" times the spacing); the dip's is against ")
            .Append(DipTest.NullSamples).Append(" simulated uniform samples of the same size with tied whole numbers spread over their cells; Silverman's is the share of ").Append(SilvermanTest.DefaultResamples)
            .Append(" smoothed-bootstrap samples with a critical bandwidth at least as large. Both reject unimodality only; neither locates the clusters. Floating point, offline, and the same sample always gives the same line\n");
        return text.ToString();
    }

    private static string Line(string part, long[] sorted)
    {
        var text = new StringBuilder(part).Append(" n ").Append(sorted.Length);
        if (sorted.Length < MinimumValues) return text.Append(" not tested: fewer than ").Append(MinimumValues).Append(" values\n").ToString();
        if (sorted[0] == sorted[^1]) return text.Append(" not tested: every value is ").Append(sorted[0]).Append('\n').ToString();
        var gaps = ModalityCheck.Gaps(sorted);
        text.Append(" gap-test ").Append(gaps.Count == 0 ? "none" : "gap " + gaps[0].Low + ".." + gaps[0].High + " ratio " + gaps[0].Ratio.ToString("F0", CultureInfo.InvariantCulture));
        var dip = DipTest.Test(sorted);
        text.Append(" dip ").Append(dip.Statistic.Dip.ToString("F5", CultureInfo.InvariantCulture)).Append(" p ").Append(dip.PValue.ToString("F3", CultureInfo.InvariantCulture));
        var silverman = SilvermanTest.Test(sorted);
        text.Append(" silverman-bandwidth ").Append(silverman.CriticalBandwidth.ToString("F1", CultureInfo.InvariantCulture)).Append(" p ").Append(silverman.PValue.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }
}
