namespace GameRuntime.AlchemySim.Headless;

/// <summary>What a policy wants done: try a combination, or order a recipe. Exactly one of the two is set.</summary>
public sealed record WorkshopAction(string? Items, string? Environment, string? Recipe, int Count)
{
    public static WorkshopAction Combine(string items, string environment) => new(items, environment, null, 0);
    public static WorkshopAction Order(string recipe, int count) => new(null, null, recipe, count);

    /// <summary>The input action this decision presses, and its argument.</summary>
    public (string Action, string Argument) Key => Recipe is null
        ? (WorkshopActions.Combine, Items + "|" + Environment)
        : (WorkshopActions.Order, Recipe + "|" + Count);

    public string Describe() => Recipe is null
        ? "tries " + Items + (Environment!.Length == 0 ? "" : " in " + Environment)
        : "orders " + Recipe + " x" + Count;
}
