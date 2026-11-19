using EconomyEngine;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>Production through the component: the unlock request, its answers and refusals, the limit events, and the journal that records an unlock so a replay reproduces it.</summary>
public sealed class ProductionComponentTests
{
    [Fact]
    public void AnUnlockRequestOnlyQueuesAndIsAnsweredInTheNextUpdateWithTheEventThatSaysWhatWasUnlocked()
    {
        var host = new EcoHost(set: Samples.Fast());
        Assert.False(host.Runtime.IsUnlocked(host.Runtime.Set.RecipeById["alloy-steel"].Index));
        host.Session.Submit(new EconomyRecipeUnlockRequested("u1", "alloy-steel"));
        Assert.False(host.Runtime.IsUnlocked(host.Runtime.Set.RecipeById["alloy-steel"].Index));   // queued, not done
        Assert.Empty(host.Of<EconomyApplied>());
        host.Tick();
        Assert.True(host.Runtime.IsUnlocked(host.Runtime.Set.RecipeById["alloy-steel"].Index));
        Assert.Equal("u1", Assert.Single(host.Of<EconomyApplied>()).RequestId);
        var unlocked = Assert.Single(host.Of<EconomyRecipeUnlocked>());
        Assert.Equal(("alloy-steel", "economy"), (unlocked.Recipe, unlocked.Source));
        // Hearing of it again is applied and changes nothing: no second unlock event.
        host.Session.Submit(new EconomyRecipeUnlockRequested("u2", "alloy-steel"));
        host.Tick();
        Assert.Equal(2, host.Of<EconomyApplied>().Count());
        Assert.Single(host.Of<EconomyRecipeUnlocked>());
    }

    [Fact]
    public void AnUnknownRecipeAndARecipeThatWasNeverLockedAreRefusedWithTheirCodesAndNothingChanges()
    {
        var host = new EcoHost(set: Samples.Fast());
        var before = host.Runtime.StateDigest();
        host.Session.Submit(new EconomyRecipeUnlockRequested("nothing", "magic"));
        host.Session.Submit(new EconomyRecipeUnlockRequested("open", "mill"));
        host.Session.Submit(new EconomyRecipeUnlockRequested("elsewhere", "alloy-steel", "nobody"));
        host.Tick();
        var refused = host.Of<EconomyRefused>().ToDictionary(r => r.RequestId, r => r.Code);
        Assert.Equal(EconomyCodes.UnknownRecipe, refused["nothing"]);
        Assert.Equal(EconomyCodes.RecipeOpen, refused["open"]);
        Assert.Equal(EconomyChannels.UnknownChannel, refused["elsewhere"]);
        Assert.Empty(host.Of<EconomyRecipeUnlocked>());
        Assert.False(host.Runtime.IsUnlocked(host.Runtime.Set.RecipeById["alloy-steel"].Index));
        // The periods that closed in that Update moved the state; the refusals themselves left it as it was: compare against a control that was only ticked.
        var control = new EcoHost(set: Samples.Fast());
        control.Tick();
        Assert.Equal(control.Runtime.StateDigest(), host.Runtime.StateDigest());
        Assert.NotEqual(before, host.Runtime.StateDigest());
    }

    [Fact]
    public void ALineThatRunsShortRaisesOneEventWhenItChangesAndNotEveryPeriod()
    {
        var host = new EcoHost(set: Samples.Fast());
        host.Tick(60);
        var limited = host.Of<EconomyProductionLimited>().ToList();
        Assert.NotEmpty(limited);
        // The sample's lines are throttled, so a line can pass through "margin" on its way to a shortage; the first shortage of the smelter is of ore.
        var coal = limited.First(e => e.Market == "hillfort" && e.Recipe == "smelt-steel" && e.To == "input");
        Assert.Equal("ore", coal.Good);
        Assert.Contains(coal.From, new[] { "capacity", "margin" });
        // One event per change: the same line never reports the same change twice in a row.
        foreach (var line in limited.GroupBy(e => (e.Market, e.Recipe)))
        {
            var ordered = line.OrderBy(e => e.Tick).ToList();
            for (var i = 1; i < ordered.Count; i++) Assert.Equal(ordered[i - 1].To + "/" + ordered[i - 1].Good, ordered[i].From + "/" + (ordered[i].From is "input" or "room" ? ordered[i - 1].Good : ""));
        }
        Assert.Contains(host.Transcript.Lines, l => l.StartsWith("EconomyProductionLimited", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnlockIsInTheJournalAndAReplayFromTheStartReproducesTheStateAndTheEvents()
    {
        var set = Samples.Fast();
        var host = new EcoHost(set: set, journal: true);
        for (var tick = 1; tick <= 30; tick++)
        {
            if (tick == 10) host.Session.Submit(new EconomyRecipeUnlockRequested("discover", "alloy-steel"));
            host.Tick();
        }
        Assert.Contains(host.Journal.Lines, l => l.Contains("|unlock|discover|alloy-steel|", StringComparison.Ordinal));
        var replay = EconomyReplay.Run(EconomyRuntime.Create(set), EconomyJournal.Parse(host.Journal.ToText()), 0, 30, EcoHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
        Assert.True(host.Runtime.IsUnlocked(set.RecipeById["alloy-steel"].Index));
    }

    [Fact]
    public void ACheckpointInTheMiddleOfProductionBatchesIsRestoredWithTheUnlockAndTheBatchesAndEndsWhereTheRunEnds()
    {
        var set = Samples.Fast();
        var host = new EcoHost(set: set, journal: true);
        byte[]? checkpoint = null;
        long checkpointTick = 0;
        var lines = 0;
        for (var tick = 1; tick <= 40; tick++)
        {
            if (tick == 5) host.Session.Submit(new EconomyRecipeUnlockRequested("discover", "alloy-steel"));
            host.Tick();
            if (checkpoint is null && tick >= 12 && host.Component.IsQuiet && host.Runtime.Batches.Count > 0)
            {
                checkpoint = EconomyCheckpoint.Capture(host.Component);
                checkpointTick = host.Component.Tick;
                lines = host.Transcript.Lines.Count;
            }
        }
        Assert.NotNull(checkpoint);
        var resumed = EconomyCheckpoint.Open(set, checkpoint!);
        Assert.NotEmpty(resumed.Batches);
        Assert.True(resumed.IsUnlocked(set.RecipeById["alloy-steel"].Index));
        var replay = EconomyReplay.Run(resumed, EconomyJournal.Parse(host.Journal.ToText()), checkpointTick, 40, EcoHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(EngineCommon.Digest.Of(string.Join('\n', host.Transcript.Lines.Skip(lines))), replay.TranscriptHash);
    }
}
