namespace CombatEngine;

/// <summary>
/// How courage moves, in per-mille of a group's full morale. A group loses <see cref="CasualtyLossPercent"/> percent of the share of its
/// health it just lost, plus <see cref="SuppressionLoss"/> when suppressed, and regains <see cref="Recovery"/> when an engagement did
/// it no harm. Below <see cref="RoutBelow"/> it routs.
/// </summary>
public sealed record MoraleRules(int CasualtyLossPercent, int SuppressionLoss, int Recovery, int RoutBelow);
