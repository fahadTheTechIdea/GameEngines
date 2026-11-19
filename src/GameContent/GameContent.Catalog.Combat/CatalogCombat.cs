using CombatEngine;

namespace GameContent.Catalog;

/// <summary>
/// The bridge from the catalog to the combat engine, and the only code that knows the catalog's stat ids. Every number a fight uses is
/// read from an item or a formation here; nothing is defaulted, and an item that lacks a stat combat needs is refused with a code.
/// Lengths become thousandths of a metre, rates thousandths of a shot per minute and accuracy per-mille.
/// </summary>
public static class CatalogCombat
{
    /// <summary>1 km/h is 1000 m in 3600 s: 2500/9 thousandths of a metre per second.</summary>
    private static long KilometresPerHourToMilliMetresPerSecond(decimal kilometresPerHour) => (long)Math.Round(kilometresPerHour * 2500m / 9m, MidpointRounding.ToEven);

    private static long Milli(decimal value) => (long)Math.Round(value * 1000m, MidpointRounding.ToEven);

    private static decimal? Number(CatalogItem item, string stat) => item.Stats.TryGetValue(stat, out var value) && value is decimal number ? number : null;

    private static decimal Required(CatalogItem item, string stat) =>
        Number(item, stat) ?? throw new CombatAdaptRefusedException(CombatAdaptCodes.MissingStat, "'" + item.Resource + "' has no " + stat + ", which combat needs");

    private static string Type(CatalogItem item) =>
        item.Stats.TryGetValue(GearStats.DamageType, out var value) && value is string text ? text
        : throw new CombatAdaptRefusedException(CombatAdaptCodes.MissingStat, "'" + item.Resource + "' has no " + GearStats.DamageType + ", which combat needs");

    public static CatalogItem Item(CatalogSet set, string resource) =>
        set.TryItem(resource, out var item) ? item : throw new CombatAdaptRefusedException(CombatAdaptCodes.NotAnItem, "'" + resource + "' is not an item");

    /// <summary>The weapon numbers of an item that deals damage: a weapon, or a vehicle, structure or mount that fights.</summary>
    public static WeaponSpec Weapon(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WeaponSpec(item.Resource, Type(item), (long)Required(item, GearStats.Damage), (long)Required(item, GearStats.Penetration), Milli(Required(item, GearStats.Range)),
            (long)Math.Round(Required(item, GearStats.Accuracy) * 10m, MidpointRounding.ToEven), Milli(Required(item, GearStats.RateOfFire)), Milli(Number(item, GearStats.SplashRadius) ?? 0m));
    }

