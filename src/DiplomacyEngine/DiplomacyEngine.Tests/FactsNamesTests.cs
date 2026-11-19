using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>What the engine says as facts, the names and words of a document, the text of a change, and the checks on the time and the host facts a step is given.</summary>
public sealed class FactsNamesTests
{
    // ---- time -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AStepGivenBackwardsOrTooFarOrWithAnUnknownSeasonIsRefusedAndTheWorldIsAsItWas()
    {
        var runtime = Fixture.PlainWorld();
        Fixture.Live(runtime, 10);
        var digest = runtime.StateDigest();
        Assert.Equal(DipCodes.StepOrder, Fixture.Refusal(() => runtime.Advance(5, runtime.CycleSeasonAt, Fixture.Host())));
        Assert.Equal(DipCodes.StepOrder, Fixture.Refusal(() => runtime.Advance(10 + DipRuntime.MaxAdvance + 1, runtime.CycleSeasonAt, Fixture.Host())));
        Assert.Equal(DipCodes.UnknownSeason, Fixture.Refusal(() => runtime.Advance(13, step => step == 12 ? "monsoon" : "spring", Fixture.Host())));
        Assert.Equal(digest, runtime.StateDigest());
        Assert.Equal(10, runtime.Step);
        Assert.Empty(runtime.Advance(10, runtime.CycleSeasonAt, Fixture.Host()).Items);
        runtime.Advance(10 + DipRuntime.MaxAdvance, runtime.CycleSeasonAt, Fixture.Host());
    }

