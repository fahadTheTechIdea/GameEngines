namespace CombatEngine;

/// <summary>One group's fire in a round: who fires at whom, from what distance, with the target's cover, over what terrain.</summary>
public sealed record RoundOrder(string Attacker, string Defender, long DistanceMilli, string Cover, string Terrain);

/// <summary>All groups after a round, in the order given, and what each order's fire did. The hash covers every group.</summary>
public sealed record RoundResult(IReadOnlyList<Group> Groups, IReadOnlyList<EngagementResult> Fires, string Hash);
