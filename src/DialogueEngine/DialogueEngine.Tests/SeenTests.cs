using DialogueEngine;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>A line says whether the player has been shown it before, so a host can offer to skip text that was read (Ren'Py's "skip seen text only"). It comes from the visit counts, so a save keeps it.</summary>
public sealed class SeenTests
{
    private static List<bool> Seen(DialogueRuntime runtime, int rounds)
    {
        var seen = new List<bool>();
        var step = runtime.Start("loop", FeatureFixture.NoFacts);
        for (var i = 0; i < rounds; i++)
        {
            seen.Add(step.Line!.Seen);
            runtime.Next(FeatureFixture.NoFacts);
            step = runtime.Choose("again", FeatureFixture.NoFacts);
        }
        return seen;
    }

    [Fact]
    public void ALineIsNewTheFirstTimeAndSeenEveryTimeAfter()
    {
        // The cycle shows a, b, c, a, b, c, a: the first three are new.
        Assert.Equal([false, false, false, true, true, true, true], Seen(new DialogueRuntime(FeatureFixture.Load(FeatureFixture.VariantDocument("cycle")), 1), 7));
    }

    [Fact]
    public void ALineShownAgainAfterAnInterruptionIsSeenEvenThoughItIsNotANewVisit()
    {
        var runtime = new DialogueRuntime(FeatureFixture.Load(), 1);
        var first = runtime.Start("main", FeatureFixture.Facts());
        Assert.False(first.Line!.Seen);
        var alarm = runtime.Interrupt("alarm", FeatureFixture.Facts());
        Assert.False(alarm.Line!.Seen);
        var again = runtime.Next(FeatureFixture.Facts());   // the alarm returns: the greeting is shown again
        Assert.Equal("hi", again.Line!.Node);
        Assert.True(again.Line.Seen);
        Assert.Equal(1, runtime.State.Visits["main/hi"]);
    }

    [Fact]
    public void ASavedSessionKnowsWhatWasSeenAndGoesOnTheSameAsOneThatWasNotSaved()
    {
        var set = FeatureFixture.Load(FeatureFixture.VariantDocument("cycle"));
        var straight = new DialogueRuntime(set, 1);
        var saved = new DialogueRuntime(set, 1);
        Seen(straight, 2);
        Seen(saved, 2);
        var resumed = DialogueSave.Open(set, DialogueSave.Write(saved));
        // Both are at the line that follows the second round; going on shows the same Seen flags.
        var one = straight.Next(FeatureFixture.NoFacts);
        var two = resumed.Next(FeatureFixture.NoFacts);
        Assert.Equal(one.Kind, two.Kind);
        var oneNext = straight.Choose("again", FeatureFixture.NoFacts);
        var twoNext = resumed.Choose("again", FeatureFixture.NoFacts);
        Assert.Equal((oneNext.Line!.Node, oneNext.Line.Seen), (twoNext.Line!.Node, twoNext.Line.Seen));
    }
}
