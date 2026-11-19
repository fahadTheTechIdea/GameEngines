using System.Text.Json.Nodes;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>The calendar and the world: the calendar gives the day clock and the seasons (leap years and season starts included); the two documents are checked against each other.</summary>
public sealed class CalendarIntegrationTests
{
    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    private static long At(int year, string month, int day, int hour = 0) => Gregorian.System.InstantOf(new CalendarDate(year, Gregorian.System.Months.First(m => m.Id == month).Index, day), hour, 0);

    private static WorldSet Valley(Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(Samples.Read("valley.world.json"))!.AsObject();
        edit?.Invoke(root);
        return WorldLoader.Load(root.ToJsonString());
    }

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public CalendarComponent Calendar { get; }
        public WorldComponent World { get; }
        public List<WorldSeasonChanged> Seasons { get; } = new();
        public List<WorldRefused> Refused { get; } = new();
        public Dictionary<string, FactValue> Weather { get; } = new() { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        public Dictionary<string, FactValue> Game { get; } = new() { ["game.garrison"] = FactValue.From(30L) };

        /// <param name="scale">Calendar ticks a game tick: 60 makes a game tick an hour.</param>
        public Rig(long start, long scale = 60, WorldSet? set = null, long? origin = null)
        {
            var world = set ?? Valley();
            var calendarChannels = new CalendarChannels();
            calendarChannels.Install(Session);
            var worldChannels = new WorldChannels();
            worldChannels.Install(Session);
            Calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, scale, 1), () => Weather, calendarChannels);
            World = new WorldComponent(WorldRuntime.Create(world), WorldCalendar.Clock(Calendar, world, origin ?? start), WorldCalendar.HostFacts(Calendar, () => Game), worldChannels);
            Session.AddComponent(Calendar, 10);
            Session.AddComponent(World, 20);
            Session.Subscribe<WorldSeasonChanged>(Seasons.Add);
            Session.Subscribe<WorldRefused>(Refused.Add);
            Session.Init();
        }

        public void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) Assert.Equal(1, Session.Update(20_000));
        }

        public void RunDays(int days) => Run(days * 24);
    }

    // ---- the two documents fit each other -------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleWorldFitsTheSampleCalendar() => WorldCalendar.Check(Valley(), Gregorian);

    [Fact]
    public void ADocumentThatDoesNotFitTheCalendarIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(WorldSet world, CalendarSet calendar, string fragment)
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => WorldCalendar.Check(world, calendar));
            Assert.Equal(WorldCalendar.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(Valley(r => r["time"]!["stepsPerDay"] = 7), Gregorian, "does not divide into 7");
        Refused(Samples.Tiny, Gregorian, "has a climate for season 'warm', which the calendar does not have");
        var realms = CalendarLoader.Load(Samples.Read("valley.calendar.json"));
        Refused(Valley(), realms, "has a climate for season 'autumn'");
        var monsoon = JsonNode.Parse(Samples.Read("gregorian.calendar.json"))!.AsObject();
        monsoon["seasons"]!.AsArray().Insert(2, new JsonObject { ["id"] = "monsoon", ["key"] = "cal.season.monsoon", ["month"] = "july", ["day"] = 15, ["sunrise"] = "6:00", ["sunset"] = "19:00" });
        Refused(Valley(), CalendarLoader.Load(monsoon.ToJsonString()), "has season 'monsoon', for which the world has no climate");
        Refused(Valley(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.nonsense", ["type"] = "bool" })), Gregorian, "gives no fact 'cal.nonsense'");
        Refused(Valley(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.year", ["type"] = "bool" })), Gregorian, "'cal.year' is Integer in the calendar and Boolean in the world document");
        Refused(Valley(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 5, ["max"] = 10 })), Gregorian, "outside the 5 to 10 the world document declares");
        WorldCalendar.Check(Valley(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 0, ["max"] = 23 })), Gregorian);
    }

    // ---- the calendar's time and seasons ----------------------------------------------------------------------------------------------

    [Fact]
    public void TheWorldLivesOneStepADayOfTheCalendarAndTakesTheCalendarsSeasons()
    {
        var rig = new Rig(At(2024, "january", 1));
        rig.RunDays(100);
        Assert.InRange(rig.World.Runtime.Step, 99, 100);       // 100 days lived: the day clock is on the 100th or has just turned to the 101st
        Assert.Equal(["winter", "spring"], rig.Seasons.Select(s => s.Season));
        Assert.Equal(60, rig.Seasons[1].Step);                 // 2024 is a leap year: March 1 is day 61 of it
        Assert.Equal("winter", rig.Seasons[1].From);
    }

    [Fact]
    public void ALeapYearMovesTheSeasonsAndANonLeapYearDoesNot()
    {
        var rig = new Rig(At(2023, "december", 1));
        rig.RunDays(100);
        // December 1st 2023 to March 1st 2024: 31 + 31 + 29 days.
        Assert.Equal(91, rig.Seasons.Single(s => s.Season == "spring").Step);
        var plain = new Rig(At(2025, "december", 1));
        plain.RunDays(100);
        Assert.Equal(90, plain.Seasons.Single(s => s.Season == "spring").Step);              // 31 + 31 + 28
    }

    [Fact]
    public void ThePausedCalendarHoldsTheWorldStill()
    {
        var rig = new Rig(At(2024, "january", 1));
        rig.RunDays(3);
        var step = rig.World.Runtime.Step;
        rig.Calendar.Runtime.SetPaused(true);
        rig.RunDays(5);
        Assert.Equal(step, rig.World.Runtime.Step);
        rig.Calendar.Runtime.SetPaused(false);
        rig.RunDays(2);
        Assert.True(rig.World.Runtime.Step > step);
    }

    [Fact]
    public void ASpedUpCalendarMakesTheWorldCatchUpItsStepsInOrder()
    {
        var fast = new Rig(At(2024, "january", 1), scale: 1440 * 3);          // three days a game tick
        fast.Run(40);                                                          // 120 days
        var slow = new Rig(At(2024, "january", 1), scale: 60);
        slow.RunDays(120);
        Assert.Equal(slow.World.Runtime.Step, fast.World.Runtime.Step);
        Assert.InRange(fast.World.Runtime.Step, 119, 120);
        Assert.Equal(slow.World.Runtime.StateDigest(), fast.World.Runtime.StateDigest());      // the same world whether time came in days or in threes
    }

    [Fact]
    public void ABeforeTheOriginInstantTheWorldHasNotBegun()
    {
        var rig = new Rig(At(2024, "january", 1), origin: At(2024, "january", 10));
        rig.RunDays(8);
        Assert.Equal(-1, rig.World.Runtime.Step);
        Assert.Empty(rig.Refused);
        rig.RunDays(3);
        Assert.True(rig.World.Runtime.Step >= 1);
    }

    [Fact]
    public void TheWorldIsTheSameWhenItIsLivedUnderTheCalendarOrUnderItsOwnCycleOfTheSameSeasons()
    {
        // The document's own cycle starts in spring; the calendar's year starts in winter: give both the same season for every step and the worlds are the same.
        var rig = new Rig(At(2024, "january", 1));
        rig.RunDays(200);
        var own = WorldRuntime.Create(Valley());
        var seasons = rig.Seasons;
        own.Advance(rig.World.Runtime.Step, step => Gregorian.Seasons[Gregorian.SeasonOf(Gregorian.System.MomentOf(At(2024, "january", 1) + step * 1440).Day)].Id, rig.Game.ToDictionary(p => p.Key, p => p.Value));
        Assert.Equal(own.StateDigest(), rig.World.Runtime.StateDigest());
        Assert.NotEmpty(seasons);
    }

    // ---- host facts -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWorldsHostFactsAreTheCalendarsTogetherWithTheGamesAndAFactBothGiveIsRefused()
    {
        var rig = new Rig(At(2024, "january", 1));
        rig.Run(2);
        var facts = WorldCalendar.HostFacts(rig.Calendar, () => rig.Game)();
        Assert.Equal(30, facts["game.garrison"].Integer);
        Assert.Equal("winter", facts["cal.season"].Text);
        Assert.Contains("weather.raining", facts.Keys);
        var clash = WorldCalendar.HostFacts(rig.Calendar, () => new Dictionary<string, FactValue> { ["cal.season"] = FactValue.From("x") });
        Assert.Equal(WorldCalendar.Mismatch, Samples.Code(() => clash()));
    }

    [Fact]
    public void AGarrisonFactTheGameChangesReachesTheWorldsConditionsThroughTheCalendarsHosting()
    {
        var weak = new Rig(At(2024, "january", 1));
        weak.Game["game.garrison"] = FactValue.From(10L);
        var strong = new Rig(At(2024, "january", 1));
        strong.Game["game.garrison"] = FactValue.From(90L);
        var raidersWeak = 0;
        var raidersStrong = 0;
        weak.Session.Subscribe<WorldCardPlayed>(c => { if (c.Card == "raiders") raidersWeak++; });
        strong.Session.Subscribe<WorldCardPlayed>(c => { if (c.Card == "raiders") raidersStrong++; });
        weak.RunDays(360);
        strong.RunDays(360);
        Assert.True(raidersWeak > 0);
        Assert.Equal(0, raidersStrong);
    }
}
