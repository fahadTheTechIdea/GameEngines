namespace GameRuntime.Alchemy.Headless;

/// <summary>The policies a run can choose with <c>--player</c>. Each is built from the run's seed, so the same seed plays the same game.</summary>
public static class AlchemyPolicies
{
    private static readonly IReadOnlyDictionary<string, Func<ulong, IAlchemyPolicy>> Known =
        new Dictionary<string, Func<ulong, IAlchemyPolicy>>(StringComparer.Ordinal)
        {
            ["curious"] = seed => new CuriousPolicy(seed),
            ["methodical"] = _ => new MethodicalPolicy(),
        };

    public static string Names => "none|" + string.Join('|', Known.Keys.Order(StringComparer.Ordinal));

    /// <summary>The policy for a name, or null for <c>none</c> (no bot: only the script, if any, plays).</summary>
    public static IAlchemyPolicy? Create(string name, ulong seed)
    {
        if (name == "none") return null;
        return Known.TryGetValue(name, out var create)
            ? create(seed)
            : throw new AlchemyUsageException("--player must be one of " + Names);
    }
}
