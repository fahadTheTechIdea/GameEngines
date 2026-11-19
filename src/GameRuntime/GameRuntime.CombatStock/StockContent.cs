using System.Text.Json;
using System.Text.Json.Nodes;
using CombatEngine;

namespace GameRuntime;

/// <summary>
/// The inventory vocabulary the stock-flow layer needs for a set of combat groups: a unit for counted things, a slot unit and one squad
/// store, the resource of people, one resource for each ammunition type any group's weapons use, one for platform units and one for each
/// fuel its platforms burn. Generated from the groups, so the vocabulary can never name a thing the groups do not carry, nor miss one they do.
/// </summary>
public static class StockContent
{
    /// <summary>A store large enough for any squad or depot (fuel is counted in thousandths of a unit): the inventory refuses a total beyond 2^31 units in one place.</summary>
    private const long SquadStoreSlots = 2_000_000_000;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The ammunition resource names the group's batteries use, each once, in battery order.</summary>
    public static IReadOnlyList<string> AmmoResources(Group group) =>
        group.Batteries.Where(b => b.Ammo is not null).Select(b => StockResources.Ammo(b.Ammo!.Id)).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Everything a squad holds besides its people: ammunition, platform units and fuel. These are what a group takes with it when it is wiped out or routs.</summary>
    public static IReadOnlyList<string> HeldResources(Group group)
    {
        var held = new List<string>(AmmoResources(group));
        if (group.InitialPlatforms > 0) held.Add(StockResources.Platforms);
        if (group.FuelType is not null) held.Add(StockResources.Fuel(group.FuelType));
        return held;
    }

    /// <summary>The rounds the group's batteries hold for each ammunition resource.</summary>
    public static IReadOnlyDictionary<string, long> Rounds(Group group)
    {
        var rounds = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var battery in group.Batteries.Where(b => b.Ammo is not null))
        {
            var name = StockResources.Ammo(battery.Ammo!.Id);
            rounds[name] = rounds.GetValueOrDefault(name) + battery.Rounds;
        }
        return rounds;
    }

    /// <summary>What a squad starts with besides its people: rounds, platform units and fuel, by resource.</summary>
    public static IReadOnlyDictionary<string, long> Holdings(Group group)
    {
        var holdings = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (resource, rounds) in Rounds(group)) holdings[resource] = rounds;
        if (group.InitialPlatforms > 0) holdings[StockResources.Platforms] = group.Platforms;
        if (group.FuelType is not null) holdings[StockResources.Fuel(group.FuelType)] = group.Fuel;
        return holdings;
    }

    public static string Document(IEnumerable<Group> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var list = groups.ToList();
        var names = list.SelectMany(AmmoResources).Concat(list.Where(g => g.FuelType is not null).Select(g => StockResources.Fuel(g.FuelType!)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        JsonObject Unit(string id) => new() { ["id"] = id, ["dimension"] = "count", ["scale"] = new JsonObject { ["numerator"] = 1, ["denominator"] = 1 }, ["discrete"] = true };
        JsonObject Resource(string id, string category) => new()
        {
            ["id"] = id, ["unit"] = StockResources.CountUnit, ["category"] = category, ["phase"] = "solid", ["traits"] = new JsonArray(),
            ["capacity"] = new JsonObject { [StockResources.SlotUnit] = 1 },
        };
        var resources = new JsonArray { Resource(StockResources.People, "people") };
        if (list.Any(g => g.InitialPlatforms > 0)) resources.Add(Resource(StockResources.Platforms, "platforms"));
        foreach (var name in names) resources.Add(Resource(name, name.StartsWith("stock.fuel.", StringComparison.Ordinal) ? "fuel" : "ammunition"));
        var document = new JsonObject
        {
            ["units"] = new JsonArray { Unit(StockResources.CountUnit), Unit(StockResources.SlotUnit) },
            ["resources"] = resources,
            ["capacity_policies"] = new JsonArray { new JsonObject { ["id"] = StockResources.SquadStore, ["limits"] = new JsonObject { [StockResources.SlotUnit] = SquadStoreSlots } } },
        };
        return document.ToJsonString(Indented);
    }
}
