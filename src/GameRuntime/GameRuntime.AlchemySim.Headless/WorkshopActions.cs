namespace GameRuntime.AlchemySim.Headless;

/// <summary>The input actions the workshop player understands. A script, a bot or (in Godot) an InputMap action can press them.</summary>
public static class WorkshopActions
{
    /// <summary>A tap: try a combination of known resources. The argument is <c>items</c> or <c>items|environment</c>, items joined by a plus sign.</summary>
    public const string Combine = "combine";
    /// <summary>A tap: order a transformation once its recipe is known. The argument is <c>recipe</c> or <c>recipe|count</c>.</summary>
    public const string Order = "order";
}
