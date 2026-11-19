namespace CombatEngine;

/// <summary>
/// One side firing at another for a number of seconds at a distance over some terrain. <see cref="Seed"/> and <see cref="Attempt"/> are
/// the caller's: the same request always resolves the same way. <see cref="LoggedShots"/> bounds how many shots are written out one by one.
/// </summary>
public sealed record EngagementRequest(Group Attacker, Group Defender, long DistanceMilli, int Seconds, string Cover, string Terrain, ulong Seed, long Attempt, int LoggedShots);
