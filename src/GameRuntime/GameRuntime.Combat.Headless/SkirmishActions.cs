namespace GameRuntime.Combat.Headless;

/// <summary>The input actions the commander understands. A script, a bot or (in Godot) an InputMap action can press them.</summary>
public static class SkirmishActions
{
    /// <summary>A tap: order an engagement. Argument <c>attacker&gt;defender|metres|seconds|cover|terrain|mutual-or-one-way</c>.</summary>
    public const string Engage = "engage";

    /// <summary>A tap: march a group toward its opponent. Argument <c>group|distance-metres|seconds|terrain</c>; the answer says the new distance.</summary>
    public const string March = "march";

    /// <summary>A tap: order a round of simultaneous fire by many groups. Argument <c>seconds@attacker&gt;defender|metres|cover|terrain;...</c>.</summary>
    public const string Round = "round";

    /// <summary>A tap: put rounds into a group battery. Argument <c>group|battery|rounds</c>.</summary>
    public const string Resupply = "resupply";

    public static readonly string[] All = [Engage, March, Round, Resupply];
}
