namespace CombatEngine;

/// <summary>
/// The one rule that decides how a group stands after taking hits: its health, whether it is suppressed, its morale and whether it routs.
/// A group routs when too little of its strength is left or its morale falls below the line, and stays routed.
/// </summary>
public static class StandingRule
{
    /// <param name="platformHealthAfter">The platform pool after the hits; a group without platforms leaves it out.</param>
    public static Group After(CombatRules rules, Group before, long healthAfter, long hitsTaken, long? platformHealthAfter = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(before);
        var platformAfter = platformHealthAfter ?? before.PlatformHealth;
        var fullHealth = before.InitialMembers * before.MemberHealth + before.InitialPlatforms * before.PlatformUnitHealth;
        var lost = Math.Max(0, before.Health - healthAfter) + Math.Max(0, before.PlatformHealth - platformAfter);
        var lostPermille = fullHealth <= 0 ? 0 : lost * 1000 / fullHealth;
        var suppressed = hitsTaken >= rules.SuppressionHits;
        var morale = before.Morale - lostPermille * rules.Morale.CasualtyLossPercent / 100 - (suppressed ? rules.Morale.SuppressionLoss : 0)
            + (lost == 0 && !suppressed ? rules.Morale.Recovery : 0);
        var after = before with { Health = healthAfter, PlatformHealth = platformAfter, Suppressed = suppressed, Morale = Math.Clamp(morale, 0, 1000) };
        var routed = before.Routed || (!after.Destroyed && (after.Members * 100 < before.InitialMembers * rules.RoutStrengthPercent || after.Morale < rules.Morale.RoutBelow));
        // The fuel of a platform goes down with it.
        return after with { Routed = routed, Fuel = Math.Min(after.Fuel, after.FuelRoom) };
    }
}
