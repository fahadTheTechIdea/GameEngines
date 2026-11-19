using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Facts (the read interface of other engines), explanations, localisation (R16) and determinism (R10).</summary>
public sealed class FactsNamesTests
{
    private static RelationshipRuntime Played() => Tamper.Played();

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheDeclaredFactsCoverEveryEngineFactOfAnActorAndOfAPairWithTheirTypesAndRanges()
    {
        var facts = Fixture.Village.Facts;
        Assert.Equal(FactKind.Integer, facts["actor.mood"].Kind);
        Assert.Equal((-1000L, 1000L), (facts["actor.mood"].Minimum, facts["actor.mood"].Maximum));
        Assert.Equal(FactKind.String, facts["target.mood_state"].Kind);
        Assert.Equal(FactKind.Integer, facts["actor.need.hunger"].Kind);
        Assert.Equal(1000, facts["actor.need.hunger"].Maximum);
        Assert.Equal(FactKind.Boolean, facts["actor.trait.kind"].Kind);
        Assert.Equal(FactKind.Integer, facts["edge.trust"].Kind);
        Assert.Equal((0L, 1000L), (facts["reverse.attraction"].Minimum, facts["reverse.attraction"].Maximum));
        Assert.Equal(FactKind.String, facts["edge.state.friendship"].Kind);
        Assert.Equal(FactKind.Boolean, facts["reverse.bond.spouse"].Kind);
        Assert.Equal((0L, 8L), (facts["edge.memory.was-gifted"].Minimum, facts["edge.memory.was-gifted"].Maximum));
        Assert.Equal(FactKind.Boolean, facts["edge.known"].Kind);
        Assert.Equal(FactKind.String, facts["cal.season"].Kind);      // the host's
    }

    [Fact]
    public void EveryFactReadIsOfItsDeclaredKindAndInsideItsDeclaredRangeForRandomStates()
    {
        var seed = 5UL;
        int Next(int bound) { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (int)((seed >> 33) % (ulong)bound); }
        var checkedFacts = 0;
        for (var life = 0; life < 20; life++)
        {
            var runtime = Played();
            for (var call = 0; call < 40; call++)
            {
                var names = new[] { "alice", "bob", "cara", "dan" };
                var a = names[Next(4)];
                var b = names[(Next(3) + 1 + Array.IndexOf(names, a)) % 4];
                switch (Next(4))
                {
                    case 0: runtime.AdjustAxis(a, b, new[] { "affinity", "trust", "attraction" }[Next(3)], Next(2000) - 1000); break;
                    case 1: runtime.Remember(a, b, new[] { "was-gifted", "was-insulted", "saw-theft", "shared-feast" }[Next(4)], 1000); break;
                    case 2: runtime.Advance(runtime.Step + 1 + Next(40), Fixture.Spring); break;
                    case 3: runtime.AdjustNeed(a, new[] { "hunger", "energy", "social", "fun" }[Next(4)], Next(1600) - 800); break;
                }
                foreach (var (id, value) in RelationshipFacts.Read(runtime, a, b))
                {
                    var definition = runtime.Set.Facts[id];
                    Assert.Equal(definition.Kind, value.Kind);
                    if (definition.Kind == FactKind.Integer) Assert.InRange(value.Integer, definition.Minimum!.Value, definition.Maximum!.Value);
                    checkedFacts++;
                }
            }
        }
        Assert.True(checkedFacts > 20_000);
    }

