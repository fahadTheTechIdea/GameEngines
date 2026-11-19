namespace CombatEngine;

public sealed partial class CombatResolver
{
    /// <summary>
    /// Any number of groups on any number of sides fire at once at the state they started the round with. A group fires at most one
    /// order; a group may be the target of several. Each order uses the attempt number <c>attempt + its position</c>. Damage taken from
    /// all orders is added (never below zero health) and each group's standing is decided once from that total by the one standing rule.
    /// </summary>
    public RoundResult Round(IReadOnlyList<Group> groups, IReadOnlyList<RoundOrder> orders, int seconds, ulong seed, long attempt, int loggedShots)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(orders);
        try { return RoundChecked(groups, orders, seconds, seed, attempt, loggedShots); }
        catch (OverflowException exception) { throw Overflow(exception); }
    }

    private RoundResult RoundChecked(IReadOnlyList<Group> groups, IReadOnlyList<RoundOrder> orders, int seconds, ulong seed, long attempt, int loggedShots)
    {
        if (groups.Count == 0 || orders.Count == 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a round needs groups and orders");
        var byId = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var group in groups)
            if (!byId.TryAdd(group.Id, group)) throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' appears twice in the round");
        var attackers = new HashSet<string>(StringComparer.Ordinal);
        var fires = new List<EngagementResult>();
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            if (!byId.TryGetValue(order.Attacker, out var attacker) || !byId.TryGetValue(order.Defender, out var defender))
                throw new CombatRefusedException(CombatCodes.BadRequest, "order " + i + " names a group that is not in the round");
            if (!attackers.Add(order.Attacker)) throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + order.Attacker + "' has two orders in one round");
            fires.Add(FireChecked(new EngagementRequest(attacker, defender, order.DistanceMilli, seconds, order.Cover, order.Terrain, seed, attempt + i, loggedShots)));
        }

        var final = new List<Group>();
        foreach (var start in groups)
        {
            var own = fires.FirstOrDefault(f => f.Attacker.Id == start.Id);
            long damage = 0, platformDamage = 0, hits = 0;
            for (var i = 0; i < orders.Count; i++)
            {
                if (orders[i].Defender != start.Id) continue;
                damage += start.Health - fires[i].Defender.Health;
                platformDamage += start.PlatformHealth - fires[i].Defender.PlatformHealth;
                hits += fires[i].Batteries.Sum(b => b.Hits);
            }
            var standing = StandingRule.After(Rules, start, Math.Max(0, start.Health - damage), hits, Math.Max(0, start.PlatformHealth - platformDamage));
            final.Add(own is null ? standing : standing with { Batteries = own.Attacker.Batteries });
        }
        return new RoundResult(final, fires, StateHash.Of(final));
    }

    /// <summary>
    /// Both sides fire at once at the state they started with, and each takes the other's damage: nobody fires less because the other
    /// side got the first shot. The second side's rolls use the next attempt number. A round of two orders.
    /// </summary>
    public ExchangeResult Exchange(Group first, Group second, long distanceMilli, int seconds, string cover, string terrain, ulong seed, long attempt, int loggedShots)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var round = Round([first, second], [new RoundOrder(first.Id, second.Id, distanceMilli, cover, terrain), new RoundOrder(second.Id, first.Id, distanceMilli, cover, terrain)], seconds, seed, attempt, loggedShots);
        return new ExchangeResult(round.Groups[0], round.Groups[1], round.Fires[0], round.Fires[1], round.Hash);
    }
}
