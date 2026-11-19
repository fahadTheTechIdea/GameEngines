using System.Globalization;
using System.Text;

namespace CombatEngine;

/// <summary>A hash of the state that matters after an engagement, for checkpoints, journals and desync checks. Numbers are written in the invariant culture.</summary>
public static class StateHash
{
    public static string Of(params Group[] groups) => Of((IEnumerable<Group>)groups);

    public static string Of(IEnumerable<Group> groups)
    {
        var text = new StringBuilder("combat-state-2");
        foreach (var group in groups)
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n{group.Id}|{group.Health}|{(group.Suppressed ? 1 : 0)}|{(group.Routed ? 1 : 0)}|{group.Morale}"));
            if (group.InitialPlatforms > 0) text.Append(string.Create(CultureInfo.InvariantCulture, $"|platforms:{group.PlatformHealth}"));
            if (group.FuelCapacity > 0) text.Append(string.Create(CultureInfo.InvariantCulture, $"|fuel:{group.Fuel}"));
            foreach (var battery in group.Batteries) text.Append(string.Create(CultureInfo.InvariantCulture, $"|{battery.Id}:{battery.Rounds}:{battery.CarryMilli}"));
        }
        return EngineCommon.Digest.Of(text.ToString());
    }
}
