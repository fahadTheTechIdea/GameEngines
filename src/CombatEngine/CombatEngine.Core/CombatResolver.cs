namespace CombatEngine;

/// <summary>
/// Resolves engagements shot by shot against a finite health pool. It is a function of its inputs: no clock, no files, no state of its
/// own, and randomness only through <see cref="CombatRoll"/>. Every shot earned lands in exactly one outcome and has a code. Arithmetic is
/// checked: a number too large for the model is refused with <see cref="CombatCodes.NumericRange"/>, never wrapped.
/// </summary>
public sealed partial class CombatResolver(CombatRules rules)
{
    public CombatRules Rules { get; } = rules ?? throw new ArgumentNullException(nameof(rules));

    /// <summary>The attacker fires at the defender. Only the defender takes damage; the attacker's rounds and earned fractions are spent.</summary>
    public EngagementResult Fire(EngagementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try { return FireChecked(request); }
        catch (OverflowException exception) { throw Overflow(exception); }
    }

    private static CombatRefusedException Overflow(OverflowException exception) =>
        new(CombatCodes.NumericRange, "a number in the request is too large for the model: " + exception.Message);

    private EngagementResult FireChecked(EngagementRequest request)
    {
        Validate(request);
        var attacker = request.Attacker;
        var defender = request.Defender;
        var codes = new List<string>();
        var shots = new List<ShotRecord>();
        var reports = new List<BatteryReport>();
        if (attacker.Routed) codes.Add(CombatCodes.Routed);
        if (attacker.Destroyed) codes.Add(CombatCodes.Destroyed);
        var inactive = attacker.Routed || attacker.Destroyed;
        var training = Rules.TrainingLevel(attacker.Training);
        var cover = Rules.CoverLevel(request.Cover);
        var terrain = Rules.TerrainType(request.Terrain);

        var health = defender.Health;
        var platformHealth = defender.PlatformHealth;
        long hitsTaken = 0;
        var spent = new List<Battery>();
        foreach (var battery in attacker.Batteries)
        {
            var count = inactive ? 0 : FiringUnits(attacker, battery);
            var total = battery.CarryMilli + count * battery.Weapon.RatePerMinuteMilli * request.Seconds / 60;
            var earned = total / 1000;
            if (earned > Rules.MaxShotsPerBattery)
                throw new CombatRefusedException(CombatCodes.TooManyShots, "battery '" + battery.Id + "' would fire " + earned + " shots, over the limit of " + Rules.MaxShotsPerBattery);
            var tally = new Tally();
            var rounds = battery.Rounds;
            var weapon = battery.Weapon;

            if (earned > 0 && request.DistanceMilli > weapon.RangeMilli)
            {
                tally.OutOfRange = earned;
                Log(shots, request, battery, 0, CombatCodes.OutOfRange, 0, 0, 0);
            }
            else
            {
                for (long index = 0; index < earned; index++)
                {
                    if (health <= 0) { tally.Wasted = earned - index; Log(shots, request, battery, index, CombatCodes.TargetDestroyed, 0, 0, 0); break; }
                    if (battery.Ammo is not null && rounds <= 0) { tally.NoAmmo = earned - index; Log(shots, request, battery, index, CombatCodes.NoAmmo, 0, 0, 0); break; }
                    tally.Fired++;
                    if (battery.Ammo is not null) rounds--;
                    var splash = Math.Max(weapon.SplashMilli, battery.Ammo?.SplashMilli ?? 0);
                    // A hit lands in the personnel pool or the platform pool by the share of the group health each holds; the choice has its own roll, so it never moves the hit roll.
                    var onPlatform = platformHealth > 0 && PlatformTakesHit(request, attacker, battery, index, health, platformHealth);
                    var band = HitRule.BandPercent(Rules, request.DistanceMilli, weapon.RangeMilli);
                    var chance = HitRule.ChancePermille(Rules, weapon.AccuracyPermille, band, training.AccuracyPercent, terrain.AccuracyPercent, attacker.Suppressed, cover.AccuracyPercent, splash > 0);
                    var roll = CombatRoll.Permille(request.Seed, request.Attempt, attacker.Id, battery.Id, index);
                    if (roll >= chance)
                    {
                        tally.Misses++;
                        Log(shots, request, battery, index, CombatCodes.Miss, roll, chance, 0);
                        continue;
                    }
                    var damage = onPlatform
                        ? HitDamage(battery, training, cover, defender.PlatformArmor, defender.PlatformUnitHealth, splash, platformHealth)
                        : HitDamage(battery, training, cover, defender.ArmorRating, defender.MemberHealth, splash, health);
                    if (onPlatform) platformHealth = Math.Max(0, platformHealth - damage);
                    else health = Math.Max(0, health - damage);
                    tally.Hits++;
                    tally.Damage += damage;
                    hitsTaken++;
                    Log(shots, request, battery, index, CombatCodes.Hit, roll, chance, damage);
                }
            }
            reports.Add(new BatteryReport(battery.Id, earned, tally.Fired, tally.Hits, tally.Misses, tally.OutOfRange, tally.NoAmmo, tally.Wasted, tally.Damage));
            spent.Add(battery with { Rounds = rounds, CarryMilli = total % 1000 });
        }

        var after = StandingRule.After(Rules, defender, health, hitsTaken, platformHealth);
        if (after.Routed && !defender.Routed) codes.Add(CombatCodes.Routed);
        if (after.Suppressed) codes.Add(CombatCodes.Suppressed);
        if (after.Destroyed) codes.Add(CombatCodes.Destroyed);
        var firer = attacker with { Batteries = spent };
        return new EngagementResult(firer, after, reports, shots, codes, StateHash.Of(firer, after));
    }

