using System.Text.Json.Nodes;
using CalendarEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The calendar and the economy: the calendar gives the time, the seasons and the dates; the two documents are checked against each other; a pay day or a tax decree falls on the calendar's date.</summary>
public sealed class CalendarIntegrationTests
{
    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    private static long At(int year, string month, int day, int hour = 0) => Gregorian.System.InstantOf(new CalendarDate(year, Gregorian.System.Months.First(m => m.Id == month).Index, day), hour, 0);

    private sealed class Rig
    {
        public RuntimeSession Session { get; }
        public CalendarComponent Calendar { get; }
        public EconomyComponent Economy { get; }
        public EconomyCalendarDates? Dates { get; }
        public List<EconomySettled> Settled { get; } = new();
        public List<(long Tick, string Name)> Days { get; } = new();
        public List<EconomyRefused> Refused { get; } = new();
        public Dictionary<string, FactValue> Weather { get; } = new() { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };

        public Rig(long start, IEnumerable<EconomyDateRule>? rules = null, EconomySet? set = null, string calendarChannel = "", long? origin = null)
        {
            var economySet = set ?? Samples.Town;
            Session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
            var calendarChannels = new CalendarChannels();
            calendarChannels.Install(Session);
            var economyChannels = new EconomyChannels();
            economyChannels.Install(Session);
            Calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, 1, 1), () => Weather, calendarChannels, channel: calendarChannel);
            Economy = new EconomyComponent(EconomyRuntime.Create(economySet), EconomyCalendar.Clock(Calendar, economySet, origin ?? start), EconomyCalendar.HostFacts(Calendar), economyChannels);
            Session.AddComponent(Calendar, 10);
            if (rules is not null)
            {
                Dates = new EconomyCalendarDates("dates", economySet, rules);
                Session.AddComponent(Dates, 20);
            }
            Session.AddComponent(Economy, 30);
            Session.Subscribe<EconomySettled>(Settled.Add);
            Session.Subscribe<EconomyRefused>(Refused.Add);
            Session.Subscribe<CalendarDayChanged>(e => Days.Add((e.Tick, e.Weekday)));
            Session.Init();
        }

        public void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) Assert.Equal(1, Session.Update(20_000));
        }

        public void RunDays(int days) => Run(days * 1440);
    }

    // ---- the two documents fit each other -------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleTownFitsTheSampleCalendar() => EconomyCalendar.Check(Samples.Town, Gregorian);

    [Fact]
    public void ADocumentThatDoesNotFitTheCalendarIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(Action<JsonObject> edit, string fragment)
        {
            var set = EconomyLoader.Load(Edit.Doc(edit));
            var refusal = Assert.Throws<EngineRefusedException>(() => EconomyCalendar.Check(set, Gregorian));
            Assert.Equal(EconomyCalendar.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(r => { r["time"]!["stepsPerDay"] = 7; r["time"]!["periodSteps"] = 7; }, "does not divide into 7");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.nonsense", ["type"] = "bool" }), "gives no fact 'cal.nonsense'");
        Refused(r =>
        {
            r["facts"]!.AsArray()[0]!["type"] = "bool";
            r["modifiers"]!.AsArray()[0]!["when"] = new JsonObject { ["fact"] = "cal.season", ["op"] = "eq", ["value"] = true };
            r["modifiers"]!.AsArray()[1]!["when"] = new JsonObject { ["fact"] = "cal.season", ["op"] = "eq", ["value"] = true };
        }, "cal.season");
        Refused(r => r["facts"]!.AsArray()[1]!["max"] = 2, "outside the 0 to 2");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 5, ["max"] = 10 }), "outside the 5 to 10");
        EconomyCalendar.Check(EconomyLoader.Load(Edit.Doc(r => r["facts"]!.AsArray()[1]!["max"] = 9)), Gregorian);      // a wider range than the calendar's is fine
        Assert.Throws<ArgumentNullException>(() => EconomyCalendar.Check(null!, Gregorian));
        Assert.Throws<ArgumentNullException>(() => EconomyCalendar.Check(Samples.Town, null!));
    }

    // ---- time ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheStepAtTheOriginIsZeroAndBeforeItMinusOneAndAStepIsASixtyTicksHour()
    {
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, At(2024, "january", 2), 1, 1), () => new Dictionary<string, FactValue>(), new CalendarChannels());
        Assert.Equal(0, EconomyCalendar.Clock(calendar, Samples.Town, At(2024, "january", 2)).StepAt(0));
        Assert.Equal(-1, EconomyCalendar.Clock(calendar, Samples.Town, At(2024, "january", 2) + 1).StepAt(0));
        Assert.Equal(0, EconomyCalendar.Clock(calendar, Samples.Town, At(2024, "january", 2) - 59).StepAt(0));
        Assert.Equal(1, EconomyCalendar.Clock(calendar, Samples.Town, At(2024, "january", 2) - 60).StepAt(0));
        Assert.Equal(24, EconomyCalendar.Clock(calendar, Samples.Town, At(2024, "january", 1)).StepAt(0));
    }

    [Fact]
    public void AnHourOfCalendarIsAStepAndADayIsAPeriod()
    {
        var rig = new Rig(At(2024, "march", 3));
        rig.Run(59);
        Assert.Equal(0, rig.Economy.Runtime.Step);
        rig.Run(1);
        Assert.Equal(1, rig.Economy.Runtime.Step);
        rig.Run(1380);        // the rest of the day
        Assert.Equal(24, rig.Economy.Runtime.Step);
        Assert.Equal(1, rig.Economy.Runtime.Period);
        rig.RunDays(2);
        Assert.Equal(3, rig.Economy.Runtime.Period);
    }

    [Fact]
    public void AClockBeforeItsOriginIsReportedAsAFaultAndTheEconomyWaits()
    {
        var rig = new Rig(At(2024, "march", 3), origin: At(2024, "march", 3, 6));
        rig.Run(120);
        var fault = Assert.Single(rig.Refused);
        Assert.Equal((EconomyComponent.ClockRequestId, "ECON_STEP_ORDER"), (fault.RequestId, fault.Code));
        Assert.Equal(0, rig.Economy.Runtime.Step);
        rig.Run(60 * 6 + 60 - 120);
        Assert.Equal(1, rig.Economy.Runtime.Step);
    }

    [Fact]
    public void TheSeasonTheCalendarGivesIsTheSeasonTheModifiersSee()
    {
        var rig = new Rig(At(2024, "january", 1));
        var season = rig.Calendar.Facts()["cal.season"].Text!;
        Assert.Equal("winter", season);
        rig.RunDays(1);
        var direct = EconomyRuntime.Create(Samples.Town);
        direct.Advance(24, Samples.Host(season));
        Assert.Equal(direct.StateDigest(), rig.Economy.Runtime.StateDigest());
        var summer = new Rig(At(2024, "july", 1));
        summer.RunDays(1);
        var other = EconomyRuntime.Create(Samples.Town);
        other.Advance(24, Samples.Host(summer.Calendar.Facts()["cal.season"].Text!));
        Assert.Equal(other.StateDigest(), summer.Economy.Runtime.StateDigest());
        Assert.NotEqual(direct.StateDigest(), other.StateDigest());
    }

    [Fact]
    public void ThePlagueTheHostGivesTheCalendarReachesTheEconomyToo()
    {
        var calm = new Rig(At(2024, "july", 1));
        var sick = new Rig(At(2024, "july", 1));
        sick.Weather["story.plague"] = FactValue.From(2L);
        calm.RunDays(2); sick.RunDays(2);
        Assert.True(sick.Economy.Runtime.Markets[0].Pairs[0].Supply < calm.Economy.Runtime.Markets[0].Pairs[0].Supply);
    }

    // ---- pay days and tax dates on the calendar ----------------------------------------------------------------------------------------

    [Fact]
    public void ADecreeOnThe15thSettlesTheManualScheduleOnEveryFifteenthAndOnlyThen()
    {
        var rig = new Rig(At(2024, "january", 1), [new EconomyDateRule("day:15", "decree")]);
        rig.RunDays(50);
        var court = rig.Settled.Where(s => s.Name == "court").ToList();
        Assert.Equal(2, court.Count);                                   // January 15 and February 15
        Assert.Equal(2, rig.Dates!.Requested);
        Assert.All(court, s => Assert.Equal((1500L, 1500L, "upkeep", "riverton"), (s.Paid, s.Due, s.Kind, s.Market)));
        Assert.Equal(14 * 1440, court[0].Tick);                  // the first tick of the 15th, settled in the same Update that announced the day
        Assert.Equal(45 * 1440, court[1].Tick);                      // January has 31 days
        Assert.Contains(rig.Settled, s => s.Name == "luxury-duty");
        Assert.All(rig.Economy.Runtime.Conservation(), c => Assert.Equal(0, c));
    }

    [Fact]
    public void AWeekdayRuleSettlesOnEveryThatWeekday()
    {
        var rig = new Rig(At(2024, "january", 1), [new EconomyDateRule("weekday:friday", "decree")]);
        rig.RunDays(28);
        Assert.Equal(4, rig.Settled.Count(s => s.Name == "court"));
        Assert.Equal(4, rig.Dates!.Requested);
        Assert.All(rig.Settled.Where(s => s.Name == "court"), s => Assert.Equal(4L, s.Tick / 1440 % 7));      // January 1, 2024 was a Monday: the fifth day, index 4
    }

    [Fact]
    public void AHolidayRuleSettlesWhenTheHolidayBegins()
    {
        var rig = new Rig(At(2024, "december", 30), [new EconomyDateRule("holiday:new-year", "decree")]);
        rig.RunDays(4);
        Assert.Equal(1, rig.Dates!.Requested);
        Assert.Single(rig.Settled, s => s.Name == "court");
    }

    [Fact]
    public void ASkipOverADateDoesNotSeeIt()
    {
        var rig = new Rig(At(2024, "january", 10), [new EconomyDateRule("day:15", "decree"), new EconomyDateRule("day:30", "decree")]);
        rig.Run(10);
        rig.Session.Submit(new CalendarSkipRequested("sleep", 20, 6, 0));
        rig.Run(20);
        Assert.Equal(1, rig.Dates!.Requested);      // the 15th was crossed inside the skip and is not announced; the 30th was landed on
        Assert.Single(rig.Settled, s => s.Name == "court");
    }

    [Fact]
    public void ARuleOnlyListensToTheCalendarItWasToldTo()
    {
        var rig = new Rig(At(2024, "january", 14), [new EconomyDateRule("day:15", "decree")], calendarChannel: "world");
        rig.RunDays(3);
        Assert.Equal(0, rig.Dates!.Requested);
        Assert.Equal(0, rig.Settled.Count(s => s.Name == "court"));
    }

    [Fact]
    public void RulesThatAreWrongAreRefusedWhenTheGameIsAssembled()
    {
        void Bad(string trigger, string schedule, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => new EconomyCalendarDates("d", Samples.Town, [new EconomyDateRule(trigger, schedule)]));
            Assert.Equal(EconomyCalendarDates.BadRule, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Bad("day:15", "never", "does not have");
        Bad("day:15", "payday", "runs by itself");
        Bad("day:0", "decree", "weekday:<id>, day:<number> or holiday:<id>");
        Bad("day:x", "decree", "weekday:<id>");
        Bad("weekday:", "decree", "weekday:<id>");
        Bad("weekday:Friday", "decree", "weekday:<id>");
        Bad("holiday", "decree", "holiday:<id>");
        Bad("month:3", "decree", "weekday:<id>");
        var twice = Assert.Throws<EngineRefusedException>(() => new EconomyCalendarDates("d", Samples.Town, [new EconomyDateRule("day:15", "decree"), new EconomyDateRule("day:15", "decree")]));
        Assert.Contains("two rules", twice.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => new EconomyCalendarDates("d", null!, []));
        Assert.Throws<ArgumentNullException>(() => new EconomyCalendarDates("d", Samples.Town, null!));
    }
}
