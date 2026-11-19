namespace GameRuntime.Combat.Headless;

/// <summary>A group marches for some seconds over some terrain, closing the distance to its opponent as the commander holds it.</summary>
public sealed record MarchOrder(string Group, int DistanceMetres, int Seconds, string Terrain) : SkirmishOrder
{
    public override string Action => SkirmishActions.March;

    public override string Key => $"{Group}|{DistanceMetres}|{Seconds}|{Terrain}";

    public override void Request(IEventPort events, string requestId, SkirmishView view) =>
        events.Emit(new MoveRequested(requestId, Group, DistanceMetres, Seconds, Terrain));

    public static MarchOrder Parse(string argument)
    {
        var parts = argument.Split('|');
        if (parts.Length != 4 || parts[0].Length == 0 || parts[3].Length == 0) throw new SkirmishUsageException("march needs group|metres|seconds|terrain but got '" + argument + "'");
        return new MarchOrder(parts[0], Whole(parts[1], "the distance", argument), Whole(parts[2], "the seconds", argument), parts[3]);
    }
}
