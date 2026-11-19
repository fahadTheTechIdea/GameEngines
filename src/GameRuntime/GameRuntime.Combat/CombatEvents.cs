namespace GameRuntime;

// Events of the combat component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged.
// Lists are joined by ';' (orders, groups) and ',' (codes), and numbers are whole.

/// <summary>A request for two groups to fight for some seconds. It is only queued; the combat component resolves it in its next Update. A mutual request has both sides fire at once.</summary>
public readonly struct EngagementRequested
{
    public EngagementRequested(string requestId, string attacker, string defender, int distanceMetres, int seconds, string cover, string terrain, bool mutual)
    { RequestId = requestId; Attacker = attacker; Defender = defender; DistanceMetres = distanceMetres; Seconds = seconds; Cover = cover; Terrain = terrain; Mutual = mutual; }
    public string RequestId { get; }
    public string Attacker { get; }
    public string Defender { get; }
    public int DistanceMetres { get; }
    public int Seconds { get; }
    public string Cover { get; }
    public string Terrain { get; }
    public bool Mutual { get; }
}

/// <summary>
/// A request for any number of groups on any number of sides to fire at once for some seconds. <see cref="Orders"/> is
/// <c>attacker&gt;defender|distance-metres|cover|terrain</c> entries joined by ';', one per firing group. Queued like every request.
/// </summary>
public readonly struct RoundRequested
{
    public RoundRequested(string requestId, string orders, int seconds) { RequestId = requestId; Orders = orders; Seconds = seconds; }
    public string RequestId { get; }
    public string Orders { get; }
    public int Seconds { get; }
}

/// <summary>A request for a group to march for some seconds over some terrain, closing the distance to its opponent as the caller holds it. Queued like every request.</summary>
public readonly struct MoveRequested
{
    public MoveRequested(string requestId, string group, int distanceMetres, int seconds, string terrain) { RequestId = requestId; Group = group; DistanceMetres = distanceMetres; Seconds = seconds; Terrain = terrain; }
    public string RequestId { get; }
    public string Group { get; }
    public int DistanceMetres { get; }
    public int Seconds { get; }
    public string Terrain { get; }
}

/// <summary>A request to put rounds into a group battery. The caller has taken them from its stock; the answer says how many fitted. Queued like every request.</summary>
public readonly struct ResupplyRequested
{
    public ResupplyRequested(string requestId, string group, string battery, long rounds) { RequestId = requestId; Group = group; Battery = battery; Rounds = rounds; }
    public string RequestId { get; }
    public string Group { get; }
    public string Battery { get; }
    public long Rounds { get; }
}

/// <summary>A request to bring members into a group, up to the strength it started with. The caller has taken them from its stock; the answer says how many were taken in. Queued like every request.</summary>
public readonly struct ReinforceRequested
{
    public ReinforceRequested(string requestId, string group, long members) { RequestId = requestId; Group = group; Members = members; }
    public string RequestId { get; }
    public string Group { get; }
    public long Members { get; }
}

