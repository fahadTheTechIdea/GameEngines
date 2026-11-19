using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The journal and the replay: a recorded session replayed from a checkpoint, from the start or from other quiet ticks ends in the same state and raises the same events; a missing request or a changed fact is detected.</summary>
public sealed class ReplayTests
{
    /// <summary>A short session with requests, observations, a fact that changes and a tip: 60 ticks.</summary>
    private static TutHost Play(Action<TutHost, int>? during = null, int ticks = 60)
    {
        var host = new TutHost(journal: true);
        for (var tick = 1; tick <= ticks; tick++)
        {
            switch (tick)
            {
                case 4: host.Ask("observe", "e1"); break;
                case 9: host.Ask("observe", "e2"); break;
                case 12: host.HostFacts["flag"] = FactValue.From(true); break;
                case 20: host.Ask("text-scale", "", 1500); break;
                case 25: host.Ask("dismiss-tip", "t1"); break;
                case 30: host.HostFacts["flag"] = FactValue.From(false); break;
                case 35: host.Ask("replay"); break;
                case 38: host.Ask("observe", "e3"); break;
                case 40: host.Ask("nonsense"); break;
                case 41: host.Ask("observe", "e1"); break;
                case 50: host.Ask("skip-step", "s2"); break;
            }
            host.Tick();
            during?.Invoke(host, tick);
        }
        return host;
    }

    private static string Tail(TutHost host, int from) => Digest.Of(string.Join('\n', host.Transcript.Lines.Skip(from)));

    [Fact]
    public void AJournalIsTextOneLinePerEntryInTheFormOfItsDocumentation()
    {
        var host = Play();
        var lines = host.Journal.Lines;
        Assert.Contains("F|1|flag=B:false", lines);
        Assert.Contains("S|1|1", lines);
        Assert.Contains("R|4||observe|r1|e1|1", lines);
        Assert.Contains("F|12|flag=B:true", lines);
        Assert.Contains("R|40||nonsense|r7||1", lines);
        Assert.Equal(host.Journal.ToText(), string.Join('\n', lines));
        Assert.Equal(lines.Count(l => l.StartsWith("R|", StringComparison.Ordinal)), host.Component.Answered);
    }

    [Fact]
    public void AJournalIsReadBackToTheSameEntries()
    {
        var host = Play();
        var entries = TutorialJournal.Parse(host.Journal.ToText());
        Assert.Equal(host.Journal.Lines.Count, entries.Count);
        var request = entries.First(e => e.Kind == "observe");
        Assert.Equal((4L, "", "r1", "e1", 1L), (request.Tick, request.Channel, request.RequestId, request.Text, request.Number));
        Assert.True(entries.First(e => e.Facts is not null).Facts!["flag"].Kind == FactKind.Boolean);
        Assert.Equal(1L, entries.First(e => e.Step is not null).Step);
        Assert.Empty(TutorialJournal.Parse(""));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("F|1")]
    [InlineData("F|x|flag=B:true")]
    [InlineData("F|1|flag")]
    [InlineData("F|1|flag=Z:true")]
    [InlineData("F|1|flag=B:maybe")]
    [InlineData("F|1|flag=I:x")]
    [InlineData("F|1|flag=B:true;flag=B:false")]
    [InlineData("S|1|x")]
    [InlineData("S|1|-1")]
    [InlineData("S|1|2|3")]
    [InlineData("R|1|||observe|id|e1")]
    [InlineData("R|1|||observe|id|e1|x")]
    [InlineData("Q|1|a")]
    [InlineData("S|5|1\nS|4|2")]
    public void AJournalThatIsNotOneIsRefusedWhole(string text) => Assert.Equal(TutorialJournal.BadJournal, Samples.Code(() => TutorialJournal.Parse(text)));

