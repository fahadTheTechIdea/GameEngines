using System.Globalization;
using CombatEngine;

namespace GameRuntime;

public sealed partial class CombatComponent
{
    private Group Known(string id) => _groups.TryGetValue(id, out var group) ? group : throw new CombatRefusedException(UnknownGroup, "there is no group '" + id + "'");

    private void AnswerEngagement(IEventPort events, long tick, in EngagementRequested request)
    {
        var attacker = Known(request.Attacker);
        var defender = Known(request.Defender);
        if (request.Attacker == request.Defender) throw new CombatRefusedException(SameGroup, "a group cannot fight itself");
        var distance = request.DistanceMetres * 1000L;
        var orders = request.Mutual
            ? new[] { new RoundOrder(attacker.Id, defender.Id, distance, request.Cover, request.Terrain), new RoundOrder(defender.Id, attacker.Id, distance, request.Cover, request.Terrain) }
            : new[] { new RoundOrder(attacker.Id, defender.Id, distance, request.Cover, request.Terrain) };
        var involved = new[] { attacker, defender };
        var round = resolver.Round(involved, orders, request.Seconds, seed, _attempt, loggedShots);
        Commit(involved, round, orders.Length, request.DistanceMetres);

        var fired = round.Fires.Sum(f => f.Batteries.Sum(b => b.Fired));
        var hits = round.Fires.Sum(f => f.Batteries.Sum(b => b.Hits));
        var codes = round.Fires.SelectMany(f => f.Codes).Distinct(StringComparer.Ordinal);
        events.Emit(new Engagement(request.RequestId, tick, attacker.Id, defender.Id, fired, hits, defender.Health - round.Groups[1].Health,
            request.Mutual ? attacker.Health - round.Groups[0].Health : 0, round.Groups[0].Health, round.Groups[1].Health, string.Join(',', codes), round.Hash));
        for (var i = 0; i < involved.Length; i++) Report(events, tick, request.RequestId, involved[i], round.Groups[i]);
    }

    private void AnswerRound(IEventPort events, long tick, in RoundRequested request)
    {
        var orders = ParseOrders(request.Orders);
        var ids = orders.SelectMany(o => new[] { o.Attacker, o.Defender }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var involved = ids.Select(Known).ToList();
        var round = resolver.Round(involved, orders, request.Seconds, seed, _attempt, loggedShots);
        Commit(involved, round, orders.Count, orders.Min(o => (int)(o.DistanceMilli / 1000)));

        for (var i = 0; i < orders.Count; i++)
        {
            var fire = round.Fires[i];
            var attackerBefore = involved.First(g => g.Id == orders[i].Attacker);
            var defenderBefore = involved.First(g => g.Id == orders[i].Defender);
            var attackerAfter = round.Groups[ids.IndexOf(orders[i].Attacker)];
            var defenderAfter = round.Groups[ids.IndexOf(orders[i].Defender)];
            events.Emit(new Engagement(request.RequestId, tick, orders[i].Attacker, orders[i].Defender, fire.Batteries.Sum(b => b.Fired), fire.Batteries.Sum(b => b.Hits),
                defenderBefore.Health - fire.Defender.Health, attackerBefore.Health - attackerAfter.Health, attackerAfter.Health, defenderAfter.Health, string.Join(',', fire.Codes), fire.Hash));
        }
        for (var i = 0; i < involved.Count; i++) Report(events, tick, request.RequestId, involved[i], round.Groups[i]);
    }

    private void AnswerMove(IEventPort events, long tick, in MoveRequested request)
    {
        var group = Known(request.Group);
        var march = MovementRule.March(resolver.Rules, group, request.DistanceMetres * 1000L, request.Seconds, request.Terrain);
        var after = march.DistanceMilli / 1000;
        _groups[group.Id] = march.Group;
        _lastDistance = (int)after;
        events.Emit(new Moved(request.RequestId, tick, group.Id, request.DistanceMetres, (int)after, request.Terrain));
        if (march.FuelBurnedMilli > 0) events.Emit(new FuelBurned(request.RequestId, tick, group.Id, march.FuelBurnedMilli, march.Group.Fuel));
    }

    private void AnswerRefuel(IEventPort events, long tick, in RefuelRequested request)
    {
        var group = Known(request.Group);
        var result = Logistics.Refuel(group, request.AmountMilli);
        _groups[group.Id] = result.Group;
        events.Emit(new Refuelled(request.RequestId, tick, group.Id, result.Accepted, result.Group.Fuel));
    }

    private void AnswerResupply(IEventPort events, long tick, in ResupplyRequested request)
    {
        var group = Known(request.Group);
        var battery = request.Battery;
        var result = Logistics.Resupply(group, battery, request.Rounds);
        _groups[group.Id] = result.Group;
        events.Emit(new Resupplied(request.RequestId, tick, group.Id, request.Battery, result.Accepted, result.Group.Batteries.First(b => b.Id == battery).Rounds));
    }

    private void AnswerReinforce(IEventPort events, long tick, in ReinforceRequested request)
    {
        var group = Known(request.Group);
        var result = Logistics.Reinforce(group, request.Members);
        _groups[group.Id] = result.Group;
        events.Emit(new Reinforced(request.RequestId, tick, group.Id, result.Accepted, result.Group.Members));
    }

    /// <summary>Takes the result of a resolved round into the component: the groups, the attempt numbers it used and the distance fought at.</summary>
    private void Commit(IReadOnlyList<Group> involved, RoundResult round, int orders, int distanceMetres)
    {
        for (var i = 0; i < involved.Count; i++) _groups[involved[i].Id] = round.Groups[i];
        _attempt += orders;
        _engagements += orders;
        _lastDistance = distanceMetres;
    }

    /// <summary>Orders are <c>attacker&gt;defender|metres|cover|terrain</c> joined by ';'. Anything else is a bad request, never a guess.</summary>
    private static List<RoundOrder> ParseOrders(string text)
    {
        var orders = new List<RoundOrder>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = part.Split('|');
            var sides = fields[0].Split('>');
            if (fields.Length != 4 || sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0
                || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var metres))
                throw new CombatRefusedException(CombatCodes.BadRequest, "a round order needs attacker>defender|metres|cover|terrain but got '" + part + "'");
            orders.Add(new RoundOrder(sides[0], sides[1], metres * 1000L, fields[2], fields[3]));
        }
        return orders.Count > 0 ? orders : throw new CombatRefusedException(CombatCodes.BadRequest, "a round needs at least one order");
    }
}
