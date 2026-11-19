namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// The bot that plays the workshop. On every fourth tick, while nothing it asked is unanswered, it first orders any recipe it knows but
/// has not ordered, and otherwise tries the first combination of known resources it has not tried. It depends only on its view and the
/// tick, so the same events always lead to the same play, including after a save and load.
/// </summary>
public sealed class AlchemistPolicy
{
    public string Name => "alchemist";

    public IReadOnlyList<WorkshopAction> Decide(WorkshopView view)
    {
        if (view.Ended || view.Awaiting > 0 || view.Tick % 4 != 0) return Array.Empty<WorkshopAction>();
        foreach (var recipe in view.KnownRecipes)
            if (!view.Ordered.Contains(recipe)) return [WorkshopAction.Order(recipe, 1)];
        foreach (var a in view.Known)
            foreach (var b in view.Known)
            {
                var attempt = WorkshopAction.Combine(a + "+" + b, "");
                if (!view.Tried.Contains(attempt.Key.Argument)) return [attempt];
            }
        return Array.Empty<WorkshopAction>();
    }
}