    /// <summary>What an ammunition item adds to the weapon that fires it. Its damage type is optional: without one the weapon's own applies.</summary>
    public static AmmoSpec Ammo(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Class != ItemClass.Ammunition) throw new CombatAdaptRefusedException(CombatAdaptCodes.NotAmmunition, "'" + item.Resource + "' is not ammunition");
        return new AmmoSpec(item.Resource, item.Stats.TryGetValue(GearStats.DamageType, out var type) && type is string text ? text : null,
            (long)Required(item, GearStats.Damage), (long)Required(item, GearStats.Penetration), Milli(Number(item, GearStats.SplashRadius) ?? 0m));
    }

    private static bool IsPlatform(CatalogItem item) =>
        item.Class is ItemClass.VehicleLand or ItemClass.VehicleSea or ItemClass.VehicleAir or ItemClass.VehicleSpace or ItemClass.StructureUnit or ItemClass.Mount;

    /// <summary>
    /// A fighting group from a formation. Persons make the personnel pool; vehicles, structures and mounts make a separate platform pool of
    /// their structure points, in as many units as the formation holds, and need the crew their items name. Armor is the health-weighted
    /// mean of the armor worn for the people and of the armor of the platforms for the platforms. Every weapon, vehicle, structure or mount
    /// that deals damage becomes a battery of as many as the formation holds, loaded with a full load of its ammunition.
    /// </summary>
    public static Group Group(CatalogSet set, CombatRules rules, string formation, string groupId, int side)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(rules);
        GearUnits.Check(set);
        if (!set.TryUnit(formation, out var unit)) throw new CombatAdaptRefusedException(CombatAdaptCodes.NotAFormation, "'" + formation + "' is not a formation");

        decimal personsHealth = unit.Strength * (decimal)rules.PersonnelHealth;
        decimal platformHealth = 0, personalArmorWeight = 0, platformArmorWeight = 0;
        long platforms = 0, crewNeeded = 0, fuelCapacity = 0, burnPerHour = 0;
        string? fuelResource = null;
        long? slowest = null;
        var batteries = new List<Battery>();
        long armoredPersons = 0;
        foreach (var equipment in unit.Equipment)
        {
            var item = Item(set, equipment.Item);
            if (item.Class == ItemClass.Armor)
            {
                var worn = Math.Min(equipment.Count, unit.Strength - armoredPersons);
                armoredPersons += worn;
                personalArmorWeight += worn * rules.PersonnelHealth * Required(item, GearStats.ArmorRating);
            }
            else if (IsPlatform(item))
            {
                var structure = Required(item, GearStats.Structure) * equipment.Count;
                platformHealth += structure;
                platforms += equipment.Count;
                crewNeeded += item.Crew.Sum(c => (long)c.Count) * equipment.Count;
                if (item.Fuel is { } fuel)
                {
                    if (fuelResource is not null && fuelResource != fuel.Resource)
                        throw new CombatAdaptRefusedException(CombatAdaptCodes.MixedFuel, "formation '" + formation + "' burns more than one fuel ('" + fuelResource + "' and '" + fuel.Resource + "'), which one tank pool cannot hold");
                    fuelResource = fuel.Resource;
                    fuelCapacity += Milli(fuel.Capacity) * equipment.Count;
                    burnPerHour += Milli(fuel.BurnPerHour) * equipment.Count;
                }
                var pace = item.Class == ItemClass.StructureUnit ? 0 : KilometresPerHourToMilliMetresPerSecond(Required(item, GearStats.Speed));
                slowest = slowest is null ? pace : Math.Min(slowest.Value, pace);
                platformArmorWeight += structure * (Number(item, GearStats.ArmorRating) ?? 0m);
            }
            if (Number(item, GearStats.Damage) is null) continue;
            var ammo = item.Ammo is null ? null : Ammo(Item(set, item.Ammo.Resource));
            var load = item.Ammo is null ? 0 : (long)item.Ammo.Capacity * equipment.Count;
            batteries.Add(new Battery(item.Resource, Weapon(item), ammo, equipment.Count, load, 0, load, Platform: IsPlatform(item), CrewPerUnit: IsPlatform(item) ? 0 : item.Crew.Sum(c => (long)c.Count)));
        }
        var training = Names<TrainingLevel>.Of(unit.Training);
        // A unit is the whole structure over the count, rounded up, so the pool never holds more than its units can.
        var unitHealth = platforms == 0 ? 0 : (long)Math.Ceiling(platformHealth / platforms);
        var crewPerPlatform = platforms == 0 ? 0 : (crewNeeded + platforms - 1) / platforms;
        return new Group(groupId, side, training, unit.Strength, rules.PersonnelHealth, (long)personsHealth,
            personsHealth == 0 ? 0 : (long)Math.Round(personalArmorWeight / personsHealth, MidpointRounding.ToEven), false, false, batteries,
            rules.TrainingLevel(training).MoralePermille, slowest ?? rules.FootSpeedMilliPerSecond,
            PlatformHealth: (long)platformHealth, PlatformUnitHealth: unitHealth, InitialPlatforms: platforms,
            PlatformArmor: platformHealth == 0 ? 0 : (long)Math.Round(platformArmorWeight / platformHealth, MidpointRounding.ToEven), CrewPerPlatform: crewPerPlatform,
            Fuel: fuelCapacity, FuelCapacity: fuelCapacity, BurnPerHourMilli: burnPerHour, FuelType: fuelResource);
    }
}
