using System.Text.Json.Nodes;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// Document format 3 (pass 47): <c>social.schedule</c> is required, and a format 2 document is refused by its own code, <see cref="RelationshipCodes.FormatTwo"/>, whether or not it carries a schedule.
/// There is no migration and no silent acceptance; a format 3 document is read as before.
/// </summary>
public sealed class FormatThreeTests
{
    [Fact]
    public void FormatTwoWithoutASocialScheduleIsRefusedByItsOwnCode()
    {
        var code = Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => { r["format"] = 2; r["social"]!.AsObject().Remove("schedule"); })), "format 2, no schedule");
        Assert.Equal(RelationshipCodes.FormatTwo, code);
        Assert.NotEqual(RelationshipCodes.Format, RelationshipCodes.FormatTwo);
        Assert.NotEqual(RelationshipCodes.MissingField, code);
    }

    [Fact]
    public void FormatTwoWithASocialScheduleIsStillRefusedNotSilentlyAccepted()
    {
        var text = Edit.Doc(r => r["format"] = 2);
        Assert.Contains("\"schedule\"", text, StringComparison.Ordinal);
        Assert.Equal(RelationshipCodes.FormatTwo, Fixture.Refusal(() => RelationshipLoader.Load(text), "format 2, with schedule"));
    }

    [Fact]
    public void TheRefusalNamesTheMissingFieldAndHowToMigrate()
    {
        var refusal = Assert.Throws<EngineCommon.EngineRefusedException>(() => RelationshipLoader.Load(Edit.Doc(r => r["format"] = 2)));
        Assert.Equal(RelationshipCodes.FormatTwo, refusal.Code);
        Assert.Contains("lack the required social.schedule", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("atomic", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("spread", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("set \"format\" to 3", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatOneKeepsItsCodeAndOtherFormatsKeepTheGeneralOne()
    {
        Assert.Equal(RelationshipCodes.OldFormat, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["format"] = 1)), "format 1"));
        Assert.Equal(RelationshipCodes.Format, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["format"] = 4)), "format 4"));
        Assert.Equal(RelationshipCodes.Format, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["format"] = "2")), "format as text"));
    }

    [Fact]
    public void FormatThreeIsAcceptedAndTheShippedSampleIsFormatThree()
    {
        Assert.Equal(3, RelationshipLoader.Format);
        Assert.Contains("\"format\": 3", Fixture.VillageText, StringComparison.Ordinal);
        Assert.Equal(DaySchedule.Spread, Fixture.Shipped.Social.Schedule);
        Assert.Equal(DaySchedule.Atomic, RelationshipLoader.Load(Edit.Doc(r => { })).Social.Schedule);
        Assert.Equal(DaySchedule.Spread, RelationshipLoader.Load(Edit.Doc(r => r["social"]!["schedule"] = "spread")).Social.Schedule);
    }
}
