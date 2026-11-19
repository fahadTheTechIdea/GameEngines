namespace GameRuntime.Combat.Headless;

/// <summary>One group as the commander knows it, from events only: its side, health now and at the start, longest weapon range, morale, march speed and standing.</summary>
public sealed class SeenGroup(string id, int side, long startHealth, long rangeMetres, long speedMilli)
{
    public string Id { get; } = id;
    public int Side { get; } = side;
    public long StartHealth { get; } = startHealth;
    public long Health { get; set; } = startHealth;
    public long RangeMetres { get; } = rangeMetres;
    public long SpeedMilli { get; } = speedMilli;
    public long Morale { get; set; }
    public string State { get; set; } = "active";

    /// <summary>A group that can still fight: not routed and not destroyed.</summary>
    public bool Standing => State is "active" or "suppressed";
}
