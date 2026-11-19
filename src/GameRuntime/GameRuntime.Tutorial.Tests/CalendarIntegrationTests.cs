using System.Text.Json.Nodes;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The calendar and the tutorial: the calendar gives the time, so hint timing is game time; the two documents are checked against each other; the game's own facts and the calendar's are one set.</summary>
public sealed class CalendarIntegrationTests
{
    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public CalendarComponent Calendar { get; }
        public TutorialComponent Tutorial { get; }
        public Dictionary<string, FactValue> Game { get; } = new(StringComparer.Ordinal)
        {
            ["input.gamepad"] = FactValue.From(false), ["ui.menu_open"] = FactValue.From(false), ["game.paused"] = FactValue.From(false), ["game.population"] = FactValue.From(0L),
        };
        public Dictionary<string, FactValue> Weather { get; } = new() { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        public List<TutorialHintChanged> Hints { get; } = new();

        public Rig(long scaleNumerator = 1, long origin = 0)
        {
            var calendarChannels = new CalendarChannels();
            calendarChannels.Install(Session);
            var tutorialChannels = new TutorialChannels();
            tutorialChannels.Install(Session);
            Calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, 0, scaleNumerator, 1), () => Weather, calendarChannels);
            var runtime = Samples.Done(Samples.Valley);
            runtime.Start();
            Tutorial = new TutorialComponent(runtime, TutorialCalendar.Clock(Calendar, Samples.Valley, origin), TutorialCalendar.HostFacts(Calendar, () => Game), tutorialChannels);
            Session.AddComponent(Calendar, 10);
            Session.AddComponent(Tutorial, 30);
            Session.Subscribe<TutorialHintChanged>(Hints.Add);
            Session.Init();
        }

        public void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) Assert.Equal(1, Session.Update(20_000));
        }
    }

    [Fact]
    public void TheSampleTutorialFitsTheSampleCalendar() => TutorialCalendar.Check(Samples.Valley, Gregorian);

    [Fact]
    public void ADocumentThatDoesNotFitTheCalendarIsRefusedWhenTheGameIsAssembled()
    {
        void Refused(Action<JsonObject> edit, string fragment)
        {
            var root = JsonNode.Parse(Samples.Read("valley.tutorial.json"))!.AsObject();
            edit(root);
            var set = TutorialLoader.Load(root.ToJsonString());
            var refusal = Assert.Throws<EngineRefusedException>(() => TutorialCalendar.Check(set, Gregorian));
            Assert.Equal(TutorialCalendar.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(r => r["time"]!["stepsPerDay"] = 7, "does not divide into 7");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.nonsense", ["type"] = "bool" }), "gives no fact 'cal.nonsense'");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.season", ["type"] = "bool" }), "fact 'cal.season' is");
        Refused(r => r["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.light", ["type"] = "int", ["min"] = 0, ["max"] = 500 }), "outside the 0 to 500");
    }

    [Fact]
    public void AHostFactOfTheGameOutsideTheCalendarsNamespaceIsTheGamesNotTheCalendars()
    {
        var root = JsonNode.Parse(Samples.Read("valley.tutorial.json"))!.AsObject();
        var set = TutorialLoader.Load(root.ToJsonString());
        TutorialCalendar.Check(set, Gregorian);                    // input.gamepad, ui.menu_open ... are not the calendar's to give
    }

    [Fact]
    public void TheStepsAreTheCalendarsTicksAndHintsCountGameTime()
    {
        var rig = new Rig();
        rig.Run(1);
        Assert.Equal(1, rig.Tutorial.Runtime.Step);                // a step is a calendar tick (a game minute) and the calendar runs one per session tick
        rig.Run(299);
        Assert.Equal(300, rig.Tutorial.Runtime.Step);
        Assert.Equal(0, rig.Tutorial.Runtime.StepState("move").Hint);   // the first hint is due after 300 idle steps: the step began at step 1
        rig.Run(1);
        Assert.Equal(1, rig.Tutorial.Runtime.StepState("move").Hint);
        Assert.Contains(rig.Hints, h => h.Step == "move" && h.Level == 1 && h.Tick == 301);
    }

    [Fact]
    public void ASpeededUpGameHintsSoonerInSessionTimeAndAPausedOneNotAtAll()
    {
        var fast = new Rig(scaleNumerator: 3);
        fast.Run(100);                                             // 300 game minutes
        Assert.Equal(0, fast.Tutorial.Runtime.StepState("move").Hint);
        fast.Run(1);
        Assert.Equal(1, fast.Tutorial.Runtime.StepState("move").Hint);

        var paused = new Rig();
        paused.Calendar.Runtime.SetPaused(true);
        paused.Run(2000);
        Assert.Equal(0, paused.Tutorial.Runtime.Step);
        Assert.Equal(0, paused.Tutorial.Runtime.StepState("move").Hint);   // a player in a menu, with the game stopped, is not stuck
        paused.Calendar.Runtime.SetPaused(false);
        paused.Run(299);
        Assert.Equal(0, paused.Tutorial.Runtime.StepState("move").Hint);
        paused.Run(1);
        Assert.Equal(1, paused.Tutorial.Runtime.StepState("move").Hint);
    }

    [Fact]
    public void ATutorialStartedLaterCountsFromItsOrigin()
    {
        var rig = new Rig(origin: 0);
        rig.Run(5);
        Assert.Equal(5, rig.Tutorial.Runtime.Step);
        var late = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(late);
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(late);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, 100, 1, 1), () => rig.Weather, calendarChannels);
        var clock = TutorialCalendar.Clock(calendar, Samples.Valley, 90);
        var component = new TutorialComponent(Samples.Done(Samples.Valley), clock, TutorialCalendar.HostFacts(calendar, () => rig.Game), tutorialChannels);
        late.AddComponent(calendar, 10);
        late.AddComponent(component, 30);
        late.Init();
        late.Update(20_000);
        Assert.Equal(11, component.Runtime.Step);                  // instant 101 less the origin 90
    }

    [Fact]
    public void AClockBeforeItsOriginIsAClockFaultReportedNotAGuess()
    {
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, 10, 1, 1), () => weather, calendarChannels);
        var component = new TutorialComponent(Samples.Done(Samples.Valley), TutorialCalendar.Clock(calendar, Samples.Valley, 1000), TutorialCalendar.HostFacts(calendar, () => Samples.ValleyHost()), tutorialChannels);
        session.AddComponent(calendar, 10);
        session.AddComponent(component, 30);
        var refused = new List<TutorialRefused>();
        session.Subscribe<TutorialRefused>(refused.Add);
        session.Init();
        session.Update(20_000);
        session.Update(20_000);
        Assert.Equal([TutorialCodes.StepOrder], refused.Select(r => r.Code));
        Assert.Equal(0, component.Runtime.Step);
    }

    [Fact]
    public void AFactBothTheCalendarAndTheGameGiveIsRefusedWhenTheFactsAreRead()
    {
        var rig = new Rig();
        var facts = TutorialCalendar.HostFacts(rig.Calendar, () => new Dictionary<string, FactValue> { ["cal.season"] = FactValue.From("x") });
        Assert.Equal(TutorialCalendar.Mismatch, Samples.Code(() => facts()));
        Assert.Throws<ArgumentNullException>(() => TutorialCalendar.Clock(null!, Samples.Valley, 0));
        Assert.Throws<ArgumentNullException>(() => TutorialCalendar.HostFacts(rig.Calendar, null!));
    }
}
