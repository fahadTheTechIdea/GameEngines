namespace DiplomacyEngine;

/// <summary>What a treaty clause binds its parties to. Each kind is checked by the engine itself, except tribute payments, which the host confirms.</summary>
public enum ClauseKind { NonAggression, Trade, Tribute, Defend, Vassal, Truce, Bond, Embargo }

/// <summary>Whether a treaty needs the other realm to accept (mutual) or one realm can put it on the other (imposed: an embargo, a sanction).</summary>
public enum Consent { Mutual, Imposed }

/// <summary>What happens when a party ends a treaty early: nothing (free), a breach with its penalties, or it cannot be done.</summary>
public enum CancelRule { Free, Breach, Never }

/// <summary>Who decides for a realm: the engine's acceptance scoring (ai) or the host, by a response (host).</summary>
public enum Control { Ai, Host }

/// <summary>Where a casus belli comes from: it exists whenever its condition holds (standing), or only while it was granted.</summary>
public enum CbSource { Standing, Granted }

/// <summary>The inputs of the acceptance score. Each is a raw number from minus 1,000 to 1,000 that the type weighs in per mille.</summary>
public enum FactorSource { Regard, Trust, Fear, Honour, PowerRatio, SharedEnemy, Ideology, Ties, Weariness, Threat, Stability, Coalition, Terms, WarScore, Exhaustion }

/// <summary>What an effect asks of the other layers: a named story signal, an economy shock on a realm's market, or a payment between two realms.</summary>
public enum EffectKind { Signal, Shock, Payment }

/// <summary>Which realm of a treaty an effect is for.</summary>
public enum EffectTarget { First, Second, Both }

/// <summary>What happens in a realm that its factions care about.</summary>
public enum PoliticalEvent { WarDeclared, WarOnUs, WarWon, WarLost, PeaceSigned, TreatySigned, TreatyBreached, TributePaid, TributeMissed, SpyExposed, Disaster, Succession }

/// <summary>Where a realm's order is: stable, in unrest, or in revolt (cannot act abroad).</summary>
public enum StabilityBand { Stable, Unrest, Revolt }

/// <summary>What a mission does if it succeeds.</summary>
public enum MissionKind { Intel, Sabotage, Incite, Counter }

/// <summary>Where an agent is.</summary>
public enum AgentState { Idle, Away, Captured }

/// <summary>How well one realm knows another, from nothing to everything.</summary>
public enum IntelLevel { None, Rumour, Report, Detailed, Complete }

/// <summary>Where a war score came from; the document weighs each.</summary>
public enum ScoreSource { Battle, Occupation, Blockade, Event }

/// <summary>What the diplomatic-incident log records.</summary>
public enum IncidentKind { Breach, WarDeclared, WarEnded, Ceasefire, Coalition, SpyExposed, CallIgnored, Revolt, Succession, Embargo }

public enum ProposalChange { Made, Accepted, Rejected, Expired, Withdrawn, Invalid }
public enum TreatyChange { Signed, Expired, Cancelled, Broken, Ended }
public enum ObligationChange { Due, Paid, Missed, Called, Honoured, Ignored }
public enum WarChange { Declared, Joined, Left, Ceasefire, Resumed, Ended, Score }
public enum CoalitionChange { Formed, Joined, Left, Dissolved }
public enum MissionChange { Started, Succeeded, Failed, SucceededExposed, FailedExposed }
public enum AgentChange { Recruited, Captured, Released }
public enum PoliticsChange { Unrest, Revolt, Calm, Succession, ContestedSuccession }
public enum GrantChange { Granted, Used, Expired }
