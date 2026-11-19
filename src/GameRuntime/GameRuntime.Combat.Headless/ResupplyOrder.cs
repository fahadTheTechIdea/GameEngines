namespace GameRuntime.Combat.Headless;

/// <summary>Rounds the commander took from its stock go into a group battery; the answer says how many fitted.</summary>
public sealed record ResupplyOrder(string Group, string Battery, int Rounds) : SkirmishOrder
{
    public override string Action => SkirmishActions.Resupply;

    public override string Key => $"{Group}|{Battery}|{Rounds}";

    public override void Request(IEventPort events, string requestId, SkirmishView view) =>
        events.Emit(new ResupplyRequested(requestId, Group, Battery, Rounds));

    public static ResupplyOrder Parse(string argument)
    {
        var parts = argument.Split('|');
        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0) throw new SkirmishUsageException("resupply needs group|battery|rounds but got '" + argument + "'");
        return new ResupplyOrder(parts[0], parts[1], Whole(parts[2], "the rounds", argument));
    }
}
