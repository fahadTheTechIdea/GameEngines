namespace GameRuntime.Alchemy.Headless;

/// <summary>The input actions the alchemy player understands. A script, a bot or (in Godot) an InputMap action can press them.</summary>
public static class AlchemyActions
{
    /// <summary>A tap: try a combination. The argument is <c>items</c> or <c>items|environment</c>, items joined by a plus sign and tags by commas.</summary>
    public const string Combine = "combine";
}
