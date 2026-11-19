using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>The sample document and the small helpers the tests share.</summary>
internal static class Fixture
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    /// <summary>The sample as shipped: the day's work is spread over its first steps (<c>"schedule": "spread"</c>).</summary>
    public static readonly string SampleText = Text("village.relationship.json");

    /// <summary>The sample with the atomic schedule: the document most tests build on, because they test what the day's passes decide, which is the same under either schedule, at a known step. The spread schedule has its own tests (DayScheduleTests).</summary>
    public static readonly string VillageText = SampleText.Replace("\"schedule\": \"spread\"", "\"schedule\": \"atomic\"", StringComparison.Ordinal);
    public static readonly RelationshipSet Village = RelationshipLoader.Load(VillageText);

    /// <summary>The sample as shipped, spread schedule included.</summary>
    public static readonly RelationshipSet Shipped = RelationshipLoader.Load(SampleText);

    public static IReadOnlyDictionary<string, FactValue> Host(string season = "spring", bool day = true, bool holiday = false, bool raining = false) => new Dictionary<string, FactValue>
    {
        ["cal.season"] = FactValue.From(season), ["cal.is_day"] = FactValue.From(day), ["cal.is_holiday"] = FactValue.From(holiday), ["weather.raining"] = FactValue.From(raining),
    };

    public static readonly IReadOnlyDictionary<string, FactValue> Spring = Host();

    /// <summary>The code of the refusal an action ends in; fails the test when it does not refuse.</summary>
    public static string Refusal(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    /// <summary>The same, naming the case in the failure.</summary>
    public static string Refusal(Action action, string name)
    {
        try { action(); }
        catch (EngineRefusedException refusal) { return refusal.Code; }
        throw new Xunit.Sdk.XunitException(name + " was not refused");
    }

    public static RelationshipRuntime Runtime(long step = 0) => RelationshipRuntime.Create(Village, step);

    public static string[] None => [];
}
