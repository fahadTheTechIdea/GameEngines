namespace GameRuntime.Combat.Headless;

/// <summary>Where the commander is in the approach: a march is followed by a fight, a fight by the next march. A checkpoint is only taken between a fight and the next march.</summary>
public enum SkirmishPhase { Approach, Fight }

/// <summary>What the commander knows, built only from events it has received. A policy decides from this view and nothing else.</summary>
public sealed class SkirmishView
{
    public long Tick { get; set; }
    public bool Ended { get; set; }
    public SortedDictionary<string, SeenGroup> Groups { get; } = new(StringComparer.Ordinal);
    public HashSet<string> InFlight { get; } = new(StringComparer.Ordinal);
    public int Awaiting => InFlight.Count;
    public SkirmishPhase Phase { get; set; } = SkirmishPhase.Approach;

    /// <summary>The distance between the sides as the combat component last left it; negative until a first march or fight has settled it.</summary>
    public int Distance { get; set; } = -1;

    /// <summary>True when a bot may order something: the game goes on and nothing waits for an answer. On which ticks it does is up to the bot.</summary>
    public bool Ready => !Ended && Awaiting == 0;

    /// <summary>How many requests have been answered with an engagement so far.</summary>
    public int Round { get; set; }
}
