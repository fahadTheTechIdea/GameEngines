using EngineCommon.TailTool;
using Xunit;

namespace EngineCommon.Balance.Tests;

/// <summary>The rare-event command line, the engine flags of the sweep, the per-event lists of a sweep report, and the tail tool reading per-onset rows.</summary>
public sealed class RareAndEventsTests
{
    private static string Code(Action action) => Assert.Throws<EngineRefusedException>(action).Code;

    private static RareOptions Rare(params string[] args) => RareOptions.Parse(args);

    // ---- the rare-event command line ----------------------------------------------------------------------------------------------

    [Fact]
    public void ARareLineTakesItsDefaultsAndItsNamedValues()
    {
        var plain = Rare("365", "--rare", "quake:great");
        Assert.Equal(("quake:great", 200, 21, 0L, (long?)null, (long?)365, false), (plain.Target, plain.Effort, plain.Replications, plain.FromSeed, plain.Days, plain.Horizon, plain.Rows));
        var full = Rare("--rare", "a:b", "--effort", "1000", "--replications", "30", "--from-seed", "77", "--days", "10", "--rows");
        Assert.Equal(("a:b", 1000, 30, 77L, (long?)10, (long?)null, true), (full.Target, full.Effort, full.Replications, full.FromSeed, full.Days, full.Horizon, full.Rows));
    }

    [Fact]
    public void ARareLineMayGiveAnEffortForEachStageAndAskForTheRestartBeforeTheDraw()
    {
        var many = Rare("365", "--rare", "a:b", "--efforts", "1000,200,50", "--predraw");
        Assert.Equal(new[] { 1000, 200, 50 }, many.Efforts);
        Assert.Equal(1000, many.Effort);
        Assert.True(many.Predraw);
        var plain = Rare("365", "--rare", "a:b");
        Assert.Null(plain.Efforts);
        Assert.False(plain.Predraw);
        Assert.Equal(BalanceCodes.EffortsTwice, Code(() => Rare("365", "--rare", "a:b", "--effort", "5", "--efforts", "5,5")));
        Assert.Equal(BalanceCodes.EffortRange, Code(() => Rare("365", "--rare", "a:b", "--efforts", "5,0")));
        Assert.Equal(BalanceCodes.OptionValue, Code(() => Rare("365", "--rare", "a:b", "--efforts", "5,x")));
        Assert.Equal(BalanceCodes.OptionValue, Code(() => Rare("365", "--rare", "a:b", "--efforts")));
        Assert.Equal(BalanceCodes.OptionTwice, Code(() => Rare("365", "--rare", "a:b", "--efforts", "5", "--efforts", "5")));
        Assert.Equal(BalanceCodes.OptionTwice, Code(() => Rare("365", "--rare", "a:b", "--predraw", "--predraw")));
    }

    [Fact]
    public void ABadRareLineIsRefusedWithTheCodeOfItsRule()
    {
        Assert.Equal(BalanceCodes.RareMissing, Code(() => Rare("365")));
        Assert.Equal(BalanceCodes.HorizonMissing, Code(() => Rare("--rare", "a:b")));
        Assert.Equal(BalanceCodes.HorizonTwice, Code(() => Rare("365", "--days", "5", "--rare", "a:b")));
        Assert.Equal(BalanceCodes.HorizonTwice, Code(() => Rare("365", "7", "--rare", "a:b")));
        Assert.Equal(BalanceCodes.OptionTwice, Code(() => Rare("365", "--rare", "a:b", "--rare", "c:d")));
        Assert.Equal(BalanceCodes.OptionTwice, Code(() => Rare("365", "--rare", "a:b", "--rows", "--rows")));
        Assert.Equal(BalanceCodes.OptionValue, Code(() => Rare("365", "--rare")));
        Assert.Equal(BalanceCodes.OptionValue, Code(() => Rare("365", "--rare", "a:b", "--effort", "ten")));
        Assert.Equal(BalanceCodes.OptionUnknown, Code(() => Rare("365", "--rare", "a:b", "--seeds", "5")));
        Assert.Equal(BalanceCodes.EffortRange, Code(() => Rare("365", "--rare", "a:b", "--effort", "0")));
        Assert.Equal(BalanceCodes.EffortRange, Code(() => Rare("365", "--rare", "a:b", "--effort", (BalanceCodes.MaxEffort + 1).ToString())));
        Assert.Equal(BalanceCodes.ReplicationsRange, Code(() => Rare("365", "--rare", "a:b", "--replications", "0")));
        Assert.Equal(BalanceCodes.ReplicationsRange, Code(() => Rare("365", "--rare", "a:b", "--replications", (BalanceCodes.MaxReplications + 1).ToString())));
        Assert.Equal(BalanceCodes.FromSeedRange, Code(() => Rare("365", "--rare", "a:b", "--from-seed", (BalanceCodes.MaxSeed + 1).ToString())));
        Assert.Equal(BalanceCodes.DaysRange, Code(() => Rare("--rare", "a:b", "--days", "0")));
    }

