using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A4, A5 and A8: what each node does, one at a time, in a tree of its own.</summary>
public sealed class NodeTests
{
    private const long Budget = 1_000_000;

    private static string Act(string id, string what, string? extra = null) =>
        "{ \"id\": \"" + id + "\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"" + what + "\", \"amount\": 1 }" + (extra is null ? "" : ", " + extra) + " }";

    private static AiRuntime One(string root, int every = 1, ulong seed = 1)
    {
        var runtime = new AiRuntime(Fixture.LoadMini(root, every), seed);
        runtime.Add("a", "t", 0);
        return runtime;
    }

    /// <summary>The names of the intents each tick raised, as "tick:what" entries, for ticks 1 to <paramref name="last"/>; <paramref name="between"/> may change the world first.</summary>
    private static List<string> Play(AiRuntime runtime, int last, Action<int>? between = null)
    {
        var seen = new List<string>();
        for (var tick = 1; tick <= last; tick++)
        {
            between?.Invoke(tick);
            foreach (var intent in runtime.Step(tick, Budget).Intents) seen.Add(tick + ":" + (intent.Name == "order" ? intent.Arg("what").Text : intent.Name));
        }
        return seen;
    }

    // ---- the commander of the fixture reproduces the rule it was written from -----------------------------------------------------------------

    private static AiRuntime Commander(ulong seed = 1)
    {
        var runtime = new AiRuntime(Fixture.Load(), seed);
        runtime.Add("cmd", "commander", 0);
        foreach (var own in new[] { "a1", "a2", "a3" }) Fixture.Tell(runtime, "cmd", own, hostile: false);
        foreach (var foe in new[] { "b1", "b2" }) Fixture.Tell(runtime, "cmd", foe, hostile: true);
        return runtime;
    }

    [Fact]
    public void TheNthOwnUnitFiresAtTheNthFoeRoundTheEndOnTheBeatOfFifteenOnceTheHostSaysReady()
    {
        var runtime = Commander();
        runtime.SetKey("cmd", "ready", FactValue.From(true));
        var fired = new List<string>();
        for (var tick = 1; tick <= 30; tick++)
            foreach (var intent in runtime.Step(tick, Budget).Intents)
                fired.Add($"{tick} {intent.Arg("attacker").Text}>{intent.Arg("defender").Text}@{intent.Arg("metres").Integer}");
        Assert.Equal(["15 a1>b1@100", "15 a2>b2@100", "15 a3>b1@100", "30 a1>b1@100", "30 a2>b2@100", "30 a3>b1@100"], fired);
    }

    [Fact]
    public void ABeatThatFallsDueWhileTheHostIsNotReadyIsKeptUntilItIs()
    {
        var runtime = Commander();
        var fired = new List<string>();
        for (var tick = 1; tick <= 20; tick++)
        {
            if (tick == 17) runtime.SetKey("cmd", "ready", FactValue.From(true));
            fired.AddRange(runtime.Step(tick, Budget).Intents.Select(i => tick + " " + i.Arg("attacker").Text));
        }
        // The beat at 15 passed while not ready; the order goes at 17, the first tick the host is ready, and not again until the next beat.
        Assert.Equal(["17 a1", "17 a2", "17 a3"], fired);
    }

    [Fact]
    public void OnlyStandingUnitsFireOnlyAtStandingFoesAndNobodyFiresWithoutAFoe()
    {
        var runtime = Commander();
        runtime.SetKey("cmd", "ready", FactValue.From(true));
        Fixture.Tell(runtime, "cmd", "a2", hostile: false, standing: false);   // a2 is down
        Fixture.Tell(runtime, "cmd", "b1", hostile: true, standing: false);    // b1 is down
        var fired = new List<string>();
        for (var tick = 1; tick <= 15; tick++) fired.AddRange(runtime.Step(tick, Budget).Intents.Select(i => i.Arg("attacker").Text + ">" + i.Arg("defender").Text));
        Assert.Equal(["a1>b2", "a3>b2"], fired);
        Fixture.Tell(runtime, "cmd", "b2", hostile: true, standing: false, tick: 15);
        var more = new List<string>();
        for (var tick = 16; tick <= 30; tick++) more.AddRange(runtime.Step(tick, Budget).Intents.Select(i => i.Name));
        Assert.Empty(more);
    }

