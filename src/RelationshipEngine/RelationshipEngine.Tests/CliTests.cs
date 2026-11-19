using System.Text.Json.Nodes;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>The authoring tool (R18): every command, its output, and the exit code of every mistake.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rel-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = RelationshipCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    private string Saved(int days = 3)
    {
        var path = Path.Combine(_folder, "state.json");
        var (code, _, error) = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), days.ToString(), "--save", path);
        Assert.Equal((0, ""), (code, error));
        return path;
    }

    // ---- check ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACheckOfAGoodDocumentSaysSoWithWhatIsInIt()
    {
        var (code, output, error) = Run("check", Sample("village.relationship.json"));
        Assert.Equal((0, ""), (code, error));
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal($"ok village digest {Fixture.Shipped.Digest} seed 20261028 steps-per-day 24 axes 3 needs 4 sources 9 moodlets 5 memories 9 traits 5 ladders 2 bonds 2 gifts 3 interactions 7 host-facts 4 facts {Fixture.Shipped.Facts.Count} text-keys 48", lines[0]);
        Assert.Equal("limits actors 2000 edges-per-actor 24 memories-per-edge 8 moodlets 12", lines[1]);
        Assert.Equal("need hunger max 1000 decay-per-day 900 critical 200 rearm 350 rules 1", lines[2]);
        Assert.Contains("interaction give-gift cooldown 24 gift desire 1 accept 0 effects 3/0 witnesses 3", lines);
    }

    [Fact]
    public void ACheckOfABadDocumentNamesTheCodeAndTheFindingAndExitsWithOne()
    {
        var path = Write("bad.json", Edit.Doc(r => r["mood"]!["needWeights"]![0]!["need"] = "thirst"));
        var (code, output, _) = Run("check", path);
        Assert.Equal(1, code);
        Assert.StartsWith("refused REL_REFERENCE: mood.needWeights[0].need 'thirst' is not a declared need", output, StringComparison.Ordinal);
    }

    // ---- simulate ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASimulationPrintsEachDayTheScriptedInteractionsAndTheFinalStateAndEndsWithTheDigest()
    {
        var (code, output, error) = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "5");
        Assert.Equal((0, ""), (code, error));
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("day 1 (step 24): alice=", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.StartsWith("step 30: alice chat bob: accepted (score 100, noise 0, needs 50)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("step 36: bob give-gift alice: accepted", StringComparison.Ordinal));
        Assert.Equal(5, lines.Count(l => l.StartsWith("day ", StringComparison.Ordinal)));
        Assert.Contains("state at step 120: 4 actors, 7 edges, 2 rolls", lines);
        Assert.Matches("^digest [0-9A-F]{64}$", lines[^1]);
    }

    [Fact]
    public void TheSameSimulationPrintsTheSameThingTwice()
    {
        var one = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "8", "--verbose");
        var two = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "8", "--verbose");
        Assert.Equal(one, two);
        Assert.Contains("is critical at", one.Output, StringComparison.Ordinal);
        Assert.Contains(" mood ", one.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AScriptedInteractionThatIsRefusedIsPrintedWithItsCodeAndTheExitIsOne()
    {
        var scenario = JsonNode.Parse(File.ReadAllText(Sample("village.scenario.json")))!.AsObject();
        scenario["script"]!.AsArray().Add(new JsonObject { ["step"] = 31, ["interaction"] = "chat", ["actor"] = "alice", ["target"] = "bob" });   // within the cooldown of the chat at step 30
        var path = Write("scenario.json", scenario.ToJsonString());
        var (code, output, _) = Run("simulate", Sample("village.relationship.json"), path, "2");
        Assert.Equal(1, code);
        Assert.Contains("step 31: alice chat bob: refused REL_COOLDOWN:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulateSavesAStateThatOpensWithTheSameDigestAsThePrintedOne()
    {
        var path = Path.Combine(_folder, "state.json");
        var (_, output, _) = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "4", "--save", path);
        var runtime = RelationshipSave.Open(Fixture.Shipped, File.ReadAllBytes(path));
        Assert.EndsWith("digest " + runtime.StateDigest() + "\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioProblemsAndBadArgumentsExitWithTwo()
    {
        var doc = Sample("village.relationship.json");
        Assert.Equal(2, Run("simulate", doc, Write("a.json", "not json"), "3").Code);
        Assert.Equal(2, Run("simulate", doc, Write("b.json", "[]"), "3").Code);
        Assert.Equal(2, Run("simulate", doc, Write("c.json", "{ \"actors\": [ { \"id\": \"x\", \"traits\": [], \"routine\": [ \"idle\" ] } ] }"), "3").Code);
        Assert.Equal(2, Run("simulate", doc, Write("d.json", "{ \"actors\": 5 }"), "3").Code);
        Assert.Equal(2, Run("simulate", doc, Sample("village.scenario.json"), "0").Code);
        Assert.Equal(2, Run("simulate", doc, Sample("village.scenario.json"), "many").Code);
        Assert.Equal(2, Run("simulate", doc, Sample("village.scenario.json"), "3", "--nonsense").Code);
        Assert.Equal(2, Run("simulate", doc, Path.Combine(_folder, "missing.json"), "3").Code);
        var bad = JsonNode.Parse(File.ReadAllText(Sample("village.scenario.json")))!.AsObject();
        bad["facts"]!.AsObject()["weather.raining"] = new JsonObject();
        Assert.Equal(2, Run("simulate", doc, Write("e.json", bad.ToJsonString()), "3").Code);
        var noStep = JsonNode.Parse(File.ReadAllText(Sample("village.scenario.json")))!.AsObject();
        noStep["script"]!.AsArray()[0]!.AsObject().Remove("step");
        Assert.Equal(2, Run("simulate", doc, Write("f.json", noStep.ToJsonString()), "3").Code);
    }

    [Fact]
    public void ASimulationWithAScenarioActorRefusedByTheDocumentExitsWithOne()
    {
        var scenario = JsonNode.Parse(File.ReadAllText(Sample("village.scenario.json")))!.AsObject();
        scenario["actors"]!.AsArray()[0]!.AsObject()["id"] = "Bad Id";
        var (code, output, _) = Run("simulate", Sample("village.relationship.json"), Write("g.json", scenario.ToJsonString()), "2");
        Assert.Equal(1, code);
        Assert.StartsWith("refused REL_UNKNOWN_NAME", output, StringComparison.Ordinal);
    }

    // ---- explain ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ExplainMoodSaysWhyTheMoodIsWhatItIsAndTheTermsAddUp()
    {
        var (code, output, _) = Run("explain", Sample("village.relationship.json"), Saved(), "mood", "bob");
        Assert.Equal(0, code);
        Assert.StartsWith("mood of bob = ", output, StringComparison.Ordinal);
        Assert.Contains("traits grumpy: -60", output, StringComparison.Ordinal);
        Assert.Contains("need hunger: ", output, StringComparison.Ordinal);
        Assert.Contains("\n  sum ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainOpinionListsTheBaseAndEveryMemory()
    {
        var (code, output, _) = Run("explain", Sample("village.relationship.json"), Saved(), "opinion", "alice", "bob", "affinity");
        Assert.Equal(0, code);
        Assert.StartsWith("opinion of alice about bob on affinity = ", output, StringComparison.Ordinal);
        Assert.Contains("base affinity: ", output, StringComparison.Ordinal);
        Assert.Contains("memory was-gifted: ", output, StringComparison.Ordinal);
    }


    [Fact]
    public void ExplainCommunityStandingAndBalanceAnswerFromTheSavedState()
    {
        var saved = Saved(5);
        var (code, community, _) = Run("explain", Sample("village.relationship.json"), saved, "community", "alice");
        Assert.Equal(0, code);
        Assert.StartsWith("community of alice = ", community, StringComparison.Ordinal);
        var (standingCode, standing, _) = Run("explain", Sample("village.relationship.json"), saved, "standing", "alice");
        Assert.Equal(0, standingCode);
        Assert.StartsWith("standing of alice = ", standing, StringComparison.Ordinal);
        Assert.Contains("from the opinions now: ", standing, StringComparison.Ordinal);
        var (balanceCode, balance, _) = Run("explain", Sample("village.relationship.json"), saved, "balance", "alice", "bob");
        Assert.Equal(0, balanceCode);
        Assert.StartsWith("balance of alice about bob on affinity", balance, StringComparison.Ordinal);
        Assert.Equal(1, Run("explain", Sample("village.relationship.json"), saved, "standing", "nobody").Code);          // an unknown actor is a refusal
        Assert.Equal(2, Run("explain", Sample("village.relationship.json"), saved, "balance", "alice").Code);            // the pair is missing
    }

    [Fact]
    public void ACheckSaysWhatTheSocialBlockDoesAndASimulationReportsWhatItDerives()
    {
        var (_, check, _) = Run("check", Sample("village.relationship.json"));
        Assert.Contains("social communities on on affinity above 100 | standing on on trust above 0 alpha 150 iterations 12 | balance on on affinity like 200 dislike -200 strength 1000", check, StringComparison.Ordinal);
        var (code, output, error) = Run("simulate", Sample("village.relationship.json"), Sample("village.scenario.json"), "6", "--verbose");
        Assert.Equal((0, ""), (code, error));
        Assert.Matches(@" community [a-z]+ standing \d+", output);
        Assert.Contains("standing 1000 -> ", output, StringComparison.Ordinal);
    }
    [Fact]
    public void ExplainInteractionTakesHostFactsAndSaysWhatItNeeds()
    {
        var saved = Saved();
        var (code, output, _) = Run("explain", Sample("village.relationship.json"), saved, "interaction", "cara", "alice", "share-meal", "--fact", "cal.is_day=true");
        Assert.Equal(0, code);
        Assert.StartsWith("share-meal cara -> alice: ", output, StringComparison.Ordinal);
        Assert.Contains("requires cal.is_day eq true [true] = true", output, StringComparison.Ordinal);
        var night = Run("explain", Sample("village.relationship.json"), saved, "interaction", "cara", "alice", "share-meal", "--fact", "cal.is_day=false");
        Assert.Contains("not available (REL_PRECONDITION)", night.Output, StringComparison.Ordinal);
        Assert.Equal(1, Run("explain", Sample("village.relationship.json"), saved, "interaction", "cara", "alice", "share-meal").Code);   // the fact is missing
        Assert.Equal(2, Run("explain", Sample("village.relationship.json"), saved, "interaction", "cara", "alice", "share-meal", "--fact", "novalue").Code);
    }

    [Fact]
    public void ExplainRefusesWhatIsNotThereAndAStateOfAnotherDocument()
    {
        var saved = Saved();
        Assert.Equal(1, Run("explain", Sample("village.relationship.json"), saved, "mood", "zed").Code);
        Assert.Equal(1, Run("explain", Sample("village.relationship.json"), saved, "opinion", "alice", "bob", "charm").Code);
        var otherDocument = Write("other.json", Edit.Doc(r => r["seed"] = 3));
        var (code, output, _) = Run("explain", otherDocument, saved, "mood", "alice");
        Assert.Equal(1, code);
        Assert.StartsWith("refused REL_SAVE_DOCUMENT", output, StringComparison.Ordinal);
        Assert.Equal(2, Run("explain", Sample("village.relationship.json"), saved, "weather", "alice").Code);
        Assert.Equal(2, Run("explain", Sample("village.relationship.json"), Path.Combine(_folder, "none.json"), "mood", "alice").Code);
    }

    // ---- coverage -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void CoverageOfACompleteTableExitsWithZeroAndOfAPartialOneWithOneListingWhatIsMissing()
    {
        var (complete, output, _) = Run("coverage", Sample("village.relationship.json"), Sample("village.en.json"));
        Assert.Equal(0, complete);
        Assert.Equal("en: 48/48 (100%) missing 0 unknown 0 problems 0\n", output);
        var partial = Write("fr.json", "{ \"format\": 1, \"locale\": \"fr\", \"messages\": { \"rel.axis.affinity\": \"affinité {x}\", \"rel.extra\": \"x\" } }");
        var (code, report, _) = Run("coverage", Sample("village.relationship.json"), Sample("village.en.json"), partial);
        Assert.Equal(1, code);
        Assert.Contains("fr: 1/48 (2%) missing 47 unknown 1 problems 1", report, StringComparison.Ordinal);
        Assert.Contains("  missing rel.axis.trust", report, StringComparison.Ordinal);
        Assert.Contains("  unknown rel.extra", report, StringComparison.Ordinal);
        Assert.Contains("  problem rel.axis.affinity: uses '{x}', which a name does not give", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageOfABadTableOrABadDocumentIsARefusal()
    {
        Assert.Equal(1, Run("coverage", Sample("village.relationship.json"), Write("bad-table.json", "{ \"format\": 9 }")).Code);
        Assert.Equal(1, Run("coverage", Write("bad-doc.json", "{}"), Sample("village.en.json")).Code);
    }

    // ---- the command line -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATooShortOrUnknownCommandLineIsUsageAndExitsWithTwo()
    {
        foreach (var args in new[] { Array.Empty<string>(), ["check"], ["dance", "x"], ["check", "a", "b"], ["simulate", "a", "b"], ["coverage", "a"] })
        {
            var (code, _, error) = Run(args);
            Assert.Equal(2, code);
            Assert.StartsWith("usage: RelationshipEngine.Cli check", error, StringComparison.Ordinal);
        }
        Assert.Equal(2, Run("check", Path.Combine(_folder, "nothing.json")).Code);
    }
}
