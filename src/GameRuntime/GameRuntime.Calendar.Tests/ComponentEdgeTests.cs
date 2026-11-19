using System.Text.Json.Nodes;
using CalendarEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>Cases found by mutation testing: a stale context, tags of a holiday, and the journal of another channel.</summary>
public sealed class ComponentEdgeTests
{
    [Fact]
    public void AWhereAnswerUsesTheFactsOfItsOwnUpdateEvenWhenTheInstantHasNotMoved()
    {
        var set = Samples.Gregorian;
        // One calendar tick every 60 game ticks, so the instant is the same in both Updates.
        var host = new CalendarHost(set, Samples.At(set, 2024, "january", 10, 10), 1, 60);
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Session.Submit(new CalendarWhereRequested("dry", "anna"));
        host.Tick();
        host.HostFacts["weather.raining"] = FactValue.From(true);
        host.Session.Submit(new CalendarWhereRequested("wet", "anna"));
        host.Tick();
        Assert.Equal(["dry open-morning", "wet stay-in"], host.Of<CalendarWhereAnswered>().Select(w => w.RequestId + " " + w.Block).ToArray());
    }

    [Fact]
    public void AHolidayAnnouncesAllItsTagsInOrder()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "gregorian.calendar.json"));
        var root = JsonNode.Parse(text)!.AsObject();
        root["holidays"]!.AsArray().First(h => h!["id"]!.GetValue<string>() == "harvest-week")!["tags"] = new JsonArray("festival", "fair", "autumn");
        var changed = CalendarLoader.Load(root.ToJsonString());
        var host = new CalendarHost(changed, Samples.At(changed, 2024, "september", 30, 23, 59), 2, 1);
        host.Tick();
        Assert.Equal("festival,fair,autumn", host.Of<CalendarHolidayStarted>().Single().Tags);
    }

    [Fact]
    public void JournalEntriesOfAnotherChannelAreNotReplayedHere()
    {
        var runtime = CalendarRuntime.Create(Samples.Gregorian, 0, 1, 1);
        var entries = new[]
        {
            new CalendarJournalEntry(1, "other", null, "scale", "x", "", "", 5, 1, 0),
            new CalendarJournalEntry(1, "", null, "pause", "y", "", "", 0, 0, 0),
        };
        var result = CalendarReplay.Run(runtime, entries, 3, CalendarHost.TicksPerSecond);
        Assert.Equal(1, runtime.ScaleNumerator);
        Assert.False(runtime.Paused);
        Assert.Equal(3, result.Tick);
        Assert.Equal(3, runtime.Instant);
        Assert.Equal(4, result.Events);   // the pause is applied, and three ticks of time pass; nothing is said about the other channel
    }
}
