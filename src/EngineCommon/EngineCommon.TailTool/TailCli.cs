using System.Globalization;
using System.Text;

namespace EngineCommon.TailTool;

/// <summary>
/// <c>propose &lt;sweep.txt&gt; &lt;metric&gt; --levels a,b,c [--threshold-percent P] [--min-exceedances K]</c>: reads the <c>sample</c> rows of a balance sweep (written with <c>--rows</c>) and proposes, for each severity level, the integer
/// <c>tailPpm</c> a hazard ladder rung carries (the chance an onset is at least that bad, in parts per million). The first level is the floor rung and is 1000000 by the document rule. The list printed last is what to paste into the document; the
/// lines above it are the inputs and the fit. Exit codes: 0 proposed, 1 refused (the code is printed), 2 wrong command line or unreadable file.
/// </summary>
public static class TailCli
{
    public const string Usage = "usage: EngineCommon.TailTool propose <sweep.txt> <metric> --levels <a,b,c,...> [--threshold-percent <1-99>] [--min-exceedances <k>]\n" +
                                "       EngineCommon.TailTool modality <sweep.txt> <metric> [--threshold-percent <1-99>]   (what the gap, dip and Silverman tests say about the sample; decides nothing)\n" +
                                "exit: 0 proposed | 1 refused (the code is printed) | 2 usage or file problem";

    private const int Ppm = 1_000_000;

    /// <summary>The p-value at or under which the dip test of the values above the threshold refuses the fit: 0.01, under the usual 0.05, because a refusal withholds a proposal: the calibration (docs/WORLD_EVENTS_ENGINE.md section 16) found 4 refusals in 3,300 simulated unimodal tails at it, all from the uniform family, whose tail is exactly the test's null (1.3% of 300), and none in the other ten families.</summary>
    public const double DipRefusalLevel = 0.01;

