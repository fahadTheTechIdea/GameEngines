using System.Globalization;
using System.Numerics;

namespace EngineCommon.Balance;

/// <summary>What one conditional run produced: the weight of its seed in units of 10^-12 of a probability (an exact whole number), whether the run met any chance at all, and how many engine steps it lived.</summary>
public sealed record ConditionalOutcome(BigInteger Weight, bool Informative, long Work);

/// <summary>An engine's side of a conditional estimate: lives one seed and returns the weight (see <see cref="ConditionalEstimator"/>).</summary>
public interface IConditionalModel
{
    ConditionalOutcome Run(long seed);
}

/// <summary>
/// The conditional (Rao-Blackwellised) estimate of a rare event that is decided by one draw at each of a run's opportunities. A run is lived by the engine as it is, except that it stops at the first opportunity whose draw reaches the
/// event. At each opportunity t before that, the engine's own chance that the event happens there is the product of two numbers it knows exactly: the chance c_t that the opportunity arises (the hazard's onset chance in that state) and the
/// chance q that the draw taken then reaches the event (a tail of the ladder). The run's weight is the sum of c_t q over the opportunities up to and including the one that stopped it. Its mean is the probability of the event (Wald's identity
/// for the compensator of a point process stopped at its first qualifying point; the weight at an opportunity depends only on what happened before it), whatever the earlier draws did to the later chances, so excitation and every other
/// modifier are in the estimate as the engine states them. The estimate of a replication is the mean weight of its runs, rounded down to a whole ppb; the replications go through <see cref="SplitSummary"/>, so the report has the numbers
/// of the splitting report. Every seed is a pure function of the root seed, the replication and the run number, nothing is parallel, and all arithmetic is in whole numbers.
/// </summary>
public static class ConditionalEstimator
{
    private const long Billion = 1_000_000_000;

    /// <summary>One unit of weight is 10^-12 of a probability, so a weight is turned into parts per billion by dividing by this.</summary>
    public const long WeightPerPpb = 1000;

    /// <summary>The seed of run <paramref name="run"/> of <paramref name="replication"/>: the first stage's seed of the splitting procedure, so the same fresh runs are lived.</summary>
    public static long SeedOf(long rootSeed, int replication, int run) => FixedEffortSplitting.DerivedSeed(rootSeed, replication, 1, run);

    /// <summary>
    /// Lives <paramref name="runs"/> seeds in each of <paramref name="replications"/> replications. The report has one stage; a replication's hits are its runs that met a chance (so a replication is called extinct only when its estimate
    /// is a flat zero because nothing in it could happen), and its work is the engine steps it lived.
    /// </summary>
    public static SplitReport Run(IConditionalModel model, long rootSeed, int runs, int replications)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (runs < 1 || runs > BalanceCodes.MaxEffort) throw new EngineRefusedException(BalanceCodes.EffortRange, "the runs per replication are 1 to " + BalanceCodes.MaxEffort + ", not " + runs);
        if (replications < 1 || replications > BalanceCodes.MaxReplications) throw new EngineRefusedException(BalanceCodes.ReplicationsRange, "the replications are 1 to " + BalanceCodes.MaxReplications + ", not " + replications);
        var done = new List<SplitReplication>(replications);
        for (var replication = 0; replication < replications; replication++)
        {
            BigInteger weight = 0;
            long work = 0;
            var informative = 0;
            for (var run = 0; run < runs; run++)
            {
                var outcome = model.Run(SeedOf(rootSeed, replication, run));
                weight += outcome.Weight;
                work += outcome.Work;
                if (outcome.Informative) informative++;
            }
            done.Add(new SplitReplication(replication + 1, [informative], runs, (long)(weight / (WeightPerPpb * (BigInteger)runs)), work));
        }
        return new SplitReport(1, runs, done);
    }

    /// <summary>The report text, in the shape of <see cref="FixedEffortSplitting.Render"/> with the word <c>conditional</c> after the target, so the two compare line by line.</summary>
    public static string Render(string heading, string target, SplitReport report, bool rows) =>
        FixedEffortSplitting.Compose(heading, target, report, rows, " estimator conditional",
            (replication, effort) => "replication " + replication.Index.ToString(CultureInfo.InvariantCulture) + " informative " + replication.Hits[0].ToString(CultureInfo.InvariantCulture) + "/" + effort(1).ToString(CultureInfo.InvariantCulture)
                + " runs " + replication.Runs.ToString(CultureInfo.InvariantCulture) + " work-steps " + replication.Work.ToString(CultureInfo.InvariantCulture) + " estimate-ppb " + replication.Ppb.ToString(CultureInfo.InvariantCulture) + "\n");
}
