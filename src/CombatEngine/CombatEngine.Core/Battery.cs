namespace CombatEngine;

/// <summary>
/// A number of identical weapons in a group. <see cref="Ammo"/> null means the weapon needs no ammunition (melee). Otherwise
/// <see cref="CrewPerUnit"/> is how many people work one weapon of a battery that is not a platform (a machine gun crew); the weapons fire only while the people alive can crew them. <see cref="Rounds"/> is what the group holds, up to <see cref="Capacity"/> (a full load); <see cref="CarryMilli"/> is the fraction of a
/// shot already earned, so slow weapons fire over several engagements.
/// </summary>
public sealed record Battery(string Id, WeaponSpec Weapon, AmmoSpec? Ammo, long Count, long Rounds, long CarryMilli, long Capacity, bool Platform = false, long CrewPerUnit = 0);