    // ---- engine flags of the sweep ----------------------------------------------------------------------------------------------

    [Fact]
    public void AnEngineFlagIsKeptWhenNamedAndRefusedWhenNotOrRepeated()
    {
        var options = SweepOptions.Parse(["10", "--seeds", "3", "--onsets"], ["--onsets"]);
        Assert.True(options.Has("--onsets"));
        Assert.False(SweepOptions.Parse(["10", "--seeds", "3"], ["--onsets"]).Has("--onsets"));
        Assert.Equal(BalanceCodes.OptionUnknown, Code(() => SweepOptions.Parse(["10", "--seeds", "3", "--onsets"])));
        Assert.Equal(BalanceCodes.OptionTwice, Code(() => SweepOptions.Parse(["10", "--seeds", "3", "--onsets", "--onsets"], ["--onsets"])));
        Assert.Equal(BalanceCodes.OptionUnknown, Code(() => SweepOptions.Parse(["10", "--seeds", "3", "--verbose"], ["--onsets"])));
        Assert.Contains("--onsets", Assert.Throws<EngineRefusedException>(() => SweepOptions.Parse(["10", "--seeds", "3", "--verbose"], ["--onsets"])).Message, StringComparison.Ordinal);
    }

    // ---- per-event lists in a sweep report ----------------------------------------------------------------------------------------

    private static SeedObservation Seen(long seed) => new(
        new Dictionary<string, long> { ["m"] = seed },
        new Dictionary<string, IReadOnlyList<long>> { ["size"] = Enumerable.Range(0, (int)(seed % 3)).Select(i => seed * 10 + i).ToList(), ["empty"] = [] });

    [Fact]
    public void EventsAreKeptWholeInSeedOrderSummarisedAndListedWithRows()
    {
        var options = SweepOptions.Parse(["5", "--seeds", "4", "--from-seed", "1", "--rows"]);
        var result = SeedSweep.RunWithEvents(options, Seen);
        // Seeds 1..4 give 1, 2, 0, 1 values: 10 | 20 21 | none | 40.
        Assert.Equal([(1L, 10L), (2L, 20L), (2L, 21L), (4L, 40L)], result.Events!["size"]);
        Assert.Empty(result.Events["empty"]);
        var report = SeedSweep.Render("heading", options, result);
        Assert.Contains("\nevents empty n 0 seeds 0\n", report, StringComparison.Ordinal);
        Assert.Contains("\nevents size n 4 seeds 3 min 10 p10 10 p50 20 p90 40 p99 40 max 40 mean 22.750\n", report, StringComparison.Ordinal);
        Assert.Contains("\nonset size 1 10\nonset size 2 20\nonset size 2 21\nonset size 4 40\n", report, StringComparison.Ordinal);
        Assert.True(report.IndexOf("\nevents size", StringComparison.Ordinal) < report.IndexOf("\nsample m 1 1", StringComparison.Ordinal), "the event summaries come before the rows");
        Assert.Equal(report, SeedSweep.Render("heading", options, SeedSweep.RunWithEvents(options, Seen)));
    }

