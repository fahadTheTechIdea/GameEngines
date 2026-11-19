using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>The calendar component: requests queue and are answered in Update in arrival order, refusals are events, time moves only in Update and what it crosses is raised in a stated order.</summary>
public sealed class CalendarComponentTests
{
    private static CalendarSet Set => Samples.Gregorian;

    private static CalendarHost Host(long instant, long numerator = 1, long denominator = 1, int maxAnnouncements = 1000) => new(Set, instant, numerator, denominator, maxAnnouncements);

    private static long At(string month, int day, int hour = 0, int minute = 0, long year = 2024) => Samples.At(Set, year, month, day, hour, minute);

    [Fact]
    public void ARequestChangesNothingUntilTheNextUpdate()
    {
        var host = Host(At("march", 6, 10));
        var before = host.Component.Fingerprint();
        host.Session.Submit(new CalendarActorAddRequested("r1", "anna", "shopkeeper"));
        host.Session.Submit(new CalendarScaleRequested("r2", 5, 1));
        Assert.Equal(before, host.Component.Fingerprint());
        Assert.Empty(host.Runtime.Actors);
        Assert.Equal(1, host.Runtime.ScaleNumerator);
        host.Tick();
        Assert.Single(host.Runtime.Actors);
        Assert.Equal(5, host.Runtime.ScaleNumerator);
        Assert.Equal(["r1", "r2"], host.Of<CalendarApplied>().Select(a => a.RequestId).ToArray());
        Assert.All(host.Of<CalendarApplied>(), a => Assert.Equal((1L, "calendar", ""), (a.Tick, a.Source, a.Channel)));
    }

    [Fact]
    public void RequestsAreAnsweredInTheOrderTheyArrived()
    {
        var host = Host(At("march", 6, 10));
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Session.Submit(new CalendarWhereRequested("b", "anna"));
        host.Session.Submit(new CalendarInterruptRequested("c", "anna", "alarm", 60));
        host.Session.Submit(new CalendarWhereRequested("d", "anna"));
        host.Session.Submit(new CalendarActorRemoveRequested("e", "anna"));
        host.Session.Submit(new CalendarWhereRequested("f", "anna"));
        host.Tick();
        var order = host.All.Select(e => e switch
        {
            CalendarApplied a => "applied " + a.RequestId,
            CalendarWhereAnswered w => "where " + w.RequestId + " " + w.Location + "/" + w.Origin,
            CalendarRefused r => "refused " + r.RequestId + " " + r.Code,
            CalendarTimeAdvanced => "time",
            CalendarScheduleBlockChanged b => "block " + b.Actor,
            _ => e.GetType().Name,
        }).ToArray();
        Assert.Equal(["applied a", "where b shop/routine", "applied c", "where d guardhouse/interrupt", "applied e", "refused f CAL_UNKNOWN_ACTOR", "time"], order);
    }

    [Fact]
    public void AnAnswerToWhereSaysWhichRuleSaidSo()
    {
        var host = Host(At("december", 25, 9));
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Tick();
        host.Session.Submit(new CalendarWhereRequested("w", "anna"));
        host.Tick();
        var answer = host.Of<CalendarWhereAnswered>().Single();
        Assert.Equal(("w", "anna", "shopkeeper", "square", "celebrate", "routine", "holiday", "feast", 20), (answer.RequestId, answer.Actor, answer.Schedule, answer.Location, answer.Activity, answer.Origin, answer.Routine, answer.Block, answer.Priority));
    }

