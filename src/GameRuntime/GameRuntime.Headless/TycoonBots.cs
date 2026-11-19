using AIEngine;

namespace GameRuntime.Headless;

/// <summary>
/// The players a run can choose with <c>--player</c>. Each is a behaviour of one AI document (<c>tycoon-bots.json</c>): how big a lot to sell, when to wait, how a refusal
/// shrinks the lot and when to give up are written there, and nowhere in code. The seed only draws how big the first lot is, inside the range the document gives.
/// </summary>
public static class TycoonBots
{
    private static readonly Lazy<AiSet> Document = new(() =>
    {
        using var stream = typeof(TycoonBots).Assembly.GetManifestResourceStream("tycoon-bots.json") ?? throw new InvalidOperationException("the program does not carry tycoon-bots.json");
        using var reader = new StreamReader(stream);
        return AiLoader.Load(reader.ReadToEnd());
    });

    public static AiSet Set => Document.Value;

    public static string Names => "none|" + string.Join('|', Set.Behaviors.Keys.Order(StringComparer.Ordinal));

    /// <summary>The bot for a name, or null for <c>none</c> (no player: only the script, if any, plays).</summary>
    public static TycoonBot? Create(string name, ulong seed, long goalProfit)
    {
        if (name == "none") return null;
        return Set.Behaviors.ContainsKey(name) ? new TycoonBot(name, new PlayerRandom(seed), seed, goalProfit) : throw new UsageException("--player must be one of " + Names);
    }
}