using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>Replays that depend on the clock the recorded session used and on the length of the session. Written from the survivors of the third round of mutation checking.</summary>
public sealed class ReplayClockTests
{
    [Fact]
    public void ASessionThatLivedFiveTicksToAStepReplaysWithTheStepsItUsed()
    {
        var host = new TutHost(journal: true, clock: new TickTutorialClock(5));
        for (var tick = 1; tick <= 60; tick++)
        {
            if (tick == 12) host.Ask("observe", "e1");
            if (tick == 33) host.Ask("observe", "e2");
            host.Tick();
        }
        Assert.Equal(12, host.Runtime.Step);
        var fresh = Samples.Done(Samples.Small);
        fresh.Start();
        var replay = TutorialReplay.Run(fresh, TutorialJournal.Parse(host.Journal.ToText()), 0, 60, TutHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void AReplayOfManyThousandsOfTicksRunsOneTickForEachAndNeverTwo()
    {
        var runtime = Samples.Done(Samples.Small);
        runtime.Start();
        var replay = TutorialReplay.Run(runtime, [], 0, 25_000, TutHost.TicksPerSecond);
        Assert.Equal(25_000, replay.Tick);
    }
}
