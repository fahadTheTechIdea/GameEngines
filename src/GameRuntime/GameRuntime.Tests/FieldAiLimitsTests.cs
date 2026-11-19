using CombatEngine;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>What the field AI document can hold, and that a fight beyond it is refused before the first tick instead of losing what the agents are told.</summary>
public sealed class FieldAiLimitsTests
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static readonly string[] Rifles =
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "2", "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "2", "--distance", "250", "--frame-ms", "100"];

    private static Group Template() => FieldForceSource.Load(FieldOptions.Parse(Rifles)).Groups[0];

    [Fact]
    public void TheDocumentHoldsTheMostGroupsTheCommandLineCanRaiseAndThePerGroupBatteriesMakeTheBatteryCapacity()
    {
        // One group for each squad, 1000 squads a side at most (FieldOptions): the capacities are those two numbers, not a guess.
        var set = FieldAi.Set;
        Assert.Equal(2 * 1000, set.Percepts.Single(p => p.Id == "group").Capacity);
        Assert.Equal(2 * 1000, set.Percepts.Single(p => p.Id == "unit").Capacity);
        var battery = set.Percepts.Single(p => p.Id == "battery");
        Assert.Equal(2000 * battery.OwnerCapacity, battery.Capacity);
        Assert.True(battery.Capacity <= 65_536);
        Assert.Throws<FieldUsageException>(() => FieldOptions.Parse([.. Rifles.Select(a => a == "2" ? "1001" : a)]));
    }

    [Fact]
    public void ThereAreMoreBatteriesAGroupCanHoldThanAnyShippedFormationHas()
    {
        // The widest formation of every pack the game ships, as the combat adapter turns it into a group.
        var widest = 0;
        foreach (var options in ShippedFormations()) widest = Math.Max(widest, FieldForceSource.Load(options).Groups.Max(g => g.Batteries.Count));
        Assert.True(widest > 0);
        Assert.True(widest * 4 <= FieldAi.Set.Percepts.Single(p => p.Id == "battery").OwnerCapacity, $"a formation has {widest} batteries");
    }

    private static IEnumerable<FieldOptions> ShippedFormations()
    {
        foreach (var (pack, formation) in new[] { ("arms-modern", "arms-modern.tank-platoon"), ("arms-modern", "arms-modern.rifle-company"), ("arms-ancient", "arms-ancient.iron-warband") })
            yield return FieldOptions.Parse(["--pack-a", pack, "--formation-a", formation, "--squads-a", "1", "--pack-b", pack, "--formation-b", formation, "--squads-b", "1", "--distance", "250"]);
    }

    [Fact]
    public void AFightWithMoreGroupsThanTheDocumentHoldsIsRefusedBeforeItStarts()
    {
        var group = Template();
        var fits = Enumerable.Range(0, 2000).Select(i => group with { Id = "g" + i }).ToList();
        FieldAi.Check(fits);
        var refused = Assert.Throws<FieldUsageException>(() => FieldAi.Check([.. fits, group with { Id = "extra" }]));
        Assert.Contains("2001 groups", refused.Message);
    }

    [Fact]
    public void AGroupWithMoreBatteriesThanTheDocumentHoldsForOneIsRefusedBeforeItStarts()
    {
        var group = Template();
        var most = FieldAi.Set.Percepts.Single(p => p.Id == "battery").OwnerCapacity;
        var battery = group.Batteries[0];
        FieldAi.Check([group with { Batteries = [.. Enumerable.Repeat(battery, most)] }]);
        var refused = Assert.Throws<FieldUsageException>(() => FieldAi.Check([group with { Batteries = [.. Enumerable.Repeat(battery, most + 1)] }]));
        Assert.Contains("batteries", refused.Message);
    }

    [Fact]
    public void ASessionIsNotCreatedForAFightTheDocumentCannotHold()
    {
        var options = FieldOptions.Parse(Rifles);
        var forces = FieldForceSource.Load(options);
        var wide = forces with { Groups = [.. forces.Groups.Select(g => g with { Batteries = [.. Enumerable.Repeat(g.Batteries[0], 33)] })] };
        Assert.Throws<FieldUsageException>(() => FieldSession.Create(options, wide, FieldWorld.Create(options, forces, null), new Reporter()));
    }

    [Fact]
    public void AmountsBeyondTheDocumentAreToldAsTheMostItHoldsNeverWrappedAndNeverBelowZero()
    {
        // 10 billion thousandths of a litre or rounds: the other layers (the world's stock) overflow before this does, so the limit is the document's, and the document keeps a load times 100 inside the engine.
        Assert.Equal(10_000_000_000, FieldAi.MaxQuantity);
        Assert.Equal(FieldAi.MaxQuantity, FieldQuartermaster.Cap(FieldAi.MaxQuantity));
        Assert.Equal(FieldAi.MaxQuantity, FieldQuartermaster.Cap(FieldAi.MaxQuantity + 1));
        Assert.Equal(FieldAi.MaxQuantity, FieldQuartermaster.Cap(long.MaxValue));
        Assert.Equal(0, FieldQuartermaster.Cap(-1));
        Assert.Equal(1234, FieldQuartermaster.Cap(1234));
    }

    [Fact]
    public void EachCommanderAgentThinksOnEveryTickItIsDueAndNeverWaitsForTheOther()
    {
        var options = FieldOptions.Parse(Rifles);
        var forces = FieldForceSource.Load(options);
        var session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), new Reporter());
        var steps = new List<AiStepDone>();
        session.Runtime.Subscribe<AiStepDone>(e => steps.Add(e));
        session.Runtime.Init();
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Runtime.Clock.TicksPerSecond - 1) / session.Runtime.Clock.TicksPerSecond;
        for (var i = 0; i < 40 && session.Runtime.State == RuntimeState.Running; i++) session.Runtime.Update(oneTick);
        var commander = steps.Where(s => s.Source == "commander-ai").ToList();
        Assert.True(commander.Count > 30);
        Assert.All(commander, s => { Assert.Equal(FieldCommander.AgentCount, s.Thought); Assert.Equal(0, s.Deferred); });
    }
}
