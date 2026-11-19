using CombatEngine;

namespace CombatEngine.Tests;

/// <summary>Small fixed groups and the standard rules, so each test states only what it changes.</summary>
internal static class Scenes
{
    public static CombatRules Rules() => CombatRulesLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "combat-rules.json")));

    // Numbers in the units the engine takes: lengths in thousandths of a metre, accuracy in per-mille.
    public static WeaponSpec Rifle(long damage = 100, long penetration = 10, long rangeMetres = 400, long accuracyPercent = 80, long perMinute = 60, long splashMetres = 0, string type = "ballistic") =>
        new("rifle", type, damage, penetration, rangeMetres * 1000, accuracyPercent * 10, perMinute * 1000, splashMetres * 1000);

    public static Group Squad(string id = "a", int side = 0, int members = 10, long armor = 0, string training = "regular", long rounds = 1000, WeaponSpec? weapon = null, AmmoSpec? ammo = null, long health = -1)
    {
        var memberHealth = 100L;
        var battery = new Battery("main", weapon ?? Rifle(), ammo, members, rounds, 0, rounds);
        return new Group(id, side == 0 ? (id == "a" ? 1 : 2) : side, training, members, memberHealth, health < 0 ? members * memberHealth : health, armor, false, false, [battery], 850, 1400);
    }

    public static EngagementRequest Request(Group attacker, Group defender, long distanceMetres = 100, int seconds = 10, string cover = "open", ulong seed = 7, long attempt = 1, int logged = 5) =>
        new(attacker, defender, distanceMetres * 1000, seconds, cover, "open", seed, attempt, logged);
}
