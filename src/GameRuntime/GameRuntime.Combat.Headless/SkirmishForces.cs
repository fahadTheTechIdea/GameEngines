using CombatEngine;
using GameContent.Catalog;

namespace GameRuntime.Combat.Headless;

/// <summary>The rules and the fighting groups of a run, with where they came from for the first output line.</summary>
public sealed record SkirmishForces(CombatRules Rules, IReadOnlyList<Group> Groups, string Source);

/// <summary>
/// The one place the headless game chooses its forces: the combat rules file and a formation from a catalog pack for each group, read
/// from JSON at run time and turned into groups by the catalog adapter. A new pack or formation needs no rebuild.
/// </summary>
public static class SkirmishForceSource
{
    public static string DefaultRulesPath => Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json");
    public static string DefaultPacksPath => Path.Combine(AppContext.BaseDirectory, "Packs");

    public static SkirmishForces Load(SkirmishOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rules = CombatRulesLoader.Load(File.ReadAllText(options.RulesPath ?? DefaultRulesPath));
        rules.TerrainType(options.Terrain);
        var source = new DirectoryCatalogSource(options.PacksPath ?? DefaultPacksPath);
        var slots = options.Slots();
        var groups = slots.Select(slot => Build(source, rules, slot)).ToList();
        return new SkirmishForces(rules, groups, string.Join(' ', slots.Select(s => $"{s.Letter}={s.Formation}@{s.Pack}#{s.Side}")));
    }

    private static Group Build(DirectoryCatalogSource source, CombatRules rules, SkirmishSlot slot)
    {
        var set = CatalogLoader.Load(source, slot.Pack).Required();
        if (!set.TryUnit(slot.Formation, out _))
            throw new SkirmishUsageException($"'{slot.Formation}' is not a formation of pack {slot.Pack}; it has: " + string.Join(", ", set.Root.Units.Select(u => u.Resource).Order(StringComparer.Ordinal)));
        return CatalogCombat.Group(set, rules, slot.Formation, slot.GroupId, slot.Side);
    }
}
