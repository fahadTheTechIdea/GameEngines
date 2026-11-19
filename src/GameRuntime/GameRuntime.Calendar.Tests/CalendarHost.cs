using CalendarEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>The sample documents the tests use.</summary>
internal static class Samples
{
    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    public static readonly CalendarSet Gregorian = CalendarLoader.Load(Read("gregorian.calendar.json"));
    public static readonly CalendarSet Valley = CalendarLoader.Load(Read("valley.calendar.json"));

    public static long At(CalendarSet set, long year, string month, int day, int hour = 0, int minute = 0) =>
        set.System.InstantOf(new CalendarDate(year, set.System.Months.First(m => m.Id == month).Index, day), hour, minute);
}

/// <summary>A session with a calendar component, a channel registry and a record of everything the component raised.</summary>
internal sealed class CalendarHost
{
    public sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    public const int TicksPerSecond = 50;
    public const long TickMicroseconds = 1_000_000 / TicksPerSecond;

    public Reporter Faults { get; } = new();
    public RuntimeSession Session { get; }
    public CalendarChannels Channels { get; }
    public CalendarComponent Component { get; }
    public CalendarRuntime Runtime { get; }
    public CalendarTranscript Transcript { get; }
    public CalendarJournal Journal { get; } = new();
    public Dictionary<string, FactValue> HostFacts { get; } = new(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false) };
    public List<object> All { get; } = new();

    public CalendarHost(CalendarSet set, long startInstant, long scaleNumerator = 1, long scaleDenominator = 1, int maxAnnouncements = 1000, string channel = "", CalendarRuntime? runtime = null,
        IReadOnlyList<(IGameComponent Component, int Order)>? extra = null, bool journal = false, long startTick = 0)
    {
        Runtime = runtime ?? CalendarRuntime.Create(set, startInstant, scaleNumerator, scaleDenominator, startTick);
        Session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8, Runtime.LastTick), Faults);
        Channels = new CalendarChannels();
        Channels.Install(Session);
        Component = new CalendarComponent(Runtime, () => HostFacts, Channels, channel: channel, maxAnnouncements: maxAnnouncements, journal: journal ? Journal : null);
        Session.AddComponent(Component, 10);
        foreach (var (component, order) in extra ?? []) Session.AddComponent(component, order);
        Transcript = new CalendarTranscript(Session);
        Session.Subscribe<CalendarApplied>(e => All.Add(e));
        Session.Subscribe<CalendarRefused>(e => All.Add(e));
        Session.Subscribe<CalendarTimeAdvanced>(e => All.Add(e));
        Session.Subscribe<CalendarDayChanged>(e => All.Add(e));
        Session.Subscribe<CalendarDayPartChanged>(e => All.Add(e));
        Session.Subscribe<CalendarSeasonChanged>(e => All.Add(e));
        Session.Subscribe<CalendarHolidayStarted>(e => All.Add(e));
        Session.Subscribe<CalendarScheduleBlockChanged>(e => All.Add(e));
        Session.Subscribe<CalendarWhereAnswered>(e => All.Add(e));
        Session.Init();
    }

    public IEnumerable<T> Of<T>() where T : struct => All.OfType<T>();

    public void Tick(int ticks = 1)
    {
        for (var i = 0; i < ticks; i++)
        {
            var ran = Session.Update(TickMicroseconds);
            Assert.True(ran == 1, "the session ran " + ran + " ticks (state " + Session.State + "): " + string.Join("; ", Faults.Reports));
        }
    }

}
