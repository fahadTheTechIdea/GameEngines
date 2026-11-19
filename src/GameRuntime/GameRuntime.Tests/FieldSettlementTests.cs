using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>States of the field game that the ordinary fights never reach, built on purpose: a firing beat that is owed, a supply look on the tick of a round, and a fight that ends on a beat.</summary>
public sealed class FieldSettlementTests
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static readonly string[] Rifles =
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "2", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "2", "--distance", "250", "--frame-ms", "100"];

    private sealed class Game
    {
        public FieldSession Session { get; }
        public Reporter Reporter { get; } = new();
        public List<(long Tick, string Kind)> Requests { get; } = new();
        public long Tick => Session.Runtime.Clock.Tick;

        public Game(Func<long, bool> authorisedAt, int percentOfRounds = 100)
        {
            var options = FieldOptions.Parse(Rifles);
            var forces = FieldForceSource.Load(options);
            if (percentOfRounds != 100) forces = forces with { Groups = [.. forces.Groups.Select(g => g with { Batteries = [.. g.Batteries.Select(b => b with { Rounds = b.Capacity * percentOfRounds / 100 })] })] };
            FieldSession? session = null;
            session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), Reporter, authorised: () => authorisedAt(session!.Runtime.Clock.Tick));
            Session = session;
            session.Runtime.Subscribe<RoundRequested>(_ => Requests.Add((session.Runtime.Clock.Tick, "round")));
            session.Runtime.Subscribe<ResupplyRequested>(_ => Requests.Add((session.Runtime.Clock.Tick, "resupply")));
            session.Runtime.Init();
        }

        public void RunTo(long tick)
        {
            var oneTick = (RuntimeClock.MicrosecondsPerSecond + Session.Runtime.Clock.TicksPerSecond - 1) / Session.Runtime.Clock.TicksPerSecond;
            while (Tick < tick && Session.Runtime.State == RuntimeState.Running) Session.Runtime.Update(oneTick);
        }
    }

    [Fact]
    public void ABeatThatIsOwedKeepsTheSessionUnsettledSoNoCheckpointIsTakenWithARoundOwed()
    {
        var game = new Game(tick => tick >= 20);
        game.RunTo(10);
        Assert.True(game.Session.IsSettled);          // nothing owed yet: the first beat is at 15
        game.RunTo(18);
        Assert.Empty(game.Requests);                  // the beat at 15 passed with the squads held back
        Assert.True(game.Session.Combat.IsQuiet && game.Session.Stock.Settled && game.Session.Quartermaster.Settled);
        Assert.False(game.Session.Commander.Settled);
        Assert.False(game.Session.IsSettled);         // the only thing open is the round that is owed
        game.RunTo(21);
        Assert.Equal(20, Assert.Single(game.Requests).Tick);   // let go, it is ordered at once
        Assert.Empty(game.Reporter.Reports);
    }

    [Fact]
    public void ASupplyLookOnTheTickARoundIsRequestedWaitsForTheNextBeat()
    {
        // Groups start with 52 percent of their rounds, above the 50 percent line, so the look at 8 offers nothing. A round at 15 spends some. The squads are then held back until 38,
        // which is a look (38 mod 30 is 8): the owed round is asked on the very tick of the look, and the round, asked first, keeps combat busy, so the look offers nothing that tick.
        var game = new Game(tick => tick < 16 || tick >= 38, percentOfRounds: 52);
        game.RunTo(75);
        Assert.Contains((15L, "round"), game.Requests);
        Assert.Contains((38L, "round"), game.Requests);
        Assert.DoesNotContain(game.Requests, r => r.Kind == "resupply" && r.Tick == 38);   // the look on the tick of the round
        Assert.Empty(game.Reporter.Reports);
    }

    /// <summary>Stands in for combat at one tick: says that every group of the second side is routed, as the combat component would after a round.</summary>
    private sealed class Rout(long atTick, IEnumerable<string> groups) : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "rout";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context)
        {
            if (context.Tick == atTick) foreach (var group in groups) _events!.Emit(new Standing("outside", context.Tick, group, "routed", 0));
        }
    }

    [Fact]
    public void AFightThatEndsOnABeatTickOrdersNoFurtherRoundBecauseTheAgentsAreToldWhoIsStillStanding()
    {
        var options = FieldOptions.Parse(Rifles);
        var forces = FieldForceSource.Load(options);
        var reporter = new Reporter();
        var session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), reporter);
        session.Runtime.AddComponent(new Rout(14, forces.SideB), 240);     // the events reach the commander at 14 and it ends the fight at 15, the first beat; the agents were told ready at 14
        var requests = new List<long>();
        string? ending = null;
        session.Runtime.Subscribe<RoundRequested>(_ => requests.Add(session.Runtime.Clock.Tick));
        long endedAt = -1;
        session.Runtime.Subscribe<SessionEnded>(e => { ending = e.EndingId; endedAt = e.Tick; });
        session.Runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Runtime.Clock.TicksPerSecond - 1) / session.Runtime.Clock.TicksPerSecond;
        for (var i = 0; i < 20 && session.Runtime.State == RuntimeState.Running; i++) session.Runtime.Update(oneTick);
        Assert.Equal("side-1-wins", ending);
        Assert.Empty(requests);                          // the beat at 15 passed with nobody left to fire at
        Assert.Equal(15, endedAt);                       // the fight ended on the beat itself
        Assert.Empty(reporter.Reports);
    }
}
