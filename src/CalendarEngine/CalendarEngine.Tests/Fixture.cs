using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The sample documents, loaded once, and helpers to change one thing in a document or to expect a refusal.</summary>
internal static class Fixture
{
    private static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "samples", name);

    public static string GregorianText { get; } = File.ReadAllText(Path("gregorian.calendar.json"));
    public static string ValleyText { get; } = File.ReadAllText(Path("valley.calendar.json"));
    public static string RealmsText { get; } = File.ReadAllText(Path("realms.calendar.json"));
    public static string EnglishText { get; } = File.ReadAllText(Path("gregorian.en.json"));
    public static string ValleyEnglishText { get; } = File.ReadAllText(Path("valley.en.json"));

    public static CalendarSet Gregorian { get; } = CalendarLoader.Load(GregorianText);
    public static CalendarSet Valley { get; } = CalendarLoader.Load(ValleyText);
    public static CalendarSet Realms { get; } = CalendarLoader.Load(RealmsText);
    public static LocaleTable English { get; } = LocaleTable.Load(EnglishText);

    /// <summary>The document with one change made to its JSON tree.</summary>
    public static string Mutate(string document, Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(document)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    public static string MutateGregorian(Action<JsonObject> edit) => Mutate(GregorianText, edit);

    public static EngineRefusedException Refused(Action action)
    {
        var refusal = Assert.Throws<EngineRefusedException>(action);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Message));
        return refusal;
    }

    public static string CodeOf(Action action) => Refused(action).Code;

    /// <summary>The instant of a date and time in a set, by month id.</summary>
    public static long At(CalendarSet set, long year, string month, int day, int hour = 0, int minute = 0)
    {
        var index = set.System.Months.First(m => m.Id == month).Index;
        return set.System.InstantOf(new CalendarDate(year, index, day), hour, minute);
    }

    public static IReadOnlyDictionary<string, FactValue> Facts(params (string Id, FactValue Value)[] facts) => facts.ToDictionary(f => f.Id, f => f.Value, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, FactValue> Dry = Facts(("weather.raining", FactValue.From(false)));
    public static readonly IReadOnlyDictionary<string, FactValue> Rain = Facts(("weather.raining", FactValue.From(true)));
}
