using System.Globalization;
using System.Numerics;
using System.Text;

namespace EngineCommon.Balance;

/// <summary>
/// What a pilot saw and what it set: per stage the runs started, the checkpoints that reached the level and the work spent (pooled over the pilot's replications), the efforts chosen from them, and the budget they share. The pilot's runs and work are
/// part of the report's totals.
/// </summary>
public sealed record PilotPlan(int PilotEffort, int PilotReplications, IReadOnlyList<long> Runs, IReadOnlyList<long> Hits, IReadOnlyList<long> Work, IReadOnlyList<int> Efforts, int Budget)
{
    public long TotalRuns => Runs.Sum();

    public long TotalWork => Work.Sum();

    /// <summary>The line the report carries for the pilot (no line feed): what was run, what it saw per stage, the budget and the rule.</summary>
    public string Describe()
    {
        var text = new StringBuilder("rare-auto pilot-effort ");
        text.Append(PilotEffort.ToString(CultureInfo.InvariantCulture)).Append(" pilot-replications ").Append(PilotReplications.ToString(CultureInfo.InvariantCulture))
            .Append(" pilot-runs ").Append(TotalRuns.ToString(CultureInfo.InvariantCulture));
        if (TotalWork > 0) text.Append(" pilot-work-steps ").Append(TotalWork.ToString(CultureInfo.InvariantCulture));
        text.Append(" pilot-hits ").Append(string.Join(',', Hits.Select((h, i) => h.ToString(CultureInfo.InvariantCulture) + "/" + Runs[i].ToString(CultureInfo.InvariantCulture))))
            .Append(" budget ").Append(Budget.ToString(CultureInfo.InvariantCulture)).Append(" efforts ").Append(string.Join(',', Efforts.Select(e => e.ToString(CultureInfo.InvariantCulture))))
            .Append(" rule effort-k ~ sqrt((1-p_k)/(p_k c_k)) with p_k = (hits_k+1/2)/(runs_k+1) and c_k = work per run of stage k (1 when the model counts none), scaled to the budget, kept to ")
            .Append(AutoEfforts.MinEffort.ToString(CultureInfo.InvariantCulture)).Append("..").Append(BalanceCodes.MaxEffort.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }
}

/// <summary>
/// The automatic choice of the runs at each stage of a fixed-effort splitting estimate, from a pilot. Integers only and a pure function of the pilot's counts.
/// <para>
/// Rule. For stage fractions p_k estimated independently with N_k runs each, the relative variance of their product is about the sum of (1 - p_k) / (p_k N_k) (the delta method; Garvels 2000 and L'Ecuyer, Le Gland, Lezaud and Tuffin 2009 give
/// this form for fixed-effort splitting when the stage's entry states are plentiful). Minimising that sum for a total cost sum of c_k N_k, with c_k the work of a run of stage k, gives N_k proportional to sqrt((1 - p_k) / (p_k c_k)) (Lagrange; the
/// same square-root law as the optimal allocation of a sample over strata, Neyman 1934). The pilot's pooled counts give p_k = (hits_k + 1/2) / (runs_k + 1) (so a stage with no hit still has a finite fraction) and c_k = work_k / runs_k (1 when the
/// model counts no work). The budget is the total number of runs of the main estimate (stages x the effort asked for); each N_k is the budget's share rounded half up and kept within <see cref="MinEffort"/> and the cap of a stage, so the
/// total is about, not exactly, the budget. A stage that no pilot replication reached has no count and the pilot is refused (<see cref="BalanceCodes.AutoPilotEmpty"/>) rather than a number guessed.
/// </para>
/// <para>
/// Limit. The formula treats the stages as independent; in a fixed-effort procedure a later stage restarts from the few checkpoints of the one before, which adds variance the formula does not see. Whether the rule beats efforts a person chose is
/// measured, not assumed (docs/WORLD_EVENTS_ENGINE.md, section 16).
/// </para>
/// </summary>
public static class AutoEfforts
{
    /// <summary>The fewest runs the rule gives a stage (when the budget allows it).</summary>
    public const int MinEffort = 10;

    private const long Scale = 1_000_000;

    /// <summary>Pools the <paramref name="pilot"/> replications (run at <paramref name="pilotEffort"/> runs a stage) and sets the efforts for <paramref name="budget"/> runs over <paramref name="stages"/> stages.</summary>
    public static PilotPlan Plan(IReadOnlyList<SplitReplication> pilot, int pilotEffort, int stages, int budget)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        var runs = new long[stages];
        var hits = new long[stages];
        var work = new long[stages];
        foreach (var replication in pilot)
            for (var stage = 0; stage < stages; stage++)
            {
                if (stage > 0 && replication.Hits[stage - 1] == 0) break;          // the replication ended at the stage before: this stage never started
                runs[stage] += pilotEffort;
                hits[stage] += replication.Hits[stage];
                work[stage] += replication.StageWork?[stage] ?? 0;
            }
        for (var stage = 0; stage < stages; stage++)
            if (runs[stage] == 0)
                throw new EngineRefusedException(BalanceCodes.AutoPilotEmpty, "no pilot replication reached stage " + (stage + 1).ToString(CultureInfo.InvariantCulture) + " of " + stages.ToString(CultureInfo.InvariantCulture) + ", so its fraction is not known; raise --pilot-effort or --pilot-replications, or give --efforts explicitly");
        return new PilotPlan(pilotEffort, pilot.Count, runs, hits, work, Allocate(runs, hits, work, budget), budget);
    }

    /// <summary>The efforts of the rule for pooled per-stage counts.</summary>
    public static IReadOnlyList<int> Allocate(IReadOnlyList<long> runs, IReadOnlyList<long> hits, IReadOnlyList<long> work, int budget)
    {
        var stages = runs.Count;
        var weights = new BigInteger[stages];
        BigInteger total = 0;
        for (var stage = 0; stage < stages; stage++)
        {
            // p = a / b with a = 2 hits + 1 and b = 2 runs + 2; c = cost / runs. weight^2 = (1 - p) / (p c) = (b - a) runs / (a cost); scaled by Scale^2 before the integer root.
            BigInteger a = 2 * hits[stage] + 1;
            BigInteger b = 2 * runs[stage] + 2;
            BigInteger cost = work[stage] > 0 ? work[stage] : runs[stage];
            weights[stage] = IntegerRoot.CeilingSqrt((b - a) * runs[stage] * Scale * Scale / (a * cost));
            total += weights[stage];
        }
        var lower = Math.Min(MinEffort, Math.Max(1, budget / stages));
        var efforts = new int[stages];
        for (var stage = 0; stage < stages; stage++)
        {
            var share = (2 * budget * weights[stage] + total) / (2 * total);
            efforts[stage] = (int)BigInteger.Min(BalanceCodes.MaxEffort, BigInteger.Max(lower, share));
        }
        return efforts;
    }
}
