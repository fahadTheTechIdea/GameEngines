using System.Reflection;
using AIEngine;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// The commanders a run can choose with <c>--player</c>. Each is a behaviour of one AI document (<c>skirmish-bots.json</c>): what it orders, at what distance, from what
/// cover and when is written there, and nowhere in code. This class only knows which of them play a duel and builds a bot from the run settings.
/// </summary>
public static class SkirmishBots
{
    /// <summary>The bots that play a duel between exactly two groups; the others play any number.</summary>
    public static readonly string[] Duelists = ["aggressor", "skirmisher", "defender"];

    private static readonly Lazy<AiSet> Document = new(() =>
    {
        using var stream = typeof(SkirmishBots).Assembly.GetManifestResourceStream("skirmish-bots.json") ?? throw new InvalidOperationException("the program does not carry skirmish-bots.json");
        using var reader = new StreamReader(stream);
        return AiLoader.Load(reader.ReadToEnd());
    });

    public static AiSet Set => Document.Value;

    public static string Names => "none|" + string.Join('|', Set.Behaviors.Keys.Order(StringComparer.Ordinal));

    /// <summary>The bot for a name, or null for <c>none</c> (no bot: only the script, if any, commands).</summary>
    public static SkirmishBot? Create(string name, SkirmishPlan plan, ulong seed)
    {
        if (name == "none") return null;
        return Set.Behaviors.ContainsKey(name) ? new SkirmishBot(name, plan, seed) : throw new SkirmishUsageException("--player must be one of " + Names);
    }
}