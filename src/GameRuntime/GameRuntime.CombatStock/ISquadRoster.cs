namespace GameRuntime;

/// <summary>
/// Which squad stands for a combat group, how many people it was raised with, and which depot feeds it
/// (none: new members are spread over the roster by the character factory). The game owns the answers; the stock layer asks.
/// </summary>
public interface ISquadRoster
{
    bool TrySquad(string group, out int squad, out int strength);

    /// <summary>The depot entity that supplies and reinforces the group, or false when its side has none.</summary>
    bool TryDepot(string group, out int depot);

}