    [Fact]
    public void AReplayFromACheckpointEndsInTheSameStateAndTheSameEventsAsTheLiveRun()
    {
        byte[]? bytes = null;
        var lines = 0;
        long at = 0;
        var host = Play((h, tick) =>
        {
            if (tick == 22) { bytes = TutorialCheckpoint.Capture(h.Component); lines = h.Transcript.Lines.Count; at = h.Component.Tick; }
        });
        var entries = TutorialJournal.Parse(host.Journal.ToText());
        var replay = TutorialReplay.Run(TutorialCheckpoint.Open(Samples.Small, bytes!), entries, at, 60, TutHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Tail(host, lines), replay.TranscriptHash);
        Assert.Equal(host.Transcript.Lines.Count - lines, replay.Events);
        Assert.Equal(60, replay.Tick);
        Assert.Contains("step=60", replay.Fingerprint);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(29)]
    [InlineData(36)]
    [InlineData(41)]
    [InlineData(49)]
    public void AReplayFromAnyQuietTickIsTheSame(int tick)
    {
        byte[]? bytes = null;
        var lines = 0;
        long at = 0;
        var host = Play((h, t) =>
        {
            if (t == tick && h.Component.IsQuiet && h.Channels.IsQuiet) { bytes = TutorialCheckpoint.Capture(h.Component); lines = h.Transcript.Lines.Count; at = h.Component.Tick; }
        });
        Assert.NotNull(bytes);
        var replay = TutorialReplay.Run(TutorialCheckpoint.Open(Samples.Small, bytes!), TutorialJournal.Parse(host.Journal.ToText()), at, 60, TutHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(Tail(host, lines), replay.TranscriptHash);
    }

    [Fact]
    public void AReplayFromTheStartOfANewPlayerIsTheSame()
    {
        var host = Play();
        var fresh = Samples.Done(Samples.Small);
        fresh.Start();
        var replay = TutorialReplay.Run(fresh, TutorialJournal.Parse(host.Journal.ToText()), 0, 60, TutHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Hash, replay.TranscriptHash);
    }

    [Fact]
    public void AMissingRequestOrAChangedFactIsDetectedByADifferentResult()
    {
        var host = Play();
        var original = host.Journal.ToText();
        string Replayed(string text)
        {
            var fresh = Samples.Done(Samples.Small);
            fresh.Start();
            return TutorialReplay.Run(fresh, TutorialJournal.Parse(text), 0, 60, TutHost.TicksPerSecond).StateDigest;
        }
        Assert.Equal(host.Runtime.StateDigest(), Replayed(original));
        Assert.NotEqual(host.Runtime.StateDigest(), Replayed(string.Join('\n', original.Split('\n').Where(l => !l.StartsWith("R|9|", StringComparison.Ordinal)))));
        Assert.NotEqual(host.Runtime.StateDigest(), Replayed(original.Replace("F|12|flag=B:true", "F|12|flag=B:false", StringComparison.Ordinal)));
        Assert.NotEqual(host.Runtime.StateDigest(), Replayed(original.Replace("R|20||text-scale|r3||1500", "R|20||text-scale|r3||2000", StringComparison.Ordinal)));       // a changed argument of a request
    }

    [Fact]
    public void AReplayChecksItsArguments()
    {
        var runtime = Samples.Done(Samples.Small);
        Assert.Equal(TutorialCodes.StepOrder, Samples.Code(() => TutorialReplay.Run(runtime, [], 0, 10, 60)));          // 60 does not divide 1,000,000
        Assert.Equal(TutorialCodes.StepOrder, Samples.Code(() => TutorialReplay.Run(runtime, [], 0, 10, 0)));
        Assert.Equal(TutorialCodes.StepOrder, Samples.Code(() => TutorialReplay.Run(runtime, [], 10, 5, 50)));
        Assert.Equal(TutorialCodes.StepOrder, Samples.Code(() => TutorialReplay.Run(runtime, [], -1, 5, 50)));
        Assert.Throws<ArgumentNullException>(() => TutorialReplay.Run(null!, [], 0, 5, 50));
        Assert.Throws<ArgumentNullException>(() => TutorialReplay.Run(runtime, null!, 0, 5, 50));
    }

    [Fact]
    public void ARequestOnAnotherChannelIsNotInThisChannelsReplay()
    {
        var host = new TutHost(journal: true, channel: "a");
        host.Ask("observe", "e1", 1, "a");
        host.Ask("observe", "e2", 1, "b");
        host.Tick(3);
        Assert.Contains(host.Journal.Lines, l => l.StartsWith("R|1|a|", StringComparison.Ordinal));
        Assert.DoesNotContain(host.Journal.Lines, l => l.StartsWith("R|1|b|", StringComparison.Ordinal));
        var fresh = Samples.Done(Samples.Small);
        fresh.Start();
        var replay = TutorialReplay.Run(fresh, TutorialJournal.Parse(host.Journal.ToText()), 0, 3, 50, "tutorial", "a");
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
    }

    [Fact]
    public void TheJournalEscapesTheCharactersItsLinesAreMadeOf()
    {
        var host = new TutHost(journal: true);
        host.Ask("intent", "a|b;c=d");
        host.Tick();
        var entry = TutorialJournal.Parse(host.Journal.ToText()).First(e => e.Kind is not null);
        Assert.Equal("a|b;c=d", entry.Text);
        Assert.Contains("R|1||intent|r1|a%7Cb%3Bc%3Dd|1", host.Journal.Lines);
    }

    [Fact]
    public void TheJournalClockReadsTheStepsARecordedRunUsed()
    {
        var entries = TutorialJournal.Parse("S|3|10\nS|8|20");
        var clock = new JournalTutorialClock(entries, 5);
        Assert.Equal([5L, 5, 10, 10, 20, 20], new[] { clock.StepAt(1), clock.StepAt(2), clock.StepAt(3), clock.StepAt(7), clock.StepAt(8), clock.StepAt(100) });
        Assert.Throws<ArgumentNullException>(() => new JournalTutorialClock(null!, 0));
    }
}
