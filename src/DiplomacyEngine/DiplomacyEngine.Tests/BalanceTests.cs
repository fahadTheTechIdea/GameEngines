using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>The balance of power: when a realm becomes a threat, who balances against it, who is left out and why, and when the coalition dissolves.</summary>
public sealed class BalanceTests
{
    /// <summary>aurel holds 500,000 of 1,650,000 (303 per mille). The balance wakes every 5 steps, a threat is 250 and over, and anyone not tied to it who does not like it much (regard 200 or less) joins.</summary>
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
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
        edit?.Invoke(root);
    });

    private static IEnumerable<int> Members(DipRuntime runtime, int target) =>
        Enumerable.Range(0, 5).Where(r => runtime.InCoalitionAgainst(r, target));

    [Fact]
    public void ARealmAboveTheThreatLineIsBalancedAgainstByEveryoneWhoIsNotTiedToIt()
    {
        var runtime = World();
        var changes = Fixture.Live(runtime, 0);
        var formed = changes.Of(ChangeKind.Coalition).Single();
        Assert.Equal((CoalitionChange.Formed, 0, -1, 4L), ((CoalitionChange)formed.Sub2, formed.A, formed.B, formed.Amount));
        Assert.Equal([1, 2, 3, 4], Members(runtime, 0).ToArray());
        var coalition = Assert.Single(runtime.Coalitions);
        Assert.Equal((0, 0L, 30UL), (coalition.Target, coalition.Since, coalition.Members));
        Assert.Equal((IncidentKind.Coalition, 0), ((IncidentKind)changes.Of(ChangeKind.Incident).Single().Sub, changes.Of(ChangeKind.Incident).Single().A));
        Assert.Equal(4, runtime.Grants.Count(g => g.Target == 0 && g.Cb == runtime.Set.CbById["balance"].Index));
    }

    [Fact]
    public void MembersLoseRegardForTheTargetWhileTheyBalanceAndNobodyElseDoes()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        Assert.Equal(-300, runtime.RegardOf("brass", "aurel"));
        Assert.Equal(0, runtime.RegardOf("aurel", "brass"));
        Assert.Equal(0, runtime.RegardOf("brass", "cinder"));
        Assert.True(runtime.Facts("", "brass", "aurel")["diplomacy.brass.aurel.coalition"].Boolean);
        Assert.False(runtime.Facts("", "aurel", "brass")["diplomacy.aurel.brass.coalition"].Boolean);
    }

    [Fact]
    public void MembersMayDeclareWarOnTheTargetOnTheBalanceCasusBelliAtNoCost()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        var infamy = Fixture.Standing(runtime, "brass", "infamy");
        runtime.DeclareWar("brass", "aurel", "balance");
        Assert.Equal(infamy, Fixture.Standing(runtime, "brass", "infamy"));
        Assert.Equal(DipCodes.NoCasusBelli, Fixture.Refusal(() => runtime.DeclareWar("aurel", "dune", "balance")));
    }

    [Fact]
    public void TheCasusBelliIsKeptAliveWhileTheCoalitionStands()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        Assert.Equal(0 + 60, runtime.Grants.First(g => g.Holder == 1).Until);
        Fixture.Live(runtime, 5);
        Assert.Equal(5 + 60, runtime.Grants.First(g => g.Holder == 1).Until);
        Assert.Equal(4, runtime.Grants.Count);
    }

    [Fact]
    public void NoCasusBelliIsGrantedWhenTheDocumentNamesNone()
    {
        var runtime = World(root => Fixture.Rules(root, "balance")["casusBelli"] = "");
        Fixture.Live(runtime, 0);
        Assert.Single(runtime.Coalitions);
        Assert.Empty(runtime.Grants);
    }

    [Fact]
    public void BelowTheThreatLineNoCoalitionForms()
    {
        var runtime = World(root => Fixture.Rules(root, "balance")["threatLine"] = 304);
        Fixture.Live(runtime, 30);
        Assert.Empty(runtime.Coalitions);
        Assert.Equal(303, runtime.ThreatOf(0));
    }

    [Fact]
    public void ARealmAtTheThreatLineExactlyIsBalancedAgainst()
    {
        var runtime = World(root => Fixture.Rules(root, "balance")["threatLine"] = 303);
        Fixture.Live(runtime, 0);
        Assert.Single(runtime.Coalitions);
    }

    [Fact]
    public void TheBalanceOnlyWakesOnItsIntervalSoAThreatThatArisesBetweenIsAnsweredAtTheNext()
    {
        var runtime = World(root => Fixture.Rules(root, "balance")["threatLine"] = 600);
        Fixture.Live(runtime, 2);
        runtime.SetMight("aurel", 5_000_000);
        Fixture.Live(runtime, 4);
        Assert.Empty(runtime.Coalitions);
        var changes = Fixture.Live(runtime, 5);
        Assert.Equal(5, changes.Of(ChangeKind.Coalition).Single().Step);
    }

    [Fact]
    public void ThreatIsTheShareOfAllMightGrownByInfamyAndCountsHalfTheMightOfARealmItProtects()
    {
        var runtime = World();
        Assert.Equal(303, runtime.ThreatOf(0));
        Assert.Equal(242, runtime.ThreatOf(1));
        var infamous = World(root => Fixture.Realm(root, "aurel")["standing"]!["infamy"] = 500);
        Assert.Equal(303 * 1500 / 1000, infamous.ThreatOf(0));
        Fixture.Treaty(runtime, "aurel", "dune", "alliance");
        Assert.Equal((500000 + 125000) * 1000 / 1650000, runtime.ThreatOf(0));
        Assert.Equal((250000 + 250000) * 1000 / 1650000, runtime.ThreatOf(3));
        var capped = World(root => Fixture.Realm(root, "aurel")["standing"]!["infamy"] = 1000);
        capped.SetMight("aurel", 1_000_000_000);
        Assert.Equal(1996, capped.ThreatOf(0));
    }

    [Theory]
    [InlineData("alliance", 3)]
    [InlineData("marriage", 3)]
    [InlineData("vassalage", 3)]
    public void RealmsTiedToTheTargetByAPactAMarriageOrVassalageDoNotJoin(string type, int tied)
    {
        var runtime = World();
        Fixture.Treaty(runtime, "aurel", "dune", type, 0, type == "vassalage" ? 100 : 0);
        Fixture.Live(runtime, runtime.Step + 5);
        Assert.DoesNotContain(tied, Members(runtime, 0));
        Assert.Equal([1, 2, 4], Members(runtime, 0).ToArray());
    }

    [Fact]
    public void ATradeTreatyDoesNotTieARealmToTheTarget()
    {
        var runtime = World();
        Fixture.Treaty(runtime, "aurel", "dune", "trade");
        Fixture.Live(runtime, runtime.Step + 5);
        Assert.Contains(3, Members(runtime, 0));
    }

    [Fact]
    public void WeakRealmsBandwagonWithAFearedTargetAndStrongOnesStillBalance()
    {
        var runtime = World(root => Fixture.Realm(root, "aurel")["standing"]!["fear"] = 700);
        Fixture.Live(runtime, 0);
        Assert.Equal([1, 2, 3], Members(runtime, 0).ToArray());          // ember (200,000) is under half of aurel (500,000) and bandwagons; dune at exactly half balances
    }

    [Fact]
    public void ATargetThatIsJustUnderTheFearLineIsNotFeared()
    {
        var runtime = World(root => Fixture.Realm(root, "aurel")["standing"]!["fear"] = 599);
        Fixture.Live(runtime, 0);
        Assert.Equal([1, 2, 3, 4], Members(runtime, 0).ToArray());
    }

    [Fact]
    public void RealmsInRevoltAndRealmsWithNoMightDoNotJoin()
    {
        var runtime = World(root =>
        {
            Fixture.Realm(root, "ember")["stability"] = 50;
            Fixture.Realm(root, "dune")["might"] = 0;
        });
        Fixture.Live(runtime, 0);
        Assert.Equal([1, 2], Members(runtime, 0).ToArray());
    }

    [Fact]
    public void ARealmThatLikesTheTargetEnoughDoesNotJoin()
    {
        var runtime = World();
        runtime.Remember("aurel", "brass", "gift", 1000);          // brass: +300, over the joining line of 200
        runtime.Remember("aurel", "dune", "gift", 600);            // dune: +180, under it
        Fixture.Live(runtime, 0);
        Assert.Equal([2, 3, 4], Members(runtime, 0).ToArray());
    }

    [Fact]
    public void ACoalitionNeedsItsMinimumNumberOfMembers()
    {
        var few = World(root => Fixture.Rules(root, "balance")["minMembers"] = 5);
        Fixture.Live(few, 10);
        Assert.Empty(few.Coalitions);
        var enough = World(root => Fixture.Rules(root, "balance")["minMembers"] = 4);
        Fixture.Live(enough, 0);
        Assert.Single(enough.Coalitions);
    }

    [Fact]
    public void ACoalitionDissolvesWhenTheThreatFallsToTheRelaxLineAndTheRegardPenaltyGoes()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        runtime.SetMight("aurel", 100_000);                       // 100,000 of 1,250,000 is 80 per mille
        var changes = Fixture.Live(runtime, 5);
        var ended = changes.Of(ChangeKind.Coalition).Single(c => c.A == 0);
        Assert.Equal((CoalitionChange.Dissolved, 0, 5L), ((CoalitionChange)ended.Sub2, ended.A, ended.Step));
        Assert.DoesNotContain(runtime.Coalitions, c => c.Target == 0);
        Assert.Equal(0, runtime.RegardOf("brass", "aurel"));
        Assert.Equal(1, changes.Of(ChangeKind.Incident).Count(i => i.Sub == (int)IncidentKind.Coalition && i.Index == 1 && i.A == 0));
    }

    [Fact]
    public void ACoalitionHoldsWhileTheThreatIsAboveTheRelaxLineEvenBelowTheThreatLine()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        runtime.SetMight("aurel", 300_000);                       // 300,000 of 1,450,000 is 206: under the line of 250, over the relax line of 150
        var changes = Fixture.Live(runtime, 5);
        Assert.DoesNotContain(changes.Of(ChangeKind.Coalition), c => c.A == 0);
        Assert.Contains(runtime.Coalitions, c => c.Target == 0);
    }

    [Fact]
    public void ACoalitionDissolvesWhenTheThreatIsExactlyAtTheRelaxLine()
    {
        var runtime = World(root => Fixture.Rules(root, "balance")["relaxLine"] = 206);
        Fixture.Live(runtime, 0);
        runtime.SetMight("aurel", 300_000);
        Assert.Equal(206, runtime.ThreatOf(0));
        Fixture.Live(runtime, 5);
        Assert.DoesNotContain(runtime.Coalitions, c => c.Target == 0);
    }

    [Fact]
    public void MembersJoinAndLeaveAsTheirFeelingsChangeWithAMarginSoACoalitionDoesNotFlicker()
    {
        var runtime = World();
        Fixture.Treaty(runtime, "aurel", "dune", "alliance");
        Fixture.Live(runtime, runtime.Step + 5);
        var start = runtime.Step;
        Assert.Equal([1, 2, 4], Members(runtime, 0).ToArray());
        runtime.Cancel(runtime.Treaties[0].Id, "dune");
        var joined = Fixture.Live(runtime, start + 5).Of(ChangeKind.Coalition).Where(c => c.A == 0).ToList();
        Assert.Equal([(CoalitionChange.Joined, 3)], joined.Select(c => ((CoalitionChange)c.Sub2, c.B)).ToArray());
        // brass comes to like aurel: +300 and the coalition's own -300 do not count against it; at the line plus the margin of 100 it stays, past it it leaves
        runtime.Remember("aurel", "brass", "gift", 1000);
        Assert.DoesNotContain(Fixture.Live(runtime, start + 10).Of(ChangeKind.Coalition), c => c.A == 0);
        Assert.Contains(1, Members(runtime, 0));
        runtime.Remember("aurel", "brass", "gift", 1000);
        var left = Fixture.Live(runtime, start + 15).Of(ChangeKind.Coalition).Where(c => c.A == 0).ToList();
        Assert.Equal([(CoalitionChange.Left, 1)], left.Select(c => ((CoalitionChange)c.Sub2, c.B)).ToArray());
    }

    [Fact]
    public void TwoRealmsCanBeBalancedAgainstAtOnce()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "balance")["threatLine"] = 200;
            Fixture.Rules(root, "balance")["relaxLine"] = 100;
        });
        Fixture.Live(runtime, 0);
        Assert.Equal([0, 1], runtime.Coalitions.Select(c => c.Target).ToArray());
        Assert.False(runtime.InCoalitionAgainst(0, 0));
    }

    [Fact]
    public void TheCoalitionExplanationSaysTheThreatTheLinesAndEachRealm()
    {
        var runtime = World();
        Fixture.Live(runtime, 0);
        var text = DipExplain.Coalition(runtime, "aurel");
        Assert.StartsWith("balance against aurel: threat 303 (joins above 250, dissolves at or below 150)\n  a coalition of 4 stands since step 0", text);
        Assert.Contains("\n  brass: regard -300, joins at or below 200, is a member", text);
    }
}
