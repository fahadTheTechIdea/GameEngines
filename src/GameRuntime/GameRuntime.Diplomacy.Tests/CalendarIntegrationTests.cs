using System.Text.Json.Nodes;
using CalendarEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>The calendar and the diplomacy: the calendar gives the day clock and the seasons (leap years and season starts included) and the length of a term in months and years; the two documents are checked against each other.</summary>
public sealed class CalendarIntegrationTests
{
    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    private static long At(int year, string month, int day, int hour = 0) => Gregorian.System.InstantOf(new CalendarDate(year, Gregorian.System.Months.First(m => m.Id == month).Index, day), hour, 0);

    private static DipSet Isles(Action<JsonObject>? edit = null) => Samples.Calm(edit);

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public CalendarComponent Calendar { get; }
        public DiplomacyComponent Diplomacy { get; }
        public List<DiplomacySeasonChanged> Seasons { get; } = new();
        public List<DiplomacyRefused> Refused { get; } = new();
        public Dictionary<string, FactValue> Weather { get; } = new() { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        public Dictionary<string, FactValue> Game { get; } = Samples.Crisis();

        /// <param name="scale">Calendar ticks a game tick: 60 makes a game tick an hour.</param>
        public Rig(long start, long scale = 60, DipSet? set = null, long? origin = null)
        {
            var diplomacy = set ?? Isles();
            var calendarChannels = new CalendarChannels();
            calendarChannels.Install(Session);
            var channels = new DiplomacyChannels();
            channels.Install(Session);
            Calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, scale, 1), () => Weather, calendarChannels);
            Diplomacy = new DiplomacyComponent(DipRuntime.Create(diplomacy), DiplomacyCalendar.Clock(Calendar, diplomacy, origin ?? start), DiplomacyCalendar.HostFacts(Calendar, () => Game), channels);
            Session.AddComponent(Calendar, 10);
            Session.AddComponent(Diplomacy, 20);
            Session.Subscribe<DiplomacySeasonChanged>(Seasons.Add);
            Session.Subscribe<DiplomacyRefused>(Refused.Add);
            Session.Init();
        }

        public void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) Assert.Equal(1, Session.Update(20_000));
        }

        public void RunDays(int days) => Run(days * 24);
    }

    private static DipSet WithSeasons(params string[] ids) => Samples.Calm(root =>
    {
        var seasons = new JsonArray();
        foreach (var id in ids) seasons.Add(new JsonObject { ["id"] = id, ["key"] = "season." + id, ["warCost"] = 1000 });
        root["seasons"] = seasons;
    });

    // ---- the two documents fit each other -------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleDiplomacyFitsTheSampleCalendar() => DiplomacyCalendar.Check(Isles(), Gregorian);

    [Fact]
    public void ADocumentThatDoesNotFitTheCalendarIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(DipSet set, CalendarSet calendar, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => DiplomacyCalendar.Check(set, calendar));
            Assert.Equal(DiplomacyCalendar.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(Isles(r => r["time"]!["stepsPerDay"] = 7), Gregorian, "does not divide into 7");
        Refused(WithSeasons("spring", "summer", "autumn", "winter", "monsoon"), Gregorian, "has a cost of war for season 'monsoon', which the calendar does not have");
        Refused(WithSeasons("spring", "summer", "autumn"), Gregorian, "the calendar has season 'winter', for which the diplomacy has no cost of war");
        var valley = CalendarLoader.Load(Samples.Read("valley.calendar.json"));
        Refused(Isles(), valley, "has a cost of war for season 'autumn'");
        Refused(Isles(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.nonsense", ["type"] = "bool" })), Gregorian, "gives no fact 'cal.nonsense'");
        Refused(Isles(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.year", ["type"] = "bool" })), Gregorian, "'cal.year' is Integer in the calendar and Boolean in the diplomacy document");
        Refused(Isles(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 5, ["max"] = 10 })), Gregorian, "outside the 5 to 10 the diplomacy document declares");
        DiplomacyCalendar.Check(Isles(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 0, ["max"] = 23 })), Gregorian);
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.Check(null!, Gregorian));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.Check(Isles(), null!));
    }

    // ---- the calendar's time and seasons ----------------------------------------------------------------------------------------------

    [Fact]
    public void TheDiplomacyLivesOneStepADayOfTheCalendarAndTakesTheCalendarsSeasons()
    {
        var rig = new Rig(At(2023, "january", 1));
        rig.RunDays(120);
        Assert.Equal(120, rig.Diplomacy.Runtime.Step);
        Assert.Equal(["winter", "spring"], rig.Seasons.Select(s => s.Season).ToArray());
        var spring = rig.Seasons.Single(s => s.Season == "spring").Step;
        Assert.Equal("winter", rig.Seasons.Single(s => s.Season == "spring").From);
        for (var step = 0L; step <= 119; step++)
        {
            var moment = Gregorian.System.MomentOf(At(2023, "january", 1) + step * 1440);
            Assert.Equal(Gregorian.Seasons[Gregorian.SeasonOf(moment.Day)].Id, rig.Seasons.Last(s => s.Step <= step).Season);
        }
        Assert.Equal(59, spring);                      // the sample calendar begins spring on the first of March: January and February of 2023 are 59 days
    }

    [Fact]
    public void ALeapYearMovesTheStartOfSpringByADay()
    {
        long Spring(int year)
        {
            var rig = new Rig(At(year, "january", 1));
            rig.RunDays(130);
            return rig.Seasons.Single(s => s.Season == "spring").Step;
        }
        Assert.Equal(Spring(2023) + 1, Spring(2024));
    }

    [Fact]
    public void TheStepsAreCountedFromTheOriginAndBeforeItTheDiplomacyHasNoStep()
    {
        var rig = new Rig(At(2024, "january", 1), origin: At(2024, "january", 11));
        rig.RunDays(5);
        Assert.Equal(-1, rig.Diplomacy.Runtime.Step);
        rig.RunDays(10);
        Assert.Equal(5, rig.Diplomacy.Runtime.Step);
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void TheCalendarAndTheGameCannotBothGiveAFact()
    {
        var rig = new Rig(At(2024, "january", 1));
        rig.Game["cal.year"] = FactValue.From(2000L);
        var facts = DiplomacyCalendar.HostFacts(rig.Calendar, () => rig.Game);
        Assert.Equal(DiplomacyCalendar.Mismatch, Samples.Code(() => facts()));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.HostFacts(null!, () => rig.Game));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.HostFacts(rig.Calendar, null!));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.Clock(null!, Isles(), 0));
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.Clock(rig.Calendar, null!, 0));
    }

    // ---- terms in months and years ----------------------------------------------------------------------------------------------------

    [Fact]
    public void ATermInMonthsAndYearsIsCountedInDaysByTheCalendarFromToday()
    {
        var leap = new Rig(At(2024, "january", 1));
        Assert.Equal(366, DiplomacyCalendar.TermDays(leap.Calendar, 0, 1));
        Assert.Equal(366, DiplomacyCalendar.TermDays(leap.Calendar, 12, 0));
        Assert.Equal(91, DiplomacyCalendar.TermDays(leap.Calendar, 3, 0));          // January 31, February 29, March 31
        Assert.Equal(366 + 31, DiplomacyCalendar.TermDays(leap.Calendar, 1, 1));
        var plain = new Rig(At(2025, "january", 1));
        Assert.Equal(365, DiplomacyCalendar.TermDays(plain.Calendar, 0, 1));
        Assert.Equal(59, DiplomacyCalendar.TermDays(plain.Calendar, 2, 0));
        var late = new Rig(At(2024, "february", 15));
        Assert.Equal(366, DiplomacyCalendar.TermDays(late.Calendar, 0, 1));          // February 15 2024 to February 15 2025 takes in February 29 2024
        Assert.Equal(365, DiplomacyCalendar.TermDays(new Rig(At(2023, "february", 15)).Calendar, 0, 1));          // and 2023 to 2024 does not: that one falls on the 29th, after the 15th
    }

    [Fact]
    public void ATermThatGoesBackIsEmptyOrTooLongIsRefused()
    {
        var rig = new Rig(At(2024, "january", 1));
        Assert.Equal(DipCodes.Terms, Samples.Code(() => DiplomacyCalendar.TermDays(rig.Calendar, -1, 0)));
        Assert.Equal(DipCodes.Terms, Samples.Code(() => DiplomacyCalendar.TermDays(rig.Calendar, 0, -1)));
        Assert.Equal(DipCodes.Terms, Samples.Code(() => DiplomacyCalendar.TermDays(rig.Calendar, 0, 0)));
        Assert.Equal(DipCodes.Terms, Samples.Code(() => DiplomacyCalendar.TermDays(rig.Calendar, 0, 101)));
        Assert.InRange(DiplomacyCalendar.TermDays(rig.Calendar, 0, 99), 36_000, 36_500);
        Assert.Throws<ArgumentNullException>(() => DiplomacyCalendar.TermDays(null!, 1, 0));
    }

    [Fact]
    public void ATreatyOfAYearRunsForTheDaysOfThatYear()
    {
        var rig = new Rig(At(2024, "january", 1));
        rig.RunDays(2);
        var days = DiplomacyCalendar.TermDays(rig.Calendar, 0, 1);
        rig.Session.Submit(new DiplomacyRequested("t", "propose", "aurel", "dune", "nap", days));
        rig.Run(1);
        rig.RunDays(4);
        var treaty = Assert.Single(rig.Diplomacy.Runtime.Treaties);
        Assert.Equal(treaty.Start + 366, treaty.End);
    }
}
