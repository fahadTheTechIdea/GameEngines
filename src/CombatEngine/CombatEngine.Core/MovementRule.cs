namespace CombatEngine;

/// <summary>
/// The one rule for closing a distance: a group moves at its own speed scaled by the going of the terrain, and the distance between two
/// groups falls by that much, never below zero. The map (who is where) belongs to the caller; the engine only answers how far a march gets.
/// A group with platforms marches at its platform speed only while a crewed platform is standing and, if it burns fuel, only while the fuel lasts;
/// after that it marches on foot at the rules pace.
/// </summary>
public static class MovementRule
{
    /// <summary>How far a march got, the group afterwards (its fuel spent) and the fuel burned and the seconds spent on the platforms, both zero for a group with no platforms.</summary>
    public sealed record MarchResult(long DistanceMilli, Group Group, long FuelBurnedMilli, int MotorSeconds);

    public static MarchResult March(CombatRules rules, Group group, long distanceMilli, int seconds, string terrain)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(group);
        Validate(rules, distanceMilli, group.SpeedMilli, seconds);
        rules.TerrainType(terrain);
        if (group.InitialPlatforms <= 0) return new MarchResult(Advance(rules, distanceMilli, group.SpeedMilli, seconds, terrain), group, 0, 0);
        var operable = group.OperablePlatforms;
        var burnPerHour = group.FuelCapacity > 0 && operable > 0 ? group.BurnPerHourMilli * operable / group.InitialPlatforms : 0;
        long motorSeconds = operable <= 0 ? 0 : burnPerHour == 0 ? seconds : Math.Min(seconds, group.Fuel * 3600 / burnPerHour);
        var motor = (int)motorSeconds;
        var distance = distanceMilli;
        if (motor > 0) distance = Advance(rules, distance, group.MarchSpeedMilli(rules.FootSpeedMilliPerSecond), motor, terrain);
        if (seconds - motor > 0) distance = Advance(rules, distance, rules.FootSpeedMilliPerSecond, seconds - motor, terrain);
        var burned = burnPerHour == 0 ? 0 : Math.Min(group.Fuel, (burnPerHour * motor + 3599) / 3600);
        return new MarchResult(distance, group with { Fuel = group.Fuel - burned }, burned, motor);
    }

    private static void Validate(CombatRules rules, long distanceMilli, long speedMilliPerSecond, int seconds)
    {
        if (distanceMilli < 0 || speedMilliPerSecond < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a distance and a speed cannot be negative");
        if (seconds <= 0 || seconds > rules.MaxEngagementSeconds) throw new CombatRefusedException(CombatCodes.BadRequest, "a march lasts 1 to " + rules.MaxEngagementSeconds + " seconds");
    }

    public static long Advance(CombatRules rules, long distanceMilli, long speedMilliPerSecond, int seconds, string terrain)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Validate(rules, distanceMilli, speedMilliPerSecond, seconds);
        var going = rules.TerrainType(terrain);
        try { return Math.Max(0, distanceMilli - speedMilliPerSecond * seconds * going.MovementPercent / 100); }
        catch (OverflowException exception) { throw new CombatRefusedException(CombatCodes.NumericRange, "a number in the request is too large for the model: " + exception.Message); }
    }
}
