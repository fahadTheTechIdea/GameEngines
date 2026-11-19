using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>
/// A seeded stream of calls, valid and not, made to a world while it lives: the world must give the same answers every time, a save at any step must open (every state the engine reaches is a state its save rules allow),
/// a resumed run must equal the straight one, and its bounds must hold after every step.
/// </summary>
public sealed class DeterminismTests
{
    private static readonly string[] Types = ["nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage", "embargo"];
    private static readonly string[] Missions = ["spy", "sabotage", "unrest", "guard"];
    private static readonly string[] Sources = ["battle", "occupation", "blockade", "event", "luck"];
    private static readonly string[] Events = ["war_won", "disaster", "tribute_missed", "earthquake"];
    private static readonly string[] Cbs = ["", "rivalry", "border", "balance", "claim", "betrayed", "spied", "nothing"];

    /// <summary>A run: the lines it printed, the codes of the calls it refused, and the digest at the end.</summary>
    internal sealed record Run(List<string> Lines, List<string> Refusals, string Digest);


    /// <summary>One random call before the given step.</summary>
    private static DipChanges Call(DipRuntime runtime, ulong seed, long step, int n)
    {
        var set = runtime.Set;
        long R(string name, long bound) => DeterministicRoll.Below(seed, name + step + "/" + n, 0, bound);
        string Pick(string name) => set.Realms[(int)R(name, set.Realms.Count)].Id;
        switch (R("kind", 20))
        {
            case 0: return runtime.Propose(Pick("a"), Pick("b"), Types[R("t", Types.Length)], (int)R("d", 4) switch { 0 => 0, 1 => 20, 2 => 45, _ => 90 }, R("amt", 3) switch { 0 => 0, 1 => 2000, _ => -1500 });
            case 1: return runtime.Respond(R("p", 6), R("y", 2) == 0);
            case 2: return runtime.Withdraw(R("p", 6), Pick("a"));
            case 3: return runtime.Cancel(R("tr", 6), Pick("a"));
            case 4: return runtime.DeclareWar(Pick("a"), Pick("b"), Cbs[R("cb", Cbs.Length)]);
            case 5: return runtime.JoinWar(R("w", 3), Pick("a"), Pick("b"));
            case 6: return runtime.LeaveWar(R("w", 3), Pick("a"));
            case 7: return runtime.Score(R("w", 3), Pick("a"), (int)R("s", 1001), Sources[R("src", Sources.Length)]);
            case 8: return runtime.Fulfil(R("tr", 6), R("y", 3) != 0);
            case 9: return runtime.StartMission(set.Agents[(int)R("ag", set.Agents.Count)].Id, Pick("b"), Missions[R("m", Missions.Length)]);
            case 10: return runtime.Recruit(Pick("a"), "new-" + R("n", 5), (int)R("sk", 1000));
            case 11: return runtime.Release(set.Agents[(int)R("ag", set.Agents.Count)].Id);
            case 12: return runtime.GrantCasusBelli(Pick("a"), Pick("b"), new[] { "border", "balance", "claim", "betrayed", "spied", "rivalry" }[R("cb", 6)], (int)R("days", 40) + 1);
            case 13: return runtime.Remember(Pick("a"), Pick("b"), new[] { "gift", "insult", "betrayal", "spying", "nothing" }[R("dd", 5)], (int)R("sc", 1000) + 1);
            case 14: return runtime.Happen(Pick("a"), Events[R("ev", Events.Length)], (int)R("sc", 1000) + 1);
            case 15: return runtime.Succession(Pick("a"));
            case 16: return runtime.SetMight(Pick("a"), R("mg", 800_000));
            case 17: return runtime.SetLeaderRegard(Pick("a"), Pick("b"), (int)R("lr", 2001) - 1000);
            case 18: return runtime.Propose(Pick("a"), Pick("b"), "embargo", (int)R("d", 40) + 10, 0);
            case 19: return runtime.Propose(Pick("a"), Pick("b"), Types[R("t", 3)], 30, 0);
        }
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>Plays <paramref name="steps"/> steps: some calls before each, then the step. A save is written and opened every <paramref name="saveEvery"/> steps and the play goes on with the reopened world.</summary>
    internal static Run Play(DipSet set, ulong seed, int steps, int saveEvery, Action<DipRuntime>? check = null)
    {
        var runtime = DipRuntime.Create(set);
        var lines = new List<string>();
        var refusals = new List<string>();
        for (var step = 0; step < steps; step++)
        {
            var calls = (int)DeterministicRoll.Below(seed, "calls" + step, 0, 4);
            for (var n = 0; n < calls; n++)
            {
                try { lines.AddRange(Call(runtime, seed, step, n).Items.Select(c => DipChangeText.Line(set, c))); }
                catch (EngineRefusedException refusal) { refusals.Add(step + ":" + n + ":" + refusal.Code); }
            }
            lines.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host(step / 40 % 11)).Items.Select(c => DipChangeText.Line(set, c)));
            check?.Invoke(runtime);
            if (saveEvery > 0 && step % saveEvery == saveEvery - 1) runtime = DipSave.Open(set, DipSave.Write(runtime));
        }
        return new Run(lines, refusals, runtime.StateDigest());
    }

    private static DipSet Busy() => Fixture.Edited(root =>
    {
        // Lively: members join and wars tire quickly, so a few hundred steps reach most of what the engine does.
        Fixture.Rules(root, "balance")["interval"] = 10;
        Fixture.Rules(root, "balance")["threatLine"] = 250;
        Fixture.Rules(root, "balance")["relaxLine"] = 150;
        Fixture.Rules(root, "war")["exhaustPpm"] = 20000;
        Fixture.Rules(root, "war")["callBase"] = 500;
        foreach (var realm in root["realms"]!.AsArray()) realm!["successionPpm"] = 3000;
        foreach (var type in root["types"]!.AsArray()) type!["considerDays"] = Math.Min(type["considerDays"]!.GetValue<int>(), 2);
        foreach (var id in new[] { "nap", "trade", "alliance", "vassalage", "peace", "ceasefire", "marriage" }) Fixture.Decided(root, id, id != "peace");
        root["limits"]!["maxIncidents"] = 20;
        root["limits"]!["maxGrants"] = 12;
    });

    [Fact]
    public void ARunIsTheSameEveryTimeItIsPlayed()
    {
        var set = Busy();
        var first = Play(set, 11, 200, 0);
        var second = Play(set, 11, 200, 0);
        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.Refusals, second.Refusals);
        Assert.Equal(first.Digest, second.Digest);
        Assert.NotEqual(first.Digest, Play(set, 12, 200, 0).Digest);
        Assert.True(first.Lines.Count > 150, "the play was too quiet to prove anything: " + first.Lines.Count + " lines");
        Assert.True(first.Refusals.Count > 50, "the play refused too little to prove anything: " + first.Refusals.Count);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    [InlineData(7UL)]
    [InlineData(8UL)]
    public void ASaveAtAnyStepOpensAndTheRunResumedFromItIsTheStraightRun(ulong seed)
    {
        var set = Busy();
        var straight = Play(set, seed, 150, 0);
        var resumed = Play(set, seed, 150, 7);
        Assert.Equal(straight.Lines, resumed.Lines);
        Assert.Equal(straight.Refusals, resumed.Refusals);
        Assert.Equal(straight.Digest, resumed.Digest);
    }

    [Fact]
    public void TheBoundsOfTheStateHoldAfterEveryStepOfARandomRun()
    {
        var set = Busy();
        for (ulong seed = 20; seed < 28; seed++)
            Play(set, seed, 150, 0, runtime =>
            {
                var s = runtime.State;
                Assert.True(s.Treaties.Count <= set.Limits.MaxTreaties && s.Proposals.Count <= set.Limits.MaxProposals && s.Wars.Count <= set.Limits.MaxWars);
                Assert.True(s.Calls.Count <= set.Limits.MaxCalls && s.Grants.Count <= set.Limits.MaxGrants && s.Missions.Count <= set.Limits.MaxMissions && s.Incidents.Count <= set.Limits.MaxIncidents);
                Assert.True(s.Agents.Count <= set.Limits.MaxAgents);
                Assert.All(s.Realms, r =>
                {
                    Assert.InRange(r.Stability, 0, 1000);
                    Assert.InRange(r.Legitimacy, 0, 1000);
                    Assert.InRange(r.Trust, -1_000_000, 1_000_000);
                    Assert.InRange(r.Infamy, 0, 1_000_000);
                });
                Assert.All(s.Wars, w => Assert.Equal(0UL, w.AttackerSide & w.DefenderSide));
                Assert.Equal(s.Treaties.Count, s.Treaties.Select(t => (Math.Min(t.First, t.Second), Math.Max(t.First, t.Second), t.Type)).Distinct().Count());
                Assert.All(s.Calls, c => Assert.Contains(s.Wars, w => w.Id == c.War));
                Assert.All(s.Missions, m => Assert.Equal((int)AgentState.Away, s.Agents[m.Agent].State));
                Assert.Equal(s.Agents.Count(a => a.State == (int)AgentState.Away), s.Missions.Count);
                // Every state the engine reaches is one its own save accepts.
                DipSave.Open(set, DipSave.Write(runtime));
            });
    }

    [Fact]
    public void TheRandomPlayReachesMostOfWhatTheEngineDoes()
    {
        var set = Busy();
        var lines = Enumerable.Range(1, 8).SelectMany(seed => Play(set, (ulong)seed, 150, 0).Lines).ToList();
        var kinds = lines.Select(l => l.Split(' ')[1]).Distinct().OrderBy(k => k).ToList();
        Assert.Equal(["agent", "casus", "coalition", "effect", "incident", "intel", "mission", "obligation", "politics", "proposal", "season", "treaty", "war"], kinds);
        string[] words = ["accepted", "expired", "broken", "ended", "declared", "joined", "formed", "succeeded", "failed", "called", "ignored", "missed", "revolt"];
        var missing = words.Where(w => !lines.Any(l => l.Contains(" " + w))).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void LivingStepByStepIsLivingInOneCall()
    {
        var set = Busy();
        var oneByOne = DipRuntime.Create(set);
        var together = DipRuntime.Create(set);
        foreach (var runtime in new[] { oneByOne, together })
        {
            runtime.Propose("aurel", "brass", "alliance", 0, 0);
            runtime.DeclareWar("ember", "dune", "");
        }
        var a = new List<string>();
        for (var step = 0; step <= 300; step++) a.AddRange(oneByOne.Advance(step, oneByOne.CycleSeasonAt, Fixture.Host()).Items.Select(c => DipChangeText.Line(set, c)));
        var b = together.Advance(300, together.CycleSeasonAt, Fixture.Host()).Items.Select(c => DipChangeText.Line(set, c)).ToList();
        Assert.Equal(a, b);
        Assert.Equal(oneByOne.StateDigest(), together.StateDigest());
    }

    [Fact]
    public void ADifferentSeedGivesADifferentWorldFromTheSameCalls()
    {
        string Digest(long seed)
        {
            var runtime = DipRuntime.Create(Fixture.Edited(root =>
            {
                root["seed"] = seed;
                foreach (var realm in root["realms"]!.AsArray()) realm!["successionPpm"] = 20000;
            }));
            runtime.Propose("aurel", "dune", "nap", 90, 0);
            Fixture.Live(runtime, 200);
            return runtime.StateDigest();
        }
        Assert.Equal(Digest(1), Digest(1));
        Assert.NotEqual(Digest(1), Digest(2));
    }
}
