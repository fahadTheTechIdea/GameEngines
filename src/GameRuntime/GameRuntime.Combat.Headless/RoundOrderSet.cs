namespace GameRuntime.Combat.Headless;

/// <summary>One firing group in a round.</summary>
public sealed record RoundShot(string Attacker, string Defender, int DistanceMetres, string Cover, string Terrain)
{
    public string Key => $"{Attacker}>{Defender}|{DistanceMetres}|{Cover}|{Terrain}";
}

/// <summary>Many groups on many sides fire at once for some seconds.</summary>
public sealed record RoundOrderSet(int Seconds, IReadOnlyList<RoundShot> Shots) : SkirmishOrder
{
    public override string Action => SkirmishActions.Round;

    /// <summary>The orders as the combat component reads them: entries joined by ';'.</summary>
    public string Orders => string.Join(';', Shots.Select(s => s.Key));

    public override string Key => $"{Seconds}@{Orders}";

    public override void Request(IEventPort events, string requestId, SkirmishView view)
    {
        view.Distance = Shots.Min(s => s.DistanceMetres);
        events.Emit(new RoundRequested(requestId, Orders, Seconds));
    }

    public static RoundOrderSet Parse(string argument)
    {
        var halves = argument.Split('@');
        if (halves.Length != 2) throw new SkirmishUsageException("round needs seconds@attacker>defender|metres|cover|terrain;... but got '" + argument + "'");
        var shots = new List<RoundShot>();
        foreach (var text in halves[1].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = text.Split('|');
            var sides = parts[0].Split('>');
            if (parts.Length != 4 || sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0 || parts[2].Length == 0 || parts[3].Length == 0)
                throw new SkirmishUsageException("a round order needs attacker>defender|metres|cover|terrain but got '" + text + "'");
            shots.Add(new RoundShot(sides[0], sides[1], Whole(parts[1], "the distance", argument), parts[2], parts[3]));
        }
        return shots.Count > 0 ? new RoundOrderSet(Whole(halves[0], "the seconds", argument), shots) : throw new SkirmishUsageException("a round needs at least one order");
    }
}
