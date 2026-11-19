using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A5: an agent decides from its blackboard and what the host told it, and nothing else; what it was told fades; what the host owns it cannot write.</summary>
public sealed class PerceptionTests
{
    private const long Budget = 1_000_000;

    private static AiRuntime Fresh(string? document = null)
    {
        var runtime = new AiRuntime(Fixture.Load(document), 1);
        runtime.Add("a", "commander", 0);
        return runtime;
    }

    private static string Perceive(AiRuntime runtime, string kind, string id, string owner, Dictionary<string, FactValue> attrs) =>
        Fixture.Refusal(() => runtime.Perceive("a", kind, id, owner, attrs, 0));

    private static Dictionary<string, FactValue> Attrs(bool hostile = true, bool standing = true, long health = 10) => new()
    {
        ["hostile"] = FactValue.From(hostile), ["standing"] = FactValue.From(standing), ["health"] = FactValue.From(health),
    };

    [Fact]
    public void WhatTheHostTellsIsKeptExactlyAndReadBack()
    {
        var runtime = Fresh();
        runtime.Perceive("a", "unit", "u1", "", Attrs(health: 77), 3);
        runtime.Perceive("a", "stock", "s1", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(12L) }, 3);
        var unit = Assert.Single(runtime.Known("a", "unit"));
        Assert.Equal(("u1", "", 3L), (unit.Id, unit.Owner, unit.Seen));
        Assert.Equal(FactValue.From(77L), unit.Attrs["health"]);
        Assert.Equal("depot-a", Assert.Single(runtime.Known("a", "stock")).Owner);
        // Telling again updates in place and notes the tick.
        runtime.Perceive("a", "unit", "u1", "", Attrs(health: 5), 9);
        var again = Assert.Single(runtime.Known("a", "unit"));
        Assert.Equal((5L, 9L), (again.Attrs["health"].Integer, again.Seen));
    }

    [Fact]
    public void EveryWayOfTellingAgentsSomethingWrongIsRefusedWithoutChangingWhatItKnows()
    {
        var runtime = Fresh();
        runtime.Perceive("a", "unit", "u1", "", Attrs(), 0);
        var before = AiSave.StateDigest(runtime);
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "ghost", "x", "", Attrs()));
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "", "", Attrs()));
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", new string('x', 129), "", Attrs()));
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "someone", Attrs()));                                   // this kind has no owners
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "stock", "x", "", new() { ["held"] = FactValue.From(1L) }));         // this one needs an owner
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "", new() { ["hostile"] = FactValue.From(true) }));     // attributes missing
        var extra = Attrs();
        extra["bonus"] = FactValue.From(1L);
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "", extra));                                            // one too many
        var misnamed = Attrs();
        misnamed.Remove("health");
        misnamed["wealth"] = FactValue.From(1L);
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "", misnamed));                                         // right count, wrong name
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "", Attrs(health: 5000)));                              // outside its range
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "unit", "x", "", new() { ["hostile"] = FactValue.From(1L), ["standing"] = FactValue.From(true), ["health"] = FactValue.From(1L) }));   // wrong type
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.Perceive("nobody", "unit", "x", "", Attrs(), 0)));
        Assert.Equal(before, AiSave.StateDigest(runtime));
    }

    [Fact]
    public void AnAgentKnowsNoMoreThanTheCapacityOfAKindAndAThingKeepsItsOwner()
    {
        var runtime = Fresh();
        for (var i = 0; i < 16; i++) runtime.Perceive("a", "unit", "u" + i.ToString("00"), "", Attrs(), 0);
        Assert.Equal(AiCodes.Capacity, Perceive(runtime, "unit", "u99", "", Attrs()));
        runtime.Perceive("a", "unit", "u03", "", Attrs(health: 1), 0);       // an update is not a new thing
        runtime.Perceive("a", "stock", "s1", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(1L) }, 0);
        Assert.Equal(AiCodes.Percept, Perceive(runtime, "stock", "s1", "depot-b", new() { ["held"] = FactValue.From(1L) }));
        Assert.True(runtime.Lose("a", "unit", "u03"));
        Assert.False(runtime.Lose("a", "unit", "u03"));                       // already gone: not a mistake
        runtime.Perceive("a", "unit", "u99", "", Attrs(), 0);                  // room again
        Assert.Equal(AiCodes.Percept, Fixture.Refusal(() => runtime.Lose("a", "ghost", "x")));
    }

    [Fact]
    public void OneOwnerHoldsNoMoreThanTheOwnerCapacityOfAKindWhateverRoomTheKindHasLeft()
    {
        var runtime = Fresh();
        Dictionary<string, FactValue> Held(long n) => new() { ["held"] = FactValue.From(n) };
        for (var i = 0; i < 4; i++) runtime.Perceive("a", "stock", "s" + i, "depot-a", Held(i), 0);
        Assert.Equal(AiCodes.Capacity, Perceive(runtime, "stock", "s4", "depot-a", Held(4)));       // the fifth of one owner; the kind (eight) has room
        runtime.Perceive("a", "stock", "s2", "depot-a", Held(99), 0);                               // an update is not a new thing
        runtime.Perceive("a", "stock", "s4", "depot-b", Held(4), 0);                                // another owner has its own four
        Assert.True(runtime.Lose("a", "stock", "s0"));
        runtime.Perceive("a", "stock", "s5", "depot-a", Held(5), 0);                                // room again for the first
    }

    [Theory]
    [InlineData("count", ", \"into\": \"foes\"", 0)]
    [InlineData("select", ", \"into\": \"target\", \"pick\": { \"min\": \"held\" }", 0)]
    [InlineData("forEach", ", \"limit\": 2, \"child\": { \"id\": \"k\", \"kind\": \"let\", \"key\": \"spare\", \"value\": 1 }", 2)]
    public void AQueryByOwnerIsBoundedByTheOwnerCapacityAndAQueryOfTheKindByTheKindCapacity(string kind, string rest, int childBound)
    {
        string Cost(string of) => Fixture.LoadMini("{ \"id\": \"q\", \"kind\": \"" + kind + "\", \"from\": \"stock\"" + rest + of + " }").Behaviors["t"].MaxCost.ToString();
        Assert.Equal((1 + 8 + childBound).ToString(), Cost(""));                    // the node itself and a look at each of the kind (eight)
        Assert.Equal((1 + 4 + childBound).ToString(), Cost(", \"of\": \"depot-a\""));   // and at each of one owner (four)
    }

    [Fact]
    public void ThingsFadeFromMemoryAfterTheirKindMemoryAndUntilThenAreStillActedOn()
    {
        // The count of known standing foes is what the commander acts on. One foe is told at tick 0 and never again; its kind remembers 50 ticks.
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
            + "{ \"id\": \"c\", \"kind\": \"count\", \"from\": \"unit\", \"into\": \"foes\", \"where\": " + Fixture.FoeFilter + " }, "
            + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"foes\", \"amount\": { \"key\": \"foes\" } } } ] }";
        var runtime = new AiRuntime(Fixture.LoadMini(root), 1);
        runtime.Add("a", "t", 0);
        Fixture.Tell(runtime, "a", "b1", hostile: true);
        long Known(int tick) => runtime.Step(tick, Budget).Intents.Single().Arg("amount").Integer;
        Assert.Equal(1, Known(1));
        // The foe is long gone from the world, but the agent was never told, so at 50 it still counts it.
        Assert.Equal(1, Known(50));
        Assert.Equal(0, Known(51));        // 51 - 0 is more than 50: forgotten
        Assert.Empty(runtime.Known("a", "unit"));
    }

    [Fact]
    public void WhatOneOwnerHoldsFollowsWhatIsToldLostForgottenAndLoaded()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
            + "{ \"id\": \"c\", \"kind\": \"count\", \"from\": \"stock\", \"into\": \"foes\", \"of\": \"depot-a\" }, "
            + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"held\", \"amount\": { \"key\": \"foes\" } } } ] }";
        var set = Fixture.LoadMini(root);
        var runtime = new AiRuntime(set, 1);
        runtime.Add("a", "t", 0);
        void Tell(string id, string owner, long tick) => runtime.Perceive("a", "stock", id, owner, new Dictionary<string, FactValue> { ["held"] = FactValue.From(1L) }, tick);
        long Held(AiRuntime r, int tick) => r.Step(tick, set.MaxCost).Intents.Single().Arg("amount").Integer;
        Tell("s1", "depot-a", 0); Tell("s2", "depot-a", 0); Tell("s3", "depot-b", 0);
        Assert.Equal(2, Held(runtime, 1));
        Assert.True(runtime.Lose("a", "stock", "s1"));
        Assert.Equal(1, Held(runtime, 2));
        Tell("s4", "depot-a", 50);
        Assert.Equal(2, Held(runtime, 3));
        // The save holds what each owner holds, and a loaded runtime counts the same.
        var loaded = AiSave.Open(set, AiSave.Write(runtime));
        Assert.Equal(2, Held(loaded, 4));
        Assert.Equal(2, Held(runtime, 4));
        // s2 was last told at tick 0 and the kind remembers 100 ticks: at 101 it is forgotten and s4 (told at 50) stays; at 151 nothing is left.
        Assert.Equal(1, Held(runtime, 101));
        Assert.Equal(0, Held(runtime, 151));
        Assert.Equal(1, Held(loaded, 101));
    }

    [Fact]
    public void WhatTheWorldDidWithoutTellingTheAgentChangesNoDecision()
    {
        // Two agents are told the same; the world then changes for one of them in ways nobody reports. They decide identically, and so does a replay.
        string Run(Action<AiRuntime>? unreported)
        {
            var runtime = Fresh();
            runtime.SetKey("a", "ready", FactValue.From(true));
            foreach (var own in new[] { "a1", "a2" }) Fixture.Tell(runtime, "a", own, hostile: false);
            Fixture.Tell(runtime, "a", "b1", hostile: true);
            var lines = new List<string>();
            for (var tick = 1; tick <= 30; tick++)
            {
                if (tick == 10) unreported?.Invoke(runtime);
                lines.Add(Fixture.Lines(runtime.Step(tick, Budget)));
            }
            return string.Join('|', lines);
        }
        var plain = Run(null);
        Assert.Contains("fire", plain);
        Assert.Equal(plain, Run(_ => { /* the foe dies in the world; nobody tells the agent */ }));
        // Telling is what changes a decision.
        var told = Run(runtime => Fixture.Tell(runtime, "a", "b1", hostile: true, standing: false, tick: 10));
        Assert.NotEqual(plain, told);
    }

    [Fact]
    public void TheHostWritesOnlyTheKeysItOwnsAndWithValuesTheyAllow()
    {
        var runtime = Fresh();
        runtime.SetKey("a", "ready", FactValue.From(true));
        runtime.SetKey("a", "distance", FactValue.From(250L));
        Assert.Equal(FactValue.From(250L), runtime.Key("a", "distance"));
        Assert.Equal(AiCodes.KeyWrite, Fixture.Refusal(() => runtime.SetKey("a", "foes", FactValue.From(3L))));          // the agent memory
        Assert.Equal(AiCodes.KeyWrite, Fixture.Refusal(() => runtime.SetKey("a", "target", FactValue.From("b1"))));
        Assert.Equal(AiCodes.Reference, Fixture.Refusal(() => runtime.SetKey("a", "nothing", FactValue.From(1L))));
        Assert.Equal(AiCodes.ValueType, Fixture.Refusal(() => runtime.SetKey("a", "distance", FactValue.From(-1L))));
        Assert.Equal(AiCodes.ValueType, Fixture.Refusal(() => runtime.SetKey("a", "distance", FactValue.From("far"))));
        Assert.Equal(AiCodes.ValueType, Fixture.Refusal(() => runtime.SetKey("a", "ready", FactValue.From(1L))));
        Assert.Equal(AiCodes.UnknownAgent, Fixture.Refusal(() => runtime.SetKey("nobody", "ready", FactValue.From(true))));
        Assert.Equal(AiCodes.Reference, Fixture.Refusal(() => runtime.Key("a", "nothing")));
    }
}