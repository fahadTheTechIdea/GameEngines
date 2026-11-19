using System.Text.Json.Nodes;

namespace GameContent.Catalog;

public static partial class CatalogToSim
{
    /// <summary>One tile of the generated map is one kilometre, so a speed in km/h is a speed in tiles per hour.</summary>
    public const decimal TileKilometres = 1m;

    public const string TileSpace = "catalog.tiles";

    /// <summary>A vehicle item that became a mover entity: its entity id, SimEngine movement class and speed in tiles per hour.</summary>
    private sealed record Vehicle(string Id, string Class, decimal TilesPerHour);

    // Indexed by ItemClass in declaration order; only the vehicle classes have a movement class.
    private static readonly string?[] MovementClasses = [null, null, null, "land", "sea", "air", "space", null, null, null];

    private static string MovementClass(ItemClass itemClass) => MovementClasses[(int)itemClass] ?? throw new InvalidOperationException("a " + Names<ItemClass>.Of(itemClass) + " is not a vehicle");

    private static bool IsVehicle(ItemClass itemClass) => itemClass is ItemClass.VehicleLand or ItemClass.VehicleSea or ItemClass.VehicleAir or ItemClass.VehicleSpace;

    /// <summary>
    /// Each vehicle item becomes an entity with a movable tile presence, a mover (its class and its speed stat) and a storage. The speed is
    /// the item stat whose vocabulary dimension is speed; without one the vehicle cannot be a mover and is refused. Everything else about
    /// the vehicle (cargo, crew, fuel, upkeep, other stats) and every non-vehicle item is read-only catalog data: SimEngine has no combat
    /// or equipment-stat system, and says so in one note each.
    /// </summary>
    private static List<Vehicle> Vehicles(CatalogSet set, ConversionReport report)
    {
        var vehicles = new List<Vehicle>();
        foreach (var item in set.Items.Where(i => IsVehicle(i.Class)).OrderBy(i => i.Resource, StringComparer.Ordinal))
        {
            var speed = item.Stats.Select(s => (Stat: set.Stats.FirstOrDefault(d => d.Id == s.Key), s.Value)).FirstOrDefault(s => s.Stat is { Dimension: "speed" });
            if (speed.Stat is null || speed.Value is not decimal value || value <= 0)
            {
                report.Refuse(SimConversionCodes.VehicleNoSpeed, item.Resource, "a mover needs a positive speed; this vehicle has no stat measured as a speed");
                continue;
            }
            var kilometresPerHour = speed.Stat.Unit == "m/s" ? value * 3.6m : value;
            vehicles.Add(new Vehicle("catalog.vehicle." + item.Resource, MovementClass(item.Class), Math.Round(kilometresPerHour / TileKilometres, 6)));
        }
        if (vehicles.Count > 0)
            report.Reduce(SimConversionCodes.VehicleStats, "pack", vehicles.Count + " vehicles become mover entities (1 tile = " + TileKilometres + " km); their cargo, crew, fuel, upkeep and other stats are not modelled");
        var others = set.Items.Count(i => !IsVehicle(i.Class));
        if (others > 0)
            report.Reduce(SimConversionCodes.NoCombatEngine, "pack", others + " weapons, armour, tools, mounts and structure units stay catalog data: SimEngine has no combat or equipment-stat system (a separate combat layer would consume the stat blocks)");
        var gated = set.Availability.Count();
        if (gated > 0)
            report.Reduce(SimConversionCodes.EraNotModelled, "pack", gated + " things have era availability: SimEngine has no eras (its mechanism is a technology unlock, which only gates player orders)");
        return vehicles;
    }

    private static JsonObject NavigationDocument(List<Vehicle> vehicles)
    {
        var entries = new JsonArray();
        foreach (var movementClass in vehicles.Select(v => v.Class).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            entries.Add(new JsonObject { ["id"] = movementClass, ["space"] = TileSpace, ["cost"] = 10 });
        return new JsonObject { ["uniform_tile_navigation"] = entries };
    }

    private static JsonObject VehicleEntity(Vehicle vehicle) => new()
    {
        ["id"] = vehicle.Id,
        ["kind"] = "vehicle",
        ["components"] = new JsonObject
        {
            ["tile_presence"] = new JsonObject { ["space"] = TileSpace, ["width"] = 1, ["height"] = 1, ["blocks"] = true, ["movable"] = true },
            ["mover"] = new JsonObject
            {
                ["classes"] = new JsonArray { vehicle.Class }, ["class"] = vehicle.Class, ["speed"] = vehicle.TilesPerHour,
                ["states"] = new JsonObject { ["moving"] = "catalog.working", ["stopped"] = "catalog.blocked" },
            },
            ["storage"] = new JsonObject { ["capacity_policy"] = "catalog.store", ["public"] = false },
        },
    };
}
