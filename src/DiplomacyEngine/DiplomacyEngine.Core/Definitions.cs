using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>Every bound a document declares for the lists the state holds.</summary>
public sealed record LimitsDef(int MaxTreaties, int MaxProposals, int MaxWars, int MaxAgents, int MaxMissions, int MaxCalls, int MaxIncidents, int MaxGrants, int MaxDeedsPerPair);

/// <summary>A season of the calendar the document knows; <paramref name="WarCost"/> is how much a step of war tires a realm in it, per mille of the ordinary cost.</summary>
public sealed record SeasonDef(int Index, string Id, string Key, int WarCost);

/// <summary>An ideology; <paramref name="Affinity"/> is how much a realm of this ideology likes a realm of each ideology, minus 1,000 to 1,000, in document order.</summary>
public sealed record IdeologyDef(int Index, string Id, string Key, IReadOnlyList<int> Affinity);

/// <summary>A trait of a realm and what it does: acceptance bias, reliability when called to arms, counter-intelligence, and the skill its agents gain.</summary>
public sealed record TraitDef(int Index, string Id, string Key, int Acceptance, int Reliability, int CounterIntel, int SpyBonus);

/// <summary>A thing one realm does to another that the other remembers: its regard effect (minus 1,000 to 1,000) fades to nothing over <paramref name="Duration"/> days.</summary>
public sealed record DeedDef(int Index, string Id, string Key, int Value, int Duration);

public sealed record LikeDef(PoliticalEvent Event, int Delta);

/// <summary>A faction inside a realm: its weight, how content it starts, and how events change its contentment.</summary>
public sealed record FactionDef(int Index, string Id, string Key, int Share, int Satisfaction, IReadOnlyList<LikeDef> Likes);

public sealed record RealmDef(int Index, string Id, string Key, int Ideology, IReadOnlyList<int> Traits, long Might, string Market, string Leader, Control Control, int Stability, int Legitimacy,
    int SuccessionPpm, int Trust, int Fear, int Honour, int Infamy, IReadOnlyList<FactionDef> Factions);

/// <summary>One obligation of a treaty. Which fields mean something depends on the kind: tribute uses interval, grace, maxMissed, dueSeason and optional; defend uses grace and mutual.</summary>
public sealed record ClauseDef(ClauseKind Kind, int Interval, int Grace, int MaxMissed, int DueSeason, bool Optional, bool Mutual);

/// <summary>What a treaty asks of the other layers when it starts, ends or is broken.</summary>
public sealed record EffectDef(EffectKind Kind, string Name, int Scale, EffectTarget Target);

public sealed record ModifierDef(string Id, string Key, Condition When, int Value);

public sealed record TreatyTypeDef(int Index, string Id, string Key, Consent Consent, IReadOnlyList<ClauseDef> Clauses, bool Permanent, int MinTerm, int MaxTerm, int Consider, int Expire, int Regard, Condition? Requires,
    IReadOnlyList<int> Excludes, bool DuringWar, bool EndsWar, bool EndsOnSuccession, CancelRule Cancel, int BreachTrust, int BreachHonour, int BreachDeed, int GrantsCb, int CbDays, int SignDeed, int Bias,
    IReadOnlyList<int> Weights, IReadOnlyList<ModifierDef> Modifiers, IReadOnlyList<EffectDef> OnSign, IReadOnlyList<EffectDef> OnEnd, IReadOnlyList<EffectDef> OnBreach)
{
    public bool Has(ClauseKind kind) => Clauses.Any(c => c.Kind == kind);

    public ClauseDef? Clause(ClauseKind kind) => Clauses.FirstOrDefault(c => c.Kind == kind);
}

public sealed record CasusBelliDef(int Index, string Id, string Key, CbSource Source, Condition? Requires, int Infamy, int Honour);

public sealed record MissionDef(int Index, string Id, string Key, MissionKind Kind, int Duration, int Difficulty, int Exposure, int Gain, string Shock, int ShockScale, int StabilityHit, int Deed, int GrantsCb);

public sealed record AgentDef(string Id, string Key, int Owner, int Skill);

public sealed record RegardRules(int Ideology, int Leader, int Trust, int Honour, int Coalition);

public sealed record StandingRules(int TrustDecayPpm, int FearDecayPpm, int HonourDecayPpm, int InfamyDecayPpm, int WitnessDeed, int ExpireTrust, int PaidTrust, int HonouredTrust, int HonouredHonour);

public sealed record WarRules(bool AllowNoCb, int NoCbInfamy, int NoCbHonour, int DeclaredDeed, int ExhaustPpm, int SourceBattle, int SourceOccupation, int SourceBlockade, int SourceEvent, int CallBase,
    int FearPerScore, int WarOnUsDeed);

public sealed record AcceptanceRules(int AcceptAt, int RejectAt, int TermUnit);

public sealed record BalanceRules(int Interval, int ThreatLine, int RelaxLine, int JoinRegard, int MinMembers, int BandwagonFear, int Cb, int CbDays);

public sealed record PoliticsRules(int InertiaPpm, int LegitimacyWeight, int ExhaustionWeight, int UnrestLine, int RevoltLine, int Hysteresis, int DriftPpm, int LegitimacyBase, int LegitimacyDriftPpm,
    int SuccessionLegitimacy, int ContestedBase, int ContestedPerLack, int ContestedStability, int ClaimCb, int ClaimDays, string UnrestSignal, string RevoltSignal, string CalmSignal, string SuccessionSignal,
    string ContestedSignal);

public sealed record EspionageRules(int CounterWeight, int ExposedOnSuccess, int CaptureDays, int ExposedHonour, int IntelDecayPpm, int CounterDecayPpm, IReadOnlyList<int> Levels, IReadOnlyList<int> Errors, int EpochDays);
