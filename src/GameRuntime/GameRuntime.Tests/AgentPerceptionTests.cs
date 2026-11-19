using CombatEngine;
using GameRuntime.Combat.Headless;
using GameRuntime.Field.Headless;
using GameRuntime.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// What the sensors tell the agents depends only on what each sensor is meant to read. For every sensor, a thing it must not read is changed and nothing it tells may change;
/// a thing it reads is changed and something it tells must. A sensor that starts reading a hidden fact (an enemy's morale, the cash behind a profit) fails the first half.
/// </summary>
public sealed class AgentPerceptionTests
{
    private sealed class Port : IEventPort
    {
        public List<string> Told { get; } = new();
        public void Emit<T>(in T evt) where T : struct
        {
            switch (evt)
            {
                case AiKeyPosted key: Told.Add($"key {key.Agent} {key.Key}={key.Value}"); break;
                case AiPerceptPosted percept: Told.Add($"percept {percept.Agent} {percept.Kind} {percept.Id} {percept.Owner} {percept.Attrs}"); break;
                case AiAgentAddRequested add: Told.Add($"agent {add.Agent} {add.Behavior}"); break;
            }
        }
        public void Connect<T>(Action<T> handler) where T : struct { }
    }

    // ---- the tycoon player ------------------------------------------------------------------------------------------------------------

    private static List<string> TycoonTold(Action<PlayerView> change)
    {
        var port = new Port();
        var view = new PlayerView(1500) { Founded = true, Stock = 40, Profit = 12 };
        change(view);
        TycoonBots.Create("cautious", 1, 1500)!.Sense(port, view);
        return port.Told;
    }

    [Fact]
    public void TheTycoonBotIsToldOnlyWhatThePlayerHasLearned()
    {
        var baseline = TycoonTold(_ => { });
        Assert.Equal(baseline, TycoonTold(v => { v.Tick = 999; v.Ended = true; v.Cash = 5_000_000; v.ObjectivesCompleted = 7; }));      // the cash behind the profit, the objectives, the clock
        Assert.NotEqual(baseline, TycoonTold(v => v.Stock = 41));
        Assert.NotEqual(baseline, TycoonTold(v => v.Profit = 13));
        Assert.NotEqual(baseline, TycoonTold(v => v.Founded = false));
        Assert.NotEqual(baseline, TycoonTold(v => v.InFlight["c1"] = (1, 4)));
        Assert.NotEqual(baseline, TycoonTold(v => v.RefusalList.Add(new RefusedSale("c1", 4, 1, "NO_STOCK"))));
    }

    // ---- the skirmish commander -------------------------------------------------------------------------------------------------------

    private static List<string> SkirmishTold(Action<SkirmishView> change)
    {
        var port = new Port();
        var view = new SkirmishView { Distance = 120, Phase = SkirmishPhase.Fight };
        view.Groups["a"] = new SeenGroup("a", 1, 100, 80, 1400);
        view.Groups["b"] = new SeenGroup("b", 2, 100, 60, 1200);
        change(view);
        SkirmishBots.Create("aggressor", new SkirmishPlan(300, 7, "hills"), 1)!.Sense(port, view);
        return port.Told;
    }

    [Fact]
    public void TheSkirmishCommanderIsToldOnlyWhatItHasSeenOfEachGroup()
    {
        var baseline = SkirmishTold(_ => { });
        Assert.Equal(baseline, SkirmishTold(v =>
        {
            v.Tick = 77; v.Round = 5;
            foreach (var group in v.Groups.Values) { group.Morale = 3; group.State = "suppressed"; }       // morale and the kind of standing are not told: a suppressed group still stands
        }));
        Assert.NotEqual(baseline, SkirmishTold(v => v.Groups["b"].Health = 40));
        Assert.NotEqual(baseline, SkirmishTold(v => v.Groups["b"].State = "routed"));
        Assert.NotEqual(baseline, SkirmishTold(v => v.Distance = 90));
        Assert.NotEqual(baseline, SkirmishTold(v => v.Phase = SkirmishPhase.Approach));
        Assert.NotEqual(baseline, SkirmishTold(v => v.InFlight.Add("r1")));
    }

    // ---- the field game ---------------------------------------------------------------------------------------------------------------

    private static readonly string[] Fight =
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.tank-platoon", "--squads-a", "2", "--pack-b", "arms-modern", "--formation-b", "arms-modern.tank-platoon", "--squads-b", "2", "--distance", "500", "--depot-percent", "100", "--frame-ms", "100"];

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <summary>Everything either field agent is told before the first round of fire, in the order it was told.</summary>
    private static List<string> FieldTold(Func<Group, Group> change)
    {
        var options = FieldOptions.Parse(Fight);
        var forces = FieldForceSource.Load(options);
        forces = forces with { Groups = [.. forces.Groups.Select(change)] };
        var reporter = new Reporter();
        var session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), reporter);
        var told = new List<string>();
        session.Runtime.Subscribe<AiKeyPosted>(e => told.Add($"{e.Component} key {e.Agent} {e.Key}={e.Value}"));
        session.Runtime.Subscribe<AiPerceptPosted>(e => told.Add($"{e.Component} percept {e.Agent} {e.Kind} {e.Id} {e.Owner} {e.Attrs}"));
        var asked = false;
        session.Runtime.Subscribe<RoundRequested>(_ => asked = true);
        session.Runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Runtime.Clock.TicksPerSecond - 1) / session.Runtime.Clock.TicksPerSecond;
        for (var i = 0; i < 400 && !asked && session.Runtime.State == RuntimeState.Running; i++) session.Runtime.Update(oneTick);
        Assert.True(asked, "no round was asked for");
        Assert.Empty(reporter.Reports);
        return told;
    }

    [Fact]
    public void TheFieldAgentsAreNotToldWhatTheirServicesDoNotNeedToKnow()
    {
        var baseline = FieldTold(g => g);
        Assert.Contains(baseline, t => t.Contains("percept") && t.Contains("battery"));
        // The morale, the suppression, the march speed, the armour and the training of a group are the combat rules' business: neither the quartermaster nor the commanders are told them.
        Assert.Equal(baseline, FieldTold(g => g with { Morale = g.Morale / 2 + 1, Suppressed = !g.Suppressed, SpeedMilli = g.SpeedMilli + 500, ArmorRating = g.ArmorRating + 3, BurnPerHourMilli = g.BurnPerHourMilli + 1000 }));
        Assert.Equal(baseline, FieldTold(g => g with { Batteries = [.. g.Batteries.Select(b => b with { CarryMilli = b.CarryMilli + 700 })] }));
    }

    [Fact]
    public void TheFieldQuartermasterIsToldWhatItNeedsToSupply()
    {
        var baseline = FieldTold(g => g);
        Assert.NotEqual(baseline, FieldTold(g => g with { Fuel = g.Fuel / 2 }));
        Assert.NotEqual(baseline, FieldTold(g => g with { Batteries = [.. g.Batteries.Select(b => b with { Rounds = b.Rounds / 2 })] }));
    }
}