    /// <summary>How many weapons of the battery fire: those the survivors can work. A platform battery follows the platforms that are standing and crewed, the rest follow the people.</summary>
    private static long FiringUnits(Group attacker, Battery battery) =>
        battery.Platform && attacker.InitialPlatforms > 0
            ? (battery.Count * attacker.OperablePlatforms + attacker.InitialPlatforms - 1) / attacker.InitialPlatforms
            : Math.Min((battery.Count * attacker.Members + attacker.InitialMembers - 1) / attacker.InitialMembers,
                battery.CrewPerUnit > 0 ? attacker.Members / battery.CrewPerUnit : long.MaxValue);

    private bool PlatformTakesHit(EngagementRequest request, Group attacker, Battery battery, long index, long personnelHealth, long platformHealth)
    {
        var share = platformHealth * 1000 / (personnelHealth + platformHealth);
        return CombatRoll.Permille(request.Seed, request.Attempt, attacker.Id, battery.Id + "#pool", index) < share;
    }

    private long HitDamage(Battery battery, TrainingRule training, CoverRule cover, long armorRating, long unitHealth, long splash, long healthBefore)
    {
        var weapon = battery.Weapon;
        var type = battery.Ammo?.DamageType ?? weapon.DamageType;
        var damage = DamageRule.Compute(Rules, weapon.Damage + (battery.Ammo?.Damage ?? 0), type, weapon.Penetration + (battery.Ammo?.Penetration ?? 0), armorRating, training.DamagePercent, cover.DamagePercent);
        if (splash <= 0) return Math.Min(damage, healthBefore);
        var alive = (healthBefore + unitHealth - 1) / unitHealth;
        var extra = Math.Min(Math.Min(Rules.SplashMaxTargets - 1, splash / Rules.SplashSpacingMilli), alive - 1);
        var total = damage + Math.Max(0, extra) * (damage * Rules.SplashDamagePercent / 100);
        return Math.Min(total, healthBefore);
    }

