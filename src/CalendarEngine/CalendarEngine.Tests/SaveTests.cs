using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Save and load: round trips, every tamper refused with its own code, ranges refused before the digest is read, nothing repaired or migrated.</summary>
public sealed class SaveTests
{
    private static CalendarSet Set => Fixture.Gregorian;

    private static CalendarRuntime Busy()
    {
        var runtime = CalendarRuntime.Create(Set, Fixture.At(Set, 2024, "march", 6, 10), 7, 3, startTick: 4);
        runtime.AddActor("anna", "shopkeeper");
        runtime.AddActor("zed", "guard");
        runtime.Step(40);
        runtime.Interrupt("anna", "alarm", 600);
        runtime.Refresh(runtime.Context(Fixture.Dry), 10);
        return runtime;
    }

    private static JsonObject Tree(byte[] save) => JsonNode.Parse(save)!.AsObject();

    private static byte[] Bytes(JsonObject tree) => Encoding.UTF8.GetBytes(tree.ToJsonString());

    /// <summary>The save with a change and the digest recomputed, so only the rules about the state can refuse it.</summary>
    private static byte[] Forged(byte[] save, Action<JsonObject> edit)
    {
        var tree = Tree(save);
        edit(tree);
        var image = System.Text.Json.JsonSerializer.Deserialize<CalendarImage>(tree.ToJsonString())!;
        tree["Digest"] = EngineCommon.Digest.Of(CalendarImage.Canonical(image));
        return Bytes(tree);
    }

    private static string CodeOfOpen(byte[] save) => Fixture.CodeOf(() => CalendarSave.Open(Set, save));

    [Fact]
    public void ASaveOpensToTheSameStateAndKeepsRunningIdentically()
    {
        var original = Busy();
        var restored = CalendarSave.Open(Set, CalendarSave.Write(original));
        Assert.Equal(CalendarSave.StateDigest(original), CalendarSave.StateDigest(restored));
        Assert.Equal((original.Instant, original.ScaleRemainder, original.LastTick, original.Backlog, original.Paused), (restored.Instant, restored.ScaleRemainder, restored.LastTick, restored.Backlog, restored.Paused));
        Assert.Equal(original.Actors.Keys.ToArray(), restored.Actors.Keys.ToArray());
        for (var tick = 41L; tick < 5000; tick += 17)
        {
            original.Step(tick);
            restored.Step(tick);
            Assert.Equal(CalendarSave.StateDigest(original), CalendarSave.StateDigest(restored));
            Assert.Equal(original.Refresh(original.Context(Fixture.Dry), 100).Select(c => c.Actor + c.Answer.Key), restored.Refresh(restored.Context(Fixture.Dry), 100).Select(c => c.Actor + c.Answer.Key));
        }
    }

    [Fact]
    public void ASaveOfAnEmptyCalendarRoundTripsToo()
    {
        var runtime = CalendarRuntime.Create(Set, 0, 1, 1);
        var restored = CalendarSave.Open(Set, CalendarSave.Write(runtime));
        Assert.Equal(CalendarSave.StateDigest(runtime), CalendarSave.StateDigest(restored));
    }

    [Fact]
    public void TheDigestChangesWithEveryPartOfTheState()
    {
        var baseline = CalendarSave.StateDigest(Busy());
        var variants = new List<Action<CalendarRuntime>>
        {
            r => r.Step(41), r => r.SetPaused(true), r => r.SetScale(5, 3), r => r.AddActor("new", "guard"), r => r.RemoveActor("zed"), r => r.ClearInterrupt("anna"),
            r => r.Interrupt("zed", "invitation", 5), r => r.SkipTo(r.Instant + 1),
        };
        foreach (var change in variants)
        {
            var runtime = Busy();
            change(runtime);
            Assert.NotEqual(baseline, CalendarSave.StateDigest(runtime));
        }
        Assert.Equal(baseline, CalendarSave.StateDigest(Busy()));
    }