    /// <summary>The fewest values above the threshold the dip test is run on.</summary>
    public const int DipMinimumValues = 20;

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 3 || (args[0] != "propose" && args[0] != "modality")) return Wrong(error, Usage);
        string text;
        try { text = File.ReadAllText(args[1]); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        catch (UnauthorizedAccessException exception) { return Wrong(error, "file problem: " + exception.Message); }
        try
        {
            if (args[0] == "modality")
            {
                output.Write(ModalityReport.Of(TailSample.Read(text, args[2]), ModalityPercent(args.Skip(3).ToList())));
                return 0;
            }
            var (levels, percent, minimum) = Options(args.Skip(3).ToList());
            output.Write(Propose(TailSample.Read(text, args[2]), levels, percent, minimum));
            return 0;
        }
        catch (TailRefusedException refusal)
        {
            output.WriteLine("refused " + refusal.Code + ": " + refusal.Message);
            return 1;
        }
    }

    private static int Wrong(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static (long[] Levels, int Percent, int Minimum) Options(IReadOnlyList<string> args)
    {
        long[]? levels = null;
        var percent = 90;
        var minimum = 30;
        for (var i = 0; i < args.Count; i++)
        {
            if (i + 1 >= args.Count) throw new TailRefusedException(TailCodes.Option, args[i] + " needs a value");
            var value = args[i + 1];
            switch (args[i])
            {
                case "--levels":
                    if (levels is not null) throw new TailRefusedException(TailCodes.Option, "--levels is given once");
                    levels = value.Split(',').Select(v => long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var level) ? level : throw new TailRefusedException(TailCodes.Levels, "a level is a whole number, not '" + v + "'")).ToArray();
                    break;
                case "--threshold-percent":
                    percent = Whole(args[i], value, 1, 99);
                    break;
                case "--min-exceedances":
                    minimum = Whole(args[i], value, 10, 1_000_000);
                    break;
                default:
                    throw new TailRefusedException(TailCodes.Option, "'" + args[i] + "' is not an option (--levels, --threshold-percent, --min-exceedances)");
            }
            i++;
        }
        if (levels is null) throw new TailRefusedException(TailCodes.Levels, "--levels is needed");
        if (levels.Length < 2) throw new TailRefusedException(TailCodes.Levels, "a ladder has at least two rungs");
        for (var i = 1; i < levels.Length; i++)
            if (levels[i] <= levels[i - 1]) throw new TailRefusedException(TailCodes.Levels, "the levels rise: " + levels[i] + " does not exceed " + levels[i - 1]);
        return (levels, percent, minimum);
    }

    /// <summary>The percentile of the <c>modality</c> command: <c>--threshold-percent</c> (default 90) and nothing else.</summary>
    private static int ModalityPercent(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return 90;
        if (args.Count != 2 || args[0] != "--threshold-percent") throw new TailRefusedException(TailCodes.Option, "modality takes only --threshold-percent <1-99>, not '" + string.Join(' ', args) + "'");
        return Whole(args[0], args[1], 1, 99);
    }

    private static int Whole(string option, string text, int min, int max) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max ? number : throw new TailRefusedException(TailCodes.Option, option + " is a whole number from " + min + " to " + max + ", not '" + text + "'");

    /// <summary>The proposal text for a sample and its levels. Public so a test can drive it without files.</summary>
    public static string Propose(TailSample sample, IReadOnlyList<long> levels, int percent, int minimum)
    {
        var sorted = sample.Values.OrderBy(v => v).ToArray();
        var fit = PeaksOverThreshold.Fit(sorted, percent, minimum);
        var text = new StringBuilder();
        text.Append("tail-tool propose metric ").Append(sample.Metric).Append(" samples ").Append(sorted.Length).Append(" min ").Append(sorted[0]).Append(" max ").Append(sorted[^1]).Append(" sample-digest ").Append(sample.Digest).Append(sample.PerOnset ? " per-onset" : "").Append('\n');
        text.Append("threshold-percent ").Append(percent).Append(" threshold ").Append(fit.Threshold).Append(" exceedances ").Append(fit.Exceedances).Append(" min-exceedances ").Append(minimum).Append('\n');
        text.Append("gpd-pwm xi ").Append(fit.Xi.ToString("F6", CultureInfo.InvariantCulture)).Append(" sigma ").Append(fit.Sigma.ToString("F6", CultureInfo.InvariantCulture))
            .Append(" (floating point, offline only; tail-ppm = floor(1000000 * p + 0.5), p = count(x >= level) / n at or under the threshold, else (k / n) * (1 + xi t / sigma)^(-1/xi) with t = level - 0.5 - threshold)\n");
        if (fit.Xi < -0.5 || fit.Xi > 0.5)
            text.Append("warning xi-outside-minus-0.5-to-0.5: the estimator is validated for shapes inside that range (Hosking and Wallis 1987); a shape this far out means a bounded or very heavy tail, so read the empirical counts and run more seeds before trusting levels above the threshold\n");
        foreach (var warning in ShapeChecks(sorted, fit, levels)) text.Append(warning).Append((char)10);
        var rungs = new List<long>();
        for (var i = 0; i < levels.Count; i++)
        {
            long proposed;
            var notes = "";
            if (i == 0)
            {
                proposed = Ppm;
                notes = " floor-rung";
            }
            else
            {
                var p = fit.AtLeast(sorted, levels[i]);
                proposed = (long)Math.Floor(p * Ppm + 0.5);
                if (proposed < 1)
                {
                    proposed = 1;
                    notes += " raised-to-1";
                }
                if (proposed >= rungs[i - 1])
                {
                    notes += " bumped-from-" + proposed;
                    proposed = rungs[i - 1] - 1;
                }
                if (proposed < 1) throw new TailRefusedException(TailCodes.CannotFall, "level " + levels[i] + " cannot be given a tailPpm below the rung before it (" + rungs[i - 1] + "); use fewer or further apart levels");
            }
            rungs.Add(proposed);
            text.Append("level ").Append(levels[i]).Append(" tail-ppm ").Append(proposed).Append(" source ").Append(i == 0 ? "rule" : levels[i] <= fit.Threshold ? "empirical" : "gpd").Append(notes).Append('\n');
        }
        text.Append("tailPpm ").Append(string.Join(",", rungs)).Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// What the sample's shape allows when some level lies above the threshold and is read from the fitted tail. A gap (<see cref="ModalityCheck"/>) between values above the threshold means the fit would run across two clusters: refused
    /// (<see cref="TailCodes.Multimodal"/>); so does Hartigan's dip test (<see cref="DipTest"/>) when it finds the values above the threshold bimodal at <see cref="DipRefusalLevel"/>, which catches clusters the gap test's ratio limit does not. A gap with the threshold at its lower edge means the fit describes only the cluster above it: a warning. A tail the sample cannot call unbounded (<see cref="BoundCheck"/>) with a level above the
    /// largest value ever seen is refused (<see cref="TailCodes.Bounded"/>); with every level inside the sample it is a warning. Levels at or under the threshold are counted from the sample and are not checked. Returns the warning lines.
    /// </summary>
    private static IReadOnlyList<string> ShapeChecks(IReadOnlyList<long> sorted, PeaksOverThreshold fit, IReadOnlyList<long> levels)
    {
        var warnings = new List<string>();
        var beyond = levels.Where(l => l > fit.Threshold).ToArray();
        if (beyond.Length == 0) return warnings;
        var gaps = ModalityCheck.Gaps(sorted);
        var across = gaps.FirstOrDefault(g => g.Low > fit.Threshold);
        if (across is not null)
            throw new TailRefusedException(TailCodes.Multimodal, "the values above the threshold " + fit.Threshold + " fall in separate clusters: none lies between " + across.Low + " and " + across.High + " (" + across.Below + " values at or under the first, " + across.Above +
                " at or over the second; the gap is " + across.Ratio.ToString("F0", CultureInfo.InvariantCulture) + " times the spacing around it), and a generalized Pareto fitted across them describes neither. Set --threshold-percent at or above the cluster you want to extend (the threshold must be " + across.High + " or more), or read the counts of the sample");
        var upper = sorted.Where(v => v > fit.Threshold).ToArray();
        if (upper.Length >= DipMinimumValues && upper[0] != upper[^1])
        {
            var dip = DipTest.Test(upper);
            if (dip.PValue <= DipRefusalLevel)
                throw new TailRefusedException(TailCodes.Multimodal, "Hartigan's dip test finds more than one mode among the " + upper.Length + " values above the threshold " + fit.Threshold + " (dip " + dip.Statistic.Dip.ToString("F5", CultureInfo.InvariantCulture) + ", p " +
                    dip.PValue.ToString("F3", CultureInfo.InvariantCulture) + " against " + dip.Resamples + " simulated uniform samples of that size), and a generalized Pareto fitted across them describes no one cluster. Set --threshold-percent above the cluster you want to extend, or read the counts of the sample");
        }
        var edge = gaps.FirstOrDefault(g => g.Low == fit.Threshold);
        if (edge is not null)
            warnings.Add("warning " + TailCodes.Multimodal + ": the sample has a gap between " + edge.Low + " and " + edge.High + " and the threshold " + fit.Threshold + " is at its lower edge, so the fitted tail describes only the " + edge.Above + " values at or over " + edge.High + "; levels above the threshold are the chance of that cluster, which is a different population from the " + edge.Below + " below");
        var bound = BoundCheck.Of(PeaksOverThreshold.ExcessesOver(sorted, fit.Threshold), fit);
        if (!bound.Bounded) return warnings;
        var end = bound.Endpoint is { } e ? " (the fit ends at " + e.ToString("F0", CultureInfo.InvariantCulture) + ")" : "";
        var past = beyond.Where(l => l > sorted[^1]).ToArray();
        if (past.Length > 0)
            throw new TailRefusedException(TailCodes.Bounded, "the tail looks bounded (" + bound.Negative + " of " + bound.Resamples + " resampled fits have a negative shape" + end + ", the largest value seen is " + sorted[^1] + ") and the level" + (past.Length > 1 ? "s " : " ") + string.Join(", ", past) +
                (past.Length > 1 ? " lie" : " lies") + " above everything the sample holds, so no chance can be read for " + (past.Length > 1 ? "them" : "it") + " from it. Drop the level" + (past.Length > 1 ? "s" : "") + " or take them from the rule the game wants (a rung that never fires is a design choice, not a measurement)");
        warnings.Add("warning " + TailCodes.Bounded + ": the tail looks bounded (" + bound.Negative + " of " + bound.Resamples + " resampled fits have a negative shape" + end + "); levels inside the sample are read from the fit, but nothing can lie past its end");
        return warnings;
    }
}