    [Fact]
    public void ASweepWithoutEventsPrintsNoEventLinesAndTheSameDigestAsBefore()
    {
        var options = SweepOptions.Parse(["5", "--seeds", "3", "--rows"]);
        var plain = SeedSweep.Run(options, seed => new Dictionary<string, long> { ["m"] = seed });
        Assert.Null(plain.Events);
        var report = SeedSweep.Render("h", options, plain);
        Assert.DoesNotContain("events ", report, StringComparison.Ordinal);
        Assert.DoesNotContain("onset ", report, StringComparison.Ordinal);
        var viaEvents = SeedSweep.RunWithEvents(options, seed => new SeedObservation(new Dictionary<string, long> { ["m"] = seed }, new Dictionary<string, IReadOnlyList<long>>()));
        Assert.Equal(report, SeedSweep.Render("h", options, viaEvents));         // an engine with no events through the event path prints exactly what the plain path prints
    }

    // ---- the tail tool reads per-onset rows -------------------------------------------------------------------------------------------

    [Fact]
    public void OnsetRowsAreReadWithManyPerSeedInSeedOrderAndKeepTheirOwnDigest()
    {
        var text = "events x n 3\nonset x 2 7\nonset x 1 5\nonset x 2 9\nonset y 1 99\nsample z 1 4\n";
        var sample = TailSample.Read(text, "x");
        Assert.True(sample.PerOnset);
        Assert.Equal([5L, 7L, 9L], sample.Values);                               // seed order, then the order the engine listed them
        Assert.Equal(TailSample.Read("onset x 1 5\nonset x 2 7\nonset x 2 9\n", "x").Digest, sample.Digest);
        Assert.NotEqual(TailSample.Read("sample x 1 5\nsample x 2 7\nsample x 3 9\n", "x").Digest, sample.Digest);
        Assert.False(TailSample.Read(text, "z").PerOnset);
    }

    [Fact]
    public void ANameWithBothKindsOfRowIsRefusedAndABadOnsetRowIsAFormatError()
    {
        Assert.Equal(TailCodes.SampleAmbiguous, Assert.Throws<TailRefusedException>(() => TailSample.Read("sample x 1 5\nonset x 1 6\n", "x")).Code);
        Assert.Equal(TailCodes.SampleFormat, Assert.Throws<TailRefusedException>(() => TailSample.Read("onset x 1\n", "x")).Code);
        Assert.Equal(TailCodes.MetricMissing, Assert.Throws<TailRefusedException>(() => TailSample.Read("onset y 1 5\n", "x")).Code);
    }

    [Fact]
    public void ThePerOnsetSampleIsProposedFromLikeAPerSeedOne()
    {
        var values = Enumerable.Range(0, 400).Select(i => (long)(i % 40) * (i % 40) / 3);
        var perSeed = string.Join("", values.Select((v, i) => "sample m " + (i + 1) + " " + v + "\n"));
        var perOnset = string.Join("", values.Select((v, i) => "onset m " + (i / 2 + 1) + " " + v + "\n"));        // two onsets per seed
        var a = TailCli.Propose(TailSample.Read(perSeed, "m"), [0, 100, 300], 90, 30);
        var b = TailCli.Propose(TailSample.Read(perOnset, "m"), [0, 100, 300], 90, 30);
        Assert.Equal(a.Split('\n').Last(l => l.StartsWith("tailPpm ", StringComparison.Ordinal)), b.Split('\n').Last(l => l.StartsWith("tailPpm ", StringComparison.Ordinal)));
        Assert.Contains("per-onset", b, StringComparison.Ordinal);
        Assert.DoesNotContain("per-onset", a, StringComparison.Ordinal);
    }
}