    [Fact]
    public void NotASaveAndAnOtherFormatAreRefusedAsFormat()
    {
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen("not json"u8.ToArray()));
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen("[]"u8.ToArray()));
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen("null"u8.ToArray()));
        var save = CalendarSave.Write(Busy());
        var tree = Tree(save);
        tree["Format"] = 0;
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen(Bytes(tree)));
        tree["Format"] = 2;
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen(Bytes(tree)));
        tree = Tree(save);
        tree["Extra"] = 1;
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen(Bytes(tree)));
        foreach (var field in new[] { "Format", "Document", "Id", "Instant", "ScaleNumerator", "ScaleDenominator", "ScaleRemainder", "Paused", "LastTick", "Backlog", "Actors", "Digest" })
        {
            tree = Tree(save);
            tree.Remove(field);
            Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen(Bytes(tree)));
        }
        tree = Tree(save);
        tree["Actors"]!.AsArray()[0]!.AsObject().Remove("Announced");
        Assert.Equal(CalendarCodes.SaveFormat, CodeOfOpen(Bytes(tree)));
        Assert.Equal(CalendarCodes.SaveFormat, Fixture.CodeOf(() => CalendarSave.Open(Set, new byte[CalendarSave.MaxBytes + 1])));
    }

    [Fact]
    public void ASaveOfAnotherDocumentIsRefused()
    {
        var other = CalendarLoader.Load(Fixture.GregorianText + " ");
        var save = CalendarSave.Write(Busy());
        Assert.Equal(CalendarCodes.SaveDocument, Fixture.CodeOf(() => CalendarSave.Open(other, save)));
        var tree = Tree(save);
        tree["Id"] = "another";
        Assert.Equal(CalendarCodes.SaveDocument, CodeOfOpen(Bytes(tree)));
        Assert.Equal(CalendarCodes.SaveDocument, Fixture.CodeOf(() => CalendarSave.Open(Fixture.Valley, save)));
    }

    [Fact]
    public void AnyChangedValueWithTheOldDigestIsRefusedAsADigestMismatch()
    {
        var save = CalendarSave.Write(Busy());
        var changes = new Dictionary<string, Action<JsonObject>>
        {
            ["instant"] = t => t["Instant"] = t["Instant"]!.GetValue<long>() + 1,
            ["numerator"] = t => t["ScaleNumerator"] = 8,
            ["remainder"] = t => t["ScaleRemainder"] = 2,
            ["paused"] = t => t["Paused"] = true,
            ["tick"] = t => t["LastTick"] = 41,
            ["backlog"] = t => t["Backlog"] = 1,
            ["schedule"] = t => t["Actors"]!.AsArray()[0]!["Schedule"] = "guard",
            ["interrupt"] = t => { t["Actors"]!.AsArray()[0]!["Interrupt"] = "invitation"; },
            ["until"] = t => t["Actors"]!.AsArray()[0]!["InterruptUntil"] = t["Actors"]!.AsArray()[0]!["InterruptUntil"]!.GetValue<long>() + 1,
            ["announced"] = t => t["Actors"]!.AsArray()[0]!["Announced"] = "elsewhere",
            ["actor id"] = t => t["Actors"]!.AsArray()[0]!["Id"] = "bob",
            ["digest"] = t => t["Digest"] = new string('0', 64),
        };
        foreach (var (name, change) in changes)
        {
            var tree = Tree(save);
            change(tree);
            Assert.True(CalendarCodes.SaveDigest == CodeOfOpen(Bytes(tree)), name);
        }
    }

    [Fact]
    public void ValuesOutsideTheirRangesAreRefusedAsStateEvenWithAMatchingDigest()
    {
        var save = CalendarSave.Write(Busy());
        var forged = new Dictionary<string, Action<JsonObject>>
        {
            ["instant below"] = t => t["Instant"] = Set.System.MinInstant - 1,
            ["instant above"] = t => t["Instant"] = Set.System.MaxInstant + 1,
            ["denominator zero"] = t => t["ScaleDenominator"] = 0,
            ["denominator large"] = t => t["ScaleDenominator"] = CalendarRuntime.MaxScaleDenominator + 1,
            ["numerator zero"] = t => t["ScaleNumerator"] = 0,
            ["numerator large"] = t => t["ScaleNumerator"] = 1_000_000_000_000_000,
            ["remainder negative"] = t => t["ScaleRemainder"] = -1,
            ["remainder at denominator"] = t => t["ScaleRemainder"] = 3,
            ["tick negative"] = t => t["LastTick"] = -1,
            ["backlog negative"] = t => t["Backlog"] = -1,
            ["backlog beyond the end"] = t => t["Backlog"] = Set.System.MaxInstant,
            ["actors out of order"] = t => { var a = t["Actors"]!.AsArray(); var first = a[0]!.DeepClone(); var second = a[1]!.DeepClone(); a[0] = second; a[1] = first; },
            ["actor twice"] = t => t["Actors"]!.AsArray()[1]!["Id"] = "anna",
            ["actor id empty"] = t => t["Actors"]!.AsArray()[0]!["Id"] = "",
            ["announced too long"] = t => t["Actors"]!.AsArray()[0]!["Announced"] = new string('x', 1025),
            ["interrupt without end"] = t => t["Actors"]!.AsArray()[0]!["InterruptUntil"] = 0,
            ["end without interrupt"] = t => t["Actors"]!.AsArray()[1]!["InterruptUntil"] = 5,
            ["interrupt too long"] = t => t["Actors"]!.AsArray()[0]!["InterruptUntil"] = t["Instant"]!.GetValue<long>() + 367 * 1440,
        };
        foreach (var (name, change) in forged)
            Assert.True(CalendarCodes.SaveState == CodeOfOpen(Forged(save, change)), name);
    }

    [Fact]
    public void StateThatTheDocumentCannotHoldIsRefusedAfterTheDigestIsChecked()
    {
        var save = CalendarSave.Write(Busy());
        Assert.Equal(CalendarCodes.SaveState, CodeOfOpen(Forged(save, t => t["Actors"]!.AsArray()[0]!["Schedule"] = "no-such-schedule")));
        Assert.Equal(CalendarCodes.SaveState, CodeOfOpen(Forged(save, t => t["Actors"]!.AsArray()[0]!["Interrupt"] = "no-such-interrupt")));
        Assert.Equal(CalendarCodes.SaveDigest, CodeOfOpen(Bytes(Tree(save).Also(t => t["Actors"]!.AsArray()[0]!["Schedule"] = "no-such-schedule"))));
    }

    [Fact]
    public void ARangeErrorIsReportedBeforeADigestErrorWhenBothArePresent()
    {
        var tree = Tree(CalendarSave.Write(Busy()));
        tree["ScaleDenominator"] = 0;   // out of range and the digest no longer matches
        Assert.Equal(CalendarCodes.SaveState, CodeOfOpen(Bytes(tree)));
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject Also(this JsonObject tree, Action<JsonObject> edit)
    {
        edit(tree);
        return tree;
    }
}