    [Fact]
    public void TheHostFactsTheDocumentDeclaresMustBeGivenWithTheirTypeAndRange()
    {
        var runtime = Fixture.PlainWorld();
        var digest = runtime.StateDigest();
        Assert.Equal(DipCodes.FactMissing, Fixture.Refusal(() => runtime.Advance(0, runtime.CycleSeasonAt, new Dictionary<string, FactValue>())));
        Assert.Equal(DipCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(0, runtime.CycleSeasonAt, new Dictionary<string, FactValue> { ["game.crisis"] = FactValue.From("high") })));
        Assert.Equal(DipCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host(11))));
        Assert.Equal(DipCodes.FactInvalid, Fixture.Refusal(() => runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host(-1))));
        Assert.Equal(digest, runtime.StateDigest());
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host(10));
        runtime.Advance(1, runtime.CycleSeasonAt, Fixture.Host(0));
    }

    [Fact]
    public void TheDocumentsOwnYearIsItsSeasonsInOrderEachForAShareOfThreeHundredAndSixtyDays()
    {
        var runtime = Fixture.PlainWorld();
        Assert.Equal("spring", runtime.CycleSeasonAt(0));
        Assert.Equal("spring", runtime.CycleSeasonAt(89));
        Assert.Equal("summer", runtime.CycleSeasonAt(90));
        Assert.Equal("autumn", runtime.CycleSeasonAt(269));
        Assert.Equal("winter", runtime.CycleSeasonAt(270));
        Assert.Equal("winter", runtime.CycleSeasonAt(359));
        Assert.Equal("spring", runtime.CycleSeasonAt(360));
        var changes = Fixture.Live(runtime, 180).Of(ChangeKind.Season);
        Assert.Equal([(0L, -1, 0), (90L, 0, 1), (180L, 1, 2)], changes.Select(c => (c.Step, c.Sub, c.Index)).ToArray());
    }

    [Fact]
    public void ADayOfSeveralStepsLengthensASeasonAndTheTermsInDays()
    {
        var set = Fixture.Plain(root => root["time"]!["stepsPerDay"] = 4);
        var runtime = DipRuntime.Create(set);
        Assert.Equal("spring", runtime.CycleSeasonAt(359));
        Assert.Equal("summer", runtime.CycleSeasonAt(360));
        Assert.Equal(40, set.Steps(10));
        runtime.Propose("aurel", "cinder", "nap", 10, 0);
        Assert.Equal(40, runtime.Proposals[0].Term);
        Assert.Equal(40, runtime.Proposals[0].ExpireAt);          // ten days of four steps
    }

    // ---- facts ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARealmsFactsAreItsOrderItsStandingItsMightAndItsTies()
    {
        var runtime = Fixture.PlainWorld();
        var facts = runtime.Facts("ext.", "aurel");
        Assert.Equal(11, facts.Count + 1);
        Assert.Equal(700, facts["ext.diplomacy.aurel.stability"].Integer);
        Assert.Equal(800, facts["ext.diplomacy.aurel.legitimacy"].Integer);
        Assert.Equal((100L, 50L, 100L, 0L), (facts["ext.diplomacy.aurel.trust"].Integer, facts["ext.diplomacy.aurel.fear"].Integer, facts["ext.diplomacy.aurel.honour"].Integer, facts["ext.diplomacy.aurel.infamy"].Integer));
        Assert.Equal(500000, facts["ext.diplomacy.aurel.might"].Integer);
        Assert.Equal((0L, 0L), (facts["ext.diplomacy.aurel.wars"].Integer, facts["ext.diplomacy.aurel.treaties"].Integer));
        Assert.Equal("stable", facts["ext.diplomacy.aurel.band"].Text);
    }

    [Fact]
    public void APairsFactsAreTheRegardTheWarTheCoalitionTheKnowledgeAndEachTreaty()
    {
        var runtime = Fixture.PlainWorld(root => Fixture.Decided(root, "nap", true));
        runtime.Remember("brass", "aurel", "gift", 1000);
        Fixture.Treaty(runtime, "aurel", "brass", "nap", 100);
        var facts = runtime.Facts("", "aurel", "brass");
        Assert.Equal(runtime.RegardOf("aurel", "brass"), facts["diplomacy.aurel.brass.regard"].Integer);
        Assert.Equal(runtime.RegardOf("brass", "aurel"), facts["diplomacy.aurel.brass.reverse"].Integer);
        Assert.True(facts["diplomacy.aurel.brass.regard"].Integer > facts["diplomacy.aurel.brass.reverse"].Integer);
        Assert.Equal(555, facts["diplomacy.aurel.brass.ratio"].Integer);
        Assert.False(facts["diplomacy.aurel.brass.at_war"].Boolean);
        Assert.Equal(1, facts["diplomacy.aurel.brass.treaties"].Integer);
        Assert.False(facts["diplomacy.aurel.brass.coalition"].Boolean);
        Assert.Equal(0, facts["diplomacy.aurel.brass.intel"].Integer);
        Assert.True(facts["diplomacy.aurel.brass.treaty.nap"].Boolean);
        Assert.False(facts["diplomacy.aurel.brass.treaty.trade"].Boolean);
        Assert.Equal(1, facts["diplomacy.aurel.treaties"].Integer);
    }

    [Fact]
    public void FactsAreGivenForARealmThatExistsAndAPairOfTwoDifferentOnes()
    {
        var runtime = Fixture.PlainWorld();
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Facts("", "nobody")));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Facts("", "aurel", "nobody")));
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.Facts("", "aurel", "aurel")));
        Assert.Throws<ArgumentNullException>(() => runtime.Facts(null!, "aurel"));
    }

    [Fact]
    public void EveryFactTheEngineGivesIsOfferedWithTheKindAndRangeItKeeps()
    {
        var runtime = Fixture.IslesWorld();
        Fixture.Live(runtime, 40);
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Remember("aurel", "dune", "gift", 700);
        var offered = DipFacts.Offered(runtime.Set);
        Assert.Equal(5 * 10 + 20 * (7 + 8), offered.Count);
        foreach (var a in runtime.Set.Realms)
        {
            var facts = new List<IReadOnlyDictionary<string, FactValue>> { runtime.Facts("", a.Id) };
            facts.AddRange(runtime.Set.Realms.Where(b => b.Index != a.Index).Select(b => runtime.Facts("", a.Id, b.Id)));
            foreach (var (id, value) in facts.SelectMany(f => f))
            {
                var definition = offered[id];
                Assert.Equal(definition.Kind, value.Kind);
                if (value.Kind == FactKind.Integer) Assert.InRange(value.Integer, definition.Minimum!.Value, definition.Maximum!.Value);
            }
        }
    }

    [Fact]
    public void AnOfferedFactCarriesThePrefixItIsGiven()
    {
        var offered = DipFacts.Offered(Fixture.Isles, "ext.");
        Assert.All(offered.Keys, k => Assert.StartsWith("ext.diplomacy.", k));
        Assert.Contains("ext.diplomacy.aurel.brass.treaty.alliance", offered.Keys);
        Assert.Throws<ArgumentNullException>(() => DipFacts.Offered(null!));
    }

    [Theory]
    [InlineData("regard", true)]
    [InlineData("step", true)]
    [InlineData("first.stability", true)]
    [InlineData("second.might", true)]
    [InlineData("treaty.nap", true)]
    [InlineData("cb.border", true)]
    [InlineData("intel", true)]
    [InlineData("game.crisis", false)]
    [InlineData("regards", false)]
    [InlineData("firstly", false)]
    public void ANameTheEngineGivesItselfIsReserved(string id, bool reserved) => Assert.Equal(reserved, DipFacts.IsReserved(id));

    // ---- words ----------------------------------------------------------------------------------------------------------------------

    private static LocaleTable English() => LocaleTable.Load(Fixture.Text("isles.diplomacy.en.json"));

    [Fact]
    public void TheSampleTableCoversEveryKeyTheDocumentUses()
    {
        var keys = DipNames.RequiredKeys(Fixture.Isles);
        Assert.Equal(56, keys.Count);
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), keys);
        var report = DipNames.Coverage(Fixture.Isles, English());
        Assert.True(report.IsComplete);
        Assert.Equal((56, 100), (report.Translated, report.Percent));
    }

    [Fact]
    public void ATableThatLacksAKeyHasAnExtraMessageOrUsesAPlaceholderIsNotComplete()
    {
        var table = LocaleTable.Load("{\"format\":1,\"locale\":\"en\",\"messages\":{\"realm.aurel\":\"Aurelia {name}\",\"realm.nowhere\":\"Nowhere\"}}");
        var report = DipNames.Coverage(Fixture.Isles, table);
        Assert.False(report.IsComplete);
        Assert.Equal(55, report.Missing.Count);
        Assert.Equal(["realm.nowhere"], report.Unknown.ToArray());
        Assert.Single(report.Problems);
        Assert.Equal("realm.aurel", report.Problems[0].Key);
    }

    [Fact]
    public void NamesAreRenderedFromTheLocaleChain()
    {
        var chain = LocaleChain.Create("en", [English()], "en");
        var set = Fixture.Isles;
        Assert.Equal("Aurelia", DipNames.Realm(set, chain, 0));
        Assert.Equal("Dune Clans", DipNames.Realm(set, chain, 3));
        Assert.Equal("Spring", DipNames.Season(set, chain, 0));
        Assert.Equal("Nap", DipNames.Type(set, chain, set.TypeById["nap"].Index));
        Assert.Equal("Betrayed", DipNames.CasusBelli(set, chain, set.CbById["betrayed"].Index));
        Assert.Equal("Gift", DipNames.Deed(set, chain, set.DeedById["gift"].Index));
        Assert.Equal("Spy", DipNames.Mission(set, chain, set.MissionById["spy"].Index));
        Assert.Equal("Crown", DipNames.Ideology(set, chain, 0));
        Assert.Equal("Loyal", DipNames.Trait(set, chain, 0));
        Assert.Equal("Aurel court", DipNames.Faction(set, chain, 0, 0));
        Assert.Throws<ArgumentNullException>(() => DipNames.RequiredKeys(null!));
    }

    // ---- the text of a change -------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryKindOfChangeHasALineOfPlainText()
    {
        var runtime = Fixture.IslesWorld();
        var set = runtime.Set;
        var changes = new List<DipChange>();
        changes.AddRange(runtime.Propose("aurel", "dune", "nap", 120, 0).Items);
        changes.AddRange(runtime.Propose("cinder", "dune", "embargo", 30, 0).Items);
        changes.AddRange(runtime.StartMission("agent-brass-1", "aurel", "spy").Items);
        changes.AddRange(Fixture.Live(runtime, 7));
        changes.AddRange(runtime.GrantCasusBelli("aurel", "ember", "border", 50).Items);
        changes.AddRange(runtime.DeclareWar("aurel", "ember", "border").Items);
        changes.AddRange(runtime.JoinWar(0, "dune", "ember").Items);
        changes.AddRange(runtime.Recruit("dune", "agent-dune-2", 500).Items);
        changes.AddRange(Fixture.Live(runtime, 40));
        var kinds = changes.Select(c => c.Kind).Distinct().ToHashSet();
        foreach (var kind in new[] { ChangeKind.Season, ChangeKind.Proposal, ChangeKind.Treaty, ChangeKind.War, ChangeKind.Mission, ChangeKind.Effect, ChangeKind.Grant, ChangeKind.Agent, ChangeKind.Incident }) Assert.Contains(kind, kinds);
        foreach (var change in changes) Assert.False(string.IsNullOrWhiteSpace(DipChangeText.Line(set, change)));
        Assert.Equal("-1 proposal 0 nap aurel -> dune made", DipChangeText.Line(set, changes[0]));
        Assert.Equal("-1 treaty 0 embargo cinder / dune signed", DipChangeText.Line(set, changes.First(c => c.Kind == ChangeKind.Treaty)));
        Assert.Equal("-1 effect shock embargo for dune against cinder scale 800 from treaty:embargo", DipChangeText.Line(set, changes.First(c => c.Kind == ChangeKind.Effect)));
    }

    [Fact]
    public void TheTextOfEachKindNamesWhatItIsAbout()
    {
        var set = Fixture.Isles;
        string Line(DipChange change) => DipChangeText.Line(set, change);
        Assert.Equal("5 season spring -> summer", Line(new DipChange(ChangeKind.Season, 5, -1, -1, 1, 0, 0, 0, 0, "", "")));
        Assert.Equal("5 season - -> spring", Line(new DipChange(ChangeKind.Season, 5, -1, -1, 0, -1, 0, 0, 0, "", "")));
        Assert.Equal("6 obligation tribute of treaty 3 dune -> brass due 5000", Line(new DipChange(ChangeKind.Obligation, 6, 3, 1, 3, (int)ClauseKind.Tribute, (int)ObligationChange.Due, 5000, 0, "", "")));
        Assert.Equal("7 war 2 aurel vs brass declared cb rivalry score 0", Line(new DipChange(ChangeKind.War, 7, 0, 1, 2, 0, (int)WarChange.Declared, 0, 0, "", "")));
        Assert.Equal("7 war 2 aurel vs brass joined score 10 realm dune", Line(new DipChange(ChangeKind.War, 7, 0, 1, 2, -1, (int)WarChange.Joined, 10, 3, "", "")));
        Assert.Equal("8 coalition against aurel formed members 4", Line(new DipChange(ChangeKind.Coalition, 8, 0, -1, 0, 0, (int)CoalitionChange.Formed, 4, 0, "", "")));
        Assert.Equal("8 coalition against aurel joined dune members 5", Line(new DipChange(ChangeKind.Coalition, 8, 0, 3, 0, 0, (int)CoalitionChange.Joined, 5, 0, "", "")));
        Assert.Equal("9 mission 4 sabotage aurel -> ember failed_exposed", Line(new DipChange(ChangeKind.Mission, 9, 0, 4, 4, 1, (int)MissionChange.FailedExposed, 0, 0, "", "")));
        Assert.Equal("9 agent 2 of brass captured", Line(new DipChange(ChangeKind.Agent, 9, 1, -1, 2, 0, (int)AgentChange.Captured, 0, 0, "", "")));
        Assert.Equal("10 intel brass on aurel none -> report", Line(new DipChange(ChangeKind.Intel, 10, 1, 0, (int)IntelLevel.Report, (int)IntelLevel.None, 0, 0, 0, "", "")));
        Assert.Equal("10 politics ember revolt stability 150", Line(new DipChange(ChangeKind.Politics, 10, 4, -1, 0, 0, (int)PoliticsChange.Revolt, 150, 0, "", "")));
        Assert.Equal("11 incident breach aurel / brass 1 3", Line(new DipChange(ChangeKind.Incident, 11, 0, 1, 1, (int)IncidentKind.Breach, 0, 3, 0, "", "")));
        Assert.Equal("12 casus belli border aurel against ember granted", Line(new DipChange(ChangeKind.Grant, 12, 0, 4, 2, 0, (int)GrantChange.Granted, 0, 0, "", "")));
        Assert.Equal("13 effect signal peace-signed for aurel against dune from treaty:peace", Line(new DipChange(ChangeKind.Effect, 13, 0, 3, (int)EffectKind.Signal, 0, 0, 0, 0, "peace-signed", "treaty:peace")));
        Assert.Equal("13 effect signal realm-calm for aurel from politics", Line(new DipChange(ChangeKind.Effect, 13, 0, -1, (int)EffectKind.Signal, 0, 0, 0, 0, "realm-calm", "politics")));
        Assert.Equal("13 effect payment vassalage for dune against brass scale 900 from treaty:vassalage", Line(new DipChange(ChangeKind.Effect, 13, 3, 1, (int)EffectKind.Payment, 0, 0, 900, 0, "vassalage", "treaty:vassalage")));
        Assert.Equal("14 proposal 1 peace aurel -> brass accepted score 12", Line(new DipChange(ChangeKind.Proposal, 14, 0, 1, 1, set.TypeById["peace"].Index, (int)ProposalChange.Accepted, 12, 0, "", "")));
        Assert.Equal("14 proposal 1 peace aurel -> brass withdrawn", Line(new DipChange(ChangeKind.Proposal, 14, 0, 1, 1, set.TypeById["peace"].Index, (int)ProposalChange.Withdrawn, 12, 0, "", "")));
        Assert.Equal("15 treaty 2 vassalage brass / dune signed tribute 700", Line(new DipChange(ChangeKind.Treaty, 15, 1, 3, 2, set.TypeById["vassalage"].Index, (int)TreatyChange.Signed, 700, 0, "", "")));
    }
}
