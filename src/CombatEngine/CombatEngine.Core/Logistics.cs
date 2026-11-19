namespace CombatEngine;

/// <summary>The group after a resupply and how many rounds it actually took in.</summary>
public sealed record ResupplyResult(Group Group, long Accepted);

/// <summary>The group after a reinforcement and how many members it actually took in.</summary>
public sealed record ReinforceResult(Group Group, long Accepted);

/// <summary>The group after a refuelling and how much fuel (thousandths of a unit) it actually took in.</summary>
public sealed record RefuelResult(Group Group, long Accepted);

/// <summary>
/// The one rule for putting ammunition into a battery: it takes in what it can hold up to its capacity and no more. Where the rounds
/// come from (a stock, a depot, a convoy) is the caller's; the caller removes from its stock only what was accepted.
/// </summary>
public static class Logistics
{
    /// <summary>
    /// New members join a group that is still in the fight: it takes in what brings it back to the strength it started with and no more, at full
    /// health each, and its morale, suppression and standing do not change. A destroyed or routed group is out of the fight for good and takes in nobody.
    /// Where the members come from is the caller as well, which removes from its own stock only what was accepted.
    /// </summary>
    public static ReinforceResult Reinforce(Group group, long offeredMembers)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (offeredMembers < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a reinforcement cannot be negative");
        if (group.Destroyed || group.Routed) throw new CombatRefusedException(CombatCodes.GroupWithdrawn, "group '" + group.Id + "' is " + group.Standing + " and takes in no one");
        var accepted = Math.Min(offeredMembers, Math.Max(0, group.InitialMembers - group.Members));
        return new ReinforceResult(group with { Health = checked(group.Health + accepted * group.MemberHealth) }, accepted);
    }

    /// <summary>
    /// Fuel goes into the tanks of the platforms that are still standing, up to their capacity and no more; the caller removes from its stock only what was accepted.
    /// A group that burns no fuel, or is destroyed or routed, takes none.
    /// </summary>
    public static RefuelResult Refuel(Group group, long offeredMilli)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (offeredMilli < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a refuelling cannot be negative");
        if (group.FuelCapacity <= 0) throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' burns no fuel");
        if (group.Destroyed || group.Routed) throw new CombatRefusedException(CombatCodes.GroupWithdrawn, "group '" + group.Id + "' is " + group.Standing + " and takes in no fuel");
        var room = Math.Max(0, group.FuelRoom - group.Fuel);
        var accepted = Math.Min(offeredMilli, room);
        return new RefuelResult(group with { Fuel = group.Fuel + accepted }, accepted);
    }

    public static ResupplyResult Resupply(Group group, string batteryId, long offeredRounds)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (offeredRounds < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a resupply cannot be negative");
        var index = -1;
        for (var i = 0; i < group.Batteries.Count; i++)
            if (group.Batteries[i].Id == batteryId) { index = i; break; }
        if (index < 0) throw new CombatRefusedException(CombatCodes.UnknownBattery, "group '" + group.Id + "' has no battery '" + batteryId + "'");
        var battery = group.Batteries[index];
        if (battery.Ammo is null) throw new CombatRefusedException(CombatCodes.BadRequest, "battery '" + batteryId + "' uses no ammunition");
        var accepted = Math.Min(offeredRounds, battery.Capacity - battery.Rounds);
        var batteries = group.Batteries.ToList();
        batteries[index] = battery with { Rounds = battery.Rounds + accepted };
        return new ResupplyResult(group with { Batteries = batteries }, accepted);
    }
}
