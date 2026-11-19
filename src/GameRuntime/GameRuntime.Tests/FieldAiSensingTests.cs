using System.Text.RegularExpressions;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>What the field game tells its supply agent and how it numbers the requests it makes from the agent offers, observed in a real session.</summary>
public sealed class FieldAiSensingTests
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static (FieldSession Session, List<AiPerceptPosted> Told, List<(string Id, string Kind)> Requests) Play(string[] args, int ticks)
    {
        var options = FieldOptions.Parse(args);
        var forces = FieldForceSource.Load(options);
        // Every group starts short of rounds and fuel, so the quartermaster has something to offer on its first look.
        forces = forces with { Groups = [.. forces.Groups.Select(g => g with { Fuel = g.FuelCapacity * 40 / 100, Batteries = [.. g.Batteries.Select(b => b with { Rounds = b.Capacity * 30 / 100 })] })] };
        var reporter = new Reporter();
        var session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), reporter);
        var told = new List<AiPerceptPosted>();
        var requests = new List<(string, string)>();
        session.Runtime.Subscribe<AiPerceptPosted>(e => { if (e.Component == "supply-ai") told.Add(e); });
        session.Runtime.Subscribe<ResupplyRequested>(e => requests.Add((e.RequestId, "resupply")));
        session.Runtime.Subscribe<RefuelRequested>(e => requests.Add((e.RequestId, "refuel")));
        session.Runtime.Subscribe<ReinforceRequested>(e => requests.Add((e.RequestId, "reinforce")));
        session.Runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Runtime.Clock.TicksPerSecond - 1) / session.Runtime.Clock.TicksPerSecond;
        for (var i = 0; i < ticks && session.Runtime.State == RuntimeState.Running; i++) session.Runtime.Update(oneTick);
        Assert.Empty(reporter.Reports);
        return (session, told, requests);
    }

    private static readonly string[] Tanks = ["--pack-a", "arms-modern", "--formation-a", "arms-modern.tank-platoon", "--squads-a", "2", "--pack-b", "arms-modern", "--formation-b", "arms-modern.tank-platoon", "--squads-b", "2",
        "--distance", "500", "--depot-percent", "100", "--frame-ms", "33"];

    private static Dictionary<string, string> Attrs(AiPerceptPosted posted) =>
        posted.Attrs.Split(';').Select(e => e.Split('=')).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

    [Fact]
    public void ABatteryIsToldAtItsPlaceInTheGroupListWithTheNameItHasAndAGroupDrawsOnItsOwnDepotStock()
    {
        var (session, told, _) = Play(Tanks, 300);
        var groups = session.Combat.Groups.Values.Where(g => g.Batteries.Count > 0).ToList();
        Assert.NotEmpty(groups);
        foreach (var group in groups)
        {
            var batteries = told.Where(t => t.Kind == "battery" && t.Owner == group.Id).GroupBy(t => t.Id).Select(g => g.Last()).OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
            Assert.Equal(group.Batteries.Count, batteries.Count);
            for (var i = 0; i < batteries.Count; i++)
            {
                Assert.Equal(group.Id + "/" + i.ToString("D4"), batteries[i].Id);
                Assert.Equal(group.Batteries[i].Id, Attrs(batteries[i])["battery"]);
            }
        }
        var tank = told.Where(t => t.Kind == "unit").Select(t => (Posted: t, Attrs: Attrs(t))).First(u => u.Attrs["fuel-stock"].Length > 0);
        var depot = session.World.Depots[tank.Posted.Id.StartsWith('a') ? 0 : 1];
        var fuelType = session.Combat.Groups[tank.Posted.Id].FuelType!;
        Assert.Equal($"{depot}:{StockResources.Fuel(fuelType)}", tank.Attrs["fuel-stock"]);
        Assert.Equal($"{depot}:{StockResources.People}", tank.Attrs["people-stock"]);
    }

    [Fact]
    public void RequestsMadeFromTheAgentOffersAreNumberedFromOneInTheOrderTheyWereOffered()
    {
        var (probe, _, requests) = Play(Tanks, 200);
        Assert.True(requests.Count > 0, $"no request by tick {probe.Runtime.Clock.Tick}, state {probe.Runtime.State}, {probe.Combat.Fingerprint()}");
        var byTick = requests.Select(r => Regex.Match(r.Id, "^supply-t(\\d+)-(\\d+)$")).ToList();
        Assert.All(byTick, m => Assert.True(m.Success));
        foreach (var tick in byTick.GroupBy(m => m.Groups[1].Value))
            Assert.Equal(Enumerable.Range(1, tick.Count()).Select(n => n.ToString()), tick.Select(m => m.Groups[2].Value));
    }
}
