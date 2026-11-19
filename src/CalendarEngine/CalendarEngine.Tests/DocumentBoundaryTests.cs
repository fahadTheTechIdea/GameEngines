using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Where a document value is exactly at its limit: the limit itself is accepted and one past it is refused.</summary>
public sealed class DocumentBoundaryTests
{
    private static string Code(Action<System.Text.Json.Nodes.JsonObject> edit)
    {
        try { CalendarLoader.Load(Fixture.MutateGregorian(edit)); return "ok"; }
        catch (EngineCommon.EngineRefusedException refusal) { return refusal.Code; }
    }

    [Fact]
    public void TwoDayPartsMayNotStartAtTheSameTime()
    {
        Assert.Equal(CalendarCodes.Order, Code(r => r["dayParts"]!.AsArray()[1]!["from"] = "0:00"));
        Assert.Equal("ok", Code(r => r["dayParts"]!.AsArray()[1]!["from"] = "0:01"));
    }

    [Fact]
    public void TheTwilightMayTouchTheStartAndTheEndOfTheDayButNotCrossThem()
    {
        Assert.Equal("ok", Code(r => r["seasons"]!.AsArray()[0]!["sunrise"] = "0:30"));
        Assert.Equal(CalendarCodes.Range, Code(r => r["seasons"]!.AsArray()[0]!["sunrise"] = "0:29"));
        Assert.Equal("ok", Code(r => r["seasons"]!.AsArray()[0]!["sunset"] = "23:30"));
        Assert.Equal(CalendarCodes.Range, Code(r => r["seasons"]!.AsArray()[0]!["sunset"] = "23:31"));
    }

    [Fact]
    public void ASeasonMayStartOnTheLastDayOfAMonthButNotAfterIt()
    {
        Assert.Equal("ok", Code(r => r["seasons"]!.AsArray()[0]!["day"] = 31));
        Assert.Equal(CalendarCodes.Date, Code(r => r["seasons"]!.AsArray()[0]!["day"] = 32));
        Assert.Equal(CalendarCodes.Date, Code(r => { r["seasons"]!.AsArray()[1]!["day"] = 31; }));   // june has 30 days
    }

    [Fact]
    public void TwoErasMayNotStartInTheSameYear()
    {
        Assert.Equal(CalendarCodes.Order, Code(r => r["eras"]!.AsArray()[1]!["from"] = -100000));
        Assert.Equal("ok", Code(r => r["eras"]!.AsArray()[1]!["from"] = -99999));
        Assert.Equal(CalendarCodes.Order, Code(r => r["eras"]!.AsArray()[0]!["from"] = -99999));
    }
}
