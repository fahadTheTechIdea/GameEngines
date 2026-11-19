namespace GameRuntime.Combat.Headless;

/// <summary>Two groups fight for some seconds: one-way (only the attacker fires) or mutual (both fire at once).</summary>
public sealed record EngageOrder(string Attacker, string Defender, int DistanceMetres, int Seconds, string Cover, string Terrain, bool Mutual) : SkirmishOrder
{
    public override string Action => SkirmishActions.Engage;

    public override void Request(IEventPort events, string requestId, SkirmishView view)
    {
        view.Distance = DistanceMetres;
        events.Emit(new EngagementRequested(requestId, Attacker, Defender, DistanceMetres, Seconds, Cover, Terrain, Mutual));
    }

    public override string Key => $"{Attacker}>{Defender}|{DistanceMetres}|{Seconds}|{Cover}|{Terrain}|{(Mutual ? "mutual" : "one-way")}";

    public static EngageOrder Parse(string argument)
    {
        var parts = argument.Split('|');
        var sides = parts[0].Split('>');
        if (parts.Length != 6 || sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0 || parts[3].Length == 0 || parts[4].Length == 0 || parts[5] is not ("mutual" or "one-way"))
            throw new SkirmishUsageException("engage needs attacker>defender|metres|seconds|cover|terrain|mutual-or-one-way but got '" + argument + "'");
        return new EngageOrder(sides[0], sides[1], Whole(parts[1], "the distance", argument), Whole(parts[2], "the seconds", argument), parts[3], parts[4], parts[5] == "mutual");
    }
}