/// <summary>A group took in members. <see cref="Accepted"/> is what fitted; the caller removes only that from its stock. <see cref="Members"/> is the group strength afterwards.</summary>
public readonly struct Reinforced
{
    public Reinforced(string requestId, long tick, string group, long accepted, long members) { RequestId = requestId; Tick = tick; Group = group; Accepted = accepted; Members = members; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public long Accepted { get; }
    public long Members { get; }
}

/// <summary>An engagement was resolved. Health is what each group has left; damage is what each side took; codes are the engagement-level outcome codes joined by ','.</summary>
public readonly struct Engagement
{
    public Engagement(string requestId, long tick, string attacker, string defender, long fired, long hits, long damageToDefender, long damageToAttacker, long attackerHealth, long defenderHealth, string codes, string hash)
    { RequestId = requestId; Tick = tick; Attacker = attacker; Defender = defender; Fired = fired; Hits = hits; DamageToDefender = damageToDefender; DamageToAttacker = damageToAttacker; AttackerHealth = attackerHealth; DefenderHealth = defenderHealth; Codes = codes; Hash = hash; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Attacker { get; }
    public string Defender { get; }
    public long Fired { get; }
    public long Hits { get; }
    public long DamageToDefender { get; }
    public long DamageToAttacker { get; }
    public long AttackerHealth { get; }
    public long DefenderHealth { get; }
    public string Codes { get; }
    public string Hash { get; }
}

/// <summary>A group lost members in an engagement.</summary>
public readonly struct Casualty
{
    public Casualty(string requestId, long tick, string group, long lost, long left) { RequestId = requestId; Tick = tick; Group = group; Lost = lost; Left = left; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public long Lost { get; }
    public long Left { get; }
}

/// <summary>A group lost platform units (vehicles, guns). <see cref="Left"/> is how many stand afterwards. Members lost are a <see cref="Casualty"/>.</summary>
public readonly struct PlatformsLost
{
    public PlatformsLost(string requestId, long tick, string group, long lost, long left, long fuelLostMilli) { RequestId = requestId; Tick = tick; Group = group; Lost = lost; Left = left; FuelLostMilli = fuelLostMilli; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public long Lost { get; }
    public long Left { get; }
    /// <summary>The fuel (thousandths of a unit) that went down with them.</summary>
    public long FuelLostMilli { get; }
}

/// <summary>A request to put fuel in a group tanks (thousandths of a unit). The caller has taken it from its stock; the answer says how much fitted. Queued like every request.</summary>
public readonly struct RefuelRequested
{
    public RefuelRequested(string requestId, string group, long amountMilli) { RequestId = requestId; Group = group; AmountMilli = amountMilli; }
    public string RequestId { get; }
    public string Group { get; }
    public long AmountMilli { get; }
}

/// <summary>A group took in fuel. <see cref="AcceptedMilli"/> is what fitted; the caller removes only that from its stock.</summary>
public readonly struct Refuelled
{
    public Refuelled(string requestId, long tick, string group, long acceptedMilli, long leftMilli) { RequestId = requestId; Tick = tick; Group = group; AcceptedMilli = acceptedMilli; LeftMilli = leftMilli; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public long AcceptedMilli { get; }
    public long LeftMilli { get; }
}

/// <summary>A group burned fuel marching (thousandths of a unit). Only a march of a group that burns fuel raises it.</summary>
public readonly struct FuelBurned
{
    public FuelBurned(string requestId, long tick, string group, long burnedMilli, long leftMilli) { RequestId = requestId; Tick = tick; Group = group; BurnedMilli = burnedMilli; LeftMilli = leftMilli; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public long BurnedMilli { get; }
    public long LeftMilli { get; }
}

/// <summary>A battery spent rounds. Only batteries that use ammunition raise it.</summary>
public readonly struct AmmoSpent
{
    public AmmoSpent(string requestId, long tick, string group, string battery, long rounds, long left) { RequestId = requestId; Tick = tick; Group = group; Battery = battery; Rounds = rounds; Left = left; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public string Battery { get; }
    public long Rounds { get; }
    public long Left { get; }
}

/// <summary>A group marched. <see cref="FromMetres"/> and <see cref="ToMetres"/> are the distance to its opponent before and after.</summary>
public readonly struct Moved
{
    public Moved(string requestId, long tick, string group, int fromMetres, int toMetres, string terrain) { RequestId = requestId; Tick = tick; Group = group; FromMetres = fromMetres; ToMetres = toMetres; Terrain = terrain; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public int FromMetres { get; }
    public int ToMetres { get; }
    public string Terrain { get; }
}

/// <summary>A battery took in rounds. <see cref="Accepted"/> is what fitted under its capacity; the caller removes only that from its stock.</summary>
public readonly struct Resupplied
{
    public Resupplied(string requestId, long tick, string group, string battery, long accepted, long left) { RequestId = requestId; Tick = tick; Group = group; Battery = battery; Accepted = accepted; Left = left; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public string Battery { get; }
    public long Accepted { get; }
    public long Left { get; }
}

/// <summary>The request was refused (an unknown group, a bad number, an unknown cover level). Nothing was resolved.</summary>
public readonly struct EngagementRefused
{
    public EngagementRefused(string requestId, long tick, string code) { RequestId = requestId; Tick = tick; Code = code; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>A group's standing changed: "active", "suppressed", "routed" or "destroyed". Raised after the request that changed it, with the group morale in per-mille.</summary>
public readonly struct Standing
{
    public Standing(string requestId, long tick, string group, string state, long morale) { RequestId = requestId; Tick = tick; Group = group; State = state; Morale = morale; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Group { get; }
    public string State { get; }
    public long Morale { get; }
}

/// <summary>
/// Raised once at Init with every group, so a listener created after a load rebuilds its view from events. Each group is
/// <c>id,side,health,longestRangeMetres,morale,speedMilli,standing</c>; groups are joined by ';'.
/// </summary>
public readonly struct ForcesRestored
{
    public ForcesRestored(string groups, long engagements, int distanceMetres) { Groups = groups; Engagements = engagements; DistanceMetres = distanceMetres; }
    public string Groups { get; }
    /// <summary>Engagements resolved before this session started (zero for a new game).</summary>
    public long Engagements { get; }
    /// <summary>The distance the last engagement or march left between the sides; -1 before the first.</summary>
    public int DistanceMetres { get; }
}