    [Fact]
    public void ARefusalIsAnEventWithItsCodeAndNothingChanged()
    {
        var host = Host(At("march", 6, 10));
        var digest = CalendarSave.StateDigest(host.Runtime);
        host.Session.Submit(new CalendarActorAddRequested("x1", "anna", "nobody"));
        host.Session.Submit(new CalendarScaleRequested("x2", 0, 1));
        host.Session.Submit(new CalendarSkipRequested("x3", 400, 6, 0));
        host.Session.Submit(new CalendarSkipRequested("x4", 1, 30, 0));
        host.Session.Submit(new CalendarSkipRequested("x5", 0, 9, 0));   // 10:00 now, 9:00 has gone
        host.Session.Submit(new CalendarInterruptRequested("x6", "ghost", "alarm", 5));
        host.Session.Submit(new CalendarActorRemoveRequested("x7", "ghost"));
        host.Session.Submit(new CalendarInterruptClearRequested("x8", "ghost"));
        host.Tick();
        Assert.Equal(["x1 CAL_UNKNOWN_SCHEDULE", "x2 CAL_SCALE", "x3 CAL_SKIP", "x4 CAL_DATE_INVALID", "x5 CAL_SKIP", "x6 CAL_UNKNOWN_ACTOR", "x7 CAL_UNKNOWN_ACTOR", "x8 CAL_UNKNOWN_ACTOR"],
            host.Of<CalendarRefused>().Select(r => r.RequestId + " " + r.Code).ToArray());
        Assert.Empty(host.Of<CalendarApplied>());
        Assert.Equal(host.Runtime.Instant, At("march", 6, 10) + 1);   // only the tick itself moved the calendar
        Assert.Equal(8, host.Component.Answered);
        Assert.NotEqual(digest, CalendarSave.StateDigest(host.Runtime));
    }

    [Fact]
    public void TimeMovesByTheScaleOnlyInUpdateAndPauseAndResumeAreRequests()
    {
        var host = Host(At("march", 6, 10), 3, 1);
        host.Tick(2);
        Assert.Equal(At("march", 6, 10) + 6, host.Runtime.Instant);
        host.Session.Submit(new CalendarPauseRequested("p", true));
        host.Tick(5);
        Assert.Equal(At("march", 6, 10) + 6, host.Runtime.Instant);
        host.Session.Submit(new CalendarPauseRequested("q", false));
        host.Tick(1);
        Assert.Equal(At("march", 6, 10) + 9, host.Runtime.Instant);
        Assert.Equal(["p", "q"], host.Of<CalendarApplied>().Select(a => a.RequestId).ToArray());
    }

    [Fact]
    public void WhatATickCrossedIsRaisedInAFixedOrder()
    {
        // 23:59 on the last day of February 2024 (a leap year): one tick of 2 minutes crosses midnight into the leap day, which is a holiday of this calendar.
        var host = Host(At("february", 28, 23, 59), 2, 1);
        host.Tick();
        var names = host.All.Select(e => e.GetType().Name).ToArray();
        Assert.Equal(["CalendarTimeAdvanced", "CalendarDayChanged", "CalendarHolidayStarted", "CalendarDayPartChanged"], names);
        var day = host.Of<CalendarDayChanged>().Single();
        Assert.Equal((2024L, "february", 29, "thursday", 1L), (day.Year, day.Month, day.Day, day.Weekday, day.DaysCrossed));
        Assert.Equal("leap-day", host.Of<CalendarHolidayStarted>().Single().Holiday);
        var part = host.Of<CalendarDayPartChanged>().Single();
        Assert.Equal(("night", 0, 1, "winter", false), (part.DayPart, part.Hour, part.Minute, part.Season, part.IsDay));
        Assert.Equal(0, part.LightPermille);
    }

    [Fact]
    public void ASeasonChangeIsRaisedBetweenTheDayAndTheHolidays()
    {
        var host = Host(At("february", 29, 23, 59), 2, 1);
        host.Tick();
        Assert.Equal(["CalendarTimeAdvanced", "CalendarDayChanged", "CalendarSeasonChanged", "CalendarDayPartChanged"], host.All.Select(e => e.GetType().Name).ToArray());
        var season = host.Of<CalendarSeasonChanged>().Single();
        Assert.Equal(("winter", "spring", 2024L, "march", 1), (season.From, season.To, season.Year, season.Month, season.Day));
    }

    [Fact]
    public void AHolidayAnnouncesItsTagsAndLength()
    {
        var host = Host(At("september", 30, 23, 59), 2, 1);
        host.Tick();
        var harvest = host.Of<CalendarHolidayStarted>().Single();
        Assert.Equal(("harvest-week", "festival", 3, "october", 1), (harvest.Holiday, harvest.Tags, harvest.Days, harvest.Month, harvest.Day));
    }

