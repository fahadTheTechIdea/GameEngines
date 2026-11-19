namespace GameRuntime.Field.Headless;

/// <summary>Answers the stock layer from what the commander remembers and the world placed: which squad stands for a combat group, how strong it was raised, and which depot feeds its side.</summary>
public sealed class FieldSquadRoster(FieldForces forces, FieldCommanderState state, IReadOnlyList<int> depots) : ISquadRoster
{
    private readonly HashSet<string> _sideA = new(forces.SideA, StringComparer.Ordinal);

    public bool TrySquad(string group, out int squad, out int strength)
    {
        strength = 0;
        if (!state.SquadOf.TryGetValue(group, out squad)) return false;
        strength = _sideA.Contains(group) ? state.StrengthA : state.StrengthB;
        return true;
    }

    public bool TryDepot(string group, out int depot)
    {
        depot = _sideA.Contains(group) ? depots[0] : depots[1];
        return true;
    }

}
