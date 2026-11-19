namespace GameRuntime.Headless;

/// <summary>A sale the mission refused, with what the player knew when it asked.</summary>
public readonly record struct RefusedSale(string CommandId, int Units, long AskedAtTick, string Code);

/// <summary>
/// Everything the player knows, and only what it learned from events: it never reads the sim or the mission directly.
/// <see cref="PlayerComponent"/> updates it from the event port; a policy reads it to decide.
/// </summary>
public sealed class PlayerView
{
    public PlayerView(long goalProfit) { GoalProfit = goalProfit; }

    public long GoalProfit { get; }
    public long Tick { get; internal set; }
    public bool Founded { get; internal set; }
    public bool Ended { get; internal set; }
    /// <summary>Units left in stock as of the last founded or sold event; -1 until the company is founded.</summary>
    public long Stock { get; internal set; } = -1;
    public long Profit { get; internal set; }
    public long Cash { get; internal set; }
    public int ObjectivesCompleted { get; internal set; }
    /// <summary>Sales asked for and not yet answered by the mission.</summary>
    public int SalesInFlight => InFlight.Count;
    /// <summary>Sales refused since the policy last decided.</summary>
    public IReadOnlyList<RefusedSale> Refusals => RefusalList;

    internal Dictionary<string, (long Tick, int Units)> InFlight { get; } = new(StringComparer.Ordinal);
    internal List<RefusedSale> RefusalList { get; } = new();
}