    [Fact]
    public void TheDayPartChangesAreRaisedOnceEachAndNotWithinAPart()
    {
        var host = Host(At("march", 6, 7, 58));
        host.Tick(6);   // 7:59, 8:00 (morning begins), 8:01 ...
        var part = host.Of<CalendarDayPartChanged>().Single();
        Assert.Equal(("morning", 8, 0, "spring", true, 1000), (part.DayPart, part.Hour, part.Minute, part.Season, part.IsDay, part.LightPermille));
        Assert.Equal(6, host.Of<CalendarTimeAdvanced>().Count());
    }

    [Fact]
    public void ASkipRequestSleepsUntilTheMorning()
    {
        var host = Host(At("march", 6, 22, 30));
        host.Session.Submit(new CalendarSkipRequested("sleep", 1, 6, 0));
        host.Tick();
        Assert.Equal(At("march", 7, 6, 0) + 1, host.Runtime.Instant);
        Assert.Equal(["CalendarApplied", "CalendarTimeAdvanced", "CalendarDayChanged", "CalendarDayPartChanged", "CalendarTimeAdvanced"], host.All.Select(e => e.GetType().Name).ToArray());
        Assert.Equal(1, host.Of<CalendarTimeAdvanced>().First().DaysCrossed);
        host.Session.Submit(new CalendarSkipRequested("again", 0, 23, 0));
        host.Tick();
        Assert.Equal(At("march", 7, 23, 0) + 1, host.Runtime.Instant);
    }

    [Fact]
    public void ScheduleChangesAreAnnouncedWhenTheMinuteBringsANewBlock()
    {
        var host = Host(At("march", 6, 6, 58));
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Session.Submit(new CalendarActorAddRequested("g", "gus", "guard"));
        host.Tick();
        Assert.Equal(["anna sleep-night", "gus sleep-day"], host.Of<CalendarScheduleBlockChanged>().Select(b => b.Actor + " " + b.Block).ToArray());
        host.Tick(2);   // 7:01: the shopkeeper has breakfast
        Assert.Equal(["anna sleep-night", "gus sleep-day", "anna breakfast"], host.Of<CalendarScheduleBlockChanged>().Select(b => b.Actor + " " + b.Block).ToArray());
        var change = host.Of<CalendarScheduleBlockChanged>().Last();
        Assert.Equal(("home", "eat", "routine", "base", 0, "shopkeeper"), (change.Location, change.Activity, change.Origin, change.Routine, change.Priority, change.Schedule));
        Assert.Equal(2, change.Tick);
    }

    [Fact]
    public void ABurstOfScheduleChangesIsSpreadOverUpdatesAndNobodyIsLost()
    {
        var host = Host(At("march", 6, 6, 55), 1, 1, maxAnnouncements: 10);
        for (var i = 0; i < 25; i++) host.Session.Submit(new CalendarActorAddRequested("a" + i, "n" + i.ToString("00"), "shopkeeper"));
        host.Tick();
        Assert.Equal(10, host.Of<CalendarScheduleBlockChanged>().Count());
        host.Tick();
        Assert.Equal(20, host.Of<CalendarScheduleBlockChanged>().Count());
        host.Tick();
        Assert.Equal(25, host.Of<CalendarScheduleBlockChanged>().Count());
        Assert.Equal(Enumerable.Range(0, 25).Select(i => "n" + i.ToString("00")).ToArray(), host.Of<CalendarScheduleBlockChanged>().Select(b => b.Actor).Take(25).ToArray().Order().ToArray());
        // 7:00 came during these ticks: the shopkeepers moved to breakfast, and they are announced too, again in id order and at most 10 an Update.
        host.Tick(5);
        Assert.Equal(50, host.Of<CalendarScheduleBlockChanged>().Count());
        Assert.All(host.All.OfType<CalendarScheduleBlockChanged>().GroupBy(b => b.Tick), g => Assert.True(g.Count() <= 10));
    }

    [Fact]
    public void AnInterruptIsAnnouncedWhenItBeginsAndWhenItEnds()
    {
        var host = Host(At("march", 6, 10));
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Tick();
        host.Session.Submit(new CalendarInterruptRequested("i", "anna", "alarm", 3));
        host.Tick();
        host.Tick(3);
        var changes = host.Of<CalendarScheduleBlockChanged>().Select(b => b.Origin + ":" + b.Location).ToArray();
        Assert.Equal(["routine:shop", "interrupt:guardhouse", "routine:shop"], changes);
    }

