using System.Globalization;
using System.Numerics;
using System.Text;

namespace EngineCommon.Balance;

/// <summary>One independent run of the whole splitting procedure: per stage how many of the <c>effort</c> runs reached the level, how many runs were started, and the product estimate in parts per billion.</summary>
public sealed record SplitReplication(int Index, IReadOnlyList<int> Hits, int Runs, long Ppb, long Work = 0, IReadOnlyList<long>? StageWork = null)
{
    /// <summary>True when a stage had no hit, so no later stage could run and the estimate is 0.</summary>
    public bool Extinct => Hits.Any(h => h == 0);
}

/// <summary>The replications of one estimate, and the pilot that chose its efforts when it had one (see <see cref="AutoEfforts"/>).</summary>
public sealed record SplitReport(int Stages, int Effort, IReadOnlyList<SplitReplication> Replications, IReadOnlyList<int>? Efforts = null, PilotPlan? Pilot = null)
{
    /// <summary>The runs started at <paramref name="stage"/> (1-based): the stage's own effort when the report has one per stage, otherwise the one effort.</summary>
    public int EffortOf(int stage) => Efforts is null ? Effort : Efforts[stage - 1];

    /// <summary>True when every stage started the same number of runs.</summary>
    public bool Uniform => Efforts is null || Efforts.All(e => e == Efforts[0]);
}

/// <summary>
/// Fixed-effort multilevel splitting (Garvels 2000; Glasserman, Heidelberger, Shahabuddin and Zajic 1999; L'Ecuyer, Le Gland, Lezaud and Tuffin 2009), integer only. The rare event is cut into nested levels. Stage 1 starts
/// <c>effort</c> fresh runs and keeps the checkpoint of each that reached level 1. Stage k starts <c>effort</c> runs from those checkpoints (checkpoint i modulo the number of hits, so each is used about equally), each under a new derived
/// seed, and keeps the checkpoints that reached level k. The estimate of the rare event is the product of the stage fractions: hits_1/effort x hits_2/effort x ... A stage with no hit ends the replication with the estimate 0 (it is
/// counted as extinct, not hidden). The whole procedure is run <c>replications</c> times from different derived seeds; the summary (<see cref="SplitSummary"/>) is taken over the replications, so its uncertainty comes from observed
/// repetition and not from a variance formula. Nothing is parallel and every seed is a pure function of the root seed, the replication, the stage and the run number, so the report is a pure function of its inputs.
/// </summary>
public static class FixedEffortSplitting
{
    private const long Billion = 1_000_000_000;

    /// <summary>The seed of run <paramref name="run"/> of <paramref name="stage"/> in <paramref name="replication"/>: a roll of the root seed in the engines' shared counter-based generator, kept below 2^62 so every engine accepts it.</summary>
    public static long DerivedSeed(long rootSeed, int replication, int stage, int run) =>
        (long)(DeterministicRoll.Next((ulong)rootSeed, "split:" + replication.ToString(CultureInfo.InvariantCulture) + ":" + stage.ToString(CultureInfo.InvariantCulture), run) >> 2);

    public static SplitReport Run<TState>(ISplittingModel<TState> model, long rootSeed, int effort, int replications) where TState : class
    {
        ArgumentNullException.ThrowIfNull(model);
        if (effort < 1 || effort > BalanceCodes.MaxEffort) throw new EngineRefusedException(BalanceCodes.EffortRange, "the effort per stage is 1 to " + BalanceCodes.MaxEffort + ", not " + effort);
        if (replications < 1 || replications > BalanceCodes.MaxReplications) throw new EngineRefusedException(BalanceCodes.ReplicationsRange, "the replications are 1 to " + BalanceCodes.MaxReplications + ", not " + replications);
        if (model.Stages < 1) throw new EngineRefusedException(BalanceCodes.RareTarget, "the rare event has no level to reach");
        return new SplitReport(model.Stages, effort, Replicate(model, rootSeed, Enumerable.Repeat(effort, model.Stages).ToArray(), replications));
    }

