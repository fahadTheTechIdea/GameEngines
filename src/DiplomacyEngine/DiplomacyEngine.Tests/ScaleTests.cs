using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>
/// Cost: 64 realms (2,016 pairs), about 200 treaties in force, 100 agents on missions, wars and a balance of power, a year of daily steps. The budgets are set from the measurements recorded in docs/DIPLOMACY_ENGINE.md section 10
/// and docs/measurements (median of repeats on this machine in Release, with a large margin for a loaded machine and a Debug build). A run writes its numbers to <c>scale-results.csv</c> beside the test assembly.
/// </summary>
public sealed class ScaleTests
{
    public const int Realms = 64;
    public const int Agents = 100;

    internal static string BigText()
    {
        var root = Fixture.Root();
        root["id"] = "big";
        root["limits"] = new JsonObject { ["maxTreaties"] = 4096, ["maxProposals"] = 512, ["maxWars"] = 64, ["maxAgents"] = 256, ["maxMissions"] = 256, ["maxCalls"] = 256, ["maxIncidents"] = 512, ["maxGrants"] = 4096, ["maxDeedsPerPair"] = 8 };
        foreach (var type in root["types"]!.AsArray()) { type!["considerDays"] = Math.Min(type["considerDays"]!.GetValue<int>(), 0); Fixture.Decided(root, type["id"]!.GetValue<string>(), type["id"]!.GetValue<string>() != "peace"); }
        var template = Fixture.Realm(root, "aurel");
        var realms = new JsonArray();
        var ideologies = new[] { "crown", "guild", "zeal" };
        for (var r = 0; r < Realms; r++)
        {
            var realm = Fixture.Copy(template).AsObject();
            var id = "r" + r.ToString("00", CultureInfo.InvariantCulture);
            realm["id"] = id;
            realm["key"] = "realm." + id;
            realm["ideology"] = ideologies[r % 3];
            realm["traits"] = new JsonArray(r % 4 == 0 ? "shady" : "loyal");
            realm["might"] = 100_000 + (r * 7919) % 400_000;
            realm["market"] = "market-" + id;
            realm["leader"] = "leader-" + id;
            realm["control"] = "ai";
            realm["stability"] = 500 + (r * 37) % 400;
            realm["legitimacy"] = 500 + (r * 53) % 400;
            realm["successionPpm"] = 300;
            realms.Add(realm);
        }
        root["realms"] = realms;
        var agents = new JsonArray();
        for (var a = 0; a < Agents; a++)
            agents.Add(new JsonObject { ["id"] = "agent-" + a.ToString("000", CultureInfo.InvariantCulture), ["key"] = "agent.aurel_1", ["realm"] = "r" + (a % Realms).ToString("00", CultureInfo.InvariantCulture), ["skill"] = 300 + (a * 11) % 600 });
        root["agents"] = agents;
        return root.ToJsonString();
    }

    private static (double Median, double Min, double Max) Time(int repeats, Action action)
    {
        var times = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        return (times[times.Count / 2], times[0], times[^1]);
    }

