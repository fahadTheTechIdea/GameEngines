using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>
/// Written from the survivors of the first round of mutation checks: the entries a document refers to by index when that index is 0 (a casus belli a breach, a balance or a capture grants; the deed witnesses remember), the
/// tribute due by the season of index 0, a defence pact that does not concern the war, the weariness of a realm at war and a save with a ceasefire in it.
/// </summary>
public sealed class BoundaryTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
    {
        foreach (var id in new[] { "nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage" }) Fixture.Decided(root, id, true);
        Fixture.Realm(root, "cinder")["control"] = "ai";
        edit?.Invoke(root);
    });

    [Fact]
    public void ABreachGrantsTheWrongedItsCasusBelliAndWitnessesRememberItWhateverPlaceTheDocumentListsThemIn()
    {
        var runtime = World(root =>
        {
            Fixture.First(root, "casusBelli", "betrayed");
            Fixture.First(root, "deeds", "witnessed");
            Fixture.Type(root, "nap")["breach"]!["trust"] = 150;
        });
        Assert.Equal((0, 0), (runtime.Set.CbById["betrayed"].Index, runtime.Set.DeedById["witnessed"].Index));
        var nap = Fixture.Treaty(runtime, "aurel", "dune", "nap", 40);
        runtime.Cancel(nap.Id, "aurel");
        Assert.True(runtime.Holds("dune", "aurel", "betrayed"));
        Assert.Contains(runtime.Grants, g => g.Holder == 3 && g.Target == 0 && g.Cb == 0);
        Assert.Contains(runtime.State.Deeds, d => d.Deed == 0 && d.Subject == 0 && d.Holder == 1);          // brass saw it
    }

    [Fact]
    public void ACoalitionGrantsItsMembersTheBalanceCasusBelliWhateverPlaceTheDocumentListsItIn()
    {
        var runtime = Fixture.PlainWorld(root =>
        {
            var balance = Fixture.Rules(root, "balance");
            balance["interval"] = 5;
            balance["threatLine"] = 250;
            balance["relaxLine"] = 150;
            balance["joinRegard"] = 200;
            balance["minMembers"] = 2;
            balance["bandwagonFear"] = 600;
            Fixture.Rules(root, "regard")["coalition"] = 300;
            foreach (var id in new[] { "alliance", "marriage", "vassalage", "trade", "nap" }) Fixture.Decided(root, id, true);
            Fixture.Realm(root, "cinder")["control"] = "ai";
            Fixture.First(root, "casusBelli", "balance");
        });
        Fixture.Live(runtime, 0);
        Assert.Equal(0, runtime.Set.CbById["balance"].Index);
        Assert.Equal(4, runtime.Grants.Count(g => g.Target == 0 && g.Cb == 0));
    }

    [Fact]
    public void ATributeDueByTheSeasonAtTheStartOfTheYearWaitsForThatSeasonAgainAfterItIsPaid()
    {
        var runtime = World(root =>
        {
            Fixture.Type(root, "vassalage")["considerDays"] = 0;
            Fixture.Type(root, "vassalage")["clauses"]![1]!["dueSeason"] = "spring";
        });
        Assert.Equal(0, runtime.Set.SeasonById["spring"].Index);
        var treaty = Fixture.Treaty(runtime, "brass", "dune", "vassalage", 0, 5000);
        while (!treaty.Awaiting && runtime.Step < 800) Fixture.Live(runtime, runtime.Step + 1);
        Assert.True(treaty.Awaiting);
        runtime.Fulfil(treaty.Id, true);
        Assert.Equal(-1, treaty.NextDue);
    }

    [Fact]
    public void ADefencePactThatDoesNotConcernTheTargetCallsNoOne()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["callBase"] = 0);
        Fixture.Treaty(runtime, "aurel", "brass", "alliance");
        runtime.DeclareWar("ember", "dune", "");
        Assert.Empty(runtime.Calls);
        var war = Assert.Single(runtime.Wars);
        Assert.Equal((16UL, 8UL), (war.AttackerSide, war.DefenderSide));
    }

    [Fact]
    public void AllianceCallsTheOtherPartyWhicheverPlaceItHoldsInTheTreaty()
    {
        var first = World(root => Fixture.Rules(root, "war")["callBase"] = 0);
        Fixture.Treaty(first, "aurel", "brass", "alliance");
        first.DeclareWar("ember", "aurel", "");                       // the target is the first party: the second is called (a mutual defence)
        Assert.Equal(1, Assert.Single(first.Calls).Callee);
        var second = World(root => Fixture.Rules(root, "war")["callBase"] = 0);
        Fixture.Treaty(second, "aurel", "brass", "alliance");
        second.DeclareWar("ember", "brass", "");                      // the target is the second party: the first is called
        Assert.Equal(0, Assert.Single(second.Calls).Callee);
    }

    private static long Weariness(DipRuntime runtime, string from, string to) => runtime.Evaluate(from, to, "nap", 30, 0).Lines.First(l => l.Name == "weariness").Raw;

    [Fact]
    public void TheWearinessOfARealmAtWarIsItsExhaustionOnWhicheverSideItFights()
    {
        var runtime = Fixture.PlainWorld(root =>
        {
            Fixture.Realm(root, "cinder")["control"] = "ai";
            Fixture.Type(root, "nap")["weights"] = new JsonObject { ["weariness"] = 1000 };
            Fixture.Type(root, "nap")["bias"] = 0;
            Fixture.Type(root, "nap")["modifiers"] = new JsonArray();
        });
        runtime.DeclareWar("aurel", "brass", "");
        runtime.State.Wars[0].AttackerExhaustion = 7000;
        runtime.State.Wars[0].DefenderExhaustion = 9000;
        Assert.Equal(7, Weariness(runtime, "dune", "aurel"));
        Assert.Equal(9, Weariness(runtime, "dune", "brass"));
        Assert.Equal(0, Weariness(runtime, "aurel", "dune"));
    }

    [Fact]
    public void ASaveWithACeasefireThatPausesAWarIsOpenedAgain()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["exhaustPpm"] = 10000);
        runtime.DeclareWar("aurel", "brass", "");
        Fixture.Live(runtime, 4);
        runtime.Propose("aurel", "brass", "ceasefire", 20, 0);
        Fixture.Live(runtime, 9);
        Assert.True(runtime.Wars[0].Ceasefire);
        Assert.Equal(runtime.Wars[0].Id, runtime.Treaties[0].War);
        var reopened = DipSave.Open(runtime.Set, DipSave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), reopened.StateDigest());
    }
}
