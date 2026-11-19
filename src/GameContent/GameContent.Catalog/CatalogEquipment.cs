namespace GameContent.Catalog;

/// <summary>A span of real years (negative is BCE) that makes an era comparable with history. Only historical packs carry one.</summary>
public sealed record EraSpan(int From, int To);

/// <summary>An era: a place in the order of time (<see cref="Order"/>, unique across the library), a technology tier, a period label and, for historical worlds, years.</summary>
public sealed record CatalogEra(string Id, string Name, int Order, int Tier, PeriodLabel Label, EraSpan? Span);

/// <summary>When one thing (resource, recipe or station) exists: from an era until an era (null: onwards). A thing with no entry is timeless.</summary>
public sealed record CatalogAvailability(string Id, string From, string? Until);

/// <summary>
/// One stat of the vocabulary: what it measures, in which unit, its legal range and the item classes it applies to. An enum stat
/// (damage type, for instance) lists its legal values. The vocabulary is data, so a game defines the stats its genre needs.
/// </summary>
public sealed record CatalogStat(string Id, string Name, string Dimension, string Unit, StatKind Kind, decimal? Min, decimal? Max,
    IReadOnlyList<string> Values, IReadOnlyList<ItemClass> Classes);

public sealed record ItemSlot(string Slot, int Count);

/// <summary>What an item can carry, in a dimension with a canonical unit: mass (kg), volume (L), count ({each}) or population ({person}).</summary>
public sealed record ItemCargo(string Dimension, decimal Amount);

public sealed record ItemCrew(string Skill, int Level, int Count);

/// <summary>The resource an item burns, how much it holds (in the resource's own unit) and how much it burns per hour of use.</summary>
public sealed record ItemFuel(string Resource, decimal Capacity, decimal BurnPerHour);

public sealed record ItemAmmo(string Resource, decimal Capacity);

public sealed record ItemUpkeep(string Resource, decimal PerDay);

/// <summary>
/// The equipment or unit side of a resource: its class, stats, slots, capacity, crew, fuel, ammunition, upkeep and the item it replaces.
/// The resource is the identity (cost, durability, availability and how it is made are the resource's and the recipes').
/// </summary>
public sealed record CatalogItem(string Resource, ItemClass Class, IReadOnlyDictionary<string, object> Stats, IReadOnlyList<ItemSlot> Slots,
    IReadOnlyList<ItemCargo> Cargo, IReadOnlyList<ItemCrew> Crew, ItemFuel? Fuel, ItemAmmo? Ammo, IReadOnlyList<ItemUpkeep> Upkeep, string? Replaces);

/// <summary>An item a formation carries and how many of them.</summary>
public sealed record UnitEquipment(string Item, int Count);

/// <summary>
/// A formation type: a count of people with their equipment, crew skills, training and upkeep. The resource is the formation (one unit
/// of it is one formation); raising it is an ordinary recipe. Numbers about fighting come from the items, not from here.
/// </summary>
public sealed record CatalogFormation(string Resource, int Strength, string Role, TrainingLevel Training, IReadOnlyList<UnitEquipment> Equipment,
    IReadOnlyList<ItemCrew> Crew, IReadOnlyList<ItemUpkeep> Upkeep, string? Replaces);
