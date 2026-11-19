using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>A realm's internal politics: contentment, legitimacy and stability, the lines that announce unrest and revolt, and the succession of rulers.</summary>
public sealed class PoliticsTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
    {
        foreach (var id in new[] { "alliance", "marriage", "nap" }) Fixture.Decided(root, id, true);
        edit?.Invoke(root);
    });

    /// <summary>Every faction of every realm as content as <paramref name="content"/>, stability following its target at once, and no weight on legitimacy.</summary>
    private static Action<JsonObject> Jump(int content, int startStability) => root =>
    {
        var politics = Fixture.Rules(root, "politics");
        politics["inertiaPpm"] = 1_000_000;
        politics["legitimacyWeight"] = 0;
        foreach (var realm in root["realms"]!.AsArray())
            foreach (var faction in realm!["factions"]!.AsArray()) faction!["satisfaction"] = content;
        Fixture.Realm(root, "aurel")["stability"] = startStability;
    };

    // ---- stability ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTargetStabilityIsTheFactionsContentmentWeightedByShareAndLegitimacyLessWarExhaustion()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "politics")["inertiaPpm"] = 1_000_000;
            Fixture.Rules(root, "war")["exhaustPpm"] = 200000;
        });
        Fixture.Live(runtime, 0);
        Assert.Equal(560 * 700 / 1000 + 800 * 300 / 1000, runtime.Realms[0].Stability);          // (600 x 600 + 500 x 400) / 1000 = 560 weighted; legitimacy 800 at 300 per mille
        runtime.DeclareWar("aurel", "brass", "");       // the court is at 550 and the commons at 400 after it
        Fixture.Live(runtime, 2);                        // the war tired both sides at step 1, and step 2 weighs it
        Assert.Equal(490 * 700 / 1000 + 800 * 300 / 1000 - 200 * 500 / 1000, runtime.Realms[0].Stability);   // an exhaustion of 200 per mille at 500 per mille
    }

    [Fact]
    public void StabilityMovesTowardsItsTargetByTheInertiaOfTheDocument()
    {
        var runtime = World(root => Fixture.Rules(root, "politics")["inertiaPpm"] = 50000);
        Fixture.Live(runtime, 0);
        Assert.Equal(697, runtime.Realms[0].Stability);         // 700 + (632 - 700) x 5 per cent, rounded towards the stability
        Fixture.Live(runtime, 1);
        Assert.Equal(697 + (632 - 697) * 50000 / 1_000_000, runtime.Realms[0].Stability);
    }

    [Fact]
    public void ContentmentDriftsBackTowardsTheMiddleAndLegitimacyTowardsItsBase()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "politics")["driftPpm"] = 100000;
            Fixture.Rules(root, "politics")["legitimacyDriftPpm"] = 100000;
        });
        Fixture.Live(runtime, 0);
        Assert.Equal([590, 500], runtime.Realms[0].Satisfaction);     // 600 and 500 towards 500
        Assert.Equal(790, runtime.Realms[0].Legitimacy);               // 800 towards 700
        Assert.Equal(610, runtime.Realms[1].Legitimacy);                // brass: 600 towards 700
        Fixture.Live(runtime, 1);
        Assert.Equal([581, 500], runtime.Realms[0].Satisfaction);
    }

    // ---- the lines ------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(700, 420, StabilityBand.Stable, -1)]
    [InlineData(700, 400, StabilityBand.Stable, -1)]
    [InlineData(700, 399, StabilityBand.Unrest, (int)PoliticsChange.Unrest)]
    [InlineData(700, 199, StabilityBand.Revolt, (int)PoliticsChange.Revolt)]
    [InlineData(700, 200, StabilityBand.Unrest, (int)PoliticsChange.Unrest)]
    [InlineData(300, 449, StabilityBand.Unrest, -1)]
    [InlineData(300, 450, StabilityBand.Stable, (int)PoliticsChange.Calm)]
    [InlineData(300, 199, StabilityBand.Revolt, (int)PoliticsChange.Revolt)]
    [InlineData(100, 249, StabilityBand.Revolt, -1)]
    [InlineData(100, 250, StabilityBand.Unrest, (int)PoliticsChange.Unrest)]
    [InlineData(100, 449, StabilityBand.Unrest, (int)PoliticsChange.Unrest)]
    [InlineData(100, 450, StabilityBand.Stable, (int)PoliticsChange.Calm)]
    public void ALineIsCrossedOnceWithAMarginOnTheWayBack(int start, int content, StabilityBand expected, int change)
    {
        var runtime = World(Jump(content, start));
        var changes = Fixture.Live(runtime, 0).Of(ChangeKind.Politics).Where(c => c.A == 0).ToList();
        Assert.Equal(content, runtime.Realms[0].Stability);
        Assert.Equal(expected, (StabilityBand)runtime.Realms[0].Band);
        if (change < 0) Assert.DoesNotContain(changes, c => c.A == 0);
        else
        {
            var one = Assert.Single(changes);
            Assert.Equal((change, (long)content), (one.Sub2, one.Amount));
        }
    }

    [Fact]
    public void EnteringUnrestRevoltOrCalmIsAnnouncedAsASignalAndRevoltIsInTheLog()
    {
        var calm = World(Jump(450, 300));
        Assert.Equal(["realm-calm"], Fixture.Live(calm, 0).Of(ChangeKind.Effect).Where(e => e.A == 0).Select(e => e.Name).ToArray());
        var unrest = World(Jump(399, 700));
        Assert.Equal(["realm-unrest"], Fixture.Live(unrest, 0).Of(ChangeKind.Effect).Where(e => e.A == 0).Select(e => e.Name).ToArray());
        var revolt = World(Jump(150, 700));
        var changes = Fixture.Live(revolt, 0);
        Assert.Equal(["realm-revolt"], changes.Of(ChangeKind.Effect).Where(e => e.A == 0).Select(e => e.Name).ToArray());
        Assert.Contains(changes.Of(ChangeKind.Incident), i => i.Sub == (int)IncidentKind.Revolt && i.A == 0 && i.Amount == 150);
        Assert.Equal((int)StabilityBand.Revolt, revolt.Realms[0].Band);
    }

    [Fact]
    public void ASignalTheDocumentLeavesEmptyIsNotIssued()
    {
        var runtime = World(root =>
        {
            Jump(150, 700)(root);
            Fixture.Rules(root, "politics")["signals"]!["revolt"] = "";
        });
        var changes = Fixture.Live(runtime, 0);
        Assert.DoesNotContain(changes.Of(ChangeKind.Effect), e => e.A == 0);
        Assert.Contains(changes.Of(ChangeKind.Politics), c => c.A == 0 && c.Sub2 == (int)PoliticsChange.Revolt);
    }

    // ---- succession -----------------------------------------------------------------------------------------------------------------

    private static Action<JsonObject> Never() => root =>
    {
        Fixture.Rules(root, "politics")["succession"]!["contestedBase"] = 0;
        Fixture.Rules(root, "politics")["succession"]!["contestedPerLack"] = 0;
    };

    private static Action<JsonObject> Always() => root =>
    {
        Fixture.Rules(root, "politics")["succession"]!["contestedBase"] = 1000;
    };

    [Fact]
    public void ASmoothSuccessionResetsLegitimacyEndsTheTreatiesThatEndWithTheRulerAndMakesNoClaims()
    {
        var runtime = World(Never());
        Fixture.Treaty(runtime, "aurel", "brass", "marriage");
        Fixture.Treaty(runtime, "aurel", "dune", "alliance");
        var changes = runtime.Succession("aurel");
        Assert.Equal((1, 700), (runtime.Realms[0].Generation, runtime.Realms[0].Legitimacy));
        var politics = changes.Of(ChangeKind.Politics).Single();
        Assert.Equal((PoliticsChange.Succession, 0), ((PoliticsChange)politics.Sub2, politics.A));
        Assert.Equal(["succession"], changes.Of(ChangeKind.Effect).Select(e => e.Name).ToArray());
        Assert.Equal([TreatyChange.Ended], changes.Of(ChangeKind.Treaty).Select(c => (TreatyChange)c.Sub2).ToArray());
        Assert.Equal(["alliance"], runtime.Treaties.Select(t => runtime.Set.Types[t.Type].Id).ToArray());
        Assert.Empty(runtime.Grants);
        var incident = changes.Of(ChangeKind.Incident).Single();
        Assert.Equal((IncidentKind.Succession, 0, 1L), ((IncidentKind)incident.Sub, incident.Index, incident.Amount));
    }

    [Fact]
    public void AContestedSuccessionHalvesLegitimacyShakesTheRealmAndGivesTheBondPartnersAClaim()
    {
        var runtime = World(Always());
        Fixture.Treaty(runtime, "aurel", "brass", "marriage");
        var step = runtime.Step;
        var changes = runtime.Succession("aurel");
        Assert.Equal((350, 700 - 150), (runtime.Realms[0].Legitimacy, runtime.Realms[0].Stability));
        Assert.Equal(PoliticsChange.ContestedSuccession, (PoliticsChange)changes.Of(ChangeKind.Politics).Single().Sub2);
        Assert.Equal(["succession-contested"], changes.Of(ChangeKind.Effect).Select(e => e.Name).ToArray());
        var grant = Assert.Single(runtime.Grants);
        Assert.Equal((1, 0, runtime.Set.CbById["claim"].Index, step + 120), (grant.Holder, grant.Target, grant.Cb, grant.Until));
        Assert.Equal(1, changes.Of(ChangeKind.Incident).Single().Index);
        Assert.True(runtime.Holds("brass", "aurel", "claim"));
        Assert.Empty(runtime.Treaties);
    }

    [Fact]
    public void ContestedStabilityNeverGoesBelowNothing()
    {
        var runtime = World(root =>
        {
            Always()(root);
            Fixture.Realm(root, "aurel")["stability"] = 100;
        });
        runtime.Succession("aurel");
        Assert.Equal(0, runtime.Realms[0].Stability);
    }

    [Fact]
    public void WhetherASuccessionIsContestedIsRolledFromTheRealmsOwnStreamAtTheDocumentsOdds()
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "politics")["succession"]!["contestedBase"] = 300;
            Fixture.Rules(root, "politics")["succession"]!["contestedPerLack"] = 0;
        });
        var contested = 0;
        for (var i = 0; i < 1000; i++)
            if (runtime.Succession("dune").Of(ChangeKind.Politics).Single().Sub2 == (int)PoliticsChange.ContestedSuccession) contested++;
        Assert.InRange(contested, 250, 350);
        var again = World(root =>
        {
            Fixture.Rules(root, "politics")["succession"]!["contestedBase"] = 300;
            Fixture.Rules(root, "politics")["succession"]!["contestedPerLack"] = 0;
        });
        for (var i = 0; i < 1000; i++) again.Succession("dune");
        Assert.Equal(runtime.StateDigest(), again.StateDigest());
    }

    [Fact]
    public void ALowLegitimacyMakesASuccessionLikelierToBeContested()
    {
        int Count(int legitimacy)
        {
            var count = 0;
            for (var seed = 1; seed <= 300; seed++)
            {
                var runtime = World(root =>
                {
                    root["seed"] = seed;
                    Fixture.Rules(root, "politics")["succession"]!["contestedBase"] = 100;
                    Fixture.Rules(root, "politics")["succession"]!["contestedPerLack"] = 600;
                    Fixture.Realm(root, "dune")["legitimacy"] = legitimacy;
                });
                if (runtime.Succession("dune").Of(ChangeKind.Politics).Single().Sub2 == (int)PoliticsChange.ContestedSuccession) count++;
            }
            return count;
        }
        Assert.InRange(Count(1000), 15, 50);          // 10 per cent
        Assert.InRange(Count(0), 190, 250);           // 100 + 600 = 70 per cent
    }

    [Fact]
    public void ASuccessionTheEngineRollsHappensAtTheRealmsRateGrownByLowLegitimacy()
    {
        var never = World();
        Fixture.Live(never, 200);
        Assert.All(never.Realms, r => Assert.Equal(0, r.Generation));
        var certain = World(root =>
        {
            foreach (var realm in root["realms"]!.AsArray()) realm!["successionPpm"] = 1_000_000;
        });
        Fixture.Live(certain, 4);
        Assert.All(certain.Realms, r => Assert.Equal(5, r.Generation));
        var half = World(root =>
        {
            Fixture.Realm(root, "dune")["successionPpm"] = 250000;
            Fixture.Realm(root, "dune")["legitimacy"] = 1000;
        });
        Fixture.Live(half, 400);
        Assert.InRange(half.Realms[3].Generation, 60, 200);
    }

    [Fact]
    public void TheFactionsReactToASuccession()
    {
        var runtime = World(Never());
        runtime.Succession("dune");
        Assert.Equal([600, 450], runtime.Realms[3].Satisfaction);        // the clans: -100 at half the scale of a smooth succession
        var contested = World(Always());
        contested.Succession("dune");
        Assert.Equal([600, 400], contested.Realms[3].Satisfaction);
    }

    [Fact]
    public void ARealmWithNoChanceOfSuccessionStillSucceedsWhenTheHostSaysSo()
    {
        var runtime = World(Never());
        runtime.Succession("ember");
        Assert.Equal(1, runtime.Realms[4].Generation);
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Succession("nobody")));
    }
}
