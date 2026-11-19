using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Needs (R3): exact per-day decay and sources over any split of steps, trait and host-fact multipliers, clamps, critical and re-arm, bounded advance.</summary>
public sealed class NeedTests
{
    private static RelationshipRuntime One(params string[] traits)
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("a", traits);
        return runtime;
    }

    private static long Level(RelationshipRuntime runtime, string need, string actor = "a") => runtime.NeedLevel(runtime.Actor(actor), runtime.Set.NeedById[need].Index);

    [Fact]
    public void ANeedStartsAtItsInitialLevel()
    {
        var runtime = One();
        Assert.Equal(new long[] { 800, 900, 700, 600 }, Fixture.Village.Needs.Select(n => Level(runtime, n.Id)));
    }

    [Fact]
    public void EnergyLosesEightHundredADayEvenlyEveryStepAndTheFractionIsKeptNotRounded()
    {
        var runtime = One();
        runtime.Advance(12, Fixture.Spring);
        Assert.Equal(500, Level(runtime, "energy"));          // 900 - 800 x 12 / 24
        runtime.Advance(13, Fixture.Spring);
        Assert.Equal(466, Level(runtime, "energy"));          // 21600 - 9600 - 800 = 11200 / 24 = 466.67, shown as 466
        runtime.Advance(24, Fixture.Spring);
        Assert.Equal(100, Level(runtime, "energy"));          // 900 - 800
    }

    [Fact]
    public void HungerAfterOneStepIsTheFloorOfSevenHundredSixtyTwoAndAHalf()
    {
        var runtime = One();
        runtime.Advance(1, Fixture.Spring);
        Assert.Equal(762, Level(runtime, "hunger"));
        Assert.Equal(762, runtime.Actor("a").Need[0] / 24);
        Assert.Equal(18_300, runtime.Actor("a").Need[0]);
    }

    [Fact]
    public void TheResultDoesNotDependOnHowTheStepsAreSplitIntoCalls()
    {
        string Run(params long[] stops)
        {
            var runtime = Fixture.Runtime();
            runtime.AddActor("a", ["glutton"]);
            runtime.AddActor("b", ["sociable"]);
            runtime.SetActivity("a", "idle");
            foreach (var stop in stops) runtime.Advance(stop, Fixture.Host(season: stop > 60 ? "winter" : "spring"));
            return runtime.StateDigest();
        }
        // One call and many calls end in the same state, as long as the host facts at each step are the same.
        var oneCall = Run(60);
        Assert.Equal(oneCall, Run(1, 2, 3, 10, 11, 30, 31, 59, 60));
        Assert.Equal(oneCall, Run(30, 60));
        Assert.Equal(Run(100), Run(61, 62, 100));   // winter in both: every call here supplies winter, whatever the split
    }

    [Fact]
    public void ASourceAddsItsPerDayRateOnEachStepOfTheActivityAndTheDecayStillApplies()
    {
        var runtime = One();
        runtime.SetActivity("a", "sleep");
        runtime.Advance(1, Fixture.Spring);
        Assert.Equal(21_600 + 2400 - 800, runtime.Actor("a").Need[1]);
        runtime.SetActivity("a", "");
        runtime.Advance(2, Fixture.Spring);
        Assert.Equal(21_600 + 1600 - 800, runtime.Actor("a").Need[1]);
    }

    [Fact]
    public void SeveralSourcesOfOneActivityAddUpAndOtherActivitiesAreIgnored()
    {
        var runtime = One();
        runtime.Advance(12, Fixture.Spring);
        runtime.SetActivity("a", "celebrate");
        runtime.Advance(13, Fixture.Spring);
        var actor = runtime.Actor("a");
        Assert.Equal(700 * 24 - 600 * 12 + 9000 - 600, actor.Need[2]);
        Assert.Equal(600 * 24 - 500 * 12 + 12_000 - 500, actor.Need[3]);
        Assert.Equal(800 * 24 - 900 * 13, actor.Need[0]);   // celebrate does not feed hunger
    }

    [Fact]
    public void ALevelNeverGoesBelowZeroOrAboveItsMax()
    {
        var runtime = One();
        runtime.Advance(100, Fixture.Spring);
        Assert.Equal(0, Level(runtime, "hunger"));
        runtime.SetActivity("a", "eat");
        runtime.Advance(110, Fixture.Spring);
        Assert.Equal(1000, Level(runtime, "hunger"));
        Assert.Equal(24_000, runtime.Actor("a").Need[0]);
    }

    [Fact]
    public void ATraitScalesTheDecayOfOneNeedByItsPermilleAndNoOther()
    {
        var plain = One();
        var glutton = One("glutton");
        plain.Advance(5, Fixture.Spring);
        glutton.Advance(5, Fixture.Spring);
        Assert.Equal(19_200 - 5 * 900, plain.Actor("a").Need[0]);
        Assert.Equal(19_200 - 5 * 1260, glutton.Actor("a").Need[0]);          // 900 x 1400 / 1000
        Assert.Equal(plain.Actor("a").Need[1], glutton.Actor("a").Need[1]);
    }

    [Fact]
    public void ARuleOverHostFactsScalesTheDecayWhileItHoldsAndTraitsAndRulesMultiply()
    {
        var winter = One();
        winter.Advance(1, Fixture.Host(season: "winter"));
        Assert.Equal(19_200 - 1170, winter.Actor("a").Need[0]);               // 900 x 1300 / 1000
        var both = One("glutton");
        both.Advance(1, Fixture.Host(season: "winter"));
        Assert.Equal(19_200 - 1638, both.Actor("a").Need[0]);                  // 900 x 1400 x 1300 / 1,000,000
        var rain = One();
        rain.Advance(1, Fixture.Host(raining: true));
        Assert.Equal(600 * 24 - 750, rain.Actor("a").Need[3]);                 // fun 500 x 1500 / 1000
        Assert.Equal(19_200 - 900, rain.Actor("a").Need[0]);                   // rain does not touch hunger
    }

    [Fact]
    public void ANeedWithNoRulesDoesNotAskTheHostForAnyFact()
    {
        var text = Edit.Doc(r =>
        {
            Edit.Need(r, "hunger")["decayModifiers"] = new System.Text.Json.Nodes.JsonArray();
            Edit.Need(r, "fun")["decayModifiers"] = new System.Text.Json.Nodes.JsonArray();
        });
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.Advance(3, new Dictionary<string, FactValue>());
        Assert.Equal(3, runtime.Step);
    }

    [Fact]
    public void CriticalIsEnteredOnceAtOrBelowTheLineAndRearmedOnceAtOrAboveTheRearmLine()
    {
        var runtime = One();
        var entered = new List<(long Step, bool Entered)>();
        for (var step = 1; step <= 40; step++)
            foreach (var change in runtime.Advance(step, Fixture.Spring).Needs.Where(n => n.Need == 0)) entered.Add((runtime.Step, change.Entered));
        Assert.Equal([(16L, true)], entered);                                   // 19,200 - 900 x 16 = 4,800 = level 200 exactly: at or below the line
        Assert.True(runtime.Actor("a").Critical[0]);
        runtime.SetActivity("a", "eat");
        var back = runtime.Advance(runtime.Step + 1, Fixture.Spring).Needs;
        var rearmed = Assert.Single(back, n => n.Need == 0);
        Assert.False(rearmed.Entered);
        Assert.Equal(41, runtime.Step);
        Assert.False(runtime.Actor("a").Critical[0]);
    }

    [Fact]
    public void BetweenTheCriticalLineAndTheRearmLineTheFlagStaysWhereItWas()
    {
        var runtime = One();
        runtime.Advance(16, Fixture.Spring);                                    // critical at level 200
        runtime.AdjustNeed("a", "hunger", 100);                                 // level 300: above critical, below rearm (350)
        Assert.True(runtime.Actor("a").Critical[0]);
        runtime.AdjustNeed("a", "hunger", 50);                                  // level 350: at the rearm line
        Assert.False(runtime.Actor("a").Critical[0]);
        runtime.AdjustNeed("a", "hunger", -100);                                // level 250: above critical, so not critical again
        Assert.False(runtime.Actor("a").Critical[0]);
    }

    [Fact]
    public void AnAdvanceMovesAtMostFourHundredEightyStepsAndSaysHowManyAreOwed()
    {
        var runtime = One();
        runtime.Advance(1000, Fixture.Spring);
        Assert.Equal(RelationshipRuntime.MaxStepsPerAdvance, runtime.Step);
        Assert.Equal(520, runtime.Owed(1000));
        runtime.Advance(1000, Fixture.Spring);
        Assert.Equal(960, runtime.Step);
        runtime.Advance(1000, Fixture.Spring);
        Assert.Equal(1000, runtime.Step);
        Assert.Equal(0, runtime.Owed(1000));
        Assert.Equal(0, runtime.Owed(10));
    }

    [Fact]
    public void AnAdvanceToTheCurrentStepChangesNothing()
    {
        var runtime = One();
        runtime.Advance(5, Fixture.Spring);
        var digest = runtime.StateDigest();
        Assert.False(runtime.Advance(5, Fixture.Spring).Any);
        Assert.Equal(digest, runtime.StateDigest());
    }

    [Fact]
    public void AYearOfDaysWithTwoMealsAndNightsOfSleepMatchesAnIndependentDayByDayAccount()
    {
        // The account is kept in whole units of 1/24 of a need level, per hour of the day, the way a person would write it down.
        var runtime = One();
        long hunger = 800 * 24, energy = 900 * 24;
        for (var day = 0; day < 365; day++)
            for (var hour = 0; hour < 24; hour++)
            {
                var eating = hour is 7 or 12;
                var sleeping = hour < 7 || hour >= 22;
                runtime.SetActivity("a", eating ? "eat" : sleeping ? "sleep" : "");
                hunger = Math.Clamp(hunger - 900 + (eating ? 12_000 : 0), 0, 24_000);
                energy = Math.Clamp(energy - 800 + (sleeping ? 2400 : 0), 0, 24_000);
                runtime.Advance(day * 24L + hour + 1, Fixture.Spring);
                if (hour == 23)
                {
                    Assert.Equal(hunger, runtime.Actor("a").Need[0]);
                    Assert.Equal(energy, runtime.Actor("a").Need[1]);
                }
            }
        Assert.Equal(365 * 24, runtime.Step);
    }

    [Fact]
    public void TheUrgencyOfANeedFollowsItsCurveAtTheCurrentLevel()
    {
        var runtime = One();
        Assert.Equal(DecayMath.Urgency(Fixture.Village.Needs[0], 800), runtime.Urgency(runtime.Actor("a"), 0));
        Assert.Equal(100, runtime.Urgency(runtime.Actor("a"), 0));
        runtime.Advance(16, Fixture.Spring);
        Assert.Equal(800, runtime.Urgency(runtime.Actor("a"), 0));            // level 200
    }

    [Fact]
    public void ASetActivityMustBeAnIdOrEmpty()
    {
        var runtime = One();
        Assert.Equal(RelationshipCodes.UnknownName, Fixture.Refusal(() => runtime.SetActivity("a", "Not An Id")));
        Assert.Equal(RelationshipCodes.UnknownActor, Fixture.Refusal(() => runtime.SetActivity("zed", "eat")));
        runtime.SetActivity("a", "something-the-document-has-no-source-for");
        runtime.Advance(1, Fixture.Spring);
        Assert.Equal(19_200 - 900, runtime.Actor("a").Need[0]);
    }
}