    private static void Log(List<ShotRecord> shots, EngagementRequest request, Battery battery, long index, string code, long roll, long chance, long damage)
    {
        if (shots.Count < request.LoggedShots) shots.Add(new ShotRecord(battery.Id, index, code, roll, chance, damage));
    }

    private void Validate(EngagementRequest request)
    {
        if (request.Seconds <= 0) throw new CombatRefusedException(CombatCodes.BadRequest, "an engagement lasts at least a second");
        if (request.Seconds > Rules.MaxEngagementSeconds) throw new CombatRefusedException(CombatCodes.BadRequest, "an engagement lasts at most " + Rules.MaxEngagementSeconds + " seconds");
        if (request.DistanceMilli < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "a distance cannot be negative");
        if (request.Attempt < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "the attempt cannot be negative");
        if (request.LoggedShots < 0) throw new CombatRefusedException(CombatCodes.BadRequest, "the number of logged shots cannot be negative");
        if (request.Attacker.Side == request.Defender.Side) throw new CombatRefusedException(CombatCodes.SameSide, "groups '" + request.Attacker.Id + "' and '" + request.Defender.Id + "' are on the same side");
        ValidateGroup(request.Attacker);
        ValidateGroup(request.Defender);
        Rules.CoverLevel(request.Cover);
        Rules.TerrainType(request.Terrain);
    }

    /// <summary>A group with a negative or impossible number is refused whole; the engine never guesses what it meant.</summary>
    private void ValidateGroup(Group group)
    {
        if (group.InitialMembers <= 0 || group.MemberHealth <= 0) throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' needs members with health");
        if (group.Health < 0 || group.ArmorRating < 0 || group.SpeedMilli < 0 || group.PlatformHealth < 0 || group.PlatformArmor < 0 || group.CrewPerPlatform < 0
            || group.InitialPlatforms < 0 || group.PlatformUnitHealth < 0)
            throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' has a negative number");
        if (group.Fuel < 0 || group.FuelCapacity < 0 || group.BurnPerHourMilli < 0 || group.Fuel > group.FuelCapacity || (group.FuelCapacity > 0 && group.InitialPlatforms <= 0) || group.Fuel > group.FuelRoom)
            throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' has a fuel tank that does not match its platforms");
        if (group.InitialPlatforms > 0 ? group.PlatformUnitHealth <= 0 || group.PlatformHealth > group.InitialPlatforms * group.PlatformUnitHealth : group.PlatformHealth != 0)
            throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' has a platform pool that does not match its platforms");
        if (group.Morale is < 0 or > 1000) throw new CombatRefusedException(CombatCodes.BadRequest, "group '" + group.Id + "' has morale outside 0 to 1000");
        Rules.TrainingLevel(group.Training);
        foreach (var battery in group.Batteries)
        {
            var weapon = battery.Weapon;
            Rules.DamageType(weapon.DamageType);
            if (battery.Ammo?.DamageType is { } ammoType) Rules.DamageType(ammoType);
            if (weapon.Damage < 0 || weapon.Penetration < 0 || weapon.RangeMilli < 0 || weapon.AccuracyPermille < 0 || weapon.RatePerMinuteMilli < 0 || weapon.SplashMilli < 0
                || battery.Count < 0 || battery.CrewPerUnit < 0 || battery.Rounds < 0 || battery.CarryMilli < 0 || battery.Capacity < 0 || (battery.Ammo is { } ammo && (ammo.Damage < 0 || ammo.Penetration < 0 || ammo.SplashMilli < 0)))
                throw new CombatRefusedException(CombatCodes.BadRequest, "battery '" + battery.Id + "' has a negative number");
            if (battery.Rounds > battery.Capacity) throw new CombatRefusedException(CombatCodes.BadRequest, "battery '" + battery.Id + "' holds more rounds than its capacity");
        }
    }

    private sealed class Tally
    {
        public long Fired, Hits, Misses, OutOfRange, NoAmmo, Wasted, Damage;
    }
}
