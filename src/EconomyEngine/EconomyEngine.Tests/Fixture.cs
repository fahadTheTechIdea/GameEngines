using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>The sample document and the small helpers the tests share.</summary>
internal static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly string TownText = Text("town.economy.json");
    public static readonly EconomySet Town = EconomyLoader.Load(TownText);

    public static IReadOnlyDictionary<string, FactValue> Host(string season = "spring", int plague = 0) => new Dictionary<string, FactValue>
    {
        ["cal.season"] = FactValue.From(season), ["story.plague"] = FactValue.From(plague),
    };

    public static readonly IReadOnlyDictionary<string, FactValue> Spring = Host();

    public static readonly IReadOnlyDictionary<string, FactValue> NoFacts = new Dictionary<string, FactValue>();

    /// <summary>The code of the refusal an action ends in; fails the test when it does not refuse.</summary>
    public static string Refusal(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    /// <summary>The same, naming the case in the failure.</summary>
    public static string Refusal(Action action, string name)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException(name + " was not refused");
    }

    public static EconomyRuntime Runtime(long step = 0) => EconomyRuntime.Create(Town, step);

    /// <summary>Lives the economy forward by whole periods, collecting what changed.</summary>
    public static EconomyChanges Periods(EconomyRuntime runtime, int periods, IReadOnlyDictionary<string, FactValue>? facts = null)
    {
        var all = new EconomyChanges();
        for (var i = 0; i < periods; i++)
        {
            var changes = runtime.Advance(runtime.Step + runtime.Set.PeriodSteps, facts ?? Spring);
            all.Rates.AddRange(changes.Rates); all.Shocks.AddRange(changes.Shocks); all.Arrivals.AddRange(changes.Arrivals); all.Bands.AddRange(changes.Bands);
            all.Shortages.AddRange(changes.Shortages); all.Launches.AddRange(changes.Launches); all.Blocks.AddRange(changes.Blocks); all.Settlements.AddRange(changes.Settlements); all.Loans.AddRange(changes.Loans); all.Limits.AddRange(changes.Limits); all.Unlocks.AddRange(changes.Unlocks);
        }
        return all;
    }
}