    // ---- utility ----------------------------------------------------------------------------------------------------------------------------

    private static string Worker(AiRuntime runtime, string agent, int hunger, int tick)
    {
        runtime.SetKey(agent, "hunger", FactValue.From((long)hunger));
        return runtime.Step(tick, Budget).Intents.Single().Arg("what").Text!;
    }

    [Fact]
    public void TheBestScoringOptionWinsAndTheRunningOneKeepsItsLeadByTheMomentum()
    {
        var runtime = new AiRuntime(Fixture.Load(), 1);
        runtime.Add("w", "worker", 0);
        var picks = new List<string>();
        var tick = 0;
        // eat scores 10 per hunger point and work 1000 minus that; at 40 work wins; at 52 eat is ahead by 40 but work holds 150 of momentum; at 70 eat leads by 250.
        foreach (var hunger in new[] { 40, 52, 70, 45 }) { tick += 10; picks.Add(Worker(runtime, "w", hunger, tick)); }
        Assert.Equal(["work", "work", "food", "food"], picks);   // at 45 work leads by 100 but eat is the running option with 150 momentum
    }

    [Fact]
    public void WithoutTheMomentumTheSameInputsFlipBackAndForth()
    {
        var runtime = new AiRuntime(Fixture.Load(Fixture.With("\"momentum\": 150", "\"momentum\": 0")), 1);
        runtime.Add("w", "worker", 0);
        var tick = 0;
        var picks = new[] { 40, 52, 70, 45 }.Select(h => Worker(runtime, "w", h, tick += 10)).ToList();
        Assert.Equal(["work", "food", "food", "work"], picks);
    }

