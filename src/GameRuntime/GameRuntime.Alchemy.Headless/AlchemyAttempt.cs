namespace GameRuntime.Alchemy.Headless;

/// <summary>One combination a policy wants to try: items joined by a plus sign and at most one environment tag (empty for none).</summary>
public sealed record AlchemyAttempt(string Items, string Environment)
{
    /// <summary>The input argument, also the identity of the attempt (<c>items|environment</c>, the same shape as the book's tried keys).</summary>
    public string Key => Items + "|" + Environment;
}
