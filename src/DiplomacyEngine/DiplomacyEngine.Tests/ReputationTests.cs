using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>Standing in the world (trust, fear, honour, infamy), the regard one realm has of another and where it comes from, deeds that fade, and what a breach costs.</summary>
public sealed class ReputationTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
    {
        foreach (var id in new[] { "nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage" }) Fixture.Decided(root, id, true);
        Fixture.Realm(root, "cinder")["control"] = "ai";
        foreach (var type in root["types"]!.AsArray()) type!["signDeed"] = "";
        edit?.Invoke(root);
    });

    // ---- a breach -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABreachCostsTheCulpritTrustAndHonourAndIsRememberedByTheWrongedAndWitnessedByTheRest()
    {
        var runtime = World();
        var nap = Fixture.Treaty(runtime, "aurel", "brass", "nap", 100);
        Fixture.Live(runtime, 10);
        var changes = runtime.Cancel(nap.Id, "aurel");
        Assert.Equal(100 - 150, Fixture.Standing(runtime, "aurel", "trust"));
        Assert.Equal(100 - 100, Fixture.Standing(runtime, "aurel", "honour"));
        Assert.Equal(-600, runtime.RegardOf("brass", "aurel"));                  // the wronged remembers the betrayal in full
        Assert.Equal(-150 * 100 / 1000, runtime.RegardOf("dune", "aurel"));      // a witness in proportion to the trust lost
        Assert.Equal(-150 * 100 / 1000, runtime.RegardOf("cinder", "aurel"));
        Assert.Equal(0, runtime.RegardOf("aurel", "dune"));                        // the culprit itself remembers nothing
        var grant = Assert.Single(runtime.Grants);
        Assert.Equal((1, 0, 1, 10 + 60L), (grant.Holder, grant.Target, grant.Cb, grant.Until));
        Assert.Equal([450, 500], runtime.Realms[0].Satisfaction);   // the court: +50 for the treaty, -200 for breaking it
        var incident = Assert.Single(changes.Of(ChangeKind.Incident));
        Assert.Equal((IncidentKind.Breach, 0, 1, nap.Id), ((IncidentKind)incident.Sub, incident.A, incident.B, incident.Amount));
        Assert.Equal(["treaty-broken", "treaty-broken"], changes.Of(ChangeKind.Effect).Select(e => e.Name).ToArray());
    }

    [Fact]
    public void ATypeWithoutADeedOrACasusBelliLeavesNoneBehind()
    {
        var runtime = World(root =>
        {
            Fixture.Type(root, "nap")["breach"]!["deed"] = "";
            Fixture.Type(root, "nap")["breach"]!["casusBelli"] = "";
            Fixture.Type(root, "nap")["breach"]!["cbDays"] = 0;
        });
        var nap = Fixture.Treaty(runtime, "aurel", "brass", "nap", 100);
        runtime.Cancel(nap.Id, "aurel");
        Assert.Empty(runtime.Grants);
        Assert.Equal(0, runtime.RegardOf("brass", "aurel"));
        Assert.Equal(-15, runtime.RegardOf("dune", "aurel"));
    }

    // ---- standing decays ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void StandingFadesTowardsNothingByTheDocumentsRatePerStep()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "standing")["trustDecayPpm"] = 100000;
            Fixture.Rules(root, "standing")["fearDecayPpm"] = 200000;
            Fixture.Rules(root, "standing")["honourDecayPpm"] = 500000;
            Fixture.Rules(root, "standing")["infamyDecayPpm"] = 250000;
            Fixture.Realm(root, "ember")["standing"]!["trust"] = -1000;
        });
        Fixture.Live(runtime, 0);
        var aurel = runtime.Realms[0];
        Assert.Equal((90000L, 40000L, 50000L), (aurel.Trust, aurel.Fear, aurel.Honour));       // 100, 50, 100 per mille, each less its share
        Assert.Equal(-900000, runtime.Realms[4].Trust);                                         // a negative standing rises
        Assert.Equal(-25000, runtime.Realms[4].Honour);
        Assert.Equal(75000, runtime.Realms[4].Infamy);
        Fixture.Live(runtime, 1);
        Assert.Equal(81000L, aurel.Trust);
        Assert.Equal(56250, runtime.Realms[4].Infamy);
    }

    [Fact]
    public void ADecayAlwaysArrivesEvenWhenTheRateTimesTheDistanceRoundsToNothing()
    {
        var runtime = World(root => Fixture.Rules(root, "standing")["trustDecayPpm"] = 1);
        Fixture.Live(runtime, 4);
        Assert.Equal(100000 - 5, runtime.Realms[0].Trust);
        Assert.Equal(0, DipMath.Approach(1, 0, 1));
        Assert.Equal(0, DipMath.Approach(-1, 0, 1));
        Assert.Equal(1, DipMath.Approach(0, 1, 1));
        Assert.Equal(99999, DipMath.Approach(100000, 0, 1));
        Assert.Equal(5, DipMath.Approach(5, 5, 100));
        Assert.Equal(5, DipMath.Approach(5, 0, 0));
        Assert.Equal(0, DipMath.Approach(10, 0, 1_000_000));
    }

    [Fact]
    public void ARateOfZeroLeavesStandingAlone()
    {
        var runtime = World();
        Fixture.Live(runtime, 50);
        Assert.Equal(100000L, runtime.Realms[0].Trust);
    }

    // ---- deeds ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ADeedFadesInAStraightLineToNothingOverItsDays()
    {
        var runtime = World();
        runtime.Remember("aurel", "brass", "gift", 1000);       // brass remembers: +300 over 100 days
        Assert.Equal(300, runtime.RegardOf("brass", "aurel"));
        Fixture.Live(runtime, 49);                                // 50 steps on from step -1: half of it is left
        Assert.Equal(150, runtime.RegardOf("brass", "aurel"));
        Fixture.Live(runtime, 98);
        Assert.Equal(300 * 1 / 100, runtime.RegardOf("brass", "aurel"));
        Assert.Single(runtime.State.Deeds);
        Fixture.Live(runtime, 99);
        Assert.Equal(0, runtime.RegardOf("brass", "aurel"));
        Assert.Empty(runtime.State.Deeds);
    }

    [Fact]
    public void ADeedIsRememberedInProportionToItsScale()
    {
        var runtime = World();
        runtime.Remember("aurel", "brass", "insult", 500);
        Assert.Equal(-150, runtime.RegardOf("brass", "aurel"));
        Assert.Equal(0, runtime.RegardOf("aurel", "brass"));
    }

    [Fact]
    public void APairRemembersOnlyItsLimitOfDeedsAndForgetsTheOldestFirst()
    {
        var runtime = World();
        foreach (var scale in new[] { 100, 200, 300, 400, 500 }) runtime.Remember("aurel", "brass", "gift", scale);
        Assert.Equal(4, runtime.State.Deeds.Count);
        Assert.Equal(300 * 200 / 1000 + 300 * 300 / 1000 + 300 * 400 / 1000 + 300 * 500 / 1000, runtime.RegardOf("brass", "aurel"));
        runtime.Remember("aurel", "dune", "gift", 100);
        Assert.Equal(5, runtime.State.Deeds.Count);
    }

    [Fact]
    public void RememberingChecksItsArguments()
    {
        var runtime = World();
        Assert.Equal(DipCodes.UnknownDeed, Fixture.Refusal(() => runtime.Remember("aurel", "brass", "nothing", 5)));
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.Remember("aurel", "aurel", "gift", 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Remember("aurel", "brass", "gift", 0)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Remember("aurel", "brass", "gift", 1001)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Remember("nobody", "brass", "gift", 5)));
        runtime.Remember("aurel", "brass", "gift", 1);
        runtime.Remember("aurel", "brass", "gift", 1000);
    }

    // ---- where a regard comes from ------------------------------------------------------------------------------------------------

    [Fact]
    public void ARegardIsTheSumOfIdeologyDeedsTreatiesLeaderStandingAndCoalitionKeptWithinAThousand()
    {
        var runtime = World(root =>
        {
            var regard = Fixture.Rules(root, "regard");
            regard["ideology"] = 300;
            regard["leader"] = 400;
            regard["trust"] = 200;
            regard["honour"] = 100;
        });
        Assert.Equal(180 + 20 + 10, runtime.RegardOf("cinder", "aurel"));    // crown for crown 600 x 300 / 1000, aurel's trust 100 x 200 / 1000, honour 100 x 100 / 1000
        runtime.SetLeaderRegard("cinder", "aurel", 500);
        Assert.Equal(180 + 20 + 10 + 200, runtime.RegardOf("cinder", "aurel"));
        runtime.Remember("aurel", "cinder", "gift", 1000);
        Assert.Equal(180 + 20 + 10 + 200 + 300, runtime.RegardOf("cinder", "aurel"));
        Fixture.Treaty(runtime, "aurel", "cinder", "alliance");
        var parts = runtime.RegardTerms(runtime.Set.RealmById["cinder"].Index, 0);
        Assert.Equal((150L, 180L, 200L, 30L, 0L), (parts.Treaties, parts.Ideology, parts.Leader, parts.Standing, parts.Coalition));
        Assert.Equal(parts.Sum(), runtime.RegardOf("cinder", "aurel"));
        Assert.InRange(parts.Deeds, 280, 299);
    }

    [Fact]
    public void TheRegardTextSaysEachPart()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "regard")["ideology"] = 300;
            Fixture.Rules(root, "regard")["trust"] = 200;
        });
        runtime.Remember("aurel", "cinder", "insult", 1000);
        var text = DipExplain.Regard(runtime, "cinder", "aurel");
        Assert.Equal("regard of cinder for aurel: -100\n  ideology 180\n  deeds -300\n  treaties 0\n  leader 0\n  standing 20\n  coalition 0\n  (the sum is kept within -1000 and 1000)", text);
    }

    [Fact]
    public void ARegardIsKeptWithinMinusAndPlusAThousand()
    {
        var runtime = World(root => Fixture.Rules(root, "regard")["leader"] = 2000);
        runtime.SetLeaderRegard("brass", "aurel", 1000);
        Assert.Equal(1000, runtime.RegardOf("brass", "aurel"));
        runtime.SetLeaderRegard("brass", "aurel", -1000);
        Assert.Equal(-1000, runtime.RegardOf("brass", "aurel"));
    }

    [Fact]
    public void ALeadersRegardChecksItsArguments()
    {
        var runtime = World();
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.SetLeaderRegard("aurel", "aurel", 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.SetLeaderRegard("aurel", "brass", 1001)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.SetLeaderRegard("aurel", "brass", -1001)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.SetLeaderRegard("aurel", "nobody", 5)));
        runtime.SetLeaderRegard("aurel", "brass", 1000);
        runtime.SetLeaderRegard("aurel", "brass", -1000);
    }

    [Fact]
    public void MightIsSetWithinItsRange()
    {
        var runtime = World();
        runtime.SetMight("aurel", 0);
        runtime.SetMight("aurel", DipFacts.Saturate);
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.SetMight("aurel", -1)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.SetMight("aurel", DipFacts.Saturate + 1)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.SetMight("nobody", 1)));
        Assert.Equal(DipFacts.Saturate, runtime.Realms[0].Might);
    }

    // ---- tit for tat ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AWrongedRealmIsProvokedAtOnceAndForgivesAsTheDeedFades()
    {
        var runtime = World();
        var nap = Fixture.Treaty(runtime, "aurel", "brass", "nap", 300);
        runtime.Cancel(nap.Id, "aurel");
        var step = runtime.Step;
        Assert.Equal(-600, runtime.RegardOf("brass", "aurel"));
        Assert.True(runtime.Holds("brass", "aurel", "betrayed"));               // provocable: a casus belli at once
        Fixture.Live(runtime, step + 100);
        Assert.Equal(-300, runtime.RegardOf("brass", "aurel"));
        Fixture.Live(runtime, step + 199);
        Assert.Equal(-3, runtime.RegardOf("brass", "aurel"));
        Fixture.Live(runtime, step + 200);
        Assert.Equal(0, runtime.RegardOf("brass", "aurel"));                     // forgiving: nothing is held for ever
        Assert.False(runtime.Holds("brass", "aurel", "betrayed"));
    }

    [Fact]
    public void KeepingAWordRaisesTrustAndWitnessesNoticeNothing()
    {
        var runtime = World();
        var trust = (Fixture.Standing(runtime, "aurel", "trust"), Fixture.Standing(runtime, "brass", "trust"));
        Fixture.Treaty(runtime, "aurel", "brass", "nap", 10);
        Fixture.Live(runtime, runtime.Step + 11);
        Assert.Equal((trust.Item1 + 20, trust.Item2 + 20), (Fixture.Standing(runtime, "aurel", "trust"), Fixture.Standing(runtime, "brass", "trust")));
        Assert.Equal(0, runtime.RegardOf("dune", "aurel"));
    }

    // ---- what the host and the world report -----------------------------------------------------------------------------------------

    [Fact]
    public void AnEventTheHostReportsMovesTheFactionsThatMindItAtTheScaleGiven()
    {
        var runtime = World();
        runtime.Happen("aurel", "disaster", 500);                // commons -200 x 500 / 1000
        Assert.Equal([600, 400], runtime.Realms[0].Satisfaction);
        runtime.Happen("aurel", "disaster", 1000);
        Assert.Equal([600, 200], runtime.Realms[0].Satisfaction);
        for (var i = 0; i < 4; i++) runtime.Happen("aurel", "disaster", 1000);
        Assert.Equal(0, runtime.Realms[0].Satisfaction[1]);       // never below nothing
        runtime.Happen("aurel", "war_won", 1000);
        runtime.Happen("aurel", "war_won", 1000);
        runtime.Happen("aurel", "war_won", 1000);
        runtime.Happen("aurel", "war_won", 1000);
        Assert.Equal(1000, runtime.Realms[0].Satisfaction[0]);    // never above a thousand
    }

    [Fact]
    public void ReportingAnEventChecksItsArguments()
    {
        var runtime = World();
        Assert.Equal(DipCodes.UnknownEvent, Fixture.Refusal(() => runtime.Happen("aurel", "earthquake", 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Happen("aurel", "disaster", 0)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Happen("aurel", "disaster", 1001)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Happen("nobody", "disaster", 5)));
        runtime.Happen("aurel", "disaster", 1);
        runtime.Happen("aurel", "disaster", 1000);
    }
}
