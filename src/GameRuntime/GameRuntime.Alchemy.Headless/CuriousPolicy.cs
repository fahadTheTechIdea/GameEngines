namespace GameRuntime.Alchemy.Headless;

/// <summary>
/// Pokes around: on about a third of the ticks while nothing is waiting it draws a pair (now and then a triple) of known elements and
/// an environment from the seed and the tick, and tries it unless it already did. Different seeds explore in a different order.
/// </summary>
public sealed class CuriousPolicy(ulong seed) : IAlchemyPolicy
{
    private const int Draws = 40;

    public string Name => "curious";

    public IReadOnlyList<AlchemyAttempt> Decide(AlchemyView view)
    {
        if (view.Ended || view.Awaiting > 0 || view.Known.Count == 0) return Array.Empty<AlchemyAttempt>();
        if (PlayerMix.Mix(seed, view.Tick, 0) % 3 != 0) return Array.Empty<AlchemyAttempt>();
        var known = view.Known.ToArray();
        var environments = new[] { "" }.Concat(view.EnvironmentOptions).ToArray();
        for (var draw = 1; draw <= Draws; draw++)
        {
            var size = PlayerMix.Mix(seed, view.Tick, draw * 4) % 4 == 0 ? 3 : 2;
            var items = new string[size];
            for (var i = 0; i < size; i++) items[i] = known[PlayerMix.Mix(seed, view.Tick, draw * 4 + 1 + i) % (ulong)known.Length];
            var environment = size == 2 ? environments[PlayerMix.Mix(seed, view.Tick, draw * 4 + 3) % (ulong)environments.Length] : "";
            var attempt = new AlchemyAttempt(string.Join('+', items), environment);
            if (!view.Tried.Contains(attempt.Key)) return [attempt];
        }
        // Every draw was something already tried: it does nothing this tick and draws again on a later one.
        return Array.Empty<AlchemyAttempt>();
    }
}
