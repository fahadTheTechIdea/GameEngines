namespace CombatEngine;

/// <summary>
/// Everything about one side of an engagement: its personnel pool, its platform pool (vehicles, guns), armor, training, standing, morale and
/// batteries. Casualties follow the personnel pool; firepower follows the survivors, and a platform fires only while it has crew. <see cref="Side"/> says who is friend and foe: groups of one side never fight each
/// other. <see cref="Morale"/> is in per-mille; <see cref="SpeedMilli"/> is the group's march speed in thousandths of a metre per second.
/// A group is a value: resolving an engagement returns the changed copy.
/// </summary>
public sealed record Group(string Id, int Side, string Training, long InitialMembers, long MemberHealth, long Health, long ArmorRating, bool Suppressed, bool Routed,
    IReadOnlyList<Battery> Batteries, long Morale, long SpeedMilli,
    long PlatformHealth = 0, long PlatformUnitHealth = 0, long InitialPlatforms = 0, long PlatformArmor = 0, long CrewPerPlatform = 0,
    long Fuel = 0, long FuelCapacity = 0, long BurnPerHourMilli = 0, string? FuelType = null)
{
    /// <summary>People alive: the personnel pool alone.</summary>
    public long Members => Health <= 0 ? 0 : (Health + MemberHealth - 1) / MemberHealth;

    /// <summary>A group with no one left is destroyed, whatever is left of its platforms: nobody is there to work them.</summary>
    public bool Destroyed => Health <= 0;

    /// <summary>Platform units standing: the platform pool in whole units, never more than it started with.</summary>
    public long Platforms => PlatformHealth <= 0 || PlatformUnitHealth <= 0 ? 0 : Math.Min(InitialPlatforms, (PlatformHealth + PlatformUnitHealth - 1) / PlatformUnitHealth);

    /// <summary>
    /// The speed the group marches at, in thousandths of a metre per second. The people of a formation ride its platforms evenly; a platform that is gone
    /// or has no crew leaves its riders to walk, and the column moves at the harmonic mean of the two paces by share of people (each person covers the
    /// same distance, so the time is the share-weighted sum of the times). A structure never moves; a group with no platforms keeps its own speed.
    /// </summary>
    public long MarchSpeedMilli(long footSpeedMilli)
    {
        if (InitialPlatforms <= 0 || SpeedMilli <= 0 || Members <= 0) return SpeedMilli;
        var ridersPerPlatform = (InitialMembers + InitialPlatforms - 1) / InitialPlatforms;
        var mounted = Math.Min(Members, OperablePlatforms * ridersPerPlatform);
        var denominator = mounted * footSpeedMilli + (Members - mounted) * SpeedMilli;
        return denominator <= 0 ? SpeedMilli : Members * SpeedMilli * footSpeedMilli / denominator;
    }

    /// <summary>The fuel the standing platforms can hold: fuel goes with the platforms that carried it, so the tanks of the lost ones are gone.</summary>
    public long FuelRoom => FuelCapacity <= 0 || InitialPlatforms <= 0 ? 0 : FuelCapacity * Platforms / InitialPlatforms;

    /// <summary>Platforms that can act: those standing that have their crew. Crew are drawn from the people alive, so the platforms go quiet as the people go.</summary>
    public long OperablePlatforms => CrewPerPlatform <= 0 ? Platforms : Math.Min(Platforms, Members / CrewPerPlatform);

    /// <summary>The name of the group's standing, from worst to best: destroyed, routed, suppressed, active.</summary>
    public string Standing => Destroyed ? "destroyed" : Routed ? "routed" : Suppressed ? "suppressed" : "active";
}
