namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// Tries every combination in a fixed order (pairs, then triples, over the elements it knows, each pair also under every environment
/// tag) and takes the first one it has not tried, on every fourth tick while nothing is waiting, which leaves quiet ticks in between where a checkpoint can be taken. It always finishes the book, slowly.
/// </summary>
public sealed class MethodicalPolicy : IAlchemyPolicy
{
    public string Name => "methodical";

    public IReadOnlyList<AlchemyAttempt> Decide(AlchemyView view)
    {
        if (view.Ended || view.Awaiting > 0 || view.Tick % 4 != 0) return Array.Empty<AlchemyAttempt>();
        var known = view.Known.ToArray();
        var environments = new[] { "" }.Concat(view.EnvironmentOptions).ToArray();
        foreach (var a in known)
            foreach (var b in known)
                foreach (var environment in environments)
                {
                    var attempt = new AlchemyAttempt(a + "+" + b, environment);
                    if (!view.Tried.Contains(attempt.Key)) return [attempt];
                }
        foreach (var a in known)
            foreach (var b in known)
                foreach (var c in known)
                {
                    var attempt = new AlchemyAttempt(a + "+" + b + "+" + c, "");
                    if (!view.Tried.Contains(attempt.Key)) return [attempt];
                }
        return Array.Empty<AlchemyAttempt>();
    }
}
