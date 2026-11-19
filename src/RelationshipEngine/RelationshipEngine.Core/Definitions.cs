using ScenarioEngine.Contracts;

namespace RelationshipEngine;

// The declarations of a relationship document, as plain immutable data. Every named thing has an id (stable, language free) and, when a player can see it, a localisation key. Indexes are positions in the document's lists.

/// <summary>An opinion dimension (affinity, trust, attraction...). The base value rests at <paramref name="Baseline"/> and drifts back toward it by <paramref name="DriftPerDay"/> each day.</summary>
public sealed record AxisDef(int Index, string Id, string Key, long Min, long Max, long Baseline, long DriftPerDay);

/// <summary>One point of an urgency curve: at <paramref name="Level"/> the need's urgency is <paramref name="Urgency"/> (0 to 1000).</summary>
public sealed record CurvePoint(long Level, long Urgency);

/// <summary>A change of decay speed while a condition over host facts holds (winter: hunger decays faster).</summary>
public sealed record DecayModifierDef(Condition When, int Permille, IReadOnlyList<string> Facts);

/// <summary>A need: a level 0 to <paramref name="Max"/> that decays <paramref name="DecayPerDay"/> a day. Critical at or below <paramref name="Critical"/>, re-armed at <paramref name="Rearm"/>.</summary>
public sealed record NeedDef(int Index, string Id, string Key, long Max, long Initial, long DecayPerDay, long Critical, long Rearm, IReadOnlyList<CurvePoint> Urgency, IReadOnlyList<DecayModifierDef> DecayModifiers);

/// <summary>While an actor does <paramref name="Activity"/>, the need gains <paramref name="PerDay"/> a day.</summary>
public sealed record SourceDef(string Activity, int Need, long PerDay);

public enum DecayKind { Fixed, Linear, HalfLife }

public sealed record DecayDef(DecayKind Kind, int HalfLife);

/// <summary>How a second instance of the same modifier is treated: <c>Refresh</c> restarts the one instance; <c>Stack</c> keeps up to Max (a further one replaces the oldest); <c>Replace</c> keeps the stronger of old and new; <c>Unique</c> ignores a new one while one lasts.</summary>
public enum StackMode { Refresh, Stack, Replace, Unique }

public sealed record StackDef(StackMode Mode, int Max);

/// <summary>A timed mood modifier (a thought, a moodlet).</summary>
public sealed record MoodletDef(int Index, string Id, string Key, long Magnitude, int Duration, DecayDef Decay, StackDef Stack, IReadOnlyList<string> Tags);

public sealed record MemoryEffect(int Axis, long Magnitude);

/// <summary>How a memory travels by gossip: the listener gets memory <paramref name="Memory"/> at <paramref name="Permille"/> of the teller's scale, with chance <paramref name="Chance"/> per telling, and it may be told on <paramref name="Hops"/> more times.</summary>
public sealed record SpreadDef(int Memory, int Permille, int Chance, int Hops);

/// <summary>An opinion modifier on an edge: it adds <see cref="Effects"/> to the holder's opinion of the subject for <paramref name="Duration"/> steps, decaying by <paramref name="Decay"/>.</summary>
public sealed record MemoryDef(int Index, string Id, string Key, IReadOnlyList<MemoryEffect> Effects, int Duration, DecayDef Decay, StackDef Stack, IReadOnlyList<string> Tags, SpreadDef? Spread);

public sealed record NeedWeight(int Need, long Weight, long Neutral);

/// <summary>An ascending list of named steps over a value, with a margin that must be crossed to go down.</summary>
public sealed record StepDef(string Id, string Key, long From);

public sealed record MoodDef(long Min, long Max, long Base, long Hysteresis, IReadOnlyList<NeedWeight> NeedWeights, IReadOnlyList<StepDef> States);

public enum Sign { Gain, Loss }

public sealed record NeedDecayScale(int Need, int Permille);
public sealed record MoodScale(string Tag, int Permille);
public sealed record AxisScale(int Axis, Sign Sign, int Permille);

/// <summary>What a trait id does here. The trait itself (its id, label and who has it) belongs to the character factory.</summary>
public sealed record TraitDef(string Id, long MoodBase, IReadOnlyList<NeedDecayScale> NeedDecay, IReadOnlyList<MoodScale> MoodScales, IReadOnlyList<AxisScale> AxisScales);

