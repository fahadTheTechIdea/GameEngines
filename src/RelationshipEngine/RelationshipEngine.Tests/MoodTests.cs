using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>Mood (R4): the value is the exact sum and the explanation adds up to it; the state changes only across the hysteresis margin; the four stacking modes; traits scale what happens to an actor.</summary>
public sealed class MoodTests
{
    private static RelationshipRuntime One(params string[] traits)
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("a", traits);
        runtime.AddActor("b", []);
        return runtime;
    }

    private static int Moodlets(RelationshipRuntime runtime, string moodlet) => runtime.Actor("a").Moodlets.Count(m => runtime.Set.Moodlets[m.Moodlet].Id == moodlet);

    [Fact]
    public void TheStartingMoodIsTheSumOfWhatTheNeedsContribute()
    {
        var runtime = One();
        // (800-500) x 800/1000 + (900-500) x 600/1000 + (700-500) x 400/1000 + (600-500) x 300/1000 = 240 + 240 + 80 + 30
        Assert.Equal(590, runtime.Mood("a"));
        Assert.Equal("happy", runtime.MoodStateId("a"));
    }

    [Fact]
    public void ATraitBiasesTheBaseAndTwoTraitsAdd()
    {
        Assert.Equal(630, One("kind").Mood("a"));
        Assert.Equal(530, One("grumpy").Mood("a"));
        Assert.Equal(590 + 40 + 20, One("kind", "sociable").Mood("a"));
        Assert.Equal(590, One("stoic", "unknown-to-the-document").Mood("a"));
    }

    [Fact]
    public void AMoodletAddsItsValueNowAndItFadesToNothingAtItsDuration()
    {
        var runtime = One();
        runtime.Grant("a", "insulted", 1000);
        Assert.Equal(590 - 250, runtime.Mood("a"));
        runtime.SetActivity("a", "eat");   // keep the needs from moving the mood while the moodlet fades
        for (var step = 1; step <= 100; step++)
        {
            runtime.Advance(step, Fixture.Spring);
            var explanation = RelationshipExplain.Mood(runtime, "a");
            var moodletTerm = explanation.Terms.FirstOrDefault(t => t.Kind == "moodlet");
            if (step < 96) Assert.Equal(-250L * (96 - step) / 96, moodletTerm!.Value);
            else Assert.Null(moodletTerm);
        }
        Assert.Empty(runtime.Actor("a").Moodlets);
    }

    [Fact]
    public void ATraitScalesAMoodletByItsTagAndOnlyThatTag()
    {
        var plain = One();
        var grumpy = One("grumpy");
        var stoic = One("stoic");
        foreach (var runtime in new[] { plain, grumpy, stoic }) { runtime.Grant("a", "insulted", 1000); runtime.Grant("a", "ate-well", 1000); }
        long Value(RelationshipRuntime runtime, string moodlet) => runtime.MoodletValue(runtime.Actor("a").Moodlets.Single(m => runtime.Set.Moodlets[m.Moodlet].Id == moodlet));
        Assert.Equal(-250, Value(plain, "insulted"));
        Assert.Equal(-375, Value(grumpy, "insulted"));     // 1500 permille
        Assert.Equal(-125, Value(stoic, "insulted"));      // 500 permille
        Assert.Equal(120, Value(grumpy, "ate-well"));      // another tag: untouched
    }

    [Fact]
    public void TheMoodIsClampedToItsRangeAndTheExplanationSaysSo()
    {
        var runtime = One();
        runtime.Grant("a", "festival-joy", 1000);
        runtime.Grant("a", "ate-well", 1000);
        for (var i = 0; i < 3; i++) { runtime.Advance(runtime.Step + 1, Fixture.Spring); runtime.Grant("a", "gift-received", 1000); }
        runtime.SetActivity("a", "eat");
        var explanation = RelationshipExplain.Mood(runtime, "a");
        Assert.True(explanation.Sum > 1000, "the sum should pass the top of the range, was " + explanation.Sum);
        Assert.Equal(1000, explanation.Value);
        Assert.Equal(1000, runtime.Mood("a"));
        Assert.Contains("clamped to 1000", explanation.Text(), StringComparison.Ordinal);
    }

    // ---- stacking ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void RefreshKeepsOneInstanceAndRestartsItsClock()
    {
        var runtime = One();
        runtime.Grant("a", "insulted", 1000);
        runtime.Advance(40, Fixture.Spring);
        runtime.Grant("a", "insulted", 1000);
        Assert.Equal(1, Moodlets(runtime, "insulted"));
        Assert.Equal(40, runtime.Actor("a").Moodlets[0].Start);
        Assert.Equal(-250, runtime.MoodletValue(runtime.Actor("a").Moodlets[0]));
    }

    [Fact]
    public void StackKeepsUpToItsMaxAndAFurtherOneReplacesTheOldest()
    {
        var runtime = One();
        for (var i = 1; i <= 3; i++) { runtime.Advance(i * 2, Fixture.Spring); runtime.Grant("a", "gift-received", 1000); }
        Assert.Equal(new long[] { 2, 4, 6 }, runtime.Actor("a").Moodlets.Select(m => m.Start));
        runtime.Advance(8, Fixture.Spring);
        runtime.Grant("a", "gift-received", 1000);
        Assert.Equal(3, Moodlets(runtime, "gift-received"));
        Assert.Equal(new long[] { 4, 6, 8 }, runtime.Actor("a").Moodlets.Select(m => m.Start).OrderBy(s => s));
    }

    [Fact]
    public void ReplaceKeepsTheStrongerOfOldAndNewAndIgnoresAWeakerOne()
    {
        var runtime = One();
        runtime.Grant("a", "festival-joy", 500);
        var weaker = runtime.Grant("a", "festival-joy", 300);
        Assert.Empty(weaker.Moodlets);
        Assert.Equal(500, runtime.Actor("a").Moodlets.Single().Scale);
        var stronger = runtime.Grant("a", "festival-joy", 900);
        Assert.Single(stronger.Moodlets);
        Assert.Equal(900, runtime.Actor("a").Moodlets.Single().Scale);
        var equal = runtime.Grant("a", "festival-joy", 900);
        Assert.Single(equal.Moodlets);   // an equal one is as strong, so it restarts the clock
    }

    [Fact]
    public void UniqueIgnoresANewOneWhileOneLastsAndTakesItAfterwards()
    {
        var runtime = One();
        runtime.Grant("a", "saw-a-theft", 1000);
        runtime.Advance(10, Fixture.Spring);
        Assert.Empty(runtime.Grant("a", "saw-a-theft", 1000).Moodlets);
        Assert.Equal(0, runtime.Actor("a").Moodlets.Single().Start);
        runtime.Advance(120, Fixture.Spring);
        Assert.Empty(runtime.Actor("a").Moodlets);
        Assert.Single(runtime.Grant("a", "saw-a-theft", 1000).Moodlets);
    }

    [Fact]
    public void MoreMoodletsThanTheLimitEvictTheWeakestNowAndSayWhich()
    {
        var text = Edit.Doc(r => r["limits"]!["maxMoodlets"] = 2);
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.Grant("a", "ate-well", 1000);        // +120
        runtime.Grant("a", "insulted", 1000);        // -250
        var changes = runtime.Grant("a", "festival-joy", 1000);   // +300 pushes out the weakest by size: ate-well
        Assert.Equal([new Evicted("moodlet", "a", "ate-well")], changes.Evictions);
        Assert.Equal(["insulted", "festival-joy"], runtime.Actor("a").Moodlets.Select(m => runtime.Set.Moodlets[m.Moodlet].Id));
    }

    [Fact]
    public void WhenTwoMoodletsAreEquallyStrongTheOlderGoesFirst()
    {
        var text = Edit.Doc(r =>
        {
            r["limits"]!["maxMoodlets"] = 2;
            Edit.Moodlet(r, "ate-well")["magnitude"] = 100;
            Edit.Moodlet(r, "ate-well")["duration"] = 500;
            Edit.Moodlet(r, "ate-well")["decay"] = System.Text.Json.Nodes.JsonNode.Parse("{\"kind\":\"fixed\"}");
            Edit.Moodlet(r, "insulted")["magnitude"] = -100;
            Edit.Moodlet(r, "insulted")["duration"] = 500;
            Edit.Moodlet(r, "insulted")["decay"] = System.Text.Json.Nodes.JsonNode.Parse("{\"kind\":\"fixed\"}");
        });
        var runtime = RelationshipRuntime.Create(RelationshipLoader.Load(text));
        runtime.AddActor("a", []);
        runtime.Grant("a", "insulted", 1000);
        runtime.Advance(3, Fixture.Spring);
        runtime.Grant("a", "ate-well", 1000);
        runtime.Advance(6, Fixture.Spring);
        var changes = runtime.Grant("a", "festival-joy", 1000);
        Assert.Equal([new Evicted("moodlet", "a", "insulted")], changes.Evictions);
    }

    // ---- state and hysteresis ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AMoodJustBelowTheLineOfItsStateStaysInItUntilItFallsPastTheMargin()
    {
        var runtime = One();
        runtime.SetActivity("a", "eat");   // not important: no step is taken, the needs stay as they are
        var plainStart = runtime.MoodStateId("a");
        Assert.Equal("happy", plainStart);                              // 590, happy from 400
        var changes = runtime.Grant("a", "insulted", 920);              // -230: mood 360, below 400 but not below 400 - 50
        Assert.Equal(360, runtime.Mood("a"));
        Assert.Empty(changes.Moods);
        Assert.Equal("happy", runtime.MoodStateId("a"));
        var more = runtime.Grant("a", "insulted", 1000);                // -250: 340, past the margin
        Assert.Equal(340, runtime.Mood("a"));
        var change = Assert.Single(more.Moods);
        Assert.Equal(("a", "happy", "content"), (change.Actor, runtime.Set.Mood.States[change.From].Id, runtime.Set.Mood.States[change.To].Id));
        Assert.Equal(340, change.Mood);
    }

    [Fact]
    public void ARiseToTheStartOfTheNextStateChangesItAtOnceWithNoMargin()
    {
        var runtime = One();
        runtime.Grant("a", "insulted", 1000);                           // 340: content
        Assert.Equal("content", runtime.MoodStateId("a"));
        var changes = runtime.AdjustNeed("a", "hunger", 100);           // hunger 900: +80 -> 420
        Assert.Equal(420, runtime.Mood("a"));
        Assert.Equal("happy", runtime.MoodStateId("a"));
        Assert.Single(changes.Moods);
    }

    [Fact]
    public void TheStepReportsAMoodChangeWhenNeedsFadeAndTheMoodletsExpire()
    {
        var runtime = One();
        var all = new List<MoodChanged>();
        for (var step = 1; step <= 60; step++) all.AddRange(runtime.Advance(step, Fixture.Spring).Moods.Where(m => m.Actor == "a"));
        Assert.NotEmpty(all);
        Assert.All(all, m => Assert.NotEqual(m.From, m.To));
        for (var i = 1; i < all.Count; i++) Assert.Equal(all[i - 1].To, all[i].From);
        Assert.Equal(runtime.Actor("a").MoodState, all[^1].To);
    }

    // ---- the explanation adds up --------------------------------------------------------------------------------------------------------

    [Fact]
    public void ForRandomLivesTheTermsOfTheExplanationAlwaysAddUpToTheMood()
    {
        var seed = 7UL;
        int Next(int bound) { seed = seed * 6364136223846793005UL + 1442695040888963407UL; return (int)((seed >> 33) % (ulong)bound); }
        var moodlets = new[] { "ate-well", "gift-received", "insulted", "festival-joy", "saw-a-theft" };
        var activities = new[] { "", "eat", "sleep", "idle", "celebrate", "worship", "work" };
        var traits = new[] { "kind", "grumpy", "glutton", "sociable", "stoic" };
        var checkedMoods = 0;
        for (var life = 0; life < 40; life++)
        {
            var runtime = Fixture.Runtime();
            for (var i = 0; i < 3; i++) runtime.AddActor("p" + i, traits.Where(_ => Next(3) == 0).ToList());
            for (var call = 0; call < 60; call++)
            {
                var actor = "p" + Next(3);
                switch (Next(5))
                {
                    case 0: runtime.Advance(runtime.Step + 1 + Next(30), Fixture.Host(season: Next(2) == 0 ? "winter" : "spring", raining: Next(2) == 0)); break;
                    case 1: runtime.Grant(actor, moodlets[Next(moodlets.Length)], 200 + Next(1300)); break;
                    case 2: runtime.AdjustNeed(actor, new[] { "hunger", "energy", "social", "fun" }[Next(4)], Next(1200) - 600); break;
                    case 3: runtime.SetActivity(actor, activities[Next(activities.Length)]); break;
                    case 4:
                        var other = "p" + ((Next(2) + 1 + int.Parse(actor[1..])) % 3);
                        if (runtime.Offers(actor, other, Fixture.Spring).First(o => o.Interaction == "chat").Available) runtime.Perform(new InteractionRequest("chat", actor, other, [], ""), Fixture.Spring);
                        break;
                }
                foreach (var id in new[] { "p0", "p1", "p2" })
                {
                    var explanation = RelationshipExplain.Mood(runtime, id);
                    Assert.Equal(explanation.Terms.Sum(t => t.Value), explanation.Sum);
                    Assert.Equal(Math.Clamp(explanation.Sum, -1000, 1000), explanation.Value);
                    Assert.Equal(runtime.Mood(id), explanation.Value);
                    checkedMoods++;
                }
            }
        }
        Assert.True(checkedMoods >= 7000, "only " + checkedMoods + " moods were checked");
    }
}
