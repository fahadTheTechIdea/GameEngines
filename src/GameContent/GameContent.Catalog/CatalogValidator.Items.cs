namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    private static readonly IReadOnlySet<ItemClass> Vehicles = new HashSet<ItemClass> { ItemClass.VehicleLand, ItemClass.VehicleSea, ItemClass.VehicleAir, ItemClass.VehicleSpace };
    private static readonly IReadOnlySet<ResourceClass> ItemResourceClasses = new HashSet<ResourceClass> { ResourceClass.Equipment, ResourceClass.Tool, ResourceClass.Unique };
    private static readonly IReadOnlySet<string> FuelDimensions = new HashSet<string> { CatalogUnits.Volume, CatalogUnits.Energy, CatalogUnits.Mass };

    /// <summary>The unit that each cargo dimension is measured in.</summary>
    public static readonly IReadOnlyDictionary<string, string> CargoUnits = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mass"] = "kg", ["volume"] = "L", ["count"] = "{each}", ["population"] = "{person}",
    };

    /// <summary>The stat vocabulary: a unit must belong to its dimension, and an enum stat lists values while a number stat has a range.</summary>
    private static void CheckStats(CatalogSet set, CatalogReport report)
    {
        foreach (var stat in set.Stats)
        {
            if (!CatalogStatUnits.Allows(stat.Dimension, stat.Unit))
                report.Add(CatalogCodes.StatUnit, "'" + stat.Unit + "' is not a unit of " + stat.Dimension, stat.Id);
            if (stat.Kind == StatKind.Enum)
            {
                if (stat.Values.Count == 0) report.Add(CatalogCodes.StatShape, "an enum stat lists its values", stat.Id);
                if (stat.Dimension != "none" || stat.Min is not null || stat.Max is not null) report.Add(CatalogCodes.StatShape, "an enum stat is dimensionless and has no range", stat.Id);
            }
            else
            {
                if (stat.Values.Count > 0) report.Add(CatalogCodes.StatShape, "only an enum stat lists values", stat.Id);
                if (stat.Min is { } min && stat.Max is { } max && min > max) report.Add(CatalogCodes.StatShape, "the minimum " + min + " is above the maximum " + max, stat.Id);
                if (stat.Kind == StatKind.Integer && (stat.Min is { } a && a != decimal.Truncate(a) || stat.Max is { } b && b != decimal.Truncate(b)))
                    report.Add(CatalogCodes.StatShape, "an integer stat has whole-number limits", stat.Id);
            }
        }
    }

    /// <summary>Items as written: whose resource they are, their stats against the vocabulary, their links to fuel, ammunition, crew and upkeep, and what they replace.</summary>
    private static void CheckItems(CatalogSet set, CatalogReport report)
    {
        foreach (var group in set.Items.GroupBy(i => i.Resource, StringComparer.Ordinal).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.DuplicateId, "'" + group.Key + "' is described as an item " + group.Count() + " times", group.Key);
        foreach (var pack in set.Packs)
            foreach (var item in pack.Items)
            {
                var id = item.Resource;
                if (!set.TryResource(id, out var resource)) { report.Add(CatalogCodes.UnknownRef, "item resource '" + id + "' does not exist", id); continue; }
                if (!pack.Resources.Any(r => r.Id == id)) report.Add(CatalogCodes.ItemResource, "an item describes a resource of its own pack, and '" + id + "' belongs to another", id);
                if (item.Class == ItemClass.Ammunition)
                {
                    if (resource.Class != ResourceClass.Consumable || resource.Unit != "{each}")
                        report.Add(CatalogCodes.ItemResource, "ammunition is a consumable counted in {each}, not a " + Names<ResourceClass>.Of(resource.Class) + " in " + resource.Unit, id);
                }
                else if (!ItemResourceClasses.Contains(resource.Class))
                    report.Add(CatalogCodes.ItemResource, "a " + Names<ResourceClass>.Of(resource.Class) + " resource cannot be an item; use equipment, tool or unique", id);
                if (pack.World == WorldKind.Historical && item.Class == ItemClass.VehicleSpace)
                    report.Add(CatalogCodes.ItemWorld, "a historical world has no spacecraft", id);
                CheckItemStats(set, pack, item, report);
                CheckItemLinks(set, pack, item, report);
            }
    }

    private static void CheckItemStats(CatalogSet set, CatalogPackData pack, CatalogItem item, CatalogReport report)
    {
        foreach (var (name, value) in item.Stats.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            if (!Reference(pack, name, "stat", item.Resource, set.TryStat(name, out _), report)) continue;
            var stat = set.Stats.First(s => s.Id == name);
            if (!stat.Classes.Contains(item.Class))
                report.Add(CatalogCodes.ItemStat, "stat '" + name + "' does not apply to a " + Names<ItemClass>.Of(item.Class), item.Resource);
            if (stat.Kind == StatKind.Enum)
            {
                if (value is not string text || !stat.Values.Contains(text)) report.Add(CatalogCodes.ItemStat, "stat '" + name + "' takes one of " + string.Join(", ", stat.Values), item.Resource);
            }
            else if (value is not decimal number) report.Add(CatalogCodes.ItemStat, "stat '" + name + "' is a number in " + stat.Unit, item.Resource);
            else
            {
                if (stat.Kind == StatKind.Integer && number != decimal.Truncate(number)) report.Add(CatalogCodes.ItemStat, "stat '" + name + "' is a whole number", item.Resource);
                if (stat.Min is { } min && number < min || stat.Max is { } max && number > max)
                    report.Add(CatalogCodes.ItemStat, "stat '" + name + "' is " + number + " " + stat.Unit + ", outside " + stat.Min + " to " + stat.Max, item.Resource);
            }
        }
    }

    private static void CheckItemLinks(CatalogSet set, CatalogPackData pack, CatalogItem item, CatalogReport report)
    {
        var id = item.Resource;
        if (item.Class == ItemClass.Ammunition && (item.Crew.Count > 0 || item.Fuel is not null || item.Ammo is not null || item.Slots.Count > 0 || item.Upkeep.Count > 0))
            report.Add(CatalogCodes.ItemLink, "ammunition has no crew, fuel, ammunition, slots or upkeep of its own", id);
        if (item.Cargo.Count > 0 && !(Vehicles.Contains(item.Class) || item.Class is ItemClass.Mount or ItemClass.StructureUnit))
            report.Add(CatalogCodes.ItemLink, "a " + Names<ItemClass>.Of(item.Class) + " carries no cargo", id);
        foreach (var group in item.Cargo.GroupBy(c => c.Dimension).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.ItemLink, "cargo dimension '" + group.Key + "' appears twice", id);
        foreach (var cargo in item.Cargo.Where(c => c.Dimension == "count" || c.Dimension == "population"))
            if (cargo.Amount != decimal.Truncate(cargo.Amount)) report.Add(CatalogCodes.ItemLink, "cargo in " + CargoUnits[cargo.Dimension] + " is a whole number", id);

        foreach (var crew in item.Crew)
        {
            if (!Reference(pack, crew.Skill, "crew skill", id, set.TrySkill(crew.Skill, out _), report)) continue;
            set.TrySkill(crew.Skill, out var skill);
            if (crew.Level > skill.Max) report.Add(CatalogCodes.SkillLevel, "crew need level " + crew.Level + " of '" + crew.Skill + "', which tops out at " + skill.Max, id);
        }

        if (item.Fuel is { } fuel && Reference(pack, fuel.Resource, "fuel", id, set.TryResource(fuel.Resource, out _), report))
        {
            set.TryResource(fuel.Resource, out var burned);
            if (!CatalogUnits.TryGet(burned.Unit, out var unit) || !FuelDimensions.Contains(unit.Dimension))
                report.Add(CatalogCodes.ItemLink, "fuel '" + fuel.Resource + "' is measured in " + burned.Unit + "; fuel is a volume, an energy or a mass", id);
            if (fuel.BurnPerHour > fuel.Capacity) report.Add(CatalogCodes.ItemLink, "burns " + fuel.BurnPerHour + " an hour but holds only " + fuel.Capacity, id);
            if (unit is { Discrete: true } && (fuel.Capacity != decimal.Truncate(fuel.Capacity))) report.Add(CatalogCodes.ItemLink, "fuel counted in whole " + burned.Unit + " has a fractional capacity", id);
        }

        if (item.Ammo is { } ammo && Reference(pack, ammo.Resource, "ammunition", id, set.TryResource(ammo.Resource, out _), report))
        {
            set.TryResource(ammo.Resource, out var shot);
            if (!(item.Class == ItemClass.Weapon || Vehicles.Contains(item.Class) || item.Class == ItemClass.StructureUnit))
                report.Add(CatalogCodes.ItemLink, "a " + Names<ItemClass>.Of(item.Class) + " fires nothing", id);
            if (shot.Class != ResourceClass.Consumable || !CatalogUnits.TryGet(shot.Unit, out var unit) || unit.Dimension != CatalogUnits.Count)
                report.Add(CatalogCodes.ItemLink, "ammunition '" + ammo.Resource + "' must be a consumable counted in " + CargoUnits["count"], id);
            else if (ammo.Capacity != decimal.Truncate(ammo.Capacity)) report.Add(CatalogCodes.ItemLink, "ammunition capacity is a whole number of rounds", id);
            else if (!set.TryItem(ammo.Resource, out var ammoItem) || ammoItem.Class != ItemClass.Ammunition) report.Add(CatalogCodes.ItemLink, "ammunition '" + ammo.Resource + "' must be described as an ammunition item", id);
        }

        foreach (var keep in item.Upkeep)
        {
            if (!Reference(pack, keep.Resource, "upkeep", id, set.TryResource(keep.Resource, out _), report)) continue;
            set.TryResource(keep.Resource, out var paid);
            if (CatalogUnits.TryGet(paid.Unit, out var unit) && unit.Discrete && keep.PerDay != decimal.Truncate(keep.PerDay))
                report.Add(CatalogCodes.ItemLink, "upkeep in whole " + paid.Unit + " is a whole number a day", id);
        }

        if (item.Replaces is { } old && Reference(pack, old, "replaced item", id, set.TryResource(old, out _), report))
        {
            if (old == id) report.Add(CatalogCodes.ItemLineage, "an item cannot replace itself", id);
            else if (!set.TryItem(old, out var oldItem)) report.Add(CatalogCodes.ItemLineage, "'" + old + "' is not an item", id);
            else if (oldItem.Class != item.Class) report.Add(CatalogCodes.ItemLineage, "a " + Names<ItemClass>.Of(item.Class) + " replaces a " + Names<ItemClass>.Of(oldItem.Class), id);
        }
    }

    /// <summary>Lineages never loop, never run backwards in time, and every made item can actually be made by a manufacturing recipe.</summary>
    private static void CheckItemRules(CatalogSet set, CatalogReport report)
    {
        int Start(string id) => set.TryAvailability(id, out var entry) ? OrderOf(set, entry.From) : int.MinValue;

        foreach (var item in set.Items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { item.Resource };
            for (var at = item; at.Replaces is { } next && set.TryItem(next, out var parent); at = parent)
            {
                if (!seen.Add(next)) { report.Add(CatalogCodes.ItemLineage, "the line of replacement loops back to '" + next + "'", item.Resource); break; }
            }
            if (item.Replaces is { } replaced && set.TryItem(replaced, out _) && Start(item.Resource) < Start(replaced))
                report.Add(CatalogCodes.EraLineage, "replaces '" + replaced + "', which appears in a later era", item.Resource);

            if (set.TryResource(item.Resource, out var resource) && resource.Origin == ResourceOrigin.Crafted && item.Class != ItemClass.Mount
                && !set.Recipes.Any(r => MakingKinds.Contains(r.Kind) && r.Outputs.Any(o => o.Resource == item.Resource)))
                report.Add(CatalogCodes.ItemNoBuild, "no manufacturing recipe makes this item", item.Resource);
        }
    }
}
