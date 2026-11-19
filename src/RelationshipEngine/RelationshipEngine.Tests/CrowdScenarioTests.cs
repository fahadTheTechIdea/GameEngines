using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>
/// The crowd sample (samples/crowd.relationship.json and crowd.scenario.json): 120 people and about two thousand scripted interactions in 45 days, a document whose chat is decided by noise around its threshold. Unlike the village, which draws two
/// seeded rolls in ninety days, it draws thousands, so a seed sweep over it can show the spread of the accept noise, the gossip and the balance pushes. It has its own demo digests (the village's are untouched): the output of one run is the same twice,
/// and a run resumed from the save of day 20 ends in the state of the run that never stopped.
/// </summary>
public sealed class CrowdScenarioTests
{
    /// <summary>The SHA-256 of the 45-day single run's output (line feeds only), taken when the sample was written.</summary>
    private const string OutputSha256 = "62506D10C5D01482B616ED7B9C4128ED40904AC3C330F1BBBC0ABF53E905D4BD";

    /// <summary>The state digest the 45-day run ends in, with and without a stop at day 20.</summary>
    private const string StateDigest = "283E5F0D1AE93B4244EF446E45AD3A39B2D58FA8DEB7C1041F2B41F8C31F3503";

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = RelationshipCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), error.ToString());
    }

    private static (int Code, string Out, string Err) Simulate(params string[] extra) =>
        Run(["simulate", Sample("crowd.relationship.json"), Sample("crowd.scenario.json"), .. extra]);

    private static string Digest(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last(l => l.StartsWith("digest ", StringComparison.Ordinal))["digest ".Length..];

    private static long Positive(string report, string metric, string column)
    {
        var line = report.Split('\n').Single(l => l.StartsWith("metric " + metric + " ", StringComparison.Ordinal)).Split(' ');
        return long.Parse(line[Array.IndexOf(line, column) + 1]);
    }

    [Fact]
    public void TheDocumentAndTheScenarioAreAccepted()
    {
        Assert.Equal(0, Run("check", Sample("crowd.relationship.json")).Code);
        var run = Simulate("45");
        Assert.Contains("state at step 1080: 120 actors, 1585 edges, 3391 rolls", run.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDemoOutputIsTheSameTwiceAndItsDigestsArePinned()
    {
        var first = Simulate("45");
        var second = Simulate("45");
        Assert.Equal(first.Out, second.Out);
        Assert.Equal(OutputSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Out))));
        Assert.Equal(StateDigest, Digest(first.Out));
    }

    [Fact]
    public void ARunResumedFromTheSaveOfDayTwentyEndsInTheStateOfTheRunThatNeverStopped()
    {
        var save = Path.Combine(Path.GetTempPath(), "crowd-day20-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var head = Simulate("20", "--save", save);
            Assert.True(File.Exists(save));
            var tail = Simulate("45", "--resume", save);
            Assert.Equal(StateDigest, Digest(tail.Out));
            Assert.NotEqual(Digest(head.Out), Digest(tail.Out));
            // The resumed run carries on at the step the save was taken at, not from the start: its first day line is day 21.
            Assert.StartsWith("day 21 (step 504)", tail.Out.Split('\n').First(l => l.StartsWith("day ", StringComparison.Ordinal)), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(save);
        }
    }

    [Fact]
    public void ASaveAtOrPastTheHorizonCannotBeResumed()
    {
        var save = Path.Combine(Path.GetTempPath(), "crowd-day20-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            Simulate("20", "--save", save);
            var run = Simulate("20", "--resume", save);
            Assert.Equal(2, run.Code);
            Assert.Contains("not before the horizon", run.Err, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(save);
        }
    }

    [Fact]
    public void TheSeedChangesWhatTheNoiseGossipAndBalancePushesDo()
    {
        var sweep = Simulate("45", "--seeds", "8", "--from-seed", "1");
        Assert.Equal(0, sweep.Code);
        Assert.True(Positive(sweep.Out, "rolls", "min") > 3000, "the crowd draws thousands of seeded rolls; the village draws two");
        foreach (var metric in new[] { "interactions.accepted", "interactions.declined", "changes.memories", "changes.balance", "changes.evictions", "standing.spread", "edges.total" })
        {
            Assert.True(Positive(sweep.Out, metric, "max") > Positive(sweep.Out, metric, "min"), metric + " should differ between seeds");
            Assert.True(Positive(sweep.Out, metric, "positive") == 8, metric + " should be positive in every seed");
        }
    }
}