    private static string Ms(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>About 200 treaties: each realm with its three neighbours (a non-aggression pact, a trade treaty and a marriage), eight alliances and twenty vassalages with tribute.</summary>
    internal static void Weave(DipRuntime runtime)
    {
        var name = (int i) => "r" + (i % Realms).ToString("00", CultureInfo.InvariantCulture);
        for (var r = 0; r < Realms; r++)
        {
            runtime.Propose(name(r), name(r + 1), "nap", 360, 0);
            runtime.Propose(name(r), name(r + 2), "trade", 0, 0);
            runtime.Propose(name(r), name(r + 3), "marriage", 0, 0);
        }
        for (var r = 0; r < 8; r++) runtime.Propose(name(r * 8), name(r * 8 + 4), "alliance", 0, 0);
        for (var r = 0; r < 20; r++) runtime.Propose(name(r * 3), name(r * 3 + 5), "vassalage", 0, 1000 + r);
    }

    [Fact]
    public void SixtyFourRealmsAndTwoHundredTreatiesLiveAYearWithinTheBudgetAndKeepTheirBounds()
    {
        var csv = new StringBuilder("measurement,median,min,max,unit,repeats\n");
        var watch = Stopwatch.StartNew();
        var text = BigText();
        var set = DipLoader.Load(text);
        var load = watch.Elapsed.TotalMilliseconds;
        Assert.Equal(Realms, set.Realms.Count);
        Assert.Equal(Agents, set.Agents.Count);
        csv.Append(CultureInfo.InvariantCulture, $"load document ({text.Length / 1024} KB),{Ms(load)},{Ms(load)},{Ms(load)},ms,1\n");

        var runtime = DipRuntime.Create(set);
        var host = Fixture.Host();
        var weave = Stopwatch.StartNew();
        Weave(runtime);
        var proposeTime = weave.Elapsed.TotalMilliseconds;
        runtime.Advance(0, runtime.CycleSeasonAt, host);
        Assert.InRange(runtime.Treaties.Count, 190, 260);
        csv.Append(CultureInfo.InvariantCulture, $"propose {runtime.Treaties.Count + runtime.Proposals.Count} treaties,{Ms(proposeTime)},{Ms(proposeTime)},{Ms(proposeTime)},ms,1\n");

        for (var w = 0; w < 4; w++) runtime.DeclareWar("r" + (w * 2).ToString("00", CultureInfo.InvariantCulture), "r" + (w * 2 + 40).ToString("00", CultureInfo.InvariantCulture), "");
        var stepTimes = new List<double>();
        var changes = 0L;
        var peakTreaties = 0;
        var missions = 0;
        var year = Stopwatch.StartNew();
        for (var step = 1; step < 365; step++)
        {
            if (step % 10 == 0)
                foreach (var agent in runtime.Agents.Where(a => a.State == (int)AgentState.Idle).Take(30).ToList())
                {
                    runtime.StartMission(agent.Id, "r" + ((agent.Owner + 7) % Realms).ToString("00", CultureInfo.InvariantCulture), step % 20 == 0 ? "spy" : "sabotage");
                    missions++;
                }
            foreach (var treaty in runtime.Treaties.Where(t => t.Awaiting).ToList()) runtime.Fulfil(treaty.Id, true);
            var one = Stopwatch.StartNew();
            changes += runtime.Advance(step, runtime.CycleSeasonAt, host).Items.Count;
            stepTimes.Add(one.Elapsed.TotalMilliseconds);
            peakTreaties = Math.Max(peakTreaties, runtime.Treaties.Count);
            if (step % 60 == 0) DipSave.Open(set, DipSave.Write(runtime));
        }
        var total = year.Elapsed.TotalMilliseconds;
        stepTimes.Sort();
        Assert.True(peakTreaties >= 190, "at most " + peakTreaties + " treaties were in force at once");
        Assert.True(missions >= 100, "only " + missions + " missions were started");
        csv.Append(CultureInfo.InvariantCulture, $"ordinary step (64 realms 200 treaties 100 agents),{Ms(stepTimes[stepTimes.Count / 2])},{Ms(stepTimes[0])},{Ms(stepTimes[^1])},ms,364\n");
        csv.Append(CultureInfo.InvariantCulture, $"a year of daily steps,{Ms(total)},{Ms(total)},{Ms(total)},ms,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"changes in the year,{changes},{changes},{changes},count,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"most treaties at once,{peakTreaties},{peakTreaties},{peakTreaties},count,1\n");
        csv.Append(CultureInfo.InvariantCulture, $"missions started,{missions},{missions},{missions},count,1\n");

        var bytes = Array.Empty<byte>();
        var write = Time(7, () => bytes = DipSave.Write(runtime));
        csv.Append(CultureInfo.InvariantCulture, $"save write ({bytes.Length / 1024} KB),{Ms(write.Median)},{Ms(write.Min)},{Ms(write.Max)},ms,7\n");
        DipRuntime? reopened = null;
        var open = Time(7, () => reopened = DipSave.Open(set, bytes));
        csv.Append(CultureInfo.InvariantCulture, $"save open,{Ms(open.Median)},{Ms(open.Min)},{Ms(open.Max)},ms,7\n");
        Assert.Equal(runtime.StateDigest(), reopened!.StateDigest());

        var facts = Time(7, () => runtime.Facts("", "r00", "r01"));
        csv.Append(CultureInfo.InvariantCulture, $"facts of one pair,{Ms(facts.Median)},{Ms(facts.Min)},{Ms(facts.Max)},ms,7\n");
        var regard = Time(7, () => { for (var a = 0; a < Realms; a++) for (var b = 0; b < Realms; b++) if (a != b) runtime.RegardOf(a, b); });
        csv.Append(CultureInfo.InvariantCulture, $"regard of all 4032 ordered pairs,{Ms(regard.Median)},{Ms(regard.Min)},{Ms(regard.Max)},ms,7\n");
        var evaluation = Time(7, () => runtime.Evaluate("r00", "r30", "alliance", 0, 0));
        csv.Append(CultureInfo.InvariantCulture, $"one acceptance score,{Ms(evaluation.Median)},{Ms(evaluation.Min)},{Ms(evaluation.Max)},ms,7\n");

        var before = GC.GetAllocatedBytesForCurrentThread();
        runtime.Advance(365, runtime.CycleSeasonAt, host);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        csv.Append(CultureInfo.InvariantCulture, $"allocated by one step,{allocated},{allocated},{allocated},bytes,1\n");

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "scale-results.csv"), csv.ToString());
        // Budgets: a large margin over the Release medians recorded in the document, so a Debug build or a loaded machine still passes and an accidental quadratic does not.
        Assert.True(stepTimes[stepTimes.Count / 2] < 400, "a step took " + stepTimes[stepTimes.Count / 2] + " ms");
        Assert.True(total < 60_000, "the year took " + total + " ms");
        Assert.True(bytes.Length < 16 * 1024 * 1024, "a save is " + bytes.Length + " bytes");
    }

    [Fact]
    public void TheBigWorldIsTheSameEveryTimeItIsLived()
    {
        string Digest()
        {
            var set = DipLoader.Load(BigText());
            var runtime = DipRuntime.Create(set);
            Weave(runtime);
            Fixture.Live(runtime, 120);
            return runtime.StateDigest();
        }
        Assert.Equal(Digest(), Digest());
    }

    [Fact]
    public void AWorldOfSixtyFiveRealmsIsRefused()
    {
        var root = JsonNode.Parse(BigText())!.AsObject();
        var realms = root["realms"]!.AsArray();
        var extra = Fixture.Copy(realms[0]!).AsObject();
        extra["id"] = "extra";
        extra["market"] = "market-extra";
        extra["leader"] = "leader-extra";
        realms.Add(extra);
        Assert.Equal(DipCodes.Range, Fixture.Refusal(() => DipLoader.Load(root.ToJsonString())));
    }
}
