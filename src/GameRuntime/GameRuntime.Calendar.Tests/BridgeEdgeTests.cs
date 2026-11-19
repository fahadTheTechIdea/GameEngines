using AIEngine;
using CalendarEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Calendar.Tests;

public sealed class BridgeEdgeTests
{
    private const string Document = """
    { "format": 1, "id": "calendar.ai", "keys": [],
      "percepts": [ { "kind": "time", "capacity": 1, "memory": 100000, "owned": false, "attrs": [
          { "id": "daypart", "type": "string", "unknown": "" }, { "id": "season", "type": "string", "unknown": "" },
          { "id": "is_day", "type": "bool", "unknown": true }, { "id": "light", "type": "int", "min": 0, "max": 1000, "unknown": 1000 } ] } ],
      "intents": [ { "id": "wave", "args": [] } ],
      "behaviors": [ { "id": "idler", "every": 5, "root": { "id": "w", "kind": "act", "intent": "wave", "args": {} } } ] }
    """;

    [Fact]
    public void WhoComesLaterIsToldWhatTheSkyIsEvenWhenNothingChangedInTheSkySinceNobodyWasThere()
    {
        var set = AiLoader.Load(Document);
        var session = new RuntimeSession(new RuntimeClock(CalendarHost.TicksPerSecond, 8), new CalendarHost.Reporter());
        AiEventCatalog.Declare(session);
        var channels = new CalendarChannels();
        channels.Install(session);
        var calendar = new CalendarComponent(CalendarRuntime.Create(Samples.Gregorian, Samples.At(Samples.Gregorian, 2024, "march", 6, 12, 0), 1, 1), () => new Dictionary<string, FactValue>(), channels);
        var perceivers = new List<string>();
        var bridge = new CalendarAiBridge("calendar-ai", calendar, "ai", set, "time", "now", () => perceivers);
        session.AddComponent(calendar, 10);
        session.AddComponent(bridge, 11);
        session.Init();
        for (var i = 0; i < 5; i++) session.Update(CalendarHost.TickMicroseconds);   // 12:05, high noon, nothing in the sky changes
        Assert.Equal(0, bridge.Posted);
        perceivers.Add("late-arrival");
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(1, bridge.Posted);
        session.Update(CalendarHost.TickMicroseconds);
        Assert.Equal(1, bridge.Posted);
    }
}
