using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

// The declarations of a world-events document. All numbers are whole: temperature in hundredths of a degree Celsius, precipitation in tenths of a millimetre, wind in tenths of a metre a second, shares in per mille
// (1,000 = all), probabilities in parts per million (1,000,000 = certain) and times in steps. Every record is immutable and says nothing about a world; the runtime holds that.

public sealed record SeasonDef(int Index, string Id, string Key, int Steps);

/// <summary>A modifier is a number other layers read about a region (how far one sees, how fast one moves, how much work gets done). It starts at <paramref name="Base"/> and is moved by weather, hazards, outbreaks and cards, within its range.</summary>
public sealed record ModifierDef(int Index, string Id, string Key, int Base, int Minimum, int Maximum);

/// <summary>A change to a modifier. <paramref name="Delta"/> is added at full strength; a hazard or an outbreak scales it by its own intensity.</summary>
public sealed record ModifierEffect(int Modifier, int Delta);

public sealed record WeatherDef(int Index, string Id, string Key, int TempOffset, int Precip, int Wind, bool Wet, IReadOnlyList<ModifierEffect> Modifiers);

/// <summary>One zone in one season: the mean temperature, the spread and persistence of its anomalies, the mean wind, the chance (per mille) that the weather stays as it is, and the weights of each weather when it does not.</summary>
public sealed record ClimateDef(int Temp, int Sigma, int Phi, int Wind, int Persistence, IReadOnlyList<int> Frequency);

/// <summary>A zone's climate. <paramref name="Spill"/> (per mille, 0 to 900) is how far the weather of the regions linked to a region tilts which weather it draws when it changes: 0 draws as if alone.</summary>
public sealed record ZoneDef(int Index, string Id, string Key, string Koppen, int Spill, IReadOnlyList<ClimateDef> Seasons);

/// <summary>A regime is a large-scale pattern (a front, a blocking high) that tilts which weather comes: <paramref name="Frequency"/> per season is how often it is chosen when it changes, <paramref name="Bias"/> per weather multiplies the weights.</summary>
public sealed record RegimeDef(int Index, string Id, string Key, int Persistence, IReadOnlyList<int> Frequency, IReadOnlyList<int> Bias);

public sealed record HydrologyDef(int Freezing, int SoilCapacity, int InitialSoil, int EvapPerDegree, int MeltPerDegree, int DrainPermille, int WetDecay, int HeavyPermille, int HeavyFactor);

public sealed record RegionDef(int Index, string Id, string Key, int Zone, string Market, long Population, IReadOnlyList<int> Exposure, IReadOnlyList<int> Immune);

/// <summary>A link joins two regions both ways: hazards spread along links, and an infection in one region adds <paramref name="FlowPpm"/> of its infectious people to the other's force of infection.</summary>
public sealed record LinkDef(int A, int B, int FlowPpm);

public sealed record EffectDef(EffectKind Kind, string Name, int Index, int Amount, int Rung, int Duration);

/// <summary>
/// One rung of a hazard's severity ladder. <paramref name="TailPpm"/> is the chance that an onset is at least this bad (the first rung is 1,000,000 and the tails fall), which is how Gutenberg and Richter
/// state the frequency of earthquakes by size. The hazard rises over <paramref name="OnsetSteps"/>, holds between the two durations, and recovers over <paramref name="RecoverySteps"/>.
/// </summary>
public sealed record RungDef(int Index, string Id, string Key, int TailPpm, int MinDuration, int MaxDuration, int OnsetSteps, int RecoverySteps, int Stress, int EscalatePpm, IReadOnlyList<EffectDef> Effects);

/// <summary>
/// Self-excitation of a hazard (a Hawkes process with an exponential kernel, in its recursive form). Each onset adds <paramref name="JumpPpm"/> to the region's excitation, which falls by <paramref name="DecayPermille"/> of itself every step
/// and never passes <paramref name="CapPpm"/>; the chance of an onset is the rate plus the excitation. The mean number of onsets one onset causes, the branching ratio, is jump x (1000 - decay) / decay in millionths of an onset:
/// <see cref="BranchingPpm"/>. Below one the clusters end (a mean of 1 / (1 - ratio) onsets in all); the reader refuses a hazard at or above it.
/// </summary>
public sealed record ExciteDef(int JumpPpm, int DecayPermille, int CapPpm)
{
    /// <summary>True when the hazard excites itself at all.</summary>
    public bool On => JumpPpm > 0;

    /// <summary>The branching ratio in millionths (1,000,000 is 1.0), rounded down; 0 when the hazard does not excite itself.</summary>
    public long BranchingPpm => On ? (long)JumpPpm * (1000 - DecayPermille) / DecayPermille : 0;

    /// <summary>True when the branching ratio is below one, exactly (no rounding): jump x (1000 - decay) is below 1,000,000 x decay.</summary>
    public bool Stationary => (long)JumpPpm * (1000 - DecayPermille) < 1_000_000L * DecayPermille;
}

public sealed record HazardDef(int Index, string Id, string Key, HazardKind Kind, bool Paced, int BasePpm, ExciteDef Excite, IReadOnlyList<int> Seasonal, Condition? When, int Cooldown, IReadOnlyList<int> Excludes,
    int ReachHops, int Falloff, IReadOnlyList<RungDef> Ladder);

public sealed record DiseaseDef(int Index, string Id, string Key, DiseaseModel Model, int BetaPpm, int Incubation, int Infectious, int Waning, int Fatality, IReadOnlyList<int> Seasonal, int SeedPpm, int SeedCases,
    int OutbreakAt, bool Paced, int Stress, Condition? When, IReadOnlyList<ModifierEffect> Burden, int BurdenAt, IReadOnlyList<EffectDef> OnOutbreak);

/// <summary>A response to a disease. Active, it multiplies transmission, the mixing along links, the chance of death and the time to recover, and moves people from susceptible to immune at <paramref name="Vaccinate"/> per mille a step. <paramref name="AutoAt"/> (per mille prevalence, 0 for never) starts it by itself.</summary>
public sealed record InterventionDef(int Index, string Id, string Key, int Disease, int Beta, int Flow, int Vaccinate, int Treat, int Fatality, int AutoAt);

public sealed record StorytellerDef(int Index, string Id, string Key, int Decay, int PeakAt, int RelaxBelow, int PeakHold, int RestMin, int GapMin, int GapMax, IReadOnlyList<int> Share);

public sealed record DifficultyDef(int Index, string Id, string Key, int Hazard, int Bad, int Disease, int StressGain);

public sealed record ChainDef(int Card, int Delay, int Probability, Condition? When);

public sealed record CardDef(int Index, string Id, string Key, Tier Tier, bool Global, int Weight, Condition? When, int Cooldown, int Group, int Duration, int Intensity, IReadOnlyList<EffectDef> Effects, IReadOnlyList<ChainDef> Chain);

public sealed record LimitsDef(int MaxHazards, int MaxEffects, int MaxAgenda);
