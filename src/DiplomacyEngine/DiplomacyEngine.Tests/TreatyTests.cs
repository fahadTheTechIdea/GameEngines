using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>Proposals and how they are answered, treaties and how they end, tribute and how a missed payment becomes a breach.</summary>
public sealed class TreatyTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null) => Fixture.PlainWorld(root =>
    {
        Fixture.Realm(root, "cinder")["control"] = "ai";
        edit?.Invoke(root);
    });

    private static JsonObject Chance(JsonObject root, string type, int bias)
    {
        var def = Fixture.Type(root, type);
        def["bias"] = bias;
        def["weights"] = new JsonObject();
        def["modifiers"] = new JsonArray();
        return def;
    }

    // ---- proposals --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOfferToARealmTheEngineDecidesForIsAnsweredAfterTheTypesDaysOfConsideration()
    {
        var runtime = World(root => Chance(root, "nap", 5000));
        var made = runtime.Propose("aurel", "brass", "nap", 50, 0);
        Assert.Equal(ProposalChange.Made, (ProposalChange)made.Of(ChangeKind.Proposal).Single().Sub2);
        Assert.Empty(runtime.Treaties);
        Assert.Single(runtime.Proposals);
        Assert.Equal(2, runtime.Proposals[0].DecideAt);
        Assert.Empty(Fixture.Live(runtime, 1).Of(ChangeKind.Treaty));
        var decided = Fixture.Live(runtime, 2);
        var proposal = decided.Of(ChangeKind.Proposal).Single();
        Assert.Equal((int)ProposalChange.Accepted, proposal.Sub2);
        Assert.Equal(5000, proposal.Amount);
        Assert.Equal(2, proposal.Step);
        var treaty = Assert.Single(runtime.Treaties);
        Assert.Equal((Fixture.R(runtime.Set, "aurel"), Fixture.R(runtime.Set, "brass")), (treaty.First, treaty.Second));
        Assert.Empty(runtime.Proposals);
    }

    [Fact]
    public void ADecisionOfZeroDaysIsMadeWithinTheCall()
    {
        var runtime = World(root =>
        {
            Chance(root, "nap", 5000)["considerDays"] = 0;
        });
        var changes = runtime.Propose("aurel", "brass", "nap", 50, 0);
        Assert.Equal([ProposalChange.Made, ProposalChange.Accepted], changes.Of(ChangeKind.Proposal).Select(c => (ProposalChange)c.Sub2).ToArray());
        Assert.Single(runtime.Treaties);
        Assert.Empty(runtime.Proposals);
    }

    [Fact]
    public void ARefusedOfferMakesNoTreatyAndSaysTheScore()
    {
        var runtime = World(root => Chance(root, "nap", -5000));
        runtime.Propose("aurel", "brass", "nap", 50, 0);
        var proposal = Fixture.Live(runtime, 3).Of(ChangeKind.Proposal).Single();
        Assert.Equal((int)ProposalChange.Rejected, proposal.Sub2);
        Assert.Equal(-5000, proposal.Amount);
        Assert.Empty(runtime.Treaties);
        Assert.Empty(runtime.Proposals);
    }

    [Fact]
    public void AScoreInTheBandIsDecidedByTheProposalsOwnSeededRollSoTheSameSeedGivesTheSameAnswer()
    {
        bool Accepted(ulong seed)
        {
            var set = Fixture.Plain(root =>
            {
                Fixture.Realm(root, "cinder")["control"] = "ai";
                Chance(root, "nap", -100)["considerDays"] = 0;   // a score of -100 is a chance of half
                root["seed"] = (long)seed;
            });
            var runtime = DipRuntime.Create(set);
            runtime.Propose("aurel", "brass", "nap", 50, 0);
            return runtime.Treaties.Count == 1;
        }
        var results = Enumerable.Range(1, 400).Select(s => Accepted((ulong)s)).ToList();
        var yes = results.Count(r => r);
        Assert.InRange(yes, 150, 250);
        Assert.Equal(results, Enumerable.Range(1, 400).Select(s => Accepted((ulong)s)).ToList());
    }

    [Fact]
    public void ARealmTheHostDecidesForWaitsForAResponseUntilTheOfferExpires()
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "cinder", "nap", 50, 0);
        Assert.Equal(-1, runtime.Proposals[0].DecideAt);
        Assert.Equal(10, runtime.Proposals[0].ExpireAt);
        Assert.Empty(Fixture.Live(runtime, 9).Of(ChangeKind.Proposal));
        var expired = Fixture.Live(runtime, 10).Of(ChangeKind.Proposal).Single();
        Assert.Equal((int)ProposalChange.Expired, expired.Sub2);
        Assert.Empty(runtime.Proposals);
    }

    [Fact]
    public void TheHostsResponseSignsOrRefusesAndOnlyForARealmItDecidesFor()
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "cinder", "nap", 50, 0);
        runtime.Propose("brass", "cinder", "trade", 0, 0);
        runtime.Propose("aurel", "brass", "trade", 0, 0);
        Assert.Equal(DipCodes.NotHost, Fixture.Refusal(() => runtime.Respond(2, true)));
        var yes = runtime.Respond(0, true);
        Assert.Equal([ProposalChange.Accepted], yes.Of(ChangeKind.Proposal).Select(c => (ProposalChange)c.Sub2).ToArray());
        Assert.Single(runtime.Treaties);
        var no = runtime.Respond(1, false);
        Assert.Equal([ProposalChange.Rejected], no.Of(ChangeKind.Proposal).Select(c => (ProposalChange)c.Sub2).ToArray());
        Assert.Single(runtime.Treaties);
        Assert.Equal(DipCodes.UnknownProposal, Fixture.Refusal(() => runtime.Respond(0, true)));
    }

    [Fact]
    public void OnlyTheProposerWithdrawsAnOffer()
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "cinder", "nap", 50, 0);
        Assert.Equal(DipCodes.NotParty, Fixture.Refusal(() => runtime.Withdraw(0, "cinder")));
        var changes = runtime.Withdraw(0, "aurel");
        Assert.Equal((int)ProposalChange.Withdrawn, changes.Of(ChangeKind.Proposal).Single().Sub2);
        Assert.Empty(runtime.Proposals);
        Assert.Equal(DipCodes.UnknownProposal, Fixture.Refusal(() => runtime.Withdraw(0, "aurel")));
    }

    [Fact]
    public void TheSameOfferCannotWaitTwiceNorTheSameTreatyBeMadeTwice()
    {
        var runtime = World(root => Chance(root, "nap", 5000)["considerDays"] = 0);
        Fixture.Treaty(runtime, "aurel", "dune", "nap", 30);
        Assert.Equal(DipCodes.AlreadyActive, Fixture.Refusal(() => runtime.Propose("aurel", "dune", "nap", 30, 0)));
        Assert.Equal(DipCodes.AlreadyActive, Fixture.Refusal(() => runtime.Propose("dune", "aurel", "nap", 30, 0)));
        var host = Fixture.PlainWorld();
        host.Propose("aurel", "cinder", "trade", 0, 0);
        Assert.Equal(DipCodes.AlreadyActive, Fixture.Refusal(() => host.Propose("aurel", "cinder", "trade", 0, 0)));
    }

    [Fact]
    public void AnOfferThatBecameImpossibleWhileItWaitedIsAnsweredAsInvalid()
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "cinder", "trade", 0, 0);
        runtime.Propose("aurel", "cinder", "embargo", 30, 0);   // trade cannot stand beside an embargo
        var changes = runtime.Respond(0, true);
        Assert.Equal([ProposalChange.Invalid], changes.Of(ChangeKind.Proposal).Select(c => (ProposalChange)c.Sub2).ToArray());
        Assert.Equal(["embargo"], runtime.Treaties.Select(t => runtime.Set.Types[t.Type].Id).ToArray());
        Assert.Empty(runtime.Proposals);
    }

    [Fact]
    public void AnAiOfferThatBecameImpossibleIsInvalidWhenItsTurnComes()
    {
        var runtime = World(root => Chance(root, "trade", 5000));
        runtime.Propose("aurel", "brass", "trade", 0, 0);
        runtime.Propose("aurel", "brass", "embargo", 30, 0);
        var changes = Fixture.Live(runtime, 2).Of(ChangeKind.Proposal);
        Assert.Equal([(int)ProposalChange.Invalid], changes.Select(c => c.Sub2).ToArray());
        Assert.Single(runtime.Treaties);
    }

    // ---- terms and requirements -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("nap", 9)]
    [InlineData("nap", 401)]
    [InlineData("nap", 0)]
    [InlineData("nap", -5)]
    [InlineData("trade", 29)]
    [InlineData("trade", 721)]
    public void ATermOutsideTheTypesDaysIsRefused(string type, int days)
    {
        var runtime = Fixture.PlainWorld();
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Propose("aurel", "brass", type, days, 0)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Evaluate("aurel", "brass", type, days, 0)));
    }

    [Theory]
    [InlineData("nap", 10)]
    [InlineData("nap", 400)]
    [InlineData("trade", 30)]
    [InlineData("trade", 720)]
    [InlineData("trade", 0)]
    public void ATermAtTheEdgeOfTheTypesDaysIsAccepted(string type, int days)
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "brass", type, days, 0);
        Assert.Single(runtime.Proposals);
        Assert.Equal(days == 0 ? 0 : days, runtime.Proposals[0].Term);
    }

    [Fact]
    public void AnAmountIsOnlyForATributeAndATributeNeedsOneUnlessItIsOptional()
    {
        var runtime = Fixture.PlainWorld();
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 50, 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "vassalage", 0, 0)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "vassalage", 0, DipRuntime.MaxAmount + 1)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "vassalage", 0, -DipRuntime.MaxAmount - 1)));
        runtime.Propose("aurel", "brass", "vassalage", 0, DipRuntime.MaxAmount);
        runtime.Propose("aurel", "dune", "vassalage", 0, -DipRuntime.MaxAmount);
        Assert.Equal(2, runtime.Proposals.Count);
    }

    [Fact]
    public void ARequirementThatDoesNotHoldBlocksTheTreatyAndOneThatDoesLetsItThrough()
    {
        var runtime = World(root => Fixture.Type(root, "nap")["requires"] = Fixture.Cond("regard", "gt", 100));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 50, 0)));
        runtime.Remember("brass", "aurel", "gift", 1000);   // aurel now holds 300 for brass
        runtime.Propose("aurel", "brass", "nap", 50, 0);
        Assert.Single(runtime.Proposals);
    }

    [Fact]
    public void ARequirementThatReadsAHostFactNeedsTheHostToHaveGivenIt()
    {
        var runtime = World(root => Fixture.Type(root, "nap")["requires"] = Fixture.Cond("game.crisis", "lt", 5));
        Assert.Equal(DipCodes.FactMissing, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 50, 0)));
        Fixture.Live(runtime, 0, Fixture.Host(9));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 50, 0)));
        Fixture.Live(runtime, 1, Fixture.Host(1));
        runtime.Propose("aurel", "brass", "nap", 50, 0);
        Assert.Single(runtime.Proposals);
    }

    [Fact]
    public void ARealmInRevoltMakesNoTreatiesAndSomeoneElsesTreatyWithItIsNotBlocked()
    {
        var runtime = Fixture.PlainWorld(root => Fixture.Realm(root, "aurel")["stability"] = 100);
        Assert.Equal((int)StabilityBand.Revolt, runtime.Realms[0].Band);
        Assert.Equal(DipCodes.Revolt, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nap", 50, 0)));
        runtime.Propose("brass", "aurel", "nap", 50, 0);
        Assert.Single(runtime.Proposals);
    }

    [Fact]
    public void ARealmCannotMakeATreatyWithItselfOrWithAThingThatIsNotThere()
    {
        var runtime = Fixture.PlainWorld();
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.Propose("aurel", "aurel", "nap", 50, 0)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Propose("aurel", "nowhere", "nap", 50, 0)));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Propose("nowhere", "aurel", "nap", 50, 0)));
        Assert.Equal(DipCodes.UnknownType, Fixture.Refusal(() => runtime.Propose("aurel", "brass", "nothing", 50, 0)));
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.Evaluate("aurel", "aurel", "nap", 50, 0)));
    }

    [Fact]
    public void TheWorldHoldsAtMostTheDocumentsTreatiesAndProposals()
    {
        var runtime = World(root =>
        {
            Chance(root, "nap", 5000)["considerDays"] = 0;
            root["limits"]!["maxTreaties"] = 1;
            root["limits"]!["maxProposals"] = 1;
        });
        runtime.Propose("aurel", "cinder", "trade", 0, 0);
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.Propose("brass", "dune", "trade", 0, 0)));
        runtime.Withdraw(0, "aurel");
        runtime.Propose("aurel", "brass", "nap", 50, 0);
        Assert.Single(runtime.Treaties);
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.Propose("dune", "ember", "nap", 50, 0)));
    }

    // ---- imposed treaties and exclusions ---------------------------------------------------------------------------------------------

    [Fact]
    public void AnImposedTreatyIsMadeAtOnceWithItsEffectsForTheRealmItIsImposedOn()
    {
        var runtime = Fixture.PlainWorld();
        var changes = runtime.Propose("aurel", "brass", "embargo", 30, 0);
        var treaty = Assert.Single(runtime.Treaties);
        Assert.Equal(0, treaty.First);
        Assert.Equal(1, treaty.Second);
        Assert.Empty(runtime.Proposals);
        var effect = changes.Of(ChangeKind.Effect).Single();
        Assert.Equal((int)EffectKind.Shock, effect.Index);
        Assert.Equal("embargo", effect.Name);
        Assert.Equal(800, effect.Amount);
        Assert.Equal((1, 0), (effect.A, effect.B));
        Assert.Equal("treaty:embargo", effect.Source);
        Assert.Equal(IncidentKind.Embargo, (IncidentKind)changes.Of(ChangeKind.Incident).Single().Sub);
    }

    [Fact]
    public void AnEmbargoExcludesATradeTreatyBothWays()
    {
        var runtime = Fixture.PlainWorld();
        Fixture.Treaty(runtime, "aurel", "cinder", "trade");
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => runtime.Propose("aurel", "cinder", "embargo", 30, 0)));
        var other = Fixture.PlainWorld();
        other.Propose("aurel", "brass", "embargo", 30, 0);
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => other.Propose("aurel", "brass", "trade", 0, 0)));
        Assert.Equal(DipCodes.Blocked, Fixture.Refusal(() => other.Propose("brass", "aurel", "trade", 0, 0)));
    }

    [Fact]
    public void OnlyTheRealmThatImposedATreatyEndsItAndItEndsFreely()
    {
        var runtime = Fixture.PlainWorld();
        runtime.Propose("aurel", "brass", "embargo", 30, 0);
        Assert.Equal(DipCodes.NotParty, Fixture.Refusal(() => runtime.Cancel(0, "brass")));
        Assert.Equal(DipCodes.NotParty, Fixture.Refusal(() => runtime.Cancel(0, "dune")));
        var trustBefore = Fixture.Standing(runtime, "aurel", "trust");
        var changes = runtime.Cancel(0, "aurel");
        Assert.Equal(TreatyChange.Cancelled, (TreatyChange)changes.Of(ChangeKind.Treaty).Single().Sub2);
        Assert.Equal("embargo-lifted", changes.Of(ChangeKind.Effect).Single().Name);
        Assert.Empty(runtime.Treaties);
        Assert.Equal(trustBefore, Fixture.Standing(runtime, "aurel", "trust"));
    }

    // ---- ending a treaty --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATreatyWithATermEndsWhenItIsOverAndBothTrustTheOtherForKeepingItsWord()
    {
        var runtime = World(root => Chance(root, "nap", 5000)["considerDays"] = 0);
        var trust = (Fixture.Standing(runtime, "aurel", "trust"), Fixture.Standing(runtime, "dune", "trust"));
        var treaty = Fixture.Treaty(runtime, "aurel", "dune", "nap", 10);
        Assert.Equal(10, treaty.End);
        Assert.Empty(Fixture.Live(runtime, 9).Of(ChangeKind.Treaty));
        var ended = Fixture.Live(runtime, 10).Of(ChangeKind.Treaty).Single();
        Assert.Equal(TreatyChange.Expired, (TreatyChange)ended.Sub2);
        Assert.Equal(10, ended.Step);
        Assert.Empty(runtime.Treaties);
        Assert.Equal(trust.Item1 + 20, Fixture.Standing(runtime, "aurel", "trust"));
        Assert.Equal(trust.Item2 + 20, Fixture.Standing(runtime, "dune", "trust"));
    }

    [Fact]
    public void ATreatyWithNoTermNeverExpires()
    {
        var runtime = World(root => Chance(root, "trade", 5000)["considerDays"] = 0);
        var treaty = Fixture.Treaty(runtime, "aurel", "dune", "trade");
        Assert.Equal(-1, treaty.End);
        Fixture.Live(runtime, 400);
        Assert.Single(runtime.Treaties);
    }

    [Fact]
    public void CancellingATreatyThatIsFreeToEndEndsItQuietlyOneThatBreaksTheWordIsABreachAndOneThatCannotBeEndedIsRefused()
    {
        var runtime = World(root =>
        {
            foreach (var id in new[] { "trade", "nap", "vassalage" }) Chance(root, id, 5000)["considerDays"] = 0;
        });
        var trade = Fixture.Treaty(runtime, "aurel", "dune", "trade");
        var nap = Fixture.Treaty(runtime, "aurel", "brass", "nap", 100);
        var vassal = Fixture.Treaty(runtime, "brass", "ember", "vassalage", 0, 100);
        Assert.Equal(DipCodes.CannotCancel, Fixture.Refusal(() => runtime.Cancel(vassal.Id, "ember")));
        Assert.Equal(DipCodes.NotParty, Fixture.Refusal(() => runtime.Cancel(nap.Id, "dune")));
        Assert.Equal(DipCodes.UnknownTreaty, Fixture.Refusal(() => runtime.Cancel(99, "aurel")));
        var trust = Fixture.Standing(runtime, "dune", "trust");
        Assert.Equal(TreatyChange.Cancelled, (TreatyChange)runtime.Cancel(trade.Id, "dune").Of(ChangeKind.Treaty).Single().Sub2);
        Assert.Equal(trust, Fixture.Standing(runtime, "dune", "trust"));
        var honour = Fixture.Standing(runtime, "brass", "honour");
        var broken = runtime.Cancel(nap.Id, "brass");
        Assert.Equal(TreatyChange.Broken, (TreatyChange)broken.Of(ChangeKind.Treaty).Single().Sub2);
        Assert.Equal(honour - 100, Fixture.Standing(runtime, "brass", "honour"));
        Assert.Equal(["vassalage"], runtime.Treaties.Select(t => runtime.Set.Types[t.Type].Id).ToArray());
    }

    // ---- tribute ----------------------------------------------------------------------------------------------------------------------

    private static (DipRuntime Runtime, TreatyState Treaty) Vassal(Action<JsonObject>? edit = null, long amount = 5000)
    {
        var runtime = World(root =>
        {
            Chance(root, "vassalage", 5000)["considerDays"] = 0;
            edit?.Invoke(root);
        });
        return (runtime, Fixture.Treaty(runtime, "brass", "dune", "vassalage", 0, amount));
    }

    [Fact]
    public void TheRealmThatPaysIsTheSecondAndTheFirstIsPaid()
    {
        var (runtime, treaty) = Vassal();
        Assert.Equal((1, 3), (treaty.First, treaty.Second));
        Assert.Equal(5000, treaty.Amount);
        Assert.Equal(30, treaty.NextDue);
        var reversed = World(root => Chance(root, "vassalage", 5000)["considerDays"] = 0);
        var other = Fixture.Treaty(reversed, "brass", "dune", "vassalage", 0, -700);
        Assert.Equal((3, 1), (other.First, other.Second));
        Assert.Equal(700, other.Amount);
    }

    [Fact]
    public void APaymentFallsDueAsAnObligationAndAPaymentEffectAndWaitsForTheHost()
    {
        var (runtime, treaty) = Vassal();
        Assert.Empty(Fixture.Live(runtime, 29).Of(ChangeKind.Obligation));
        var due = Fixture.Live(runtime, 30);
        var obligation = due.Of(ChangeKind.Obligation).Single();
        Assert.Equal((ObligationChange.Due, ClauseKind.Tribute, 5000L), ((ObligationChange)obligation.Sub2, (ClauseKind)obligation.Sub, obligation.Amount));
        Assert.Equal((3, 1), (obligation.A, obligation.B));
        var payment = due.Of(ChangeKind.Effect).Single();
        Assert.Equal(((int)EffectKind.Payment, 3, 1, 5000L, "vassalage"), (payment.Index, payment.A, payment.B, payment.Amount, payment.Name));
        Assert.True(treaty.Awaiting);
        Assert.Equal(33, treaty.Deadline);
        Assert.Empty(Fixture.Live(runtime, 33).Of(ChangeKind.Obligation));      // the grace of 3 days is still running at its last day
    }

    [Fact]
    public void APaidPaymentRestoresTrustAndSchedulesTheNextOne()
    {
        var (runtime, treaty) = Vassal();
        Fixture.Live(runtime, 31);
        var trust = Fixture.Standing(runtime, "dune", "trust");
        var changes = runtime.Fulfil(treaty.Id, true);
        Assert.Equal(ObligationChange.Paid, (ObligationChange)changes.Of(ChangeKind.Obligation).Single().Sub2);
        Assert.False(treaty.Awaiting);
        Assert.Equal(61, treaty.NextDue);
        Assert.Equal(0, treaty.Missed);
        Assert.Equal(trust + 10, Fixture.Standing(runtime, "dune", "trust"));
        Assert.Equal(DipCodes.NotDue, Fixture.Refusal(() => runtime.Fulfil(treaty.Id, true)));
        var next = Fixture.Live(runtime, 61).Of(ChangeKind.Obligation);
        Assert.Equal([ObligationChange.Due], next.Select(c => (ObligationChange)c.Sub2).ToArray());
    }

    [Fact]
    public void AMissedPaymentIsASmallDeedAndLossOfTrustAndTheLimitIsABreach()
    {
        var (runtime, treaty) = Vassal();
        var trust = Fixture.Standing(runtime, "dune", "trust");
        var late = Fixture.Live(runtime, 34);
        Assert.Equal([ObligationChange.Due, ObligationChange.Missed], late.Of(ChangeKind.Obligation).Select(c => (ObligationChange)c.Sub2).ToArray());
        Assert.Equal(34, late.Of(ChangeKind.Obligation)[1].Step);
        Assert.Equal(1, treaty.Missed);
        Assert.Equal(trust - 125, Fixture.Standing(runtime, "dune", "trust"));             // 250 over a limit of two
        Assert.Equal(64, treaty.NextDue);
        Assert.Equal(-300 - 50, runtime.RegardOf("brass", "dune"));
        var final = Fixture.Live(runtime, 68);
        Assert.Contains(final, c => c.Kind == ChangeKind.Treaty && c.Sub2 == (int)TreatyChange.Broken);
        Assert.Empty(runtime.Treaties);
        Assert.Contains(runtime.Grants, g => g.Holder == 1 && g.Target == 3);
        Assert.Equal(IncidentKind.Breach, (IncidentKind)runtime.Incidents.Last().Kind);
    }

    [Fact]
    public void AHostReportOfAPaymentThatFailedCountsAtOnceAndAPaymentRepairsTheCount()
    {
        var (runtime, treaty) = Vassal();
        Fixture.Live(runtime, 30);
        var missed = runtime.Fulfil(treaty.Id, false);
        Assert.Equal(ObligationChange.Missed, (ObligationChange)missed.Of(ChangeKind.Obligation).Single().Sub2);
        Assert.Equal(1, treaty.Missed);
        Assert.False(treaty.Awaiting);
        Fixture.Live(runtime, 60);
        runtime.Fulfil(treaty.Id, true);
        Assert.Equal(0, treaty.Missed);
        Assert.Single(runtime.Treaties);
    }

    [Fact]
    public void ATributeDueByTheSeasonFallsDueWhenThatSeasonBegins()
    {
        var (runtime, treaty) = Vassal(root => Fixture.Type(root, "vassalage")["clauses"]![1]!["dueSeason"] = "winter");
        Assert.Equal(-1, treaty.NextDue);
        Assert.Empty(Fixture.Live(runtime, 269).Of(ChangeKind.Obligation));
        var due = Fixture.Live(runtime, 270).Of(ChangeKind.Obligation).Single();
        Assert.Equal((ObligationChange.Due, 270L), ((ObligationChange)due.Sub2, due.Step));
        runtime.Fulfil(treaty.Id, true);
        Assert.Equal(-1, treaty.NextDue);
        Assert.False(treaty.Awaiting);
    }

    [Fact]
    public void ATreatyThatIsNotATributeHasNothingToFulfil()
    {
        var runtime = World(root => Chance(root, "nap", 5000)["considerDays"] = 0);
        var nap = Fixture.Treaty(runtime, "aurel", "dune", "nap", 40);
        Assert.Equal(DipCodes.NotDue, Fixture.Refusal(() => runtime.Fulfil(nap.Id, true)));
        Assert.Equal(DipCodes.UnknownTreaty, Fixture.Refusal(() => runtime.Fulfil(77, true)));
    }

    // ---- what a signed treaty does ---------------------------------------------------------------------------------------------------

    [Fact]
    public void SigningIsRememberedByBothAndAnnouncedAsEffectsForEachParty()
    {
        var runtime = World(root => Chance(root, "nap", 5000)["considerDays"] = 0);
        var changes = runtime.Propose("aurel", "dune", "nap", 50, 0);
        var effects = changes.Of(ChangeKind.Effect);
        Assert.Equal(["treaty-signed", "treaty-signed"], effects.Select(e => e.Name).ToArray());
        Assert.Equal([(0, 3), (3, 0)], effects.Select(e => (e.A, e.B)).ToArray());
        Assert.Equal(runtime.Set.DeedById["good-faith"].Value + 50, runtime.RegardOf("aurel", "dune"));   // the deed, and the treaty's own regard
        Assert.Equal(runtime.Set.DeedById["good-faith"].Value + 50, runtime.RegardOf("dune", "aurel"));
    }

    [Fact]
    public void ATradeTreatyAsksTheEconomyForABoomForBothAndABustWhenItEnds()
    {
        var runtime = World(root => Chance(root, "trade", 5000)["considerDays"] = 0);
        var signed = runtime.Propose("aurel", "dune", "trade", 40, 0).Of(ChangeKind.Effect);
        Assert.Equal([("trade-boom", 500L, 0), ("trade-boom", 500L, 3)], signed.Select(e => (e.Name, e.Amount, e.A)).ToArray());
        var ended = Fixture.Live(runtime, 40).Of(ChangeKind.Effect);
        Assert.Equal([("trade-bust", 300L, 0), ("trade-bust", 300L, 3)], ended.Select(e => (e.Name, e.Amount, e.A)).ToArray());
    }

    [Fact]
    public void AnAcceptedProposalIsKeptWithItsOfferedTermAndAmountAfterTheWait()
    {
        var runtime = World(root => Chance(root, "vassalage", 5000));
        runtime.Propose("aurel", "dune", "vassalage", 90, 1234);
        Assert.Equal((90L, 1234L), (runtime.Proposals[0].Term, runtime.Proposals[0].Amount));
        Fixture.Live(runtime, 2);
        var treaty = Assert.Single(runtime.Treaties);
        Assert.Equal((1234L, 90L + 0, 2L), (treaty.Amount, treaty.End - 2, treaty.Start));
    }

    [Fact]
    public void TheNamesOfTheClosedSetsRoundTrip()
    {
        foreach (var kind in Enum.GetValues<ClauseKind>()) Assert.Equal(kind, EnumNames.Parse<ClauseKind>(EnumNames.Name(kind)));
        Assert.Equal("non_aggression", EnumNames.Name(ClauseKind.NonAggression));
    }
}
