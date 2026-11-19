using AIEngine;
using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using CalendarEngine;
using CalendarEngine.Sim;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

/// <summary>One proof of each integration: the calendar facts as dialogue conditions, the time as an AI percept, and the calendar as SimEngine own calendar projection.</summary>
public sealed class IntegrationTests
{
    private static CalendarSet Set => Samples.Gregorian;

    private static long At(string month, int day, int hour = 0, int minute = 0, long year = 2024) => Samples.At(Set, year, month, day, hour, minute);

    // ---- dialogue --------------------------------------------------------------------------------------------------------------------

    private sealed class Speakers : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "npc";
    }

    private const string Greeting = """
    { "format": 3, "id": "calendar.greeting", "variables": [],
      "externalFacts": [ { "id": "cal.season", "type": "string" }, { "id": "cal.is_holiday", "type": "bool" }, { "id": "cal.hour", "type": "int", "min": 0, "max": 23 } ],
      "speakers": [ { "id": "npc", "portraits": [ "calm" ] } ], "quests": [],
      "conversations": [ { "id": "greet", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [
            { "when": { "fact": "ext.cal.is_holiday", "op": "eq", "value": true }, "next": "festive" },
            { "when": { "all": [ { "fact": "ext.cal.season", "op": "eq", "value": "winter" }, { "fact": "ext.cal.hour", "op": "gte", "value": 18 } ] }, "next": "dark-cold" },
            { "when": { "fact": "ext.cal.season", "op": "eq", "value": "winter" }, "next": "cold" } ], "otherwise": "plain" },
        { "id": "festive", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "greet.festive", "next": "bye" },
        { "id": "dark-cold", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "greet.dark-cold", "next": "bye" },
        { "id": "cold", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "greet.cold", "next": "bye" },
        { "id": "plain", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "greet.plain", "next": "bye" },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    /// <summary>
    /// The real game-layer pieces: a calendar component and a dialogue component on one session, each with its own channel registry, the dialogue component reading the calendar component's
    /// facts (prefix <c>ext.</c>) at the tick it answers the start request, and the line arriving as a <see cref="DialogueLineShown"/> event.
    /// </summary>
    private static string Greet(long instant)
    {
        var speakers = new Speakers();
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var dialogueChannels = new DialogueChannels();
        dialogueChannels.Install(session);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Set, instant, 1, 1), () => new Dictionary<string, FactValue>(), calendarChannels);
        var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Greeting, speakers), 1), speakers, () => calendar.Facts("ext."), dialogueChannels);
        session.AddComponent(calendar, 10);
        session.AddComponent(dialogue, 20);
        var lines = new List<string>();
        session.Subscribe<DialogueLineShown>(e => lines.Add(e.Key));
        session.Init();
        session.Submit(new DialogueStartRequested("r1", "greet"));
        session.Update(1_000_000 / CalendarHost.TicksPerSecond);
        return Assert.Single(lines);
    }

    [Fact]
    public void ADialogueConditionReadsTheSeasonTheHourAndTheHolidayFromTheCalendar()
    {
        Assert.Equal("greet.plain", Greet(At("april", 10, 10)));
        Assert.Equal("greet.cold", Greet(At("january", 10, 10)));
        Assert.Equal("greet.dark-cold", Greet(At("january", 10, 19)));
        Assert.Equal("greet.festive", Greet(At("december", 25, 19)));   // a holiday outranks the cold
        Assert.Equal("greet.plain", Greet(At("june", 21, 12)));
    }

    [Fact]
    public void TheFactsADialogueDocumentDeclaresMatchTheTypesTheCalendarGives()
    {
        var definitions = CalendarFacts.Definitions(Set.System, Set.Moons);
        Assert.Equal(FactKind.String, definitions["cal.season"].Kind);
        Assert.Equal(FactKind.Boolean, definitions["cal.is_holiday"].Kind);
        Assert.Equal((0L, 23L), (definitions["cal.hour"].Minimum, definitions["cal.hour"].Maximum));
    }

    // ---- AI --------------------------------------------------------------------------------------------------------------------------

    private const string Lamplighter = """
    { "format": 1, "id": "calendar.ai",
      "keys": [ { "id": "sunny", "type": "bool", "initial": true, "source": "agent" } ],
      "percepts": [ { "kind": "time", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "daypart", "type": "string", "unknown": "" }, { "id": "season", "type": "string", "unknown": "" },
          { "id": "is_day", "type": "bool", "unknown": true }, { "id": "light", "type": "int", "min": 0, "max": 1000, "unknown": 1000 } ] } ],
      "intents": [ { "id": "lamp", "args": [ { "id": "on", "type": "bool" } ] } ],
      "behaviors": [ { "id": "lamplighter", "every": 5, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "look", "kind": "let", "key": "sunny", "value": { "known": { "from": "time", "id": "now", "attr": "is_day" } } },
          { "id": "decide", "kind": "selector", "resume": false, "children": [
              { "id": "by-day", "kind": "sequence", "resume": false, "children": [
                  { "id": "is-sunny", "kind": "condition", "when": { "fact": "key.sunny", "op": "eq", "value": true } },
                  { "id": "off", "kind": "act", "intent": "lamp", "args": { "on": false } } ] },
              { "id": "on", "kind": "act", "intent": "lamp", "args": { "on": true } } ] } ] } } ] }
    """;

    [Fact]
    public void AnAgentKnowsTheSunHasSetOnlyWhenItIsToldAndActsOnWhatItKnows()
    {
        var set = AiLoader.Load(Lamplighter);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        AiEventCatalog.Declare(session);
        var channels = new CalendarChannels();
        channels.Install(session);
        var perceivers = new List<string>();
        var calendar = new CalendarComponent(CalendarRuntime.Create(Set, At("march", 6, 18, 25), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        var bridge = new CalendarAiBridge("calendar-ai", calendar, "ai", set, "time", "now", () => perceivers);
        session.AddComponent(calendar, 10);
        session.AddComponent(bridge, 11);
        session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
        var lamps = new List<(long Tick, string Args)>();
        var refused = new List<string>();
        session.Subscribe<AiIntentRaised>(e => lamps.Add((e.Tick, e.Args)));
        session.Subscribe<AiRefused>(e => refused.Add(e.Code));
        session.Init();
        session.Submit(new AiAgentAddRequested("ai", "add", "mira", "lamplighter"));
        // Nobody is named as a perceiver at first: the agent knows nothing of the sky, so it thinks the sun is up (the document says an unknown is day) and keeps its lamp off.
        for (var i = 0; i < 12; i++) session.Update(CalendarHost.TickMicroseconds);
        Assert.NotEmpty(lamps);
        Assert.All(lamps, l => Assert.Equal("on=false", l.Args));
        Assert.Equal(0, bridge.Posted);
        // The sun has set (18:30) while the agent was told nothing. Now it is told, and the next time it thinks it lights the lamp.
        perceivers.Add("mira");
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(1, bridge.Posted);
        var told = lamps.Count;
        for (var i = 0; i < 6; i++) session.Update(CalendarHost.TickMicroseconds);
        Assert.Contains(lamps.Skip(told), l => l.Args == "on=true");
        Assert.Empty(refused);
    }

    [Fact]
    public void TheBridgeTellsAgainWhenTheSunSetsInsideAPartOfTheDayAndNotInBetween()
    {
        var set = AiLoader.Load(Lamplighter);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        AiEventCatalog.Declare(session);
        var channels = new CalendarChannels();
        channels.Install(session);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Set, At("march", 6, 17, 0), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        var bridge = new CalendarAiBridge("calendar-ai", calendar, "ai", set, "time", "now", () => ["mira"]);
        session.AddComponent(calendar, 10);
        session.AddComponent(bridge, 11);
        session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
        var posts = new List<(string Id, string Attrs)>();
        session.Subscribe<AiPerceptPosted>(e => posts.Add((e.RequestId, e.Attrs)));
        session.Init();
        session.Submit(new AiAgentAddRequested("ai", "add", "mira", "lamplighter"));
        // 17:00 to 19:00 in minute ticks: the evening part starts at 18:00 (while the sun is up), the sun sets at 18:30, the twilight light falls every minute until 19:00.
        for (var i = 0; i < 120; i++) session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal("daypart=afternoon;season=spring;is_day=true;light=1000", posts[0].Attrs);
        Assert.Contains(posts, p => p.Attrs == "daypart=evening;season=spring;is_day=true;light=1000");
        Assert.Contains(posts, p => p.Attrs == "daypart=evening;season=spring;is_day=false;light=1000");
        Assert.Contains(posts, p => p.Attrs == "daypart=evening;season=spring;is_day=false;light=0");
        Assert.True(posts.Count < 40, "told " + posts.Count + " times in two hours");
    }

    [Fact]
    public void TheBridgeRefusesAPerceptKindThePerceptDocumentDoesNotDeclareAsItNeeds()
    {
        var calendar = new CalendarHost(Set, At("march", 6, 10)).Component;
        Func<IReadOnlyList<string>> nobody = () => [];
        Assert.Equal(CalendarAiBridge.BadKind, Fixture.CodeOf(() => new CalendarAiBridge("b", calendar, "ai", AiLoader.Load(Lamplighter), "missing", "now", nobody)));
        var kinds = new[]
        {
            Lamplighter.Replace("\"max\": 1000, \"unknown\": 1000", "\"max\": 500, \"unknown\": 400"),
            Lamplighter.Replace("{ \"id\": \"season\", \"type\": \"string\", \"unknown\": \"\" },", ""),
            Lamplighter.Replace("\"id\": \"season\", \"type\": \"string\", \"unknown\": \"\"", "\"id\": \"season\", \"type\": \"int\", \"min\": 0, \"max\": 3, \"unknown\": 0"),
            Lamplighter.Replace("{ \"id\": \"light\"", "{ \"id\": \"extra\", \"type\": \"bool\", \"unknown\": true }, { \"id\": \"light\""),
            Lamplighter.Replace("\"owned\": false", "\"owned\": true, \"ownerCapacity\": 1"),
            Lamplighter.Replace("\"min\": 0, \"max\": 1000, \"unknown\": 1000", "\"min\": 1, \"max\": 1000, \"unknown\": 1000"),
        };
        foreach (var document in kinds) Assert.Equal(CalendarAiBridge.BadKind, Fixture.CodeOf(() => new CalendarAiBridge("b", calendar, "ai", AiLoader.Load(document), "time", "now", nobody)));
        _ = new CalendarAiBridge("b", calendar, "ai", AiLoader.Load(Lamplighter), "time", "now", nobody);
    }

    [Fact]
    public void AnAgentTheAiComponentDoesNotKnowIsRefusedByTheAiComponentAndNotIgnored()
    {
        var set = AiLoader.Load(Lamplighter);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        AiEventCatalog.Declare(session);
        var channels = new CalendarChannels();
        channels.Install(session);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Set, At("march", 6, 10), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        session.AddComponent(calendar, 10);
        session.AddComponent(new CalendarAiBridge("calendar-ai", calendar, "ai", set, "time", "now", () => ["ghost"]), 11);
        session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
        var refused = new List<string>();
        session.Subscribe<AiRefused>(e => refused.Add(e.Code));
        session.Init();
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(["AI_UNKNOWN_AGENT"], refused.ToArray());
    }

    [Fact]
    public void WhoPerceivesIsTheHostsDecisionAndNotifyTellsThemAgain()
    {
        var set = AiLoader.Load(Lamplighter);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        AiEventCatalog.Declare(session);
        var channels = new CalendarChannels();
        channels.Install(session);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Set, At("march", 6, 10), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        var perceivers = new List<string> { "a", "b" };
        var bridge = new CalendarAiBridge("calendar-ai", calendar, "ai", set, "time", "now", () => perceivers);
        session.AddComponent(calendar, 10);
        session.AddComponent(bridge, 11);
        var targets = new List<string>();
        session.Subscribe<AiPerceptPosted>(e => targets.Add(e.Agent));
        session.Init();
        session.Update(CalendarHost.TickMicroseconds);
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(["a", "b"], targets.ToArray());
        perceivers.Add("c");
        bridge.Notify();
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(["a", "b", "a", "b", "c"], targets.ToArray());
    }

    // ---- SimEngine -------------------------------------------------------------------------------------------------------------------

    private sealed class FakeTime(long minuteTicks) : ISimulationTime
    {
        public string Id => "fake";
        public long QuantumNumerator => 60;
        public long QuantumDenominator => minuteTicks;
        public long ExactTicks(GameDuration duration) => duration.Equals(GameDuration.Minutes(1)) ? minuteTicks : throw new ArgumentException("this clock only knows the minute");
        public long Ticks(GameDuration duration) => throw new NotSupportedException();
        public long Ticks(GameDuration duration, long numerator, long denominator) => throw new NotSupportedException();
        public long Hours(long hours) => throw new NotSupportedException();
        public long Days(long days) => throw new NotSupportedException();
        public Fix RateFor(Fix perUnit, DurationUnit unit, long ticks = 1) => throw new NotSupportedException();
        public CalendarReading Calendar(long tick) => throw new NotSupportedException();
    }

    [Fact]
    public void ACalendarIsSimEnginesCalendarProjectionOnItsTicks()
    {
        var calendar = new CalendarSimCalendar(Set, At("february", 29, 23, 58));
        var time = new FakeTime(1);
        calendar.Validate(time);
        var at0 = calendar.Project(time, 0);
        Assert.Equal(("gregorian-town", 0L), (at0.Calendar, at0.Tick));
        Assert.Equal((2024L, 2L, 29L, 60L, 23L, 58L), (at0["year"].Value, at0["month"].Value, at0["day"].Value, at0["day-of-year"].Value, at0["hour"].Value, at0["minute"].Value));
        var later = calendar.Project(time, 3);   // three minutes on: midnight, March 1
        Assert.Equal((2024L, 3L, 1L, 0L, 1L, 1L), (later["year"].Value, later["month"].Value, later["day"].Value, later["hour"].Value, later["minute"].Value, later["season"].Value));
        Assert.Equal((12L, 31L, 7L, 4L), (later["month"].Total, later["day"].Total, later["weekday"].Total, later["season"].Total));
        Assert.Equal(5L, later["weekday"].Value);   // friday
    }

    [Fact]
    public void AMinuteThatDoesNotDivideTheCalendarMinuteIsRefusedAndOneThatDoesIsAccepted()
    {
        var text = Fixture2.Mutate(Set, 8);
        var eight = CalendarLoader.Load(text);
        var calendar = new CalendarSimCalendar(eight, 0);
        Assert.Throws<Beep.SimEngine.Content.ContentException>(() => calendar.Validate(new FakeTime(12)));   // 12 ticks to a sim minute, 8 to a calendar minute
        calendar.Validate(new FakeTime(4));    // 4 divides 8
        calendar.Validate(new FakeTime(16));   // 8 divides 16
        Assert.Throws<ArgumentException>(() => calendar.Project(new FakeTime(4), -1));
        Assert.Throws<Beep.SimEngine.Content.ContentException>(() => calendar.Validate(new BadTime()));
        Assert.Equal(8L * 10 / 4, calendar.InstantOf(new FakeTime(4), 10));
    }

    private sealed class BadTime : ISimulationTime
    {
        public string Id => "bad";
        public long QuantumNumerator => 1;
        public long QuantumDenominator => 1;
        public long ExactTicks(GameDuration duration) => throw new ArgumentException("a minute is not whole ticks");
        public long Ticks(GameDuration duration) => throw new NotSupportedException();
        public long Ticks(GameDuration duration, long numerator, long denominator) => throw new NotSupportedException();
        public long Hours(long hours) => throw new NotSupportedException();
        public long Days(long days) => throw new NotSupportedException();
        public Fix RateFor(Fix perUnit, DurationUnit unit, long ticks = 1) => throw new NotSupportedException();
        public CalendarReading Calendar(long tick) => throw new NotSupportedException();
    }

    [Fact]
    public void ARealSimGameReadsItsDateFromTheCalendarAsItsTicksRun()
    {
        var definition = QuickGame.Engine.QuickModule.CreateDefinition();
        var files = new DiskFileSource(Path.Combine(AppContext.BaseDirectory, "QuickContent"));
        var game = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes)).NewGame(new NewGameRequest { Scenario = "quick", Seed = 42 });
        var calendar = new CalendarSimCalendar(Set, At("december", 31, 23, 0));
        calendar.Validate(game.SimulationTime);   // the quick game's tick is 60 seconds: one calendar minute
        for (long i = 1; i <= 61; i++) Assert.True(game.Update(i));
        var reading = calendar.Project(game.SimulationTime, game.Tick);
        Assert.Equal(61, game.Tick);
        Assert.Equal((2025L, 1L, 1L, 0L, 1L), (reading["year"].Value, reading["month"].Value, reading["day"].Value, reading["hour"].Value, reading["minute"].Value));
        Assert.Equal(game.Tick, reading.Tick);
    }
}

internal static class Fixture2
{
    /// <summary>The Gregorian sample with a calendar minute of the given number of ticks (and no schedules, whose blocks do not depend on it).</summary>
    public static string Mutate(CalendarSet set, int ticksPerMinute)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "gregorian.calendar.json")))!.AsObject();
        root["time"] = new System.Text.Json.Nodes.JsonObject { ["hoursPerDay"] = 24, ["minutesPerHour"] = 60, ["ticksPerMinute"] = ticksPerMinute };
        return root.ToJsonString();
    }
}