    [Fact]
    public void ReadForAnActorAloneGivesOnlyItsOwnFactsAndForAPairAlsoTheTargetsAndTheEdges()
    {
        var runtime = Played();
        var alone = RelationshipFacts.Read(runtime, "alice");
        Assert.All(alone.Keys, k => Assert.StartsWith("actor.", k, StringComparison.Ordinal));
        var pair = RelationshipFacts.Read(runtime, "alice", "bob");
        var expected = runtime.Set.Facts.Keys.Where(RelationshipFacts.IsReserved).OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(expected, pair.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.DoesNotContain("cal.season", pair.Keys);
        Assert.Equal(alone["actor.mood"].Integer, pair["actor.mood"].Integer);
        Assert.Equal(runtime.Mood("bob"), pair["target.mood"].Integer);
    }

    [Fact]
    public void APrefixIsPutBeforeEveryIdSoADialogueDocumentCanDeclareThemAsExternalFacts()
    {
        var facts = RelationshipFacts.Read(Played(), "alice", "bob", "ext.");
        Assert.All(facts.Keys, k => Assert.StartsWith("ext.", k, StringComparison.Ordinal));
        Assert.Contains("ext.edge.affinity", facts.Keys);
    }

    [Fact]
    public void AFactValueMatchesWhatTheRuntimeAnswersDirectly()
    {
        var runtime = Played();
        var facts = RelationshipFacts.Read(runtime, "bob", "alice");
        Assert.Equal(runtime.Mood("bob"), facts["actor.mood"].Integer);
        Assert.Equal(runtime.MoodStateId("bob"), facts["actor.mood_state"].Text);
        Assert.Equal(runtime.Opinion("bob", "alice", 0), facts["edge.affinity"].Integer);
        Assert.Equal(runtime.Opinion("alice", "bob", 0), facts["reverse.affinity"].Integer);
        Assert.Equal(runtime.NeedLevel(runtime.Actor("bob"), 0), facts["actor.need.hunger"].Integer);
        Assert.True(facts["actor.trait.grumpy"].Boolean);
        Assert.False(facts["actor.trait.kind"].Boolean);
        Assert.Equal("eat", RelationshipFacts.Read(runtime, "alice")["actor.activity"].Text);
    }

    [Fact]
    public void AnEngineFactThatDoesNotExistIsRefusedAndHostFactsAreCheckedAgainstTheirDeclaration()
    {
        var runtime = Played();
        Assert.Equal(RelationshipCodes.FactMissing, Fixture.Refusal(() => runtime.Resolve("edge.nonsense", runtime.Actor("alice"), runtime.Actor("bob"), Fixture.Spring)));
        Assert.Equal(RelationshipCodes.FactMissing, Fixture.Refusal(() => runtime.Resolve("actor.need.thirst", runtime.Actor("alice"), runtime.Actor("bob"), Fixture.Spring)));
        Assert.Equal(RelationshipCodes.FactMissing, Fixture.Refusal(() => runtime.Resolve("undeclared.host", runtime.Actor("alice"), runtime.Actor("bob"), Fixture.Spring)));
        Assert.Equal("spring", runtime.Resolve("cal.season", runtime.Actor("alice"), runtime.Actor("bob"), Fixture.Spring).Text);
    }

    // ---- explanations -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheOpinionExplanationListsTheBaseAndEveryMemoryAndAddsUpToTheOpinion()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("a", []);
        runtime.AddActor("b", []);
        runtime.AdjustAxis("a", "b", "affinity", 50);
        runtime.Remember("a", "b", "was-gifted", 1000);
        runtime.Remember("a", "b", "was-insulted", 1000);
        runtime.Remember("a", "b", "saw-theft", 1000);          // trust only: not a term of affinity
        var explanation = RelationshipExplain.Opinion(runtime, "a", "b", "affinity");
        Assert.Equal(["base", "memory", "memory"], explanation.Terms.Select(t => t.Kind));
        Assert.Equal([50L, 120, -150], explanation.Terms.Select(t => t.Value));
        Assert.Equal(20, explanation.Sum);
        Assert.Equal(20, explanation.Value);
        Assert.Equal(runtime.Opinion("a", "b", 0), explanation.Value);
        Assert.Contains("was-gifted", explanation.Text(), StringComparison.Ordinal);
        Assert.Contains("friendship stranger", explanation.State, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpinionExplanationAddsUpForRandomEdgesAndSaysWhoToldTheStory()
    {
        var runtime = Played();
        foreach (var from in runtime.Actors.Keys)
            foreach (var to in runtime.Actors.Keys.Where(t => t != from))
                foreach (var axis in runtime.Set.Axes)
                {
                    var explanation = RelationshipExplain.Opinion(runtime, from, to, axis.Id);
                    Assert.Equal(runtime.Opinion(from, to, axis.Index), explanation.Value);
                    Assert.Equal(Math.Clamp(explanation.Terms.Sum(t => t.Value), axis.Min, axis.Max), explanation.Value);
                }
        var told = Fixture.Runtime();
        told.AddActor("t", []);
        told.AddActor("s", []);
        told.AddActor("h", []);
        told.Remember("t", "s", "was-gifted", 1000);
        told.AdjustAxis("t", "h", "trust", 500);
        for (var night = 1; night <= 3; night++) told.Advance(night * 24, Fixture.Spring);
        if (told.MemoryCount("h", "s", told.Set.MemoryById["heard-kindness"].Index) == 1)
            Assert.Contains("told by t", RelationshipExplain.Opinion(told, "h", "s", "affinity").Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheMoodExplanationNamesTheTraitsTheNeedsAndTheMoodletsWithTheirNumbers()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("a", ["kind", "sociable"]);
        runtime.Grant("a", "insulted", 1000);
        var text = RelationshipExplain.Mood(runtime, "a").Text();
        Assert.StartsWith("mood of a = ", text, StringComparison.Ordinal);
        Assert.Contains("traits kind+sociable: +60", text, StringComparison.Ordinal);
        Assert.Contains("need hunger: +240 (level 800 of 1000, neutral 500, weight 800)", text, StringComparison.Ordinal);
        Assert.Contains("moodlet insulted: -250 (age 0 of 96, scale 1000)", text, StringComparison.Ordinal);
        Assert.Contains("sum ", text, StringComparison.Ordinal);
    }

    // ---- localisation ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void RequiredKeysAreEveryNameOfTheDocumentEachOnceInOrdinalOrder()
    {
        var keys = RelationshipNames.RequiredKeys(Fixture.Village);
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains("rel.act.propose", keys);
        Assert.Contains("rel.state.close-friend", keys);
        Assert.Contains("rel.gift.pie", keys);
        Assert.Contains("rel.mood.elated", keys);
        Assert.Equal(48, keys.Count);
    }

    [Fact]
    public void TheEnglishTableCoversTheDocumentAndEveryNameRenders()
    {
        var table = LocaleTable.Load(Fixture.Text("village.en.json"));
        var report = RelationshipNames.Coverage(Fixture.Village, table);
        Assert.True(report.IsComplete, string.Join(", ", report.Missing.Concat(report.Unknown)));
        Assert.Equal(100, report.Percent);
        var chain = LocaleChain.Create("en", [table], "en");
        var set = Fixture.Village;
        Assert.Equal("affinity", RelationshipNames.Axis(set, chain, 0));
        Assert.Equal("hunger", RelationshipNames.Need(set, chain, 0));
        Assert.Equal("content", RelationshipNames.MoodState(set, chain, 3));
        Assert.Equal("Was insulted", RelationshipNames.Moodlet(set, chain, 2));
        Assert.Equal("gave me a gift", RelationshipNames.Memory(set, chain, 0));
        Assert.Equal("close friend", RelationshipNames.LadderStep(set, chain, 0, 5));
        Assert.Equal("spouse", RelationshipNames.Bond(set, chain, 0));
        Assert.Equal("propose", RelationshipNames.Interaction(set, chain, 5));
        Assert.Equal("a bunch of flowers", RelationshipNames.Gift(set, chain, 0));
    }

    [Fact]
    public void CoverageSaysWhatIsMissingWhatNoDocumentUsesAndWhatUsesAPlaceholderANameDoesNotGive()
    {
        var table = LocaleTable.Load("{ \"format\": 1, \"locale\": \"en\", \"messages\": { \"rel.axis.affinity\": \"affinity {who}\", \"rel.not.used\": \"x\" } }");
        var report = RelationshipNames.Coverage(Fixture.Village, table);
        Assert.False(report.IsComplete);
        Assert.Equal(47, report.Missing.Count);
        Assert.Equal(["rel.not.used"], report.Unknown);
        var problem = Assert.Single(report.Problems);
        Assert.Equal(("rel.axis.affinity", "uses '{who}', which a name does not give"), (problem.Key, problem.Why));
    }

    [Fact]
    public void AMissingKeyFallsBackDownTheChainAndIsRememberedAndAKeyNoLocaleHasIsRefused()
    {
        var english = LocaleTable.Load(Fixture.Text("village.en.json"));
        var partial = LocaleTable.Load("{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"rel.axis.affinity\": \"affinité\" } }");
        var chain = LocaleChain.Create("fr", [english, partial], "en");
        Assert.Equal("affinité", RelationshipNames.Axis(Fixture.Village, chain, 0));
        Assert.Equal("trust", RelationshipNames.Axis(Fixture.Village, chain, 1));
        Assert.Equal(1, chain.Fallbacks["fr:rel.axis.trust"]);
        var onlyFrench = LocaleChain.Create("fr", [partial], "fr");
        Assert.Equal(LocaleCodes.MissingKey, Fixture.Refusal(() => RelationshipNames.Axis(Fixture.Village, onlyFrench, 1)));
    }

    // ---- determinism --------------------------------------------------------------------------------------------------------------------------

    private static (string Digest, string Log) Script(RelationshipSet set)
    {
        var runtime = RelationshipRuntime.Create(set);
        var log = new System.Text.StringBuilder();
        foreach (var name in new[] { "p1", "p2", "p3", "p4", "p5" }) runtime.AddActor(name, name == "p1" ? ["kind"] : name == "p2" ? ["grumpy"] : []);
        void Note(RelationshipChanges c) => log.Append(runtime.Step).Append(':').Append(string.Join(',', c.Moods.Select(m => m.Actor + m.To))).Append('|').Append(string.Join(',', c.Needs.Select(n => n.Actor + n.Need + n.Entered))).Append('|')
            .Append(string.Join(',', c.States.Select(s => s.Actor + s.Target + s.To))).Append('|').Append(string.Join(',', c.Memories.Select(m => m.Holder + m.Subject + m.Memory + m.Origin))).Append('\n');
        for (var day = 0; day < 40; day++)
        {
            foreach (var (actor, activity) in new[] { ("p1", "eat"), ("p3", "sleep"), ("p4", "celebrate") }) runtime.SetActivity(actor, activity);
            Note(runtime.Advance(runtime.Step + 24, Fixture.Host(season: day % 8 < 4 ? "spring" : "winter")));
            var result = runtime.Perform(new InteractionRequest("share-meal", "p" + (day % 5 + 1), "p" + ((day + 2) % 5 + 1), [], ""), Fixture.Spring);
            log.Append(result.Accepted).Append(result.Score).Append(result.Noise).Append('\n');
            Note(result.Changes);
            if (day % 7 == 3) Note(runtime.Remember("p1", "p3", "was-gifted", 1000));
            if (day % 5 == 1) Note(runtime.Perform(new InteractionRequest("insult", "p2", "p4", [], ""), Fixture.Spring).Changes);
        }
        return (runtime.StateDigest(), log.ToString());
    }

    [Fact]
    public void TheSameDocumentSeedAndCallsGiveTheSameEventsAndTheSameDigestAndAnotherSeedDoesNot()
    {
        var one = Script(Fixture.Village);
        var two = Script(Fixture.Village);
        Assert.Equal(one.Digest, two.Digest);
        Assert.Equal(one.Log, two.Log);
        Assert.True(one.Log.Length > 1000, "the script should have produced a real log, not " + one.Log.Length + " characters");
        var other = Script(RelationshipLoader.Load(Edit.Doc(r => r["seed"] = 424242)));
        Assert.NotEqual(one.Digest, other.Digest);
    }
}
