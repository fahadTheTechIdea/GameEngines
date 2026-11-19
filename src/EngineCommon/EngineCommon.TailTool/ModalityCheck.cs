namespace EngineCommon.TailTool;

/// <summary>An empty stretch inside a sample: no value lies strictly between <see cref="Low"/> and <see cref="High"/>, with <see cref="Below"/> values at or under the first and <see cref="Above"/> at or over the second. <see cref="Ratio"/> is its width over the typical spacing of the values around it.</summary>
public sealed record SampleGap(long Low, long High, int Below, int Above, double Ratio);

/// <summary>
/// The one test the tool makes for a sample with more than one cluster (a multimodal sample), written for whole-number data. A generalized Pareto tail has a density that only falls, so a fit over values that straddle an empty stretch describes
/// neither cluster. The sample's sorted values are walked once: at each place where the value changes by at least 2 (the data are whole numbers, so a step of 1 is no gap), the width of the step is compared with the mean step of the
/// <see cref="Window"/> values on each side of it (the step itself left out, a mean step of less than 1 counted as 1 because values cannot lie closer). Only steps with at least <c>max(5, n / 50)</c> values on both sides are looked at, so the thin end
/// of a heavy tail, whose steps are wide by nature, is not mistaken for a second cluster. A step whose ratio exceeds <see cref="RatioLimit"/> is reported.
/// <para>
/// The established tests of unimodality are Hartigan's dip test (Hartigan and Hartigan 1985, <see cref="DipTest"/>) and Silverman's critical-bandwidth test (Silverman 1981, <see cref="SilvermanTest"/>), both now in the tool and calibrated
/// next to this one (docs/WORLD_EVENTS_ENGINE.md, section 16). This test is kept: it needs no null table and no ties rule beyond the whole-number floor, it has made no false alarm in thousands of simulated unimodal samples, and it
/// finds clusters separated by a stretch many times the spacing of the values. What it does not find is a shoulder, two clusters closer than that, and there the dip test is the stronger (section 16), so <see cref="TailCli"/> asks both. Silverman's
/// test is not used to decide anything: on skewed and heavy-tailed unimodal samples it called far too many of them multimodal. Floating point is allowed here as everywhere in the tool.
/// </para>
/// </summary>
public static class ModalityCheck
{
    /// <summary>The ratio above which a step is a gap. Set above the largest ratio found in simulated unimodal samples of 200 to 5,000 values (exponential, Pareto, normal, lognormal, uniform); see the tests.</summary>
    public const double RatioLimit = 30;

    /// <summary>How many values on each side of a step are used for the typical spacing, at most.</summary>
    public const int Window = 25;

    /// <summary>The steps of the sorted sample whose ratio exceeds <paramref name="limit"/>, widest ratio first.</summary>
    public static IReadOnlyList<SampleGap> Gaps(IReadOnlyList<long> sorted, double limit = RatioLimit)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        var n = sorted.Count;
        var edge = Math.Max(5, n / 50);
        var found = new List<SampleGap>();
        for (var i = edge; i <= n - edge; i++)
        {
            var width = sorted[i] - sorted[i - 1];
            if (width < 2) continue;
            var lo = Math.Max(0, i - Window);
            var hi = Math.Min(n - 1, i + Window - 1);
            var steps = hi - lo - 1;                                   // the steps in the window, the gap's own left out
            var others = (sorted[hi] - sorted[lo]) - width;
            var typical = Math.Max(1.0, (double)others / steps);
            var ratio = width / typical;
            if (ratio > limit) found.Add(new SampleGap(sorted[i - 1], sorted[i], i, n - i, ratio));
        }
        return found.OrderByDescending(g => g.Ratio).ThenBy(g => g.Low).ToList();
    }
}