    [Fact]
    public void AMissingHostFactIsRefusedOnceAndTheAnnouncementsFollowWhenItIsGiven()
    {
        var host = Host(At("january", 10, 9, 58));
        host.HostFacts.Clear();   // winter: the rain routine needs weather.raining
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Tick(3);
        Assert.Equal(["announce CAL_FACT_MISSING"], host.Of<CalendarRefused>().Select(r => r.RequestId + " " + r.Code).ToArray());
        Assert.Empty(host.Of<CalendarScheduleBlockChanged>());
        host.HostFacts["weather.raining"] = FactValue.From(true);
        host.Tick();
        Assert.Equal("stay-in", host.Of<CalendarScheduleBlockChanged>().Single().Block);
        Assert.Single(host.Of<CalendarRefused>());
    }

    [Fact]
    public void WhenTheHostFactsChangeTheSchedulesAreAskedAgainInTheSameMinute()
    {
        var host = Host(At("january", 10, 10, 0), 1, 60);   // a calendar tick only every 60 game ticks: the minute stays the same
        host.Session.Submit(new CalendarActorAddRequested("a", "anna", "shopkeeper"));
        host.Tick();
        Assert.Equal("open-morning", host.Of<CalendarScheduleBlockChanged>().Single().Block);
        host.HostFacts["weather.raining"] = FactValue.From(true);
        host.Tick();
        Assert.Equal(["open-morning", "stay-in"], host.Of<CalendarScheduleBlockChanged>().Select(b => b.Block).ToArray());
        host.Tick(3);
        Assert.Equal(2, host.Of<CalendarScheduleBlockChanged>().Count());
    }

    [Fact]
    public void TheFactsOfTheCalendarAndTheHostCanBeReadWithAPrefix()
    {
        var host = Host(At("december", 25, 12));
        host.HostFacts["weather.raining"] = FactValue.From(true);
        var facts = host.Component.Facts("ext.");
        Assert.Equal("christmas", facts["ext.cal.holiday_id"].Text);
        Assert.True(facts["ext.weather.raining"].Boolean);
        Assert.Equal("winter", host.Component.Facts()["cal.season"].Text);
        Assert.DoesNotContain("ext.cal.season", host.Component.Facts().Keys);
        Assert.Equal(CalendarFacts.Definitions(Set.System, Set.Moons).Count + 1, facts.Count);
    }

