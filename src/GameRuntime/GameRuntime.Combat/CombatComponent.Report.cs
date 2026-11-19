using CombatEngine;

namespace GameRuntime;

public sealed partial class CombatComponent
{
    /// <summary>Tells what happened to one group in a request: a change of standing, members lost and rounds spent.</summary>
    private static void Report(IEventPort events, long tick, string requestId, Group before, Group after)
    {
        if (before.Standing != after.Standing) events.Emit(new Standing(requestId, tick, after.Id, after.Standing, after.Morale));
        if (after.Members < before.Members) events.Emit(new Casualty(requestId, tick, after.Id, before.Members - after.Members, after.Members));
        if (after.Platforms < before.Platforms) events.Emit(new PlatformsLost(requestId, tick, after.Id, before.Platforms - after.Platforms, after.Platforms, before.Fuel - after.Fuel));
        for (var i = 0; i < before.Batteries.Count; i++)
        {
            var spent = before.Batteries[i].Rounds - after.Batteries[i].Rounds;
            if (spent > 0) events.Emit(new AmmoSpent(requestId, tick, after.Id, after.Batteries[i].Id, spent, after.Batteries[i].Rounds));
        }
    }
}
