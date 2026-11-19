using System.Text.Json.Nodes;
using CalendarEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Relationship.Tests;

/// <summary>The calendar and the relationships (R15): the calendar gives the time, the facts and the schedule activities that satisfy needs; the two documents are checked against each other.</summary>
public sealed class CalendarIntegrationTests
{
    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    private static long At(string month, int day, int hour = 0) => Gregorian.System.InstantOf(new CalendarDate(2024, Gregorian.System.Months.First(m => m.Id == month).Index, day), hour, 0);

    private sealed class Rig
    {
        public RuntimeSession Session { get; }
        public CalendarComponent Calendar { get; }
        public RelationshipComponent Relationships { get; }
        public RelationshipCalendarActivities Activities { get; }
        public List<RelationshipRefused> Refused { get; } = new();
        public Dictionary<string, FactValue> Weather { get; } = new() { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };

        public Rig(long start, bool forwardActivities = true, RelationshipSet? set = null)
        {
            var relationshipSet = set ?? Samples.Village;
            Session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
            var calendarChannels = new CalendarChannels();
            calendarChannels.Install(Session);
            var relationshipChannels = new RelationshipChannels();
            relationshipChannels.Install(Session);
            Calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, 1, 1), () => Weather, calendarChannels);
            Relationships = new RelationshipComponent(RelationshipRuntime.Create(relationshipSet), RelationshipCalendar.Clock(Calendar, relationshipSet, start), RelationshipCalendar.HostFacts(Calendar), relationshipChannels);
            Activities = new RelationshipCalendarActivities("activities", Relationships);
            Session.AddComponent(Calendar, 10);
            if (forwardActivities) Session.AddComponent(Activities, 20);
            Session.AddComponent(Relationships, 30);
            Session.Subscribe<RelationshipRefused>(Refused.Add);
            Session.Init();
        }

        public void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) Assert.Equal(1, Session.Update(20_000));
        }
    }

    // ---- the two documents fit each other -------------------------------------------------------------------------------------------

    [Fact]
    public void TheSampleVillageFitsTheSampleCalendar() => RelationshipCalendar.Check(Samples.Village, Gregorian);

    [Fact]
    public void ADocumentThatDoesNotFitTheCalendarIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(Action<JsonObject> edit, string fragment)
        {
            var set = RelationshipLoader.Load(Edit.Doc(edit));
            var refusal = Assert.Throws<EngineRefusedException>(() => RelationshipCalendar.Check(set, Gregorian));
            Assert.Equal(RelationshipCalendar.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(r => r["time"]!["stepsPerDay"] = 7, "does not divide into 7");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.nonsense", ["type"] = "bool" }), "gives no fact 'cal.nonsense'");
        Refused(r => r["facts"]!.AsArray()[2]!["type"] = "string", "cal.is_holiday");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 5, ["max"] = 10 }), "outside the 5 to 10");
        Refused(r => r["sources"]!.AsArray().Add(new JsonObject { ["activity"] = "dance", ["need"] = "fun", ["perDay"] = 100 }), "'dance'");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "story.plague", ["type"] = "int", ["min"] = 0, ["max"] = 1 }), "story.plague");
    }

    [Fact]
    public void TheStepAtTheOriginIsZeroAndBeforeItMinusOne()
    {
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, At("january", 2), 1, 1), () => new Dictionary<string, FactValue>(), new CalendarChannels());
        Assert.Equal(0, RelationshipCalendar.Clock(calendar, Samples.Village, At("january", 2)).StepAt(0));
        Assert.Equal(-1, RelationshipCalendar.Clock(calendar, Samples.Village, At("january", 2) + 1).StepAt(0));
        Assert.Equal(0, RelationshipCalendar.Clock(calendar, Samples.Village, At("january", 2) - 59).StepAt(0));   // 59 ticks after the origin is still the first step
        Assert.Equal(1, RelationshipCalendar.Clock(calendar, Samples.Village, At("january", 2) - 60).StepAt(0));
    }

    [Fact]
    public void ADocumentWhoseFactRangeCoversTheCalendarsIsAccepted()
    {
        var set = RelationshipLoader.Load(Edit.Doc(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = 0, ["max"] = 23 })));
        RelationshipCalendar.Check(set, Gregorian);
    }

    [Fact]
    public void TheClockIsBuiltOnlyForADocumentThatFits()
    {
        var runtime = CalendarRuntime.Create(Gregorian, At("january", 1), 1, 1);
        var calendar = new CalendarComponent(runtime, () => new Dictionary<string, FactValue>(), new CalendarChannels());
        var bad = RelationshipLoader.Load(Edit.Doc(r => r["time"]!["stepsPerDay"] = 7));
        Assert.Equal(RelationshipCalendar.Mismatch, Fixture.Code(() => RelationshipCalendar.Clock(calendar, bad, 0)));
    }

    // ---- time -----------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARelationshipStepIsSixtyCalendarTicksFromTheOriginAndTheCalendarFactsDriveTheDecayRules()
    {
        var winter = new Rig(At("january", 1));
        winter.Session.Submit(new RelationshipActorAddRequested("w", "ghost", ""));
        winter.Run(720);          // twelve hours
        Assert.Equal(12, winter.Relationships.Runtime.Step);
        var spring = new Rig(At("april", 1));
        spring.Session.Submit(new RelationshipActorAddRequested("s", "ghost", ""));
        spring.Run(720);
        Assert.Equal(12, spring.Relationships.Runtime.Step);
        // Hunger 800 loses 900 a day; in winter the calendar says so (cal.season is winter) and it loses 30 percent more: 12 steps of 1,170 against 12 steps of 900, in scaled units over 24.
        Assert.Equal(19_200 - 12 * 1170, winter.Relationships.Runtime.Actor("ghost").Need[0]);
        Assert.Equal(19_200 - 12 * 900, spring.Relationships.Runtime.Actor("ghost").Need[0]);
        Assert.Empty(winter.Refused);
    }

    [Fact]
    public void ACalendarThatIsBeforeTheOriginIsAClockFaultAndNothingMoves()
    {
        var rig = new Rig(At("january", 1));
        var later = new Rig(At("january", 1));   // a calendar whose origin is after its own start: build the clock directly
        var set = Samples.Village;
        var calendarChannels = new CalendarChannels();
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, At("january", 1), 1, 1), () => later.Weather, calendarChannels);
        var clock = RelationshipCalendar.Clock(calendar, set, At("january", 2));
        Assert.Equal(-1, clock.StepAt(0));
        _ = rig;
    }

    // ---- activities satisfy needs --------------------------------------------------------------------------------------------------

    [Fact]
    public void AScheduleBlockThatBeginsIsForwardedAsTheActivityAndTheNeedsOfThatActivityFeedTheActor()
    {
        var rig = new Rig(At("january", 2));     // a Tuesday: the shopkeeper's base routine
        rig.Session.Submit(new CalendarActorAddRequested("c-alice", "alice", "shopkeeper"));
        rig.Session.Submit(new RelationshipActorAddRequested("r-alice", "alice", ""));
        rig.Session.Submit(new RelationshipActorAddRequested("r-ghost", "ghost", ""));
        rig.Run(1440);
        Assert.True(rig.Activities.Forwarded > 5, "the shopkeeper changes block several times a day, forwarded " + rig.Activities.Forwarded);
        var runtime = rig.Relationships.Runtime;
        var alice = runtime.NeedLevel(runtime.Actor("alice"), 0);
        var ghost = runtime.NeedLevel(runtime.Actor("ghost"), 0);
        Assert.True(alice > ghost + 400, $"alice eats at 7 and 12 and sits in the tavern in the evening (hunger {alice}); the ghost eats nothing (hunger {ghost})");
        Assert.Equal(0, ghost);
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void WhatTheActorIsDoingIsTheActivityOfItsBlockAtTheEndOfEachBlock()
    {
        var rig = new Rig(At("january", 2));
        rig.Session.Submit(new CalendarActorAddRequested("c-alice", "alice", "shopkeeper"));
        rig.Session.Submit(new RelationshipActorAddRequested("r-alice", "alice", ""));
        var seen = new List<(long Tick, string Activity)>();
        rig.Session.Subscribe<RelationshipApplied>(e => { });
        var runtime = rig.Relationships.Runtime;
        var last = "";
        for (var tick = 1; tick <= 1440; tick++)
        {
            rig.Run(1);
            if (runtime.Actors.TryGetValue("alice", out var alice) && alice.Activity != last) { last = alice.Activity; seen.Add((tick, last)); }
        }
        // 00:00 sleep, 07:00 eat, 08:00 work, 12:00 eat, 13:00 work, 18:00 idle, 22:00 sleep (the calendar announces a block in the tick it begins, the bridge forwards it and the component answers in the same tick; the first block waited a tick for the actor to be known).
        Assert.Equal(["sleep", "eat", "work", "eat", "work", "idle", "sleep"], seen.Select(s => s.Activity));
        Assert.All(new[] { 420, 480, 720, 780, 1080, 1320 }.Zip(seen.Skip(1)), p => Assert.InRange(p.Second.Tick - p.First, 0, 2));
    }

    [Fact]
    public void WithoutTheBridgeNobodyIsDoingAnythingAndTheNeedsOnlyFall()
    {
        var rig = new Rig(At("january", 2), forwardActivities: false);
        rig.Session.Submit(new CalendarActorAddRequested("c-alice", "alice", "shopkeeper"));
        rig.Session.Submit(new RelationshipActorAddRequested("r-alice", "alice", ""));
        rig.Run(1440);
        Assert.Equal("", rig.Relationships.Runtime.Actor("alice").Activity);
        Assert.Equal(0, rig.Relationships.Runtime.NeedLevel(rig.Relationships.Runtime.Actor("alice"), 0));
    }

    [Fact]
    public void ABlockOfSomeoneTheRelationshipsDoNotKnowYetWaitsForThem()
    {
        var rig = new Rig(At("january", 2));
        rig.Session.Submit(new CalendarActorAddRequested("c-stranger", "stranger", "guard"));
        rig.Run(200);
        Assert.Equal(0, rig.Activities.Forwarded);
        Assert.Equal(1, rig.Activities.Waiting);
        Assert.Empty(rig.Refused);
    }

    [Fact]
    public void ABlockOfAnotherCalendarChannelIsNotForwarded()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var relationshipChannels = new RelationshipChannels();
        relationshipChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var start = At("january", 2);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, 1, 1), () => weather, calendarChannels, "dungeon", "dungeon");
        var relationships = new RelationshipComponent(RelationshipRuntime.Create(Samples.Village), RelationshipCalendar.Clock(calendar, Samples.Village, start), RelationshipCalendar.HostFacts(calendar), relationshipChannels);
        var activities = new RelationshipCalendarActivities("activities", relationships);   // listens to the calendar of the empty channel only
        session.AddComponent(calendar, 10);
        session.AddComponent(activities, 20);
        session.AddComponent(relationships, 30);
        session.Init();
        session.Submit(new CalendarActorAddRequested("c-alice", "alice", "shopkeeper", "dungeon"));
        session.Submit(new RelationshipActorAddRequested("r-alice", "alice", ""));
        for (var i = 0; i < 100; i++) session.Update(20_000);
        Assert.Equal((0, 0), (activities.Forwarded, activities.Waiting));
    }

    // ---- the relationship run is a function of its own journal ------------------------------------------------------------------------

    [Fact]
    public void ARunDrivenByTheCalendarReplaysFromTheRelationshipJournalAloneBecauseTheJournalHoldsTheStepsFactsAndRequests()
    {
        var relationshipSet = Samples.Village;
        var start = At("january", 2);
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var relationshipChannels = new RelationshipChannels();
        relationshipChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, 1, 1), () => weather, calendarChannels);
        var journal = new RelationshipJournal();
        var relationships = new RelationshipComponent(RelationshipRuntime.Create(relationshipSet), RelationshipCalendar.Clock(calendar, relationshipSet, start), RelationshipCalendar.HostFacts(calendar), relationshipChannels, journal: journal);
        session.AddComponent(calendar, 10);
        session.AddComponent(new RelationshipCalendarActivities("activities", relationships), 20);
        session.AddComponent(relationships, 30);
        var transcript = new RelationshipTranscript(session);
        session.Init();
        session.Submit(new CalendarActorAddRequested("c-alice", "alice", "shopkeeper"));
        session.Submit(new CalendarActorAddRequested("c-dan", "dan", "guard"));
        session.Submit(new RelationshipActorAddRequested("r-alice", "alice", "kind"));
        session.Submit(new RelationshipActorAddRequested("r-dan", "dan", "stoic"));
        for (var tick = 1; tick <= 2880; tick++)
        {
            if (tick == 1000) weather["weather.raining"] = FactValue.From(true);
            session.Update(20_000);
        }
        var replay = RelationshipReplay.Run(RelationshipRuntime.Create(relationshipSet), RelationshipJournal.Parse(journal.ToText()), 0, 2880, 50);
        Assert.Equal(relationships.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(transcript.Hash, replay.TranscriptHash);
        Assert.True(replay.Events > 20, "events: " + replay.Events);
    }
}
