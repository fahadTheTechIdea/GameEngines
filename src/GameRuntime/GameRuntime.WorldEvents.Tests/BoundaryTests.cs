using System.Text.Json.Nodes;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;
using Xunit;

namespace GameRuntime.WorldEvents.Tests;

/// <summary>Written from the survivors of the first round of mutation checks: the edges of the component's comparisons and the fields the first tests did not tell apart.</summary>
public sealed class BoundaryTests
{
    [Fact]
    public void TheOwedQueueMayHoldExactlyWhatOneUpdateRaises()
    {
        var runtime = WorldRuntime.Create(Samples.Tiny);
        var channels = new WorldChannels();
        _ = new WorldComponent(runtime, new TickWorldClock(Samples.Tiny), () => Samples.Alert(), channels, maxEvents: 5, maxOwed: 5);
    }

    [Fact]
    public void AnInvalidHostFactIsRefusedAsTheAdvanceAndNotAsTheClock()
    {
        var host = new WorldHost();
        host.HostFacts["ext.alert"] = FactValue.From("x");
        host.Tick();
        var refused = Assert.Single(host.Of<WorldRefused>());
        Assert.Equal((WorldCodes.FactInvalid, WorldComponent.AdvanceRequestId), (refused.Code, refused.RequestId));
    }

    [Fact]
    public void TheSecondSeasonChangeNamesTheSeasonBefore()
    {
        var host = new WorldHost();
        host.Tick(14);
        var changes = host.Of<WorldSeasonChanged>().ToList();
        Assert.Equal([("warm", ""), ("cold", "warm")], changes.Select(c => (c.Season, c.From)));
    }

    [Fact]
    public void TheAnswersCarryTheRiverAndTheSnowEachWhereItBelongs()
    {
        var host = new WorldHost();
        host.Tick(30);
        host.Runtime.Regions[0].River = 111;
        host.Runtime.Regions[0].Snow = 222;
        host.Ask("weather", "a");
        host.Tick();
        var answer = Assert.Single(host.Of<WorldWeatherAnswered>());
        Assert.NotEqual(host.Runtime.Regions[0].River, host.Runtime.Regions[0].Snow);
        Assert.Equal((host.Runtime.Regions[0].River, host.Runtime.Regions[0].Snow), (answer.River, answer.Snow));        // the answer is given in the same Update that lived one more step
    }

    [Fact]
    public void AReplayOfNoTicksReturnsTheStateItWasGiven()
    {
        var runtime = WorldRuntime.Create(Samples.Tiny);
        var result = WorldReplay.Run(runtime, [], 0, 0, 50);
        Assert.Equal(runtime.StateDigest(), result.StateDigest);
        Assert.Equal(0, result.Events);
    }

    // ---- calendar --------------------------------------------------------------------------------------------------------------------

