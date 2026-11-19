using CombatEngine;
using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// One rule of the game's table: over this base terrain, when the region's modifier is at or below this level, the fight is on this other terrain of the combat rules (a fog that cuts visibility to 400 makes open
/// ground fight like forest; a flood that cuts mobility to 400 makes a road fight like rough ground).
/// </summary>
public sealed record WorldTerrainRule(string Base, string Modifier, int AtOrBelow, string Terrain);

/// <summary>
/// The combat engine has no weather and no clock (a terrain is an id in the order of a round), and is not changed for one. What the world can give it is data: this table maps a base terrain and the world's modifiers in
/// the region to the terrain id the round order should carry, so rain, fog and floods change the accuracy and movement the combat rules already give each terrain. Rules are tried in order; the first whose base
/// matches and whose modifier is at or below its level wins; none leaves the base terrain. Rules are checked when the game is assembled: every terrain must be one of the combat rules, every modifier one of the world's.
/// </summary>
public sealed class WorldCombatTerrain
{
    public const string BadRule = "WE_COMBAT_RULE";

    private readonly WorldComponent _world;
    private readonly List<(WorldTerrainRule Rule, int Modifier)> _rules = new();

    public WorldCombatTerrain(WorldComponent world, IEnumerable<WorldTerrainRule> rules, CombatRules combat)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(combat);
        _world = world;
        foreach (var rule in rules)
        {
            if (!world.Runtime.Set.ModifierById.TryGetValue(rule.Modifier, out var modifier)) throw new EngineRefusedException(BadRule, "the world document has no modifier '" + rule.Modifier + "'");
            if (!combat.Terrain.Any(t => t.Id == rule.Base)) throw new EngineRefusedException(BadRule, "the combat rules have no terrain '" + rule.Base + "'");
            if (!combat.Terrain.Any(t => t.Id == rule.Terrain)) throw new EngineRefusedException(BadRule, "the combat rules have no terrain '" + rule.Terrain + "'");
            _rules.Add((rule, modifier.Index));
        }
    }

    /// <summary>The terrain id a round order should carry for a fight on <paramref name="baseTerrain"/> in the region, in the world as it is now.</summary>
    public string TerrainFor(string region, string baseTerrain)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(baseTerrain);
        if (!_world.Runtime.Set.RegionById.TryGetValue(region, out var where)) throw new EngineRefusedException(WorldCodes.UnknownRegion, "there is no region '" + region + "'");
        foreach (var (rule, modifier) in _rules)
            if (rule.Base == baseTerrain && _world.Runtime.Modifier(where.Index, modifier) <= rule.AtOrBelow) return rule.Terrain;
        return baseTerrain;
    }
}
