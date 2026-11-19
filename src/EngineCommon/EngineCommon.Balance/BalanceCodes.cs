namespace EngineCommon.Balance;

/// <summary>The codes a bad balance command line is refused with. Each is raised by exactly one rule in <see cref="SweepOptions"/> or <see cref="SeedSweep"/>.</summary>
public static class BalanceCodes
{
    public const string SeedsMissing = "BAL_SEEDS_MISSING";
    public const string SeedsRange = "BAL_SEEDS_RANGE";
    public const string FromSeedRange = "BAL_FROM_SEED_RANGE";
    public const string DaysRange = "BAL_DAYS_RANGE";
    public const string HorizonMissing = "BAL_HORIZON_MISSING";
    public const string HorizonTwice = "BAL_HORIZON_TWICE";
    public const string OptionUnknown = "BAL_OPTION_UNKNOWN";
    public const string OptionTwice = "BAL_OPTION_TWICE";
    public const string OptionValue = "BAL_OPTION_VALUE";
    public const string MetricUnknown = "BAL_METRIC_UNKNOWN";
    public const string MetricsDiffer = "BAL_METRICS_DIFFER";
    public const string MetricName = "BAL_METRIC_NAME";
    public const string DocumentSeed = "BAL_DOCUMENT_SEED";
    public const string RareMissing = "BAL_RARE_MISSING";
    public const string RareTarget = "BAL_RARE_TARGET";
    public const string EffortRange = "BAL_EFFORT_RANGE";
    public const string ReplicationsRange = "BAL_REPLICATIONS_RANGE";
    public const string OnsetAmbiguous = "BAL_ONSET_AMBIGUOUS";
    public const string EffortsCount = "BAL_EFFORTS_COUNT";
    public const string EffortsTwice = "BAL_EFFORTS_TWICE";
    public const string PredrawHazard = "BAL_PREDRAW_HAZARD";
    public const string PredrawLost = "BAL_PREDRAW_LOST";
    public const string ConditionalMix = "BAL_CONDITIONAL_MIX";
    public const string PilotWithoutAuto = "BAL_PILOT_WITHOUT_AUTO";
    public const string AutoPilotEmpty = "BAL_AUTO_PILOT_EMPTY";

    /// <summary>The most seeds one sweep runs.</summary>
    public const int MaxSeeds = 100_000;

    /// <summary>The most runs one stage of a splitting replication starts (each hit keeps a checkpoint in memory until the next stage has used it).</summary>
    public const int MaxEffort = 5_000;

    /// <summary>The fewest replications of a splitting estimate that can state an interval: six are the smallest sample with a distribution-free 95% interval for the median.</summary>
    public const int MinReplications = 6;

    public const int MaxReplications = 1_000;

    /// <summary>The largest seed any engine document accepts (the economy and relationship readers cap the seed here; the world-events reader allows more).</summary>
    public const long MaxSeed = 9_000_000_000_000_000;
}
