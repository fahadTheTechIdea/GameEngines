using Xunit;

namespace GameRuntime.Calendar.Tests;

public sealed class SkipBoundaryTests
{
    [Fact]
    public void TheLastHourAndMinuteOfTheDayAreValidTargetsAndTheNextOnesAreNot()
    {
        var host = new CalendarHost(Samples.Gregorian, Samples.At(Samples.Gregorian, 2024, "march", 6, 10), 1, 1);
        host.Session.Submit(new CalendarSkipRequested("late", 0, 23, 59));
        host.Tick();
        Assert.Equal(Samples.At(Samples.Gregorian, 2024, "march", 6, 23, 59) + 1, host.Runtime.Instant);
        host.Session.Submit(new CalendarSkipRequested("hour24", 1, 24, 0));
        host.Session.Submit(new CalendarSkipRequested("minute60", 1, 6, 60));
        host.Session.Submit(new CalendarSkipRequested("hour-1", 1, -1, 0));
        host.Session.Submit(new CalendarSkipRequested("minute-1", 1, 6, -1));
        host.Tick();
        Assert.Equal(["hour24", "minute60", "hour-1", "minute-1"], host.Of<CalendarRefused>().Select(r => r.RequestId).ToArray());
        Assert.All(host.Of<CalendarRefused>(), r => Assert.Equal("CAL_DATE_INVALID", r.Code));
    }
}