    [Fact]
    public void AUtilityNodeWithNothingToChooseFailsSoTheNextBranchOfASelectorRuns()
    {
        var utility = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ { \"id\": \"only\", \"weight\": 1000, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("go", "go") + " } ] }";
        var runtime = One("{ \"id\": \"s\", \"kind\": \"selector\", \"resume\": false, \"children\": [ " + utility + ", " + Act("fallback", "fallback") + " ] }");
        Assert.Equal(["1:fallback"], Play(runtime, 1));                            // hunger 0: the only option scores 0, the node fails, the fallback runs
        runtime.SetKey("a", "hunger", FactValue.From(50L));
        Assert.Equal(["2:go"], Play(runtime, 2).Where(s => s.StartsWith("2:", StringComparison.Ordinal)));
    }

    [Fact]
    public void ASelectThatFindsNothingEmptiesItsKeyInsteadOfLeavingTheLastAnswer()
    {
        var root = "{ \"id\": \"pick\", \"kind\": \"select\", \"from\": \"unit\", \"into\": \"target\", \"pick\": { \"min\": \"health\" }, \"where\": " + Fixture.FoeFilter + " }";
        var runtime = One(root);
        Fixture.Tell(runtime, "a", "b1", hostile: true);
        runtime.Step(1, Budget);
        Assert.Equal(FactValue.From("b1"), runtime.Key("a", "target"));
        Fixture.Tell(runtime, "a", "b1", hostile: true, standing: false, tick: 1);
        runtime.Step(2, Budget);
        Assert.Equal(FactValue.From(""), runtime.Key("a", "target"));
    }

    [Fact]
    public void ATieGoesToTheLowerIdAndAnOptionThatScoresNothingIsNeverChosen()
    {
        var runtime = new AiRuntime(Fixture.Load(), 1);
        runtime.Add("w", "worker", 0);
        Assert.Equal("food", Worker(runtime, "w", 50, 10));   // 500 to 500: eat before work
        // At 0 hunger eat scores 0 and is out; at 100 work scores 0 and is out.
        var fresh = new AiRuntime(Fixture.Load(), 1);
        fresh.Add("w", "worker", 0);
        Assert.Equal("work", Worker(fresh, "w", 0, 10));
        var hungry = new AiRuntime(Fixture.Load(), 1);
        hungry.Add("w", "worker", 0);
        Assert.Equal("food", Worker(hungry, "w", 100, 10));
    }

    [Fact]
    public void ANodeWithNothingWorthDoingFails()
    {
        var root = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ { \"id\": \"only\", \"weight\": 1000, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("go", "go") + " } ] }";
        var runtime = One(root);
        Assert.Empty(runtime.Step(1, Budget).Intents);                       // hunger 0 scores 0
        runtime.SetKey("a", "hunger", FactValue.From(5L));
        Assert.Single(runtime.Step(2, Budget).Intents);
    }

    [Fact]
    public void SeveralConsiderationsAreMultipliedAndThenPartlyMadeUpForHavingSeveral()
    {
        string Contest(long constant)
        {
            // "a-const" scores exactly the constant (one consideration, linear over 0..1000); "b-comp" has two considerations of 500 and 555 permille.
            var root = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ "
                + "{ \"id\": \"a-const\", \"weight\": 1000, \"considerations\": [ { \"input\": " + constant + ", \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pa", "const") + " }, "
                + "{ \"id\": \"b-comp\", \"weight\": 1000, \"considerations\": [ { \"input\": { \"key\": \"hunger\" }, \"min\": 0, \"max\": 100, \"curve\": { \"kind\": \"linear\" } }, { \"input\": { \"key\": \"side\" }, \"min\": 0, \"max\": 9, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pb", "comp") + " } ] }";
            var runtime = One(root);
            runtime.SetKey("a", "hunger", FactValue.From(50L));
            runtime.SetKey("a", "side", FactValue.From(5L));
            return runtime.Step(1, Budget).Intents.Single().Arg("what").Text!;
        }
        // Two considerations of 500 and 555 permille. Each is made up by (1 - s) x (1 - 1/2) x s: 500 becomes 625, 555 becomes 678 (445 x 500 / 1000 = 222, x 555 / 1000 = 123). 625 x 678 = 423 (floor).
        Assert.Equal("comp", Contest(422));
        Assert.Equal("const", Contest(423));   // a tie goes to the lower id
        Assert.Equal("const", Contest(424));
    }

    [Theory]
    // The published values of the compensation factor for a consideration score of one half, 1 to 9 considerations (UIntel utility documentation, 0.500 0.391 0.296 0.223 0.168 0.126 0.095 0.071 0.053):
    // the integer pipeline agrees to within five permille (the floors lose a little at each step, more where the score is small).
    [InlineData(1, 500)] [InlineData(2, 391)] [InlineData(3, 296)] [InlineData(4, 223)] [InlineData(5, 168)] [InlineData(6, 126)] [InlineData(7, 95)] [InlineData(8, 71)] [InlineData(9, 53)]
    public void TheCompensationAgreesWithThePublishedValuesForAScoreOfOneHalf(int considerations, int published)
    {
        var parts = string.Join(", ", Enumerable.Repeat("{ \"input\": 500, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } }", considerations));
        var root = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ { \"id\": \"a-const\", \"weight\": 1000, \"considerations\": [ { \"input\": CONST, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pa", "const") + " }, "
            + "{ \"id\": \"b-comp\", \"weight\": 1000, \"considerations\": [ " + parts + " ], \"child\": " + Act("pb", "comp") + " } ] }";
        // Which side of the published value the integer score falls on, found by moving the constant: the score is within five permille of the published figure.
        string Winner(int constant) => One(root.Replace("CONST", constant.ToString())).Step(1, Budget).Intents.Single().Arg("what").Text!;
        Assert.Equal("comp", Winner(published - 5));    // the made-up score beats a constant five below the published value
        Assert.Equal("const", Winner(published + 5));   // and does not reach one five above it
    }

    [Fact]
    public void TheWeightScalesAnOptionScore()
    {
        string Winner(int weightOfFirst)
        {
            var root = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ "
                + "{ \"id\": \"a\", \"weight\": " + weightOfFirst + ", \"considerations\": [ { \"input\": 800, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pa", "first") + " }, "
                + "{ \"id\": \"b\", \"weight\": 1000, \"considerations\": [ { \"input\": 500, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pb", "second") + " } ] }";
            return One(root).Step(1, Budget).Intents.Single().Arg("what").Text!;
        }
        Assert.Equal("first", Winner(1000));    // 800 against 500
        Assert.Equal("second", Winner(600));    // 480 against 500
    }

    [Theory]
    [InlineData(CurveKind.Linear, 500, 0, 500)]
    [InlineData(CurveKind.Linear, 0, 0, 0)]
    [InlineData(CurveKind.Linear, 1000, 0, 1000)]
    [InlineData(CurveKind.Quadratic, 500, 0, 250)]
    [InlineData(CurveKind.Quadratic, 1000, 0, 1000)]
    [InlineData(CurveKind.Cubic, 500, 0, 125)]
    [InlineData(CurveKind.Cubic, 1000, 0, 1000)]
    [InlineData(CurveKind.Smooth, 0, 0, 0)]
    [InlineData(CurveKind.Smooth, 250, 0, 155)]
    [InlineData(CurveKind.Smooth, 500, 0, 500)]
    [InlineData(CurveKind.Smooth, 750, 0, 843)]
    [InlineData(CurveKind.Smooth, 1000, 0, 1000)]
    [InlineData(CurveKind.Step, 499, 500, 0)]
    [InlineData(CurveKind.Step, 500, 500, 1000)]
    public void TheCurvesAreIntegerShapesOfAnInputFromZeroToAThousand(CurveKind curve, long x, long at, long expected) =>
        Assert.Equal(expected, Consideration.Shape(curve, x, at));

    [Fact]
    public void AnInvertedCurveScoresAThousandMinusTheShape()
    {
        string Pick(string curve)
        {
            var root = "{ \"id\": \"u\", \"kind\": \"utility\", \"momentum\": 0, \"options\": [ "
                + "{ \"id\": \"a\", \"weight\": 1000, \"considerations\": [ { \"input\": 100, \"min\": 0, \"max\": 1000, \"curve\": " + curve + " } ], \"child\": " + Act("pa", "curve") + " }, "
                + "{ \"id\": \"b\", \"weight\": 1000, \"considerations\": [ { \"input\": 500, \"min\": 0, \"max\": 1000, \"curve\": { \"kind\": \"linear\" } } ], \"child\": " + Act("pb", "half") + " } ] }";
            return One(root).Step(1, Budget).Intents.Single().Arg("what").Text!;
        }
        Assert.Equal("half", Pick("{ \"kind\": \"linear\" }"));                       // 100 against 500
        Assert.Equal("curve", Pick("{ \"kind\": \"linear\", \"invert\": true }"));    // 900 against 500
        Assert.Equal("half", Pick("{ \"kind\": \"step\", \"at\": 200 }"));            // 0 against 500
        Assert.Equal("curve", Pick("{ \"kind\": \"step\", \"at\": 100, \"invert\": false }"));   // 1000 against 500
    }

    // ---- the other nodes --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnActWithoutUntilSucceedsAtOnceAndOneWithUntilWaitsAndRaisesItsIntentOnce()
    {
        var runtime = One(Act("go", "go", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }"));
        var seen = Play(runtime, 6, tick => { if (tick == 5) runtime.SetKey("a", "ready", FactValue.From(true)); });
        // Raised at 1, waits through 4, succeeds at 5 (the root succeeded, nothing raised), and at 6 the node is entered fresh.
        Assert.Equal(["1:go", "6:go"], seen);
    }

    [Fact]
    public void AWaitingActThatNeverFinishesFailsAfterItsTimeoutAndStartsAgain()
    {
        var runtime = One(Act("go", "go", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }, \"timeout\": 3"));
        Assert.Equal(["1:go", "5:go", "9:go"], Play(runtime, 10));
    }

    [Fact]
    public void FailWhenEndsTheWaitAsAFailure()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"selector\", \"resume\": false, \"children\": [ "
            + Act("go", "go", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }, \"failWhen\": { \"fact\": \"key.hunger\", \"op\": \"gte\", \"value\": 90 }")
            + ", " + Act("plan-b", "plan-b") + " ] }";
        var runtime = One(root);
        Assert.Equal(["1:go", "3:plan-b"], Play(runtime, 3, tick => { if (tick == 3) runtime.SetKey("a", "hunger", FactValue.From(95L)); }));
    }

    [Fact]
    public void ASequenceThatResumesSkipsWhatItAlreadyPassedAndOneThatDoesNotChecksItAgain()
    {
        string Tree(bool resume) => "{ \"id\": \"seq\", \"kind\": \"sequence\", \"resume\": " + (resume ? "true" : "false") + ", \"children\": [ "
            + "{ \"id\": \"calm\", \"kind\": \"condition\", \"when\": { \"fact\": \"key.hunger\", \"op\": \"lt\", \"value\": 50 } }, "
            + Act("wait", "wait", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }") + ", " + Act("done", "done") + " ] }";
        List<string> Run(bool resume)
        {
            var runtime = One(Tree(resume));
            return Play(runtime, 8, tick =>
            {
                if (tick == 3) runtime.SetKey("a", "hunger", FactValue.From(80L));    // the condition would now fail
                if (tick == 5) runtime.SetKey("a", "ready", FactValue.From(true));
                if (tick == 7) runtime.SetKey("a", "hunger", FactValue.From(0L));
            });
        }
        // Resuming: the wait goes on through the hunger (the condition is not asked again) and finishes at 5 with "done"; at 6 the hunger stops a fresh pass, at 7 it is gone and the wait starts again.
        Assert.Equal(["1:wait", "5:done", "7:wait", "8:done"], Run(true));
        // Not resuming: at 3 the condition fails and the wait is forgotten; nothing comes at 5 though the host is ready; at 7 the wait is raised afresh and finishes at 8.
        Assert.Equal(["1:wait", "7:wait", "8:done"], Run(false));
    }

    [Fact]
    public void ACooldownBlocksItsChildForTheTicksAfterItFinished()
    {
        var root = "{ \"id\": \"nap\", \"kind\": \"cooldown\", \"ticks\": 3, \"child\": { \"id\": \"sleep\", \"kind\": \"act\", \"intent\": \"rest\", \"args\": { } } }";
        Assert.Equal(["1:rest", "4:rest", "7:rest", "10:rest"], Play(One(root), 10));   // it can run again 3 ticks after it ran
    }

    [Fact]
    public void InvertAndSucceedChangeTheAnswerButNotRunning()
    {
        var failsToSucceeds = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"inv\", \"kind\": \"invert\", \"child\": { \"id\": \"c\", \"kind\": \"condition\", \"when\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true } } }, " + Act("go", "go") + " ] }";
        var runtime = One(failsToSucceeds);
        Assert.Equal(["1:go"], Play(runtime, 1));                               // not ready, inverted: success
        runtime.SetKey("a", "ready", FactValue.From(true));
        Assert.Empty(Play(runtime, 1, _ => { }));
        var wrapped = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"ok\", \"kind\": \"succeed\", \"child\": { \"id\": \"c\", \"kind\": \"condition\", \"when\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true } } }, " + Act("go", "go") + " ] }";
        Assert.Equal(["1:go"], Play(One(wrapped), 1));                          // not ready, but succeeded anyway
        var waits = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"ok\", \"kind\": \"succeed\", \"child\": " + Act("w", "w", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }") + " }, " + Act("go", "go") + " ] }";
        Assert.Equal(["1:w"], Play(One(waits), 3));                              // a child still running is still running: "go" never comes
    }

    [Fact]
    public void ARandomNodeIsSeededKeepsAChoiceWhileItRunsAndIsIndependentOfWhatElseExists()
    {
        var root = "{ \"id\": \"dice\", \"kind\": \"random\", \"options\": [ { \"weight\": 1, \"child\": " + Act("h", "heads") + " }, { \"weight\": 3, \"child\": " + Act("t", "tails") + " } ] }";
        List<string> Roll(ulong seed, bool crowded)
        {
            var runtime = One(root, 1, seed);
            if (crowded) for (var i = 0; i < 20; i++) runtime.Add("extra" + i, "t", 0);
            var seen = new List<string>();
            for (var tick = 1; tick <= 40; tick++) seen.AddRange(runtime.Step(tick, Budget).Intents.Where(i => i.Agent == "a").Select(i => i.Arg("what").Text!));
            return seen;
        }
        var one = Roll(1, false);
        Assert.Equal(one, Roll(1, false));
        Assert.Equal(one, Roll(1, true));                        // other agents rolling changes nothing for this one
        Assert.NotEqual(one, Roll(2, false));
        var heads = one.Count(w => w == "heads");
        Assert.InRange(heads, 3, 20);                            // about a quarter of 40
        var waiting = "{ \"id\": \"dice\", \"kind\": \"random\", \"options\": [ { \"weight\": 1, \"child\": " + Act("h", "heads", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }") + " }, { \"weight\": 1, \"child\": " + Act("t", "tails", "\"until\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true }") + " } ] }";
        var kept = Play(One(waiting, 1, 5), 6);
        Assert.Single(kept);                                     // chosen once, kept while running, not re-rolled
    }

    // ---- queries over what the agent knows --------------------------------------------------------------------------------------------

    [Fact]
    public void SelectChoosesTheLowestOrHighestOrTheNthInIdOrder()
    {
        string Pick(string how)
        {
            var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
                + "{ \"id\": \"pick\", \"kind\": \"select\", \"from\": \"unit\", \"into\": \"target\", \"pick\": " + how + ", \"where\": " + Fixture.FoeFilter + " }, "
                + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": { \"key\": \"target\" }, \"amount\": 1 } } ] }";
            var runtime = One(root);
            Fixture.Tell(runtime, "a", "b3", hostile: true, health: 50);
            Fixture.Tell(runtime, "a", "b1", hostile: true, health: 80);
            Fixture.Tell(runtime, "a", "b2", hostile: true, health: 50);
            Fixture.Tell(runtime, "a", "a1", hostile: false, health: 1);
            Fixture.Tell(runtime, "a", "b4", hostile: true, standing: false, health: 1);
            var intents = runtime.Step(1, Budget).Intents;
            return intents.Count == 0 ? "-" : intents[0].Arg("what").Text!;
        }
        Assert.Equal("b2", Pick("{ \"min\": \"health\" }"));    // b2 and b3 tie at 50: the lower id
        Assert.Equal("b1", Pick("{ \"max\": \"health\" }"));
        Assert.Equal("b1", Pick("{ \"nth\": 0 }"));
        Assert.Equal("b3", Pick("{ \"nth\": 2 }"));
        Assert.Equal("-", Pick("{ \"nth\": 3 }"));              // only three match: no such place, the node fails and the key is empty
    }

    [Fact]
    public void ACountTakesOnlyWhatMatchesAndAnOwnerFilterTakesOnlyWhatThatOwnerHolds()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
            + "{ \"id\": \"c\", \"kind\": \"count\", \"from\": \"stock\", \"into\": \"foes\", \"of\": \"depot-a\", \"where\": { \"fact\": \"item.held\", \"op\": \"gt\", \"value\": 0 } }, "
            + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"stocks\", \"amount\": { \"key\": \"foes\" } } } ] }";
        var runtime = One(root);
        foreach (var (id, owner, held) in new[] { ("s1", "depot-a", 5L), ("s2", "depot-a", 0L), ("s3", "depot-a", 9L), ("s4", "depot-b", 7L) })
            runtime.Perceive("a", "stock", id, owner, new Dictionary<string, FactValue> { ["held"] = FactValue.From(held) }, 0);
        Assert.Equal(2, runtime.Step(1, Budget).Intents.Single().Arg("amount").Integer);
    }

    [Fact]
    public void ForEachRunsItsBodyForTheFirstLimitMatchesInIdOrderWithTheirPlace()
    {
        var root = "{ \"id\": \"each\", \"kind\": \"forEach\", \"from\": \"unit\", \"limit\": 2, \"where\": " + Fixture.OwnFilter + ", \"child\": "
            + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": { \"item\": \"id\" }, \"amount\": { \"item\": \"index\" } } } }";
        var runtime = One(root);
        foreach (var id in new[] { "u3", "u1", "u2", "u4" }) Fixture.Tell(runtime, "a", id, hostile: false);
        Fixture.Tell(runtime, "a", "u0", hostile: true);
        var intents = runtime.Step(1, Budget).Intents;
        Assert.Equal(["u1:0", "u2:1"], intents.Select(i => i.Arg("what").Text + ":" + i.Arg("amount").Integer));
    }

    [Fact]
    public void AdjustChangesWhatTheAgentBelievesAndKnownReadsItBackOrReadsTheDeclaredUnknown()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ "
            + "{ \"id\": \"before\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"before\", \"amount\": { \"known\": { \"from\": \"stock\", \"id\": \"s1\", \"attr\": \"held\" } } } }, "
            + "{ \"id\": \"spend\", \"kind\": \"adjust\", \"from\": \"stock\", \"target\": \"s1\", \"attr\": \"held\", \"by\": -7 }, "
            + "{ \"id\": \"after\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"after\", \"amount\": { \"known\": { \"from\": \"stock\", \"id\": \"s1\", \"attr\": \"held\" } } } }, "
            + "{ \"id\": \"unknown\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": \"unknown\", \"amount\": { \"known\": { \"from\": \"stock\", \"id\": \"nobody\", \"attr\": \"held\" } } } } ] }";
        var runtime = One(root);
        runtime.Perceive("a", "stock", "s1", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(10L) }, 0);
        Assert.Equal(["before:10", "after:3", "unknown:7"], runtime.Step(1, Budget).Intents.Select(i => i.Arg("what").Text + ":" + i.Arg("amount").Integer));
        // Another step spends again, saturating at the bottom of the range; the host word replaces the belief the moment it comes.
        Assert.Equal(["before:3", "after:0", "unknown:7"], runtime.Step(2, Budget).Intents.Select(i => i.Arg("what").Text + ":" + i.Arg("amount").Integer));
        runtime.Perceive("a", "stock", "s1", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(40L) }, 2);
        Assert.Equal("before:40", runtime.Step(3, Budget).Intents.Select(i => i.Arg("what").Text + ":" + i.Arg("amount").Integer).First());
    }

    [Fact]
    public void AdjustFailsOnSomethingTheAgentDoesNotKnow()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"spend\", \"kind\": \"adjust\", \"from\": \"stock\", \"target\": \"s9\", \"attr\": \"held\", \"by\": -1 }, " + Act("go", "go") + " ] }";
        Assert.Empty(One(root).Step(1, Budget).Intents);
    }

    [Fact]
    public void ALetWritesTheAgentOwnKeyAndCannotBeReadAsAnythingElse()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"set\", \"kind\": \"let\", \"key\": \"mode\", \"value\": { \"op\": \"concat\", \"args\": [ \"on-\", \"duty\" ] } }, "
            + "{ \"id\": \"go\", \"kind\": \"act\", \"intent\": \"order\", \"args\": { \"what\": { \"key\": \"mode\" }, \"amount\": { \"op\": \"mod\", \"args\": [ { \"op\": \"tick\" }, 1000 ] } } } ] }";
        var runtime = One(root);
        var intent = runtime.Step(5, Budget).Intents.Single();
        Assert.Equal("on-duty", intent.Arg("what").Text);
        Assert.Equal(5, intent.Arg("amount").Integer);
        Assert.Equal(FactValue.From("on-duty"), runtime.Key("a", "mode"));
    }

    [Fact]
    public void ConditionsReadTheTickAndTheKeysThroughTheSharedGrammar()
    {
        var root = "{ \"id\": \"s\", \"kind\": \"sequence\", \"resume\": false, \"children\": [ { \"id\": \"late\", \"kind\": \"condition\", \"when\": { \"all\": [ { \"fact\": \"tick\", \"op\": \"gte\", \"value\": 3 }, { \"not\": { \"fact\": \"key.ready\", \"op\": \"eq\", \"value\": true } } ] } }, " + Act("go", "go") + " ] }";
        Assert.Equal(["3:go", "4:go"], Play(One(root), 4));
    }
}