    /// <summary>
    /// The same procedure with its own number of runs at each stage (<paramref name="efforts"/>, one per stage): the cheap early stage can start many runs and the later ones, which restart from few checkpoints, fewer. The estimate is the
    /// product of hits_k / effort_k, still unbiased (each stage fraction is estimated on its own), and the seeds of a stage are those of the uniform procedure, so equal efforts give exactly the uniform report's numbers.
    /// </summary>
    public static SplitReport Run<TState>(ISplittingModel<TState> model, long rootSeed, IReadOnlyList<int> efforts, int replications) where TState : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(efforts);
        foreach (var effort in efforts)
            if (effort < 1 || effort > BalanceCodes.MaxEffort) throw new EngineRefusedException(BalanceCodes.EffortRange, "the effort of a stage is 1 to " + BalanceCodes.MaxEffort + ", not " + effort);
        if (replications < 1 || replications > BalanceCodes.MaxReplications) throw new EngineRefusedException(BalanceCodes.ReplicationsRange, "the replications are 1 to " + BalanceCodes.MaxReplications + ", not " + replications);
        if (model.Stages < 1) throw new EngineRefusedException(BalanceCodes.RareTarget, "the rare event has no level to reach");
        if (efforts.Count != model.Stages) throw new EngineRefusedException(BalanceCodes.EffortsCount, "the rare event has " + model.Stages + " stages and " + efforts.Count + " efforts were given; give one per stage");
        var copy = efforts.ToArray();
        return new SplitReport(model.Stages, copy[0], Replicate(model, rootSeed, copy, replications), copy);
    }

    /// <summary>
    /// The same procedure with the efforts chosen from a pilot (<see cref="AutoEfforts"/>): <paramref name="pilotReplications"/> replications at <paramref name="pilotEffort"/> runs a stage, under seeds that are a roll of the root seed in their own stream (so
    /// none is a seed of the main run), set the number of runs of each stage from the stage fractions and costs they saw; the main run then spends about <paramref name="effort"/> runs a stage on average. The pilot's runs and work are in the report and in its totals.
    /// </summary>
    public static SplitReport RunAuto<TState>(ISplittingModel<TState> model, long rootSeed, int effort, int replications, int pilotEffort, int pilotReplications) where TState : class
    {
        ArgumentNullException.ThrowIfNull(model);
        if (effort < 1 || effort > BalanceCodes.MaxEffort) throw new EngineRefusedException(BalanceCodes.EffortRange, "the effort per stage is 1 to " + BalanceCodes.MaxEffort + ", not " + effort);
        if (pilotEffort < 1 || pilotEffort > BalanceCodes.MaxEffort) throw new EngineRefusedException(BalanceCodes.EffortRange, "the pilot effort per stage is 1 to " + BalanceCodes.MaxEffort + ", not " + pilotEffort);
        if (replications < 1 || replications > BalanceCodes.MaxReplications) throw new EngineRefusedException(BalanceCodes.ReplicationsRange, "the replications are 1 to " + BalanceCodes.MaxReplications + ", not " + replications);
        if (pilotReplications < 1 || pilotReplications > BalanceCodes.MaxReplications) throw new EngineRefusedException(BalanceCodes.ReplicationsRange, "the pilot replications are 1 to " + BalanceCodes.MaxReplications + ", not " + pilotReplications);
        if (model.Stages < 1) throw new EngineRefusedException(BalanceCodes.RareTarget, "the rare event has no level to reach");
        var pilotSeed = (long)(DeterministicRoll.Next((ulong)rootSeed, "pilot", 0) >> 2);
        var pilot = Replicate(model, pilotSeed, Enumerable.Repeat(pilotEffort, model.Stages).ToArray(), pilotReplications);
        var plan = AutoEfforts.Plan(pilot, pilotEffort, model.Stages, checked(effort * model.Stages));
        var efforts = plan.Efforts.ToArray();
        return new SplitReport(model.Stages, efforts[0], Replicate(model, rootSeed, efforts, replications), efforts, plan);
    }

    private static List<SplitReplication> Replicate<TState>(ISplittingModel<TState> model, long rootSeed, int[] efforts, int replications) where TState : class
    {
        var done = new List<SplitReplication>(replications);
        for (var replication = 0; replication < replications; replication++)
        {
            var hits = new int[model.Stages];
            var runs = 0;
            long work = 0;
            var stageWork = new long[model.Stages];
            var entries = new List<TState>();
            BigInteger numerator = 1;
            BigInteger denominator = 1;
            for (var stage = 1; stage <= model.Stages; stage++)
            {
                var reached = new List<TState>();
                var effort = efforts[stage - 1];
                for (var run = 0; run < effort; run++)
                {
                    var seed = DerivedSeed(rootSeed, replication, stage, run);
                    var outcome = stage == 1 ? model.First(seed) : model.Next(entries[run % entries.Count], stage, seed);
                    runs++;
                    work += outcome.Work;
                    stageWork[stage - 1] += outcome.Work;
                    if (outcome.Checkpoint is { } checkpoint) reached.Add(checkpoint);
                }
                hits[stage - 1] = reached.Count;
                numerator *= reached.Count;
                denominator *= effort;
                if (reached.Count == 0) break;
                entries = reached;
            }
            done.Add(new SplitReplication(replication + 1, hits, runs, (long)(numerator * Billion / denominator), work, stageWork));
        }
        return done;
    }

    /// <summary>The report text: the heading the engine gives, a line per replication when asked for, the summary lines and a digest of everything before it. Lines end with a line feed.</summary>
    public static string Render(string heading, string target, SplitReport report, bool rows) =>
        Compose(heading, target, report, rows, "", (replication, effort) =>
            "replication " + replication.Index.ToString(CultureInfo.InvariantCulture) + " hits "
            + string.Join(',', replication.Hits.Select((h, i) => h.ToString(CultureInfo.InvariantCulture) + "/" + effort(i + 1).ToString(CultureInfo.InvariantCulture)))
            + " runs " + replication.Runs.ToString(CultureInfo.InvariantCulture) + (replication.Work > 0 ? " work-steps " + replication.Work.ToString(CultureInfo.InvariantCulture) : "") + " estimate-ppb " + replication.Ppb.ToString(CultureInfo.InvariantCulture) + "\n");

    /// <summary>The one layout of a rare-estimate report, shared by the splitting and the conditional estimator: heading, the replication lines (<paramref name="row"/>, given the effort of a stage), the summary line with <paramref name="afterTarget"/> after the target, the note and the digest.</summary>
    internal static string Compose(string heading, string target, SplitReport report, bool rows, string afterTarget, Func<SplitReplication, Func<int, int>, string> row)
    {
        var summary = SplitSummary.Of(report);
        var text = new StringBuilder();
        text.Append(heading).Append('\n');
        if (rows)
            foreach (var replication in report.Replications)
                text.Append(row(replication, report.EffortOf));
        text.Append("rare-estimate target ").Append(target).Append(afterTarget).Append(" stages ").Append(report.Stages.ToString(CultureInfo.InvariantCulture)).Append(report.Uniform && report.Pilot is null ? " effort " + report.Effort.ToString(CultureInfo.InvariantCulture) : " efforts " + string.Join(',', report.Efforts!.Select(e => e.ToString(CultureInfo.InvariantCulture))))
            .Append(" replications ").Append(report.Replications.Count.ToString(CultureInfo.InvariantCulture)).Append(" runs ").Append(summary.Runs.ToString(CultureInfo.InvariantCulture))
            .Append(" mean-ppb ").Append(summary.MeanPpb.ToString(CultureInfo.InvariantCulture)).Append(" se-ppb ").Append(summary.SePpb.ToString(CultureInfo.InvariantCulture))
            .Append(" band-ppb ").Append(summary.LowPpb.ToString(CultureInfo.InvariantCulture)).Append("..").Append(summary.HighPpb.ToString(CultureInfo.InvariantCulture))
            .Append(" median-ppb ").Append(summary.MedianPpb.ToString(CultureInfo.InvariantCulture))
            .Append(" median95-ppb ").Append(summary.Median95 is { } median ? median.Low.ToString(CultureInfo.InvariantCulture) + ".." + median.High.ToString(CultureInfo.InvariantCulture) : "n/a")
            .Append(" min-ppb ").Append(summary.MinPpb.ToString(CultureInfo.InvariantCulture)).Append(" max-ppb ").Append(summary.MaxPpb.ToString(CultureInfo.InvariantCulture))
            .Append(" extinct ").Append(summary.Extinct.ToString(CultureInfo.InvariantCulture));
        var work = report.Replications.Sum(r => r.Work) + (report.Pilot?.TotalWork ?? 0);
        if (work > 0) text.Append(" work-steps ").Append(work.ToString(CultureInfo.InvariantCulture));       // the engine's own count of the steps it lived, for a model that keeps one
        text.Append('\n');
        text.Append("rare-note band = mean +- 2 se, a normal approximation over the replications (about 95%, approximate because the estimates are skewed); each replication is rounded down to a whole ppb and se up; median95 is the distribution-free 95% interval for the median of the replications\n");
        if (report.Pilot is { } pilot) text.Append(pilot.Describe()).Append('\n');
        var digest = Digest.Of(text.ToString());
        text.Append("rare-digest ").Append(digest).Append('\n');
        return text.ToString();
    }
}
