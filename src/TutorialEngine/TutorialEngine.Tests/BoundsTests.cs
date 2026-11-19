using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Random sequences of calls, good and bad: every reachable state obeys the rules a save checks (so a save of it opens to the same state), nothing leaves its bounds, and the same sequence gives the same state.</summary>
public sealed class BoundsTests
{
    /// <summary>A tiny seeded generator, so a failing sequence can be run again.</summary>
    private sealed class Lcg(ulong seed)
    {
        private ulong _state = seed * 6364136223846793005UL + 1442695040888963407UL;
        public int Next(int below)
        {
            _state = _state * 6364136223846793005UL + 1442695040888963407UL;
            return (int)((_state >> 33) % (ulong)below);
        }
    }

    private static readonly string[] Events = ["moved", "camera_moved", "menu_opened", "structure_built", "road_built", "worker_hired", "dialogue_finished", "trade_made", "bond_made", "saved_game"];

    internal static void Invariants(TutorialRuntime runtime)
    {
        var set = runtime.Set;
        Assert.True(runtime.ActiveSteps().Count() <= set.Limits.MaxActiveSteps);
        Assert.True(runtime.VisibleTips().Count <= set.Limits.MaxVisibleTips);
        Assert.InRange(runtime.ProgressPermille(), 0, 1000);
        Assert.InRange(runtime.TextScale, TutorialRuntime.MinTextScale, TutorialRuntime.MaxTextScale);
        Assert.True(runtime.LastActivity <= runtime.Step && runtime.LastTipShown <= runtime.Step);
        for (var k = 0; k < set.Skills.Count; k++) Assert.InRange(runtime.SkillPoints[k], 0, set.Skills[k].Mastered);
        for (var t = 0; t < set.Tips.Count; t++) Assert.InRange(runtime.Tips[t].Shows, 0, set.Tips[t].MaxShows);
        var bytes = TutorialSave.Write(runtime);
        Assert.Equal(runtime.StateDigest(), TutorialSave.Open(set, bytes).StateDigest());          // the rules a save checks hold for a state the runtime made
    }

    private static string Run(ulong seed, int operations, bool check)
    {
        var random = new Lcg(seed);
        var runtime = TutorialRuntime.Create(Fixture.Valley);
        var step = 0L;
        var gamepad = false; var menu = false; var paused = false; var population = 0L;
        var log = new System.Text.StringBuilder();
        for (var n = 0; n < operations; n++)
        {
            var choice = random.Next(24);
            try
            {
                switch (choice)
                {
                    case 0: case 1: case 2: case 3: runtime.Observe(Events[random.Next(Events.Length)], 1 + random.Next(3)); break;
                    case 4: case 5: case 6: case 7: case 8:
                        step += random.Next(500);
                        gamepad = random.Next(2) == 0; menu = random.Next(2) == 0; paused = random.Next(3) == 0; population = random.Next(40);
                        runtime.Advance(step, Fixture.ValleyHost(gamepad, menu, paused, population));
                        break;
                    case 9: runtime.Touch(); break;
                    case 10: runtime.SkipStep(Fixture.Valley.Steps[random.Next(Fixture.Valley.Steps.Count)].Id); break;
                    case 11: if (random.Next(6) == 0) runtime.SkipTutorial(); break;
                    case 12: if (random.Next(4) == 0) runtime.Replay(); break;
                    case 13: runtime.DismissTip(Fixture.Valley.Tips[random.Next(Fixture.Valley.Tips.Count)].Id); break;
                    case 14: runtime.SelectLanguage(Fixture.Valley.Journey.Languages[random.Next(Fixture.Valley.Journey.Languages.Count)]); break;
                    case 15: runtime.StoryNext(); break;
                    case 16: runtime.StoryBack(); break;
                    case 17: if (random.Next(3) == 0) runtime.StorySkip(); break;
                    case 18: runtime.ChooseEntry(Fixture.Valley.Journey.Entries[random.Next(Fixture.Valley.Journey.Entries.Count)]); break;
                    case 19: if (random.Next(3) == 0) runtime.ReplayStory(); break;
                    case 20: runtime.Start(); break;
                    case 21: runtime.SetTextScale(1000 + random.Next(2001)); runtime.SetReducedMotion(random.Next(2) == 0); runtime.SetTipsEnabled(random.Next(5) != 0); break;
                    case 22: runtime.Resume(); runtime.ResetTips(); break;
                    case 23: runtime.SetIntent(random.Next(2) == 0 ? "" : "join:room." + random.Next(100)); break;
                }
            }
            catch (EngineRefusedException refusal)
            {
                log.Append(refusal.Code).Append(';');    // a refused call is an answer; the state must be exactly as it was, which the invariant below and the determinism check rely on
            }
            if (check) Invariants(runtime);
        }
        return runtime.StateDigest() + "|" + log;
    }

    [Fact]
    public void ThreeHundredRandomSequencesOfEightyCallsKeepEveryInvariantAndEveryStateSavesAndOpens()
    {
        for (ulong seed = 1; seed <= 300; seed++) Run(seed, 80, check: true);
    }

    [Fact]
    public void TheSameSequenceGivesTheSameStateAndTheSameRefusals()
    {
        for (ulong seed = 1000; seed < 1040; seed++) Assert.Equal(Run(seed, 150, false), Run(seed, 150, false));
        Assert.NotEqual(Run(1, 150, false), Run(2, 150, false));
    }

    [Fact]
    public void ARefusedCallLeavesTheStateAsItWas()
    {
        var runtime = Fixture.Ready(Fixture.Valley);
        runtime.Advance(0, Fixture.ValleyHost());
        var before = runtime.StateDigest();
        Assert.Throws<EngineRefusedException>(() => runtime.SkipStep("build"));
        Assert.Throws<EngineRefusedException>(() => runtime.Observe("zzz"));
        Assert.Throws<EngineRefusedException>(() => runtime.SelectLanguage("zz"));
        Assert.Throws<EngineRefusedException>(() => runtime.ChooseEntry("zzz"));
        Assert.Throws<EngineRefusedException>(() => runtime.SetTextScale(5));
        Assert.Throws<EngineRefusedException>(() => runtime.SetIntent("A"));
        Assert.Throws<EngineRefusedException>(() => runtime.MoveTo(-1));
        Assert.Throws<EngineRefusedException>(() => runtime.Start());
        Assert.Equal(before, runtime.StateDigest());
    }

    private static string Continue(ulong seed, bool reopen)
    {
        var random = new Lcg(seed);
        var runtime = Fixture.Ready(Fixture.Valley);
        var step = 0L;
        for (var n = 0; n < 60; n++)
        {
            if (reopen && n == 30) runtime = TutorialSave.Open(Fixture.Valley, TutorialSave.Write(runtime));
            if (random.Next(2) == 0) runtime.Observe(Events[random.Next(Events.Length)]);
            else
            {
                step += random.Next(300);
                runtime.Advance(step, Fixture.ValleyHost(population: random.Next(20)));
            }
        }
        return runtime.StateDigest();
    }

    [Fact]
    public void ASaveOpenedMidSequenceGoesOnExactlyAsTheOriginal()
    {
        for (ulong seed = 500; seed < 540; seed++) Assert.Equal(Continue(seed, false), Continue(seed, true));
    }
}
