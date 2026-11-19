using System.Text.Json.Nodes;
using Xunit;

namespace CalendarEngine.Tests;

public sealed class ClockEdgeTests
{
    private static string Code(string to)
    {
        var text = Fixture.MutateGregorian(r =>
        {
            var blocks = r["schedules"]!.AsArray()[0]!["routines"]!.AsArray()[0]!["blocks"]!.AsArray();
            blocks[6]!["to"] = to;
        });
        try { CalendarLoader.Load(text); return "ok"; }
        catch (EngineCommon.EngineRefusedException refusal) { return refusal.Code; }
    }

    [Fact]
    public void OnlyTheExactEndOfTheDayIsAnAcceptedEndOfBlock()
    {
        Assert.Equal("ok", Code("24:00"));
        Assert.Equal(CalendarCodes.Clock, Code("24:30"));
        Assert.Equal(CalendarCodes.Clock, Code("25:00"));
        Assert.Equal(CalendarCodes.BaseCoverage, Code("23:59"));
    }

    [Fact]
    public void ALongestMonthHolidayAndAFirstMonthWithNoDaysAreCheckedAtTheirBounds()
    {
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => r["holidays"]!.AsArray()[0]!["days"] = 32))));
        _ = CalendarLoader.Load(Fixture.MutateGregorian(r => { r["holidays"]!.AsArray()[0]!["days"] = 31; }));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r =>
        {
            var january = r["months"]!.AsArray()[0]!;
            january["days"] = 0; january["leapDays"] = 3;
        }))));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => CalendarLoader.Load(Fixture.MutateGregorian(r => r["seasons"]!.AsArray()[0]!["sunrise"] = "0:10"))));
    }
}
