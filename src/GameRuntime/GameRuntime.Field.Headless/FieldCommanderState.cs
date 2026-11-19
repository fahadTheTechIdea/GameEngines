namespace GameRuntime.Field.Headless;

/// <summary>
/// What the commander has to remember to continue after a load: whether the field is raised, which squad entity each combat group
/// stands for, how many members each squad has left and how many rounds were fought. Everything else it learns again from events.
/// </summary>
public sealed class FieldCommanderState
{
    public bool Requested { get; set; }
    public bool Spawned { get; set; }
    public bool Over { get; set; }
    public int Round { get; set; }
    public int StrengthA { get; set; }
    public int StrengthB { get; set; }
    public SortedDictionary<string, int> SquadOf { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<int, int> Headcount { get; set; } = new();
}
