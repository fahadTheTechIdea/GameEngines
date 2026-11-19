namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    /// <summary>
    /// Formations as written: a unique resource of the pack's own, equipment that are items, crew skills that exist within the formation's
    /// strength, upkeep that is paid in real resources, and a replaced formation of the same role.
    /// </summary>
    private static void CheckFormations(CatalogSet set, CatalogReport report)
    {
        foreach (var group in set.Units.GroupBy(u => u.Resource, StringComparer.Ordinal).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.DuplicateId, "'" + group.Key + "' is described as a formation " + group.Count() + " times", group.Key);
        foreach (var pack in set.Packs)
            foreach (var unit in pack.Units)
            {
                var id = unit.Resource;
                if (!set.TryResource(id, out var resource)) { report.Add(CatalogCodes.UnknownRef, "formation resource '" + id + "' does not exist", id); continue; }
                if (!pack.Resources.Any(r => r.Id == id)) report.Add(CatalogCodes.UnitResource, "a formation describes a resource of its own pack, and '" + id + "' belongs to another", id);
                if (resource.Class != ResourceClass.Unique || resource.Unit != "{each}")
                    report.Add(CatalogCodes.UnitResource, "a formation is a unique resource counted in {each}, not a " + Names<ResourceClass>.Of(resource.Class) + " in " + resource.Unit, id);

                foreach (var group in unit.Equipment.GroupBy(e => e.Item).Where(g => g.Count() > 1))
                    report.Add(CatalogCodes.UnitEquipment, "'" + group.Key + "' is listed twice; give one line with the total", id);
                foreach (var equipment in unit.Equipment)
                {
                    if (!Reference(pack, equipment.Item, "equipment", id, set.TryResource(equipment.Item, out _), report)) continue;
                    if (!set.TryItem(equipment.Item, out var item)) report.Add(CatalogCodes.UnitEquipment, "'" + equipment.Item + "' is not an item", id);
                    else if (item.Class == ItemClass.Ammunition) report.Add(CatalogCodes.UnitEquipment, "ammunition is supplied by logistics, not carried as formation equipment", id);
                }

                if (unit.Crew.Sum(c => c.Count) > unit.Strength)
                    report.Add(CatalogCodes.UnitCrew, "the crew positions add up to more than the strength of " + unit.Strength, id);
                foreach (var crew in unit.Crew)
                {
                    if (!Reference(pack, crew.Skill, "crew skill", id, set.TrySkill(crew.Skill, out _), report)) continue;
                    set.TrySkill(crew.Skill, out var skill);
                    if (crew.Level > skill.Max) report.Add(CatalogCodes.SkillLevel, "crew need level " + crew.Level + " of '" + crew.Skill + "', which tops out at " + skill.Max, id);
                }
                foreach (var keep in unit.Upkeep)
                {
                    if (!Reference(pack, keep.Resource, "upkeep", id, set.TryResource(keep.Resource, out _), report)) continue;
                    set.TryResource(keep.Resource, out var paid);
                    if (CatalogUnits.TryGet(paid.Unit, out var unitOfPaid) && unitOfPaid.Discrete && keep.PerDay != decimal.Truncate(keep.PerDay))
                        report.Add(CatalogCodes.UnitCrew, "upkeep in whole " + paid.Unit + " is a whole number a day", id);
                }

                if (unit.Replaces is { } old && Reference(pack, old, "replaced formation", id, set.TryResource(old, out _), report))
                {
                    if (old == id) report.Add(CatalogCodes.UnitLineage, "a formation cannot replace itself", id);
                    else if (!set.TryUnit(old, out var oldUnit)) report.Add(CatalogCodes.UnitLineage, "'" + old + "' is not a formation", id);
                    else if (oldUnit.Role != unit.Role) report.Add(CatalogCodes.UnitLineage, "role '" + unit.Role + "' replaces role '" + oldUnit.Role + "'", id);
                }
            }
    }

    /// <summary>Formation lineages never loop or run back in time, a formation is not older than its equipment, and every formation can be raised by a recipe.</summary>
    private static void CheckFormationRules(CatalogSet set, CatalogReport report)
    {
        int Start(string id) => set.TryAvailability(id, out var entry) ? OrderOf(set, entry.From) : int.MinValue;
        foreach (var unit in set.Units)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { unit.Resource };
            for (var at = unit; at.Replaces is { } next && set.TryUnit(next, out var parent); at = parent)
                if (!seen.Add(next)) { report.Add(CatalogCodes.UnitLineage, "the line of replacement loops back to '" + next + "'", unit.Resource); break; }
            if (unit.Replaces is { } replaced && set.TryUnit(replaced, out _) && Start(unit.Resource) < Start(replaced))
                report.Add(CatalogCodes.EraLineage, "replaces '" + replaced + "', which appears in a later era", unit.Resource);
            foreach (var equipment in unit.Equipment)
                if (Start(equipment.Item) is var made && made != int.MinValue && Start(unit.Resource) < made)
                    report.Add(CatalogCodes.EraAnachronism, "is raised before its equipment '" + equipment.Item + "' exists", unit.Resource);
            if (!set.Recipes.Any(r => MakingKinds.Contains(r.Kind) && r.Outputs.Any(o => o.Resource == unit.Resource)))
                report.Add(CatalogCodes.UnitNoBuild, "no recipe raises this formation", unit.Resource);
        }
    }
}