public sealed record LadderDef(int Index, string Id, int Axis, long Hysteresis, IReadOnlyList<StepDef> Steps);

/// <summary>A binary relation. Symmetric: set on both edges. Exclusive: an actor holds it toward one other actor only. It breaks when the axis opinion falls below <paramref name="BreakBelow"/> (when <paramref name="BreakAxis"/> is not -1).</summary>
public sealed record BondDef(int Index, string Id, string Key, bool Symmetric, bool Exclusive, int BreakAxis, long BreakBelow);

public sealed record GiftDef(int Index, string Id, string Key, IReadOnlyList<string> Tags);

public sealed record GossipDef(int TrustAxis, long MinTrust, int MaxTellings, int MaxListeners);

public sealed record LimitsDef(int MaxActors, int MaxEdgesPerActor, int MaxMemoriesPerEdge, int MaxMoodlets);

public sealed record RuleDef(Condition When, int Weight, IReadOnlyList<string> Facts);

public enum EffectKind { Axis, Memory, Moodlet, Need, Bond }

/// <summary>
/// One effect of an interaction. For <c>Axis</c> and <c>Memory</c>, <paramref name="TargetEdge"/> says whose opinion changes (false: the actor's of the target; true: the target's of the actor). For <c>Moodlet</c> and
/// <c>Need</c>, <paramref name="TargetEdge"/> says who it happens to (false: the actor; true: the target). <paramref name="Index"/> is the axis, memory, moodlet, need or bond; <paramref name="Amount"/> the delta (or 1 to set a bond, 0 to clear it).
/// </summary>
public sealed record EffectDef(EffectKind Kind, bool TargetEdge, int Index, long Amount, bool ByTaste);

public sealed record WitnessDef(int Memory, int ScalePermille, int Max);

public sealed record InteractionDef(int Index, string Id, string Key, IReadOnlyList<string> Tags, Condition? Requires, IReadOnlyList<string> RequiresFacts, int Cooldown, bool Gift,
    IReadOnlyList<RuleDef> Desire, IReadOnlyList<RuleDef> Accept, long AcceptAt, int Noise, IReadOnlyList<EffectDef> Effects, IReadOnlyList<EffectDef> RefusedEffects, WitnessDef? Witness);

/// <summary>Communities: label propagation over the edges whose opinion on <paramref name="Axis"/> is above <paramref name="Above"/> (the weight is the excess), at most <paramref name="MaxRounds"/> sweeps.</summary>
public sealed record CommunityDef(bool Enabled, int Axis, long Above, int MaxRounds);

/// <summary>
/// Standing: EigenTrust-style power iteration over the opinion on <paramref name="Axis"/> (weight: the excess over <paramref name="Above"/>), mixing in <paramref name="AlphaPermille"/> of the pre-trust vector (the
/// actors with trait <paramref name="PreTrustTrait"/> share it; empty, or nobody has it: everyone equally), <paramref name="Iterations"/> iterations. A change of at least <paramref name="EventDelta"/> is announced.
/// </summary>
public sealed record StandingDef(bool Enabled, int Axis, long Above, int AlphaPermille, int Iterations, string PreTrustTrait, long EventDelta);

/// <summary>
/// Balance pressure (Heider): an actor whose strong opinion of B (at or above <paramref name="Like"/>, or at or below <paramref name="Dislike"/>) meets B's strong opinion of C is pushed toward the sign of their product
/// as its opinion of C: <paramref name="Memory"/> given at <paramref name="StrengthPermille"/>, naming B as its origin. At most <paramref name="TopK"/> strong edges of A and of B count, at most
/// <paramref name="MaxPerActor"/> pushes per actor per day, each with the seeded chance <paramref name="ChancePermille"/>. <paramref name="Memory"/> is -1 only when balance is disabled and names none.
/// </summary>
public sealed record BalanceDef(bool Enabled, int Axis, long Like, long Dislike, int Memory, int StrengthPermille, int TopK, int MaxPerActor, int ChancePermille);

/// <summary>When the day's derived work happens. <see cref="Atomic"/>: all of it in the first step of the day (one long step). <see cref="Spread"/>: one stage per step, in the first <see cref="RelationshipRuntime.DayStages"/> steps of the day (see RelationshipRuntime.Day.cs).</summary>
public enum DaySchedule { Atomic, Spread }

public sealed record SocialDef(CommunityDef Communities, StandingDef Standing, BalanceDef Balance, DaySchedule Schedule);
