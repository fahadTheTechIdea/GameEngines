using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>Casus belli and declaring war, calls to arms, joining and leaving, the war score, ceasefires and peace, and the exhaustion that makes a realm want one.</summary>
public sealed class WarTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
    {
        foreach (var id in new[] { "nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage" }) Fixture.Decided(root, id, true);
        Fixture.Realm(root, "cinder")["control"] = "ai";
        edit?.Invoke(root);
    });

    private static IEnumerable<T> Of<T>(IEnumerable<DipChange> changes, ChangeKind kind, Func<DipChange, T> pick) => changes.Where(c => c.Kind == kind).Select(pick);

    // ---- declaring ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AWarWithNoCasusBelliWhereTheDocumentAllowsItCostsInfamyAndHonourAndIsRememberedByTheTarget()
    {
        var runtime = World();
        var infamy = Fixture.Standing(runtime, "aurel", "infamy");
        var honour = Fixture.Standing(runtime, "aurel", "honour");
        var changes = runtime.DeclareWar("aurel", "brass", "");
        var war = Assert.Single(runtime.Wars);
        Assert.Equal((0, 1, -1, 0), (war.Attacker, war.Defender, war.Cb, war.Score));
        Assert.Equal(1UL, war.AttackerSide);
        Assert.Equal(2UL, war.DefenderSide);
        Assert.Equal(infamy + 200, Fixture.Standing(runtime, "aurel", "infamy"));
        Assert.Equal(honour - 150, Fixture.Standing(runtime, "aurel", "honour"));
        Assert.Equal(-500, runtime.RegardOf("brass", "aurel"));
        Assert.Equal(0, runtime.RegardOf("aurel", "brass"));
        Assert.Equal(WarChange.Declared, (WarChange)changes.Of(ChangeKind.War).Single().Sub2);
        Assert.Equal(IncidentKind.WarDeclared, (IncidentKind)changes.Of(ChangeKind.Incident).Single().Sub);
        Assert.True(runtime.AtWar(0, 1) && runtime.AtWar(1, 0));
        Assert.False(runtime.AtWar(0, 2));
    }

    [Fact]
    public void TheFactionsOfBothRealmsReactToTheDeclaration()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Equal([550, 400], runtime.Realms[0].Satisfaction);        // court -50, commons -100
        Assert.Equal([600, 500], runtime.Realms[1].Satisfaction);        // war on brass: nobody there minds
    }

    [Fact]
    public void WhereTheDocumentForbidsAWarWithNoCasusBelliItIsRefused()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["allowNoCb"] = false);
        Assert.Equal(DipCodes.NoCasusBelli, Fixture.Refusal(() => runtime.DeclareWar("aurel", "brass", "")));
        Assert.Empty(runtime.Wars);
    }

    [Fact]
    public void AStandingCasusBelliIsHeldWhileItsConditionHoldsAndCostsWhatItSays()
    {
        var runtime = World();
        Assert.False(runtime.Holds("aurel", "brass", "rivalry"));
        Assert.Equal(DipCodes.NoCasusBelli, Fixture.Refusal(() => runtime.DeclareWar("aurel", "brass", "rivalry")));
        runtime.Remember("brass", "aurel", "betrayal", 1000);       // aurel remembers a betrayal by brass: -600
        Assert.True(runtime.Holds("aurel", "brass", "rivalry"));
        Assert.False(runtime.Holds("brass", "aurel", "rivalry"));
        var infamy = Fixture.Standing(runtime, "aurel", "infamy");
        var honour = Fixture.Standing(runtime, "aurel", "honour");
        var changes = runtime.DeclareWar("aurel", "brass", "rivalry");
        Assert.Equal(0, runtime.Wars[0].Cb);
        Assert.Equal(infamy + 100, Fixture.Standing(runtime, "aurel", "infamy"));
        Assert.Equal(honour - 50, Fixture.Standing(runtime, "aurel", "honour"));
        Assert.Equal(0, changes.Of(ChangeKind.War).Single().Sub);
    }

    [Fact]
    public void AGrantedCasusBelliIsHeldUntilItEndsAndIsUsedUp()
    {
        var runtime = World();
        Assert.Equal(DipCodes.NoCasusBelli, Fixture.Refusal(() => runtime.DeclareWar("aurel", "ember", "border")));
        var granted = runtime.GrantCasusBelli("aurel", "ember", "border", 30);
        Assert.Equal(GrantChange.Granted, (GrantChange)granted.Of(ChangeKind.Grant).Single().Sub2);
        Assert.True(runtime.Holds("aurel", "ember", "border"));
        Assert.False(runtime.Holds("ember", "aurel", "border"));
        Assert.False(runtime.Holds("aurel", "dune", "border"));
        var infamy = Fixture.Standing(runtime, "aurel", "infamy");
        var changes = runtime.DeclareWar("aurel", "ember", "border");
        Assert.Equal(infamy, Fixture.Standing(runtime, "aurel", "infamy"));
        Assert.Equal(GrantChange.Used, (GrantChange)changes.Of(ChangeKind.Grant).Single().Sub2);
        Assert.Empty(runtime.Grants);
    }

    [Fact]
    public void AGrantedCasusBelliRunsOutAfterItsDays()
    {
        var runtime = World();
        runtime.GrantCasusBelli("aurel", "ember", "border", 30);
        Assert.Empty(Fixture.Live(runtime, 29).Of(ChangeKind.Grant));
        Assert.True(runtime.Holds("aurel", "ember", "border"));
        var expired = Fixture.Live(runtime, 30).Of(ChangeKind.Grant).Single();
        Assert.Equal((GrantChange.Expired, 30L), ((GrantChange)expired.Sub2, expired.Step));
        Assert.False(runtime.Holds("aurel", "ember", "border"));
        Assert.Equal(DipCodes.NoCasusBelli, Fixture.Refusal(() => runtime.DeclareWar("aurel", "ember", "border")));
    }

    [Fact]
    public void AGrantAgainExtendsTheOldOneAndTheWorldKeepsAtMostItsLimit()
    {
        var runtime = World(root => root["limits"]!["maxGrants"] = 2);
        runtime.GrantCasusBelli("aurel", "ember", "border", 10);
        runtime.GrantCasusBelli("aurel", "ember", "border", 50);
        runtime.GrantCasusBelli("aurel", "ember", "border", 20);
        Assert.Equal(50, runtime.Grants.Single().Until);
        runtime.GrantCasusBelli("brass", "ember", "border", 10);
        var overflow = runtime.GrantCasusBelli("dune", "ember", "border", 10);
        Assert.Equal([GrantChange.Expired, GrantChange.Granted], overflow.Of(ChangeKind.Grant).Select(c => (GrantChange)c.Sub2).ToArray());
        Assert.Equal(2, runtime.Grants.Count);
        Assert.False(runtime.Holds("aurel", "ember", "border"));
    }

    [Fact]
    public void GrantingChecksItsArguments()
    {
        var runtime = World();
        Assert.Equal(DipCodes.UnknownCb, Fixture.Refusal(() => runtime.GrantCasusBelli("aurel", "ember", "nothing", 5)));
        Assert.Equal(DipCodes.UnknownCb, Fixture.Refusal(() => runtime.GrantCasusBelli("aurel", "ember", "rivalry", 5)));
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.GrantCasusBelli("aurel", "aurel", "border", 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.GrantCasusBelli("aurel", "ember", "border", 0)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.GrantCasusBelli("aurel", "ember", "border", 36501)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.GrantCasusBelli("nobody", "ember", "border", 5)));
        runtime.GrantCasusBelli("aurel", "ember", "border", 36500);
        Assert.Equal(DipCodes.UnknownCb, Fixture.Refusal(() => runtime.Holds("aurel", "ember", "nothing")));
        Assert.False(runtime.Holds("aurel", "aurel", "border"));
    }

    [Fact]
    public void AWarCannotBeDeclaredOnItselfInRevoltOnARealmAlreadyFoughtOrOnAnAllyOnTheSameSide()
    {
        var runtime = World(root =>
        {
            Fixture.Realm(root, "ember")["stability"] = 50;
            root["limits"]!["maxWars"] = 2;
        });
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.DeclareWar("aurel", "aurel", "")));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.DeclareWar("aurel", "nobody", "")));
        Assert.Equal(DipCodes.Revolt, Fixture.Refusal(() => runtime.DeclareWar("ember", "aurel", "")));
        Assert.Equal(DipCodes.UnknownCb, Fixture.Refusal(() => runtime.DeclareWar("aurel", "brass", "nothing")));
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Equal(DipCodes.AtWar, Fixture.Refusal(() => runtime.DeclareWar("aurel", "brass", "")));
        Assert.Equal(DipCodes.AtWar, Fixture.Refusal(() => runtime.DeclareWar("brass", "aurel", "")));
        runtime.JoinWar(0, "dune", "aurel");
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.DeclareWar("aurel", "dune", "")));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.DeclareWar("dune", "aurel", "")));
        runtime.DeclareWar("cinder", "ember", "");
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.DeclareWar("dune", "cinder", "")));
    }

    // ---- what a declaration does to treaties ----------------------------------------------------------------------------------------

    [Fact]
    public void DeclaringWarOnARealmOneHasANonAggressionPactWithBreaksItAndEverySeesIt()
    {
        var runtime = World();
        var nap = Fixture.Treaty(runtime, "aurel", "brass", "nap", 100);
        Fixture.Treaty(runtime, "aurel", "brass", "trade");
        Fixture.Live(runtime, 3);
        var trust = Fixture.Standing(runtime, "aurel", "trust");
        var honour = Fixture.Standing(runtime, "aurel", "honour");
        var changes = runtime.DeclareWar("aurel", "brass", "");
        var treaties = changes.Of(ChangeKind.Treaty).Select(c => ((TreatyChange)c.Sub2, runtime.Set.Types[c.Sub].Id)).ToList();
        Assert.Equal([(TreatyChange.Broken, "nap"), (TreatyChange.Ended, "trade")], treaties);
        Assert.Empty(runtime.Treaties);
        Assert.Equal(trust - 150, Fixture.Standing(runtime, "aurel", "trust"));
        Assert.Equal(honour - 150 - 100, Fixture.Standing(runtime, "aurel", "honour"));       // the war with no casus belli, and the broken word
        Assert.Equal(-100 * 150 / 1000, runtime.RegardOf("dune", "aurel"));                      // a witness remembers the breach in proportion to the trust lost
        Assert.True(runtime.Holds("brass", "aurel", "betrayed"));
        Assert.Contains(changes.Items, c => c.Kind == ChangeKind.Effect && c.Name == "treaty-broken");
        Assert.Equal([IncidentKind.Breach, IncidentKind.WarDeclared], changes.Of(ChangeKind.Incident).Select(c => (IncidentKind)c.Sub).ToArray());
        Assert.Equal(nap.Id, changes.Of(ChangeKind.Incident)[0].Amount);
    }

    // ---- calls to arms --------------------------------------------------------------------------------------------------------------

    private static DipRuntime Allied(int callBase, Action<JsonObject>? edit = null)
    {
        var runtime = World(root =>
        {
            Fixture.Rules(root, "war")["callBase"] = callBase;
            edit?.Invoke(root);
        });
        Fixture.Treaty(runtime, "brass", "dune", "alliance");
        return runtime;
    }

    [Fact]
    public void AnAllyTheEngineDecidesForAnswersACallThatIsCertainAndKeepsItsWord()
    {
        var runtime = Allied(1000);
        var trust = Fixture.Standing(runtime, "brass", "trust");
        var honour = Fixture.Standing(runtime, "brass", "honour");
        var changes = runtime.DeclareWar("aurel", "dune", "");
        Assert.Equal([ObligationChange.Called, ObligationChange.Honoured], changes.Of(ChangeKind.Obligation).Select(c => (ObligationChange)c.Sub2).ToArray());
        Assert.Equal([WarChange.Declared, WarChange.Joined], changes.Of(ChangeKind.War).Select(c => (WarChange)c.Sub2).ToArray());
        Assert.Equal(1, changes.Of(ChangeKind.War)[1].Amount2);
        Assert.Equal(8UL | 2UL, runtime.Wars[0].DefenderSide);
        Assert.Empty(runtime.Calls);
        Assert.Equal(trust + 50, Fixture.Standing(runtime, "brass", "trust"));
        Assert.Equal(honour + 40, Fixture.Standing(runtime, "brass", "honour"));
        Assert.Equal(-500, runtime.RegardOf("brass", "aurel"));
    }

    [Fact]
    public void AnAllyThatDoesNotAnswerBreaksItsWordWhenTheGraceIsOver()
    {
        var runtime = Allied(0);
        var changes = runtime.DeclareWar("aurel", "dune", "");
        var call = Assert.Single(runtime.Calls);
        var declared = runtime.Step;
        Assert.Equal((1, 3, declared + 3), (call.Callee, call.Caller, call.Due));
        Assert.Equal([ObligationChange.Called], changes.Of(ChangeKind.Obligation).Select(c => (ObligationChange)c.Sub2).ToArray());
        Assert.Empty(Fixture.Live(runtime, declared + 3).Of(ChangeKind.Obligation));
        var trust = Fixture.Standing(runtime, "brass", "trust");
        var late = Fixture.Live(runtime, declared + 4);
        Assert.Equal([ObligationChange.Ignored], late.Of(ChangeKind.Obligation).Select(c => (ObligationChange)c.Sub2).ToArray());
        Assert.Contains(late, c => c.Kind == ChangeKind.Treaty && c.Sub2 == (int)TreatyChange.Broken);
        Assert.Equal(trust - 300, Fixture.Standing(runtime, "brass", "trust"));
        Assert.Empty(runtime.Calls);
        Assert.Empty(runtime.Treaties);
        Assert.Contains(late, c => c.Kind == ChangeKind.Incident && c.Sub == (int)IncidentKind.CallIgnored);
        Assert.True(runtime.Holds("dune", "brass", "betrayed"));
    }

    [Fact]
    public void ARealmTheHostDecidesForAnswersTheCallByJoiningAndIsHonouredForIt()
    {
        var runtime = Allied(1000, root => Fixture.Realm(root, "brass")["control"] = "host");
        runtime.DeclareWar("aurel", "dune", "");
        Assert.Single(runtime.Calls);
        var trust = Fixture.Standing(runtime, "brass", "trust");
        var join = runtime.JoinWar(0, "brass", "dune");
        Assert.Equal(ObligationChange.Honoured, (ObligationChange)join.Of(ChangeKind.Obligation).Single().Sub2);
        Assert.Empty(runtime.Calls);
        Assert.Equal(trust + 50, Fixture.Standing(runtime, "brass", "trust"));
        Assert.Empty(Fixture.Live(runtime, runtime.Step + 10).Of(ChangeKind.Obligation));
    }

    [Fact]
    public void JoiningTheOtherSideDoesNotAnswerTheCall()
    {
        var runtime = Allied(0, root => Fixture.Realm(root, "brass")["control"] = "host");
        runtime.DeclareWar("aurel", "dune", "");
        var join = runtime.JoinWar(0, "brass", "aurel");
        Assert.Empty(join.Of(ChangeKind.Obligation));
        Assert.Single(runtime.Calls);
    }

    [Fact]
    public void AMutualPactCallsEitherPartnerAndAOneWayPactOnlyCallsTheProtector()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["callBase"] = 0);
        Fixture.Treaty(runtime, "brass", "dune", "vassalage", 0, 100);       // brass protects its vassal dune
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Empty(runtime.Calls);                                          // the vassal owes the protector nothing
        runtime.DeclareWar("ember", "dune", "");
        Assert.Equal(1, Assert.Single(runtime.Calls).Callee);
    }

    [Fact]
    public void ADefenderIsCalledOnceEvenIfItHasTwoPactsAndNeverTheAttacker()
    {
        var runtime = Allied(0, root =>
        {
            Fixture.Decided(root, "vassalage", true);
        });
        Fixture.Treaty(runtime, "brass", "dune", "vassalage", 0, 100);
        runtime.DeclareWar("aurel", "dune", "");
        Assert.Equal(1, Assert.Single(runtime.Calls).Callee);
        var other = Allied(0);
        Fixture.Treaty(other, "aurel", "dune", "trade");
        other.DeclareWar("brass", "dune", "");
        Assert.DoesNotContain(other.Calls, c => c.Callee == 1);
    }

    [Fact]
    public void ACallIsDroppedWhenItsWarEndsOrItsPactEnds()
    {
        var runtime = Allied(0);
        runtime.DeclareWar("aurel", "dune", "");
        runtime.Propose("aurel", "dune", "peace", 60, 0);
        Fixture.Live(runtime, runtime.Step + 2);
        Assert.Empty(runtime.Wars);
        Assert.Empty(runtime.Calls);
        Assert.Contains(runtime.Treaties, t => runtime.Set.Types[t.Type].Id == "alliance");
    }

    [Fact]
    public void TheWorldHoldsAtMostItsLimitOfCallsAndRefusesTheWarThatWouldPassIt()
    {
        var runtime = Allied(0, root => root["limits"]!["maxCalls"] = 1);
        Fixture.Treaty(runtime, "ember", "dune", "alliance");
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.DeclareWar("aurel", "dune", "")));
        Assert.Empty(runtime.Wars);
        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public void TheChanceToAnswerACallIsTheBaseWithTheTraitsAndAQuarterOfHonour()
    {
        var runtime = Allied(500, root =>
        {
            Fixture.Realm(root, "brass")["traits"] = new JsonArray("shady");
            Fixture.Realm(root, "brass")["standing"]!["honour"] = 400;
        });
        Assert.Equal(500 - 300 + 100, runtime.CallChance(1));
        Assert.Equal(525, runtime.CallChance(0));
    }

    // ---- joining and leaving -------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARealmJoinsOneOfTheTwoSidesOnceAndCanLeaveAgainButNotThePrincipals()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Equal(DipCodes.UnknownWar, Fixture.Refusal(() => runtime.JoinWar(5, "dune", "aurel")));
        Assert.Equal(DipCodes.NotPrincipal, Fixture.Refusal(() => runtime.JoinWar(0, "dune", "cinder")));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.JoinWar(0, "aurel", "brass")));
        var joined = runtime.JoinWar(0, "dune", "brass");
        Assert.Equal(8UL | 2UL, runtime.Wars[0].DefenderSide);
        Assert.Equal((WarChange.Joined, 3L), ((WarChange)joined.Of(ChangeKind.War).Single().Sub2, joined.Of(ChangeKind.War).Single().Amount2));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.JoinWar(0, "dune", "aurel")));
        Assert.Equal(DipCodes.NotPrincipal, Fixture.Refusal(() => runtime.LeaveWar(0, "aurel")));
        Assert.Equal(DipCodes.NotPrincipal, Fixture.Refusal(() => runtime.LeaveWar(0, "brass")));
        Assert.Equal(DipCodes.NotInWar, Fixture.Refusal(() => runtime.LeaveWar(0, "cinder")));
        var left = runtime.LeaveWar(0, "dune");
        Assert.Equal(WarChange.Left, (WarChange)left.Of(ChangeKind.War).Single().Sub2);
        Assert.Equal(2UL, runtime.Wars[0].DefenderSide);
    }

    [Fact]
    public void ARealmInRevoltJoinsNoWar()
    {
        var runtime = World(root => Fixture.Realm(root, "ember")["stability"] = 100);
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Equal(DipCodes.Revolt, Fixture.Refusal(() => runtime.JoinWar(0, "ember", "aurel")));
    }

    // ---- the score ------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("battle", 1000, "aurel", 300)]
    [InlineData("battle", 1000, "brass", -300)]
    [InlineData("occupation", 1000, "aurel", 500)]
    [InlineData("blockade", 1000, "aurel", 200)]
    [InlineData("event", 1000, "aurel", 400)]
    [InlineData("battle", 500, "aurel", 150)]
    [InlineData("battle", 0, "aurel", 0)]
    public void AScoreIsTheAmountTimesTheSourcesWeightForTheSideOfTheRealmFavoured(string source, int amount, string favoured, int expected)
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        var changes = runtime.Score(0, favoured, amount, source);
        Assert.Equal(expected, runtime.Wars[0].Score);
        var change = changes.Of(ChangeKind.War).Single();
        Assert.Equal((WarChange.Score, (long)expected), ((WarChange)change.Sub2, change.Amount));
    }

    [Fact]
    public void TheScoreStaysWithinAThousandEitherWay()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["sourceWeights"]!["battle"] = 2000);
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Score(0, "aurel", 1000, "battle");
        runtime.Score(0, "aurel", 1000, "battle");
        Assert.Equal(1000, runtime.Wars[0].Score);
        for (var i = 0; i < 4; i++) runtime.Score(0, "brass", 1000, "battle");
        Assert.Equal(-1000, runtime.Wars[0].Score);
    }

    [Fact]
    public void AScoreChecksItsArguments()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        Assert.Equal(DipCodes.UnknownWar, Fixture.Refusal(() => runtime.Score(9, "aurel", 5, "battle")));
        Assert.Equal(DipCodes.UnknownSource, Fixture.Refusal(() => runtime.Score(0, "aurel", 5, "luck")));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Score(0, "aurel", -1, "battle")));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Score(0, "aurel", 1001, "battle")));
        Assert.Equal(DipCodes.NotInWar, Fixture.Refusal(() => runtime.Score(0, "cinder", 5, "battle")));
        runtime.Score(0, "aurel", 1000, "battle");
        runtime.Score(0, "aurel", 0, "event");
    }

    // ---- ceasefire and peace -------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACeasefireBetweenThePrincipalsPausesTheWarAndStopsItsScoreAndExhaustion()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["exhaustPpm"] = 10000);
        runtime.DeclareWar("aurel", "brass", "");
        Fixture.Live(runtime, 4);
        var tired = runtime.Wars[0].AttackerExhaustion;
        Assert.Equal(50000, tired);
        runtime.Propose("aurel", "brass", "ceasefire", 20, 0);
        var changes = Fixture.Live(runtime, 5);
        Assert.Contains(changes, c => c.Kind == ChangeKind.War && c.Sub2 == (int)WarChange.Ceasefire);
        Assert.True(runtime.Wars[0].Ceasefire);
        Assert.Equal(runtime.Wars[0].Id, runtime.Treaties[0].War);
        Assert.Equal(IncidentKind.Ceasefire, (IncidentKind)runtime.Incidents[^1].Kind);
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.Score(0, "aurel", 100, "battle")));
        Fixture.Live(runtime, 10);
        Assert.Equal(tired, runtime.Wars[0].AttackerExhaustion);       // a paused war tires no one
    }

    [Fact]
    public void ACeasefireThatRunsOutResumesTheWar()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Propose("aurel", "brass", "ceasefire", 10, 0);
        Fixture.Live(runtime, 1);
        Assert.True(runtime.Wars[0].Ceasefire);
        var changes = Fixture.Live(runtime, 11);
        Assert.Contains(changes, c => c.Kind == ChangeKind.Treaty && c.Sub2 == (int)TreatyChange.Expired);
        Assert.Contains(changes, c => c.Kind == ChangeKind.War && c.Sub2 == (int)WarChange.Resumed);
        Assert.False(runtime.Wars[0].Ceasefire);
        Assert.Empty(runtime.Treaties);
    }

    [Fact]
    public void DeclaringWarAgainInACeasefireResumesTheWarAndBreaksTheCeasefire()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Propose("aurel", "brass", "ceasefire", 30, 0);
        Fixture.Live(runtime, 2);
        var trust = Fixture.Standing(runtime, "brass", "trust");
        var changes = runtime.DeclareWar("brass", "aurel", "");
        Assert.Single(runtime.Wars);
        Assert.False(runtime.Wars[0].Ceasefire);
        Assert.Equal(TreatyChange.Broken, (TreatyChange)changes.Of(ChangeKind.Treaty).Single().Sub2);
        Assert.Contains(changes.Items, c => c.Kind == ChangeKind.War && c.Sub2 == (int)WarChange.Resumed);
        Assert.Equal(trust - 250, Fixture.Standing(runtime, "brass", "trust"));
        Assert.Empty(runtime.Treaties);
        Assert.Equal(0, runtime.Wars[0].Attacker);
    }

    [Fact]
    public void OnlyThePrincipalsMakeAPeaceOrACeasefireAndOnlyInAWar()
    {
        var runtime = World();
        Assert.Equal(DipCodes.NotAtWar, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "peace", 60, 0)));
        runtime.DeclareWar("aurel", "brass", "");
        runtime.JoinWar(0, "dune", "aurel");
        Assert.Equal(DipCodes.NotPrincipal, Fixture.Refusal(() => runtime.Propose("dune", "brass", "peace", 60, 0)));
        Assert.Equal(DipCodes.NotPrincipal, Fixture.Refusal(() => runtime.Propose("dune", "brass", "ceasefire", 20, 0)));
        Assert.Equal(DipCodes.AtWar, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 60, 0)));
        Assert.Equal(DipCodes.AtWar, Fixture.Refusal(() => runtime.Propose("dune", "brass", "trade", 0, 0)));
        runtime.Propose("aurel", "brass", "ceasefire", 20, 0);
        Fixture.Live(runtime, 1);
        Assert.Equal(DipCodes.AlreadyActive, Fixture.Refusal(() => runtime.Propose("brass", "aurel", "ceasefire", 20, 0)));
    }

    [Fact]
    public void APeaceEndsTheWarFearsTheWinnerAndShakesTheLoser()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Score(0, "aurel", 1000, "event");           // +400
        runtime.JoinWar(0, "dune", "brass");
        var fear = Fixture.Standing(runtime, "aurel", "fear");
        runtime.Propose("aurel", "brass", "peace", 60, 0);
        var changes = Fixture.Live(runtime, 2);
        Assert.Empty(runtime.Wars);
        Assert.Contains(changes, c => c.Kind == ChangeKind.War && c.Sub2 == (int)WarChange.Ended && c.Amount == 400);
        Assert.Equal(fear + 400 * 400 / 1000, Fixture.Standing(runtime, "aurel", "fear"));
        Assert.Equal(IncidentKind.WarEnded, (IncidentKind)runtime.Incidents.Last(i => i.Kind == (int)IncidentKind.WarEnded).Kind);
        Assert.Equal(550 + 200 + 100, runtime.Realms[0].Satisfaction[0]);           // court: war declared -50, war won +200, peace +100 (the drift is off)
        Assert.Equal(600 + 150, runtime.Realms[1].Satisfaction[0]);                  // merchants: peace +150
    }

    [Fact]
    public void ATiedWarEndsWithNoWinnerAndNoFear()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        var fear = (Fixture.Standing(runtime, "aurel", "fear"), Fixture.Standing(runtime, "brass", "fear"));
        runtime.Propose("aurel", "brass", "peace", 60, 0);
        Fixture.Live(runtime, 1);
        Assert.Empty(runtime.Wars);
        Assert.Equal(fear, (Fixture.Standing(runtime, "aurel", "fear"), Fixture.Standing(runtime, "brass", "fear")));
    }

    [Fact]
    public void ThePeacesTributeIsPaidByTheRealmThatAgreedToPayIt()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Propose("aurel", "brass", "peace", 60, 4000);        // brass pays aurel 4,000 each period
        Fixture.Live(runtime, 1);
        var treaty = Assert.Single(runtime.Treaties);
        Assert.Equal((0, 1, 4000L), (treaty.First, treaty.Second, treaty.Amount));
        Fixture.Live(runtime, 31);
        Assert.True(treaty.Awaiting);
    }

    [Fact]
    public void ASecondTruceTypeCannotPauseAWarThatIsAlreadyPaused()
    {
        var runtime = World(root =>
        {
            var armistice = Fixture.Copy(Fixture.Type(root, "ceasefire"));
            armistice["id"] = "armistice";
            armistice["key"] = "type.ceasefire";
            root["types"]!.AsArray().Add(armistice);
        });
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Propose("aurel", "brass", "ceasefire", 20, 0);
        Fixture.Live(runtime, 1);
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.Propose("brass", "aurel", "armistice", 20, 0)));
    }

    [Fact]
    public void APeaceAlsoEndsTheCeasefireThatPausedTheWar()
    {
        var runtime = World();
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Propose("aurel", "brass", "ceasefire", 30, 0);
        Fixture.Live(runtime, 1);
        runtime.Propose("aurel", "brass", "peace", 60, 0);
        Fixture.Live(runtime, 2);
        Assert.Empty(runtime.Wars);
        Assert.Equal(["peace"], runtime.Treaties.Select(t => runtime.Set.Types[t.Type].Id).ToArray());
    }

    // ---- exhaustion -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABalancedWarTiresBothSidesAlikeAndALosingSideTiresFaster()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["exhaustPpm"] = 10000);
        runtime.DeclareWar("aurel", "brass", "");
        Fixture.Live(runtime, 4);
        Assert.Equal((50000L, 50000L), (runtime.Wars[0].AttackerExhaustion, runtime.Wars[0].DefenderExhaustion));
        runtime.Score(0, "aurel", 1000, "event");        // +400 for the attacker: the defender is losing by 400
        Fixture.Live(runtime, 5);
        Assert.Equal((60000L, 50000L + 10000 * 1400 / 1000), (runtime.Wars[0].AttackerExhaustion, runtime.Wars[0].DefenderExhaustion));
    }

    [Fact]
    public void AWinterTiresTwiceAsMuchAsTheDocumentSays()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["exhaustPpm"] = 1000);
        runtime.DeclareWar("aurel", "brass", "");
        Fixture.Live(runtime, 274);
        Assert.Equal(270 * 1000L + 5 * 2000, runtime.Wars[0].AttackerExhaustion);
    }

    [Fact]
    public void ExhaustionIsKeptWithinAMillion()
    {
        var runtime = World(root => Fixture.Rules(root, "war")["exhaustPpm"] = 1_000_000);
        runtime.DeclareWar("aurel", "brass", "");
        Fixture.Live(runtime, 5);
        Assert.Equal(1_000_000, runtime.Wars[0].AttackerExhaustion);
    }
}
