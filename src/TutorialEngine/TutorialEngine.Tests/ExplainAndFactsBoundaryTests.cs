using System.Text.Json.Nodes;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>More edges, written from the survivors of the third round of mutation checking: the ranges the engine declares for its own facts, what the explanations print in the shortest cases, and what the simulator's actions pass on.</summary>
public sealed class ExplainAndFactsBoundaryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tutorial-edge-" + Guid.NewGuid().ToString("N"));

    public ExplainAndFactsBoundaryTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void TheProgressFactCanBeComparedWithItsWholeRange()
    {
        Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("tutorial.progress", "gte", 1000));
        Assert.Equal(TutorialCodes.Format, Fixture.Refusal(() => Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("tutorial.progress", "gte", 1001))));
    }

    [Fact]
    public void ATipsShowsFactAcceptsTheLargestNumberOfShowsADocumentCanAskFor()
    {
        Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("tip.t1.shows", "lt", 1000));
        Assert.Equal(TutorialCodes.Format, Fixture.Refusal(() => Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("tip.t1.shows", "lt", 1001))));
    }

    [Fact]
    public void ASkillsPointsFactReachesItsOwnMasteryAndNoFurther()
    {
        Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("skill.sk.points", "gte", 3));
        Assert.Equal(TutorialCodes.Format, Fixture.Refusal(() => Fixture.Mini(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("skill.sk.points", "gte", 4))));
    }

    [Fact]
    public void ExplainingAStepWithOneAlternativeNamesIt()
    {
        var runtime = Fixture.Ready(Fixture.Mini(r =>
        {
            Tiny.Step(r, "s2")["requires"] = new JsonArray();
            Tiny.Step(r, "s2")["requiresAny"] = new JsonArray("s1");
        }));
        runtime.Advance(0, Fixture.Host());
        Assert.Contains("requires one of s1 (active)", TutorialExplain.Step(runtime, "s2", Fixture.Host()));
    }

    [Fact]
    public void ExplainingATipTheDismissedPlayerAcknowledgedSaysSo()
    {
        var runtime = Fixture.Ready();
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Contains("ok      the player has not acknowledged it", TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true)));
        runtime.DismissTip("t1");
        Assert.Contains("blocked the player acknowledged it", TutorialExplain.Tip(runtime, "t1", Fixture.Host(flag: true)));
    }

    [Fact]
    public void ExplainingTheJourneyNamesAnIntentOfOneCharacter()
    {
        var runtime = Fixture.Ready();
        Assert.DoesNotContain("intent", TutorialExplain.Journey(runtime));
        runtime.SetIntent("x");
        Assert.Contains(", intent x", TutorialExplain.Journey(runtime));
    }

    // ---- the simulator's actions pass on what the scenario says --------------------------------------------------------------------------

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out) Run(params string[] args)
    {
        var output = new StringWriter();
        var code = TutorialCli.Run(args, output, new StringWriter());
        return (code, output.ToString());
    }

    [Fact]
    public void AnObservationOfSeveralTimesInAScenarioCountsAllOfThem()
    {
        var scenario = Write("many.json", """{ "facts": { "input.gamepad": false, "ui.menu_open": false, "game.paused": false, "game.population": 0 }, "script": [ { "at": 0, "do": "language", "arg": "en" }, { "at": 1, "do": "skip-story" }, { "at": 2, "do": "entry", "arg": "host" }, { "at": 3, "do": "start" }, { "at": 5, "do": "observe", "arg": "moved", "n": 20 } ] }""");
        var (code, output) = Run("simulate", Sample("valley.tutorial.json"), scenario, "10");
        Assert.Equal(0, code);
        Assert.Contains("5 skill movement mastered", output);                 // twenty observations are the whole mastery of movement
    }

    [Fact]
    public void ASettingInAScenarioIsTheValueTheScenarioGaveNotAFixedOne()
    {
        var save = Path.Combine(_folder, "state.json");
        var scenario = Write("settings.json", """{ "facts": { "input.gamepad": false, "ui.menu_open": false, "game.paused": false, "game.population": 0 }, "script": [ { "at": 1, "do": "text-scale", "n": 2000 }, { "at": 2, "do": "reduced-motion", "n": 1 }, { "at": 3, "do": "reduced-motion", "n": 0 }, { "at": 4, "do": "tips", "n": 0 } ] }""");
        var (code, output) = Run("simulate", Sample("valley.tutorial.json"), scenario, "5", "--save", save);
        Assert.Equal(0, code);
        Assert.Contains("1 setting text_scale 2000", output);
        Assert.Contains("2 setting reduced_motion 1", output);
        Assert.Contains("3 setting reduced_motion 0", output);
        Assert.Contains("4 setting tips 0", output);
        var journey = Run("explain", Sample("valley.tutorial.json"), save, "journey");
        Assert.Contains("reduced motion off, text scale 2000, tips off", journey.Out);
    }

    [Fact]
    public void CoverageNamesTheMissingKeysAndTheProblemsOfATable()
    {
        var table = Write("fr.json", """{ "format": 1, "locale": "fr", "messages": { "tut.step.move": "Marchez {n}", "tut.stray": "x" } }""");
        var (code, output) = Run("coverage", Sample("valley.tutorial.json"), table);
        Assert.Equal(1, code);
        Assert.Contains("  missing tut.art.people", output);
        Assert.Contains("  unknown tut.stray", output);
        Assert.Contains("  problem tut.step.move: uses '{n}', which a name does not give", output);
    }
}
