using Xunit;

namespace GameRuntime.Calendar.Tests;

public sealed class OwedAnnouncementsTests
{
    [Fact]
    public void AnnouncementsStillOwedGoOnEvenWhenTheMinuteDoesNotChange()
    {
        // One calendar tick every 60 game ticks: the minute stays the same for many Updates, so only the debt of announcements makes the component look again.
        var host = new CalendarHost(Samples.Gregorian, Samples.At(Samples.Gregorian, 2024, "march", 6, 10), 1, 60, maxAnnouncements: 10);
        for (var i = 0; i < 25; i++) host.Session.Submit(new CalendarActorAddRequested("a" + i, "n" + i.ToString("00"), "shopkeeper"));
        host.Tick();
        Assert.Equal(10, host.Of<CalendarScheduleBlockChanged>().Count());
        host.Tick();
        Assert.Equal(20, host.Of<CalendarScheduleBlockChanged>().Count());
        host.Tick();
        Assert.Equal(25, host.Of<CalendarScheduleBlockChanged>().Count());
        host.Tick(3);
        Assert.Equal(25, host.Of<CalendarScheduleBlockChanged>().Count());
    }
}