    // ---- catch-up --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASessionResumedMuchLaterCatchesUpInBoundedSteps()
    {
        // The runtime last stepped at tick 0; the session continues at tick 1,000,000 (a million minutes: 694 days). One Update may apply at most 400 days; the rest follows.
        var runtime = CalendarRuntime.Create(Set, At("january", 1), 1, 1);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8, 999_999), new CalendarHost.Reporter());
        var channels = new CalendarChannels();
        channels.Install(session);
        var component = new CalendarComponent(runtime, () => new Dictionary<string, FactValue>(), channels);
        session.AddComponent(component, 10);
        var advanced = new List<CalendarTimeAdvanced>();
        var holidays = new List<CalendarHolidayStarted>();
        session.Subscribe<CalendarTimeAdvanced>(advanced.Add);
        session.Subscribe<CalendarHolidayStarted>(holidays.Add);
        session.Init();
        Assert.Equal(1, session.Update(CalendarHost.TickMicroseconds));
        Assert.Single(advanced);
        Assert.Equal(400 * 1440, advanced[0].ToInstant - advanced[0].FromInstant);
        Assert.Equal(1_000_000 - 400 * 1440, runtime.Backlog);
        Assert.Equal(1, session.Update(CalendarHost.TickMicroseconds));
        Assert.Equal(1_000_001, runtime.Instant - At("january", 1));
        Assert.Equal(0, runtime.Backlog);
        Assert.Equal(2, advanced.Count);
        Assert.InRange(holidays.Count, 6, 12);
    }

    [Fact]
    public void ATickFarBeyondTheLimitIsRefusedEachUpdateAndTheCalendarDoesNotMove()
    {
        var far = CalendarRuntime.Create(Set, At("january", 1), 1, 1);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8, CalendarRuntime.MaxElapsedTicks + 5), new CalendarHost.Reporter());
        var channels = new CalendarChannels();
        channels.Install(session);
        session.AddComponent(new CalendarComponent(far, () => new Dictionary<string, FactValue>(), channels), 10);
        var refused = new List<CalendarRefused>();
        session.Subscribe<CalendarRefused>(refused.Add);
        session.Init();
        session.Update(CalendarHost.TickMicroseconds);
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal([("clock", "CAL_TICK_ORDER"), ("clock", "CAL_TICK_ORDER")], refused.Select(r => (r.RequestId, r.Code)).ToArray());
        Assert.Equal(At("january", 1), far.Instant);
    }

    // ---- channels --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARequestForAChannelNobodyServesIsRefusedByTheRegistryInTheSameTick()
    {
        var host = Host(At("march", 6, 10));
        host.Session.Submit(new CalendarScaleRequested("lost", 9, 1, "nowhere"));
        host.Session.Submit(new CalendarScaleRequested("mine", 2, 1));
        host.Tick();
        var refusal = host.Of<CalendarRefused>().Single();
        Assert.Equal(("lost", "nowhere", "CAL_CHANNEL_UNKNOWN", 1L, CalendarChannels.ComponentId), (refusal.RequestId, refusal.Channel, refusal.Code, refusal.Tick, refusal.Source));
        Assert.Equal(2, host.Runtime.ScaleNumerator);
        Assert.Equal(1, host.Channels.Refused);
        Assert.Equal("calendar", host.Channels.Served[""]);
    }

    [Fact]
    public void TwoCalendarsOnTwoChannelsAnswerOnlyTheirOwnRequests()
    {
        var dungeon = CalendarRuntime.Create(Samples.Valley, Samples.At(Samples.Valley, 1, "spring", 1, 6), 1, 1);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        var channels = new CalendarChannels();
        channels.Install(session);
        var world = new CalendarComponent(CalendarRuntime.Create(Set, At("march", 6, 10), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        var dungeonOnThisSession = new CalendarComponent(dungeon, () => new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false) }, channels, "dungeon-calendar", "dungeon");
        session.AddComponent(world, 10);
        session.AddComponent(dungeonOnThisSession, 11);
        var applied = new List<CalendarApplied>();
        session.Subscribe<CalendarApplied>(applied.Add);
        session.Init();
        session.Submit(new CalendarScaleRequested("w", 4, 1));
        session.Submit(new CalendarScaleRequested("d", 8, 1, "dungeon"));
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal([("w", "calendar", ""), ("d", "dungeon-calendar", "dungeon")], applied.Select(a => (a.RequestId, a.Source, a.Channel)).ToArray());
        Assert.Equal((4L, 8L), (world.Runtime.ScaleNumerator, dungeonOnThisSession.Runtime.ScaleNumerator));
        Assert.Equal(["", "dungeon"], channels.Served.Keys.ToArray());
    }

    [Fact]
    public void ASecondComponentOnTheSameChannelIsRefusedAtInit()
    {
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        var channels = new CalendarChannels();
        channels.Install(session);
        session.AddComponent(new CalendarComponent(CalendarRuntime.Create(Set, 0, 1, 1), () => new Dictionary<string, FactValue>(), channels, "one"), 10);
        session.AddComponent(new CalendarComponent(CalendarRuntime.Create(Set, 0, 1, 1), () => new Dictionary<string, FactValue>(), channels, "two"), 11);
        var refusal = Assert.Throws<EngineRefusedException>(session.Init);
        Assert.Equal(CalendarChannels.DuplicateChannel, refusal.Code);
        Assert.Equal(RuntimeState.Faulted, session.State);
    }

    [Fact]
    public void AComponentWhoseRegistryRunsOnAnotherSessionFailsItsFirstUpdate()
    {
        var other = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        var registry = new CalendarChannels();
        registry.Install(other);
        other.Init();
        var faults = new CalendarHost.Reporter();
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), faults);
        var own = new CalendarChannels();
        own.Install(session);
        session.AddComponent(new CalendarComponent(CalendarRuntime.Create(Set, 0, 1, 1), () => new Dictionary<string, FactValue>(), registry), 10);
        session.Init();
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Contains("does not run on this session", faults.Reports.Single());
    }

    [Fact]
    public void ARegistryServesOneSessionOnly()
    {
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        var registry = new CalendarChannels();
        registry.Install(session);
        session.Init();
        var second = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        registry.Install(second);
        Assert.Throws<InvalidOperationException>(second.Init);
    }

    [Fact]
    public void AnUpdateBeforeInitAndABadConstructionAreRefused()
    {
        var runtime = CalendarRuntime.Create(Set, 0, 1, 1);
        var channels = new CalendarChannels();
        var component = new CalendarComponent(runtime, () => new Dictionary<string, FactValue>(), channels);
        Assert.Throws<InvalidOperationException>(() => component.Update(new UpdateContext(1, 20_000, 0, 20_000)));
        Assert.Throws<ArgumentNullException>(() => new CalendarComponent(null!, () => new Dictionary<string, FactValue>(), channels));
        Assert.Throws<ArgumentNullException>(() => new CalendarComponent(runtime, null!, channels));
        Assert.Throws<ArgumentNullException>(() => new CalendarComponent(runtime, () => new Dictionary<string, FactValue>(), null!));
        Assert.Throws<ArgumentException>(() => new CalendarComponent(runtime, () => new Dictionary<string, FactValue>(), channels, maxAnnouncements: 0));
    }

    // ---- checkpoints -----------------------------------------------------------------------------------------------------------------

    private sealed class Prober : IGameComponent
    {
        public CalendarComponent? Component { get; set; }
        public string? Code { get; private set; }
        public string Id => "probe";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context) => Code ??= Assert.Throws<EngineRefusedException>(() => CalendarCheckpoint.Capture(Component!)).Code;
    }

    [Fact]
    public void ACheckpointIsRefusedWhileARequestOrARefusalIsWaiting()
    {
        var probe = new Prober();
        var host = new CalendarHost(Set, At("march", 6, 10), extra: [(probe, 5)]);
        probe.Component = host.Component;
        // The probe updates before the calendar (order 5 against 10): the request is delivered and waits for the calendar's Update.
        host.Session.Submit(new CalendarScaleRequested("s", 2, 1));
        host.Tick();
        Assert.Equal(CalendarComponent.NotQuiet, probe.Code);
        _ = CalendarCheckpoint.Capture(host.Component);   // quiet again
        var late = new Prober();
        var other = new CalendarHost(Set, At("march", 6, 10), extra: [(late, 2_000_000_000)]);
        late.Component = other.Component;
        other.Session.Submit(new CalendarScaleRequested("lost", 2, 1, "nowhere"));
        other.Tick();
        Assert.Equal(CalendarComponent.NotQuiet, late.Code);
        _ = CalendarCheckpoint.Capture(other.Component);
    }

    [Fact]
    public void AFingerprintChangesWithTheStateTheTickAndWhatIsWaiting()
    {
        var host = Host(At("march", 6, 10));
        var seen = new HashSet<string> { host.Component.Fingerprint() };
        host.Tick();
        Assert.True(seen.Add(host.Component.Fingerprint()));
        host.Session.Submit(new CalendarPauseRequested("p", true));
        host.Tick();
        Assert.True(seen.Add(host.Component.Fingerprint()));
        Assert.StartsWith("calendar tick=2 state=", host.Component.Fingerprint());
    }
}

public sealed class SkipLimitTests
{
    [Fact]
    public void ASkipOfAYearAndADayIsAllowedAndOneDayMoreIsRefused()
    {
        var host = new CalendarHost(Samples.Gregorian, Samples.At(Samples.Gregorian, 2024, "march", 6, 10), 1, 1);
        host.Session.Submit(new CalendarSkipRequested("year", CalendarRuntime.MaxSkipDays, 6, 0));
        host.Tick();
        Assert.Equal(["year"], host.Of<CalendarApplied>().Select(a => a.RequestId).ToArray());
        host.Session.Submit(new CalendarSkipRequested("too-far", CalendarRuntime.MaxSkipDays + 1, 6, 0));
        host.Tick();
        Assert.Equal(["too-far CAL_SKIP"], host.Of<CalendarRefused>().Select(r => r.RequestId + " " + r.Code).ToArray());
        host.Session.Submit(new CalendarSkipRequested("past", -1, 6, 0));
        host.Tick();
        Assert.Equal("CAL_SKIP", host.Of<CalendarRefused>().Last().Code);
    }
}