    private static readonly CalendarSet Gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));

    [Fact]
    public void TheWorldBeginsAtTheStepZeroTheMomentTheCalendarReachesItsOrigin()
    {
        var system = Gregorian.System;
        var start = system.InstantOf(new CalendarDate(2024, 0, 1), 0, 0);
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var worldChannels = new WorldChannels();
        worldChannels.Install(session);
        var weather = new Dictionary<string, FactValue> { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
        var game = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
        var calendar = new CalendarComponent(CalendarRuntime.Create(Gregorian, start, 60, 1), () => weather, calendarChannels);
        var world = new WorldComponent(WorldRuntime.Create(Samples.Valley), WorldCalendar.Clock(calendar, Samples.Valley, start + 10 * 1440), WorldCalendar.HostFacts(calendar, () => game), worldChannels);
        session.AddComponent(calendar, 10);
        session.AddComponent(world, 20);
        session.Init();
        for (var tick = 0; tick < 9 * 24; tick++) session.Update(20_000);
        Assert.Equal(-1, world.Runtime.Step);
        for (var tick = 0; tick < 24; tick++) session.Update(20_000);
        Assert.Equal(0, world.Runtime.Step);               // the instant is now exactly the origin: step zero, not still before it
    }

    [Fact]
    public void AHostFactRangeThatIsOutsideOnOneSideOnlyIsStillRefused()
    {
        WorldSet Valley(int min, int max)
        {
            var root = JsonNode.Parse(Samples.Read("valley.world.json"))!.AsObject();
            root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "cal.hour", ["type"] = "int", ["min"] = min, ["max"] = max });
            return WorldLoader.Load(root.ToJsonString());
        }
        Assert.Equal(WorldCalendar.Mismatch, Samples.Code(() => WorldCalendar.Check(Valley(0, 10), Gregorian)));       // the top of the calendar's hours is outside
        Assert.Equal(WorldCalendar.Mismatch, Samples.Code(() => WorldCalendar.Check(Valley(5, 23), Gregorian)));       // the bottom is
    }

    // ---- economy ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheShockIsAskedOfTheRegionsMarketAndNotOfTheRegionsId()
    {
        var economy = EconomyEngine.EconomyLoader.Load(EconomyText());
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        new WorldChannels().Install(session);
        new EconomyChannels().Install(session);
        var bridge = new WorldEconomyShocks("shocks", Samples.Valley, economy);
        session.AddComponent(bridge, 10);
        var asked = new List<EconomyShockRequested>();
        session.Subscribe<EconomyShockRequested>(asked.Add);
        session.Init();
        session.Submit(new WorldEffectIssued("world", "", 1, 1, "shock", "drought", "riverton", "hillfort", 400, "hazard:drought"));
        session.Update(20_000);
        Assert.Equal("hillfort", Assert.Single(asked).Market);
    }

    private static string EconomyText() => Samples.Read("town.economy.json");

    [Fact]
    public void AMarketNameOfOneCharacterIsCheckedToo()
    {
        var economy = EconomyEngine.EconomyLoader.Load(EconomyText());
        var world = Samples.Edited(root =>
        {
            root["regions"]!.AsArray()[0]!["market"] = "m";
            foreach (var shock in new[] { "sh1" }) _ = shock;
        });
        var withShock = EconomyEngine.EconomyLoader.Load(JsonNodeWithShock(economy));
        Assert.Equal(WorldEconomyShocks.Mismatch, Samples.Code(() => WorldEconomyShocks.Check(world, withShock)));
    }

    private static string JsonNodeWithShock(EconomyEngine.EconomySet _)
    {
        var root = JsonNode.Parse(EconomyText())!.AsObject();
        var copy = JsonNode.Parse(root["shocks"]!.AsArray()[0]!.ToJsonString())!.AsObject();
        copy["id"] = "sh1";
        root["shocks"]!.AsArray().Add(copy);
        return root.ToJsonString();
    }

    // ---- the agent -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnAgentPerceivesTheStrongestOfTheHazardsInItsRegion()
    {
        var set = Samples.Calm();
        var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
        var channels = new WorldChannels();
        channels.Install(session);
        AiEventCatalog.Declare(session);
        var ai = AIEngine.AiLoader.Load(AiDoc);
        var host = new Dictionary<string, FactValue> { ["game.garrison"] = FactValue.From(30L) };
        var world = new WorldComponent(WorldRuntime.Create(set), new TickWorldClock(set, ticksPerStep: 1_000_000), () => host, channels);
        var bridge = new WorldAiPercepts("ai-world", world, "seaport", "visibility", "mobility", "ai", ai, "weather", "sky", "farmer");
        var posts = new List<string>();
        session.AddComponent(world, 10);
        session.AddComponent(bridge, 11);
        session.AddComponent(new AiComponent(new AIEngine.AiRuntime(ai, 1), 1000), 12);
        session.Subscribe<AiPerceptPosted>(e => posts.Add(e.Attrs));
        session.Init();
        session.Submit(new AiAgentAddRequested("ai", "agent", "farmer", "farmer"));
        session.Update(20_000);
        // Seaport is hit by a great quake of its own (full strength) and by a storm that began in Riverton and reached it at half: the agent perceives the stronger.
        session.Submit(new WorldRequested("q", "hazard", "seaport", "quake", "great", 1, ""));
        session.Submit(new WorldRequested("s", "hazard", "riverton", "storm", "tempest", 1, ""));
        session.Update(20_000);
        session.Update(20_000);
        Assert.Contains("danger=1000", posts[^1], StringComparison.Ordinal);
    }

    private const string AiDoc = """
    { "format": 1, "id": "world.farmer2", "keys": [ { "id": "k_danger", "type": "int", "initial": 0, "min": 0, "max": 1000, "source": "agent" } ],
      "percepts": [ { "kind": "weather", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "weather", "type": "string", "unknown": "" }, { "id": "season", "type": "string", "unknown": "" },
          { "id": "visibility", "type": "int", "min": 0, "max": 1000, "unknown": 1000 }, { "id": "mobility", "type": "int", "min": 0, "max": 1000, "unknown": 1000 },
          { "id": "danger", "type": "int", "min": 0, "max": 1000, "unknown": 0 }, { "id": "sickness", "type": "int", "min": 0, "max": 1000, "unknown": 0 } ] } ],
      "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
      "behaviors": [ { "id": "farmer", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
          { "id": "see", "kind": "let", "key": "k_danger", "value": { "known": { "from": "weather", "id": "sky", "attr": "danger" } } },
          { "id": "act", "kind": "act", "intent": "do", "args": { "what": "work" } } ] } } ] }
    """;
}
