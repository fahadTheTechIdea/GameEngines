using System.Reflection;
using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Every refusal code the engine has, with one input that makes it refuse. A code added to <see cref="CalendarCodes"/> without a row here fails the second test.</summary>
public sealed class RefusalMatrixTests
{
    private static JsonNode Parse(string text) => JsonNode.Parse(text)!;

    private static Action Load(Action<JsonObject> edit) => () => CalendarLoader.Load(Fixture.MutateGregorian(edit));

    private static Action LoadText(string text) => () => CalendarLoader.Load(text);

    private static CalendarContext Context(IReadOnlyDictionary<string, FactValue>? facts = null) => CalendarContext.At(Fixture.Gregorian, Fixture.At(Fixture.Gregorian, 2024, "january", 10, 10), facts);

    private static readonly Dictionary<string, Action> Rows = new()
    {
        [CalendarCodes.Format] = LoadText("{ not json"),
        [CalendarCodes.UnknownField] = Load(r => r["extra"] = 1),
        [CalendarCodes.MissingField] = Load(r => r.Remove("twilightMinutes")),
        [CalendarCodes.Range] = Load(r => r["twilightMinutes"] = 100_000),
        [CalendarCodes.Duplicate] = Load(r => r["months"]!.AsArray()[1]!["id"] = "january"),
        [CalendarCodes.Reference] = Load(r => r["epoch"]!["weekday"] = "someday"),
        [CalendarCodes.Leap] = Load(r => r["leap"] = new JsonArray()),
        [CalendarCodes.Clock] = Load(r => r["dayParts"]!.AsArray()[1]!["from"] = "25:00"),
        [CalendarCodes.Recurrence] = Load(r => r["holidays"]!.AsArray()[0]!["rule"]!["until"] = Parse("""{ "year": 1969, "month": "december", "day": 31 }""")),
        [CalendarCodes.Order] = Load(r => r["dayParts"]!.AsArray()[0]!["from"] = "1:00"),
        [CalendarCodes.Weeks] = LoadText(Fixture.Mutate(Fixture.ValleyText, r => r["epoch"]!["weekday"] = "mon")),
        [CalendarCodes.Date] = Load(r => r["seasons"]!.AsArray()[0]!["day"] = 40),
        [CalendarCodes.Base] = Load(r => r["schedules"]!.AsArray()[0]!["routines"]!.AsArray().RemoveAt(0)),
        [CalendarCodes.BaseCoverage] = Load(r => r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[0]!["blocks"]!.AsArray()[6]!["to"] = "23:00"),
        [CalendarCodes.Overlap] = Load(r => r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[0]!["blocks"]!.AsArray()[1]!["from"] = "6:00"),
        [CalendarCodes.Tie] = Load(r => r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[2]!["priority"] = 5),
        [CalendarCodes.Reserved] = Load(r => r["facts"]!.AsArray()[0]!["id"] = "cal.mine"),

        [CalendarCodes.DateInvalid] = () => Fixture.Gregorian.System.DayNumber(new CalendarDate(2023, 1, 29)),
        [CalendarCodes.InstantRange] = () => Fixture.Gregorian.System.MomentOf(long.MaxValue / 2),
        [CalendarCodes.FactMissing] = () => Context().WhereIs("shopkeeper"),
        [CalendarCodes.FactInvalid] = () => Context(Fixture.Facts(("weather.raining", FactValue.From(3)))),
        [CalendarCodes.UnknownSchedule] = () => Context(Fixture.Dry).WhereIs("nobody"),
        [CalendarCodes.UnknownActor] = () => CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1).RemoveActor("nobody"),
        [CalendarCodes.DuplicateActor] = () => { var r = CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1); r.AddActor("a", "guard"); r.AddActor("a", "guard"); },
        [CalendarCodes.UnknownInterrupt] = () => Context(Fixture.Dry).WhereIs("guard", "nope"),
        [CalendarCodes.Outranked] = () => { var r = CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1); r.AddActor("a", "guard"); r.Interrupt("a", "alarm", 5); r.Interrupt("a", "invitation", 5); },
        [CalendarCodes.Scale] = () => CalendarRuntime.Create(Fixture.Gregorian, 0, 0, 1),
        [CalendarCodes.Skip] = () => CalendarRuntime.Create(Fixture.Gregorian, 10, 1, 1).SkipTo(10),
        [CalendarCodes.TickOrder] = () => CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1, 5).Step(4),
        [CalendarCodes.Capacity] = () => { var r = CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1); for (var i = 0; i <= CalendarRuntime.MaxActors; i++) r.AddActor("a" + i, "guard"); },
        [CalendarCodes.UnknownName] = () => CalendarNames.Format(Fixture.Gregorian, EngineCommon.LocaleChain.Create("en", [Fixture.English], "en"), "nope", Fixture.Gregorian.System.MomentOf(0)),

        [CalendarCodes.SaveFormat] = () => CalendarSave.Open(Fixture.Gregorian, "{}"u8.ToArray()),
        [CalendarCodes.SaveDigest] = () => CalendarSave.Open(Fixture.Gregorian, ChangeSave(t => t["Paused"] = true)),
        [CalendarCodes.SaveDocument] = () => CalendarSave.Open(Fixture.Gregorian, ChangeSave(t => t["Id"] = "other")),
        [CalendarCodes.SaveState] = () => CalendarSave.Open(Fixture.Gregorian, ChangeSave(t => t["ScaleDenominator"] = 0)),
    };

    private static byte[] ChangeSave(Action<JsonObject> edit)
    {
        var tree = JsonNode.Parse(CalendarSave.Write(CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1)))!.AsObject();
        edit(tree);
        return System.Text.Encoding.UTF8.GetBytes(tree.ToJsonString());
    }

    [Fact]
    public void EachCodeIsReachedByItsInputAndOnlyThatCode()
    {
        foreach (var (code, action) in Rows)
        {
            var refusal = Assert.Throws<EngineCommon.EngineRefusedException>(action);
            Assert.True(code == refusal.Code, $"expected {code} but got {refusal.Code}: {refusal.Message}");
            Assert.StartsWith("CAL_", refusal.Code);
        }
    }

    [Fact]
    public void EveryCodeOfTheEngineHasARowAndEveryRowIsACode()
    {
        var declared = typeof(CalendarCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).Order().ToArray();
        Assert.Equal(declared, Rows.Keys.Order().ToArray());
        Assert.Equal(declared.Length, declared.Distinct().Count());
    }

    [Fact]
    public void ARefusalChangesNothingTheNextRequestIsAnsweredAsIfItHadNotHappened()
    {
        var runtime = CalendarRuntime.Create(Fixture.Gregorian, 0, 1, 1);
        runtime.AddActor("a", "guard");
        var digest = CalendarSave.StateDigest(runtime);
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.AddActor("a", "shopkeeper"));
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.Interrupt("a", "nope", 5));
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.Interrupt("a", "alarm", 0));
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.SkipTo(0));
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.SetScale(0, 0));
        Assert.Throws<EngineCommon.EngineRefusedException>(() => runtime.Step(-1));
        Assert.Equal(digest, CalendarSave.StateDigest(runtime));
    }
}
