using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>Agents and missions: the odds and how they are made, the outcomes and what each does, capture and release, and what a realm comes to know.</summary>
public sealed class EspionageTests
{
    private static DipRuntime World(Action<JsonObject>? edit = null, long seed = 20261029) => Fixture.PlainWorld(root =>
    {
        root["seed"] = seed;
        edit?.Invoke(root);
    });

    // ---- the odds -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheOddsAreTheBaseLessDifficultyPlusHalfTheSkillAboveAverageLessTheTargetsCounterIntelligence()
    {
        var runtime = World();
        var odds = runtime.OddsOf("agent-brass-1", "aurel", "spy");
        Assert.Equal((800, 130), (odds.Success, odds.Exposure));
        Assert.Equal("success 1000 - difficulty 300 + (skill 700 - 500) / 2 - counter-intelligence 0 x 500 / 1000 = 800 (kept within 50 and 950); exposure 200 x (1000 - skill / 2) / 1000 x (1000 + 0) / 1000 = 130, and 39 when it succeeds", odds.Why);
        var weaker = runtime.OddsOf("agent-brass-2", "aurel", "spy");
        Assert.Equal((650, 160), (weaker.Success, weaker.Exposure));
    }

    [Fact]
    public void TraitsAddToSkillAndToTheTargetsCounterIntelligence()
    {
        var runtime = World(root =>
        {
            Fixture.Realm(root, "brass")["traits"] = new JsonArray("shady");     // spyBonus +100
            Fixture.Realm(root, "aurel")["traits"] = new JsonArray("loyal");     // counterIntel 100
        });
        var odds = runtime.OddsOf("agent-brass-1", "aurel", "spy");
        Assert.Equal((800, 132), (odds.Success, odds.Exposure));                 // skill 800: 700 + 150 - 50; exposure 200 x 600 / 1000 x 1100 / 1000
    }

    [Theory]
    [InlineData(0, 1000, 950)]
    [InlineData(1000, 0, 50)]
    [InlineData(500, 500, 500)]
    public void TheChanceOfSuccessIsKeptBetweenFiftyAndNineHundredAndFifty(int difficulty, int skill, int expected)
    {
        var runtime = World(root =>
        {
            Fixture.Mission(root, "spy")["difficulty"] = difficulty;
            root["agents"]!.AsArray()[1]!["skill"] = skill;                       // agent-brass-1
        });
        Assert.Equal(expected, runtime.OddsOf("agent-brass-1", "aurel", "spy").Success);
    }

    [Fact]
    public void ACounterIntelligenceMissionIsAtHomeNeverExposedAndNotHeldBackByCounterIntelligence()
    {
        var runtime = World(root => Fixture.Realm(root, "aurel")["traits"] = new JsonArray("loyal"));
        var odds = runtime.OddsOf("agent-aurel-1", "aurel", "guard");
        Assert.Equal((1000 - 200 + 50, 0), (odds.Success, odds.Exposure));
    }

    [Fact]
    public void TheOddsTextIsInTheExplanation()
    {
        var runtime = World();
        var text = DipExplain.Mission(runtime, "agent-brass-1", "aurel", "spy");
        Assert.StartsWith("mission spy by agent-brass-1 against aurel: succeeds 800 per mille, exposed 130 per mille\n  success 1000 - difficulty 300", text);
        Assert.Equal(DipCodes.UnknownMission, Fixture.Refusal(() => DipExplain.Mission(runtime, "agent-brass-1", "aurel", "nothing")));
        Assert.Equal(DipCodes.UnknownAgent, Fixture.Refusal(() => DipExplain.Mission(runtime, "nobody", "aurel", "spy")));
    }

    // ---- starting a mission ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnAgentSentOnAMissionIsAwayUntilItIsDone()
    {
        var runtime = World();
        var started = runtime.StartMission("agent-brass-1", "aurel", "spy");
        var change = started.Of(ChangeKind.Mission).Single();
        Assert.Equal((MissionChange.Started, 1, 0, 0L), ((MissionChange)change.Sub2, change.A, change.B, change.Index));
        var agent = runtime.Agents[1];
        Assert.Equal((AgentState.Away, 0L), ((AgentState)agent.State, agent.Mission));
        var mission = Assert.Single(runtime.Missions);
        Assert.Equal((1, 0, 5L), (mission.Agent, mission.Target, mission.Due));
        Assert.Equal(DipCodes.Unavailable, Fixture.Refusal(() => runtime.StartMission("agent-brass-1", "dune", "spy")));
        Assert.Empty(Fixture.Live(runtime, 4).Of(ChangeKind.Mission));
        Assert.Single(Fixture.Live(runtime, 5).Of(ChangeKind.Mission));
        Assert.Empty(runtime.Missions);
    }

    [Fact]
    public void StartingAMissionChecksItsArguments()
    {
        var runtime = World(root => root["limits"]!["maxMissions"] = 1);
        Assert.Equal(DipCodes.UnknownAgent, Fixture.Refusal(() => runtime.StartMission("nobody", "aurel", "spy")));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.StartMission("agent-brass-1", "nowhere", "spy")));
        Assert.Equal(DipCodes.UnknownMission, Fixture.Refusal(() => runtime.StartMission("agent-brass-1", "aurel", "nothing")));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.StartMission("agent-brass-1", "brass", "spy")));      // not against oneself
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.StartMission("agent-brass-1", "aurel", "guard")));    // a guard is kept at home
        runtime.StartMission("agent-brass-1", "aurel", "spy");
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.StartMission("agent-brass-2", "aurel", "spy")));
    }

    [Fact]
    public void ANewAgentIsRecruitedIdleAndCanBeSentAtOnce()
    {
        var runtime = World();
        var changes = runtime.Recruit("dune", "agent-dune-2", 900);
        var agent = runtime.Agents[^1];
        Assert.Equal(("agent-dune-2", 3, 900, AgentState.Idle), (agent.Id, agent.Owner, agent.Skill, (AgentState)agent.State));
        Assert.Equal((AgentChange.Recruited, 3, runtime.Agents.Count - 1), ((AgentChange)changes.Of(ChangeKind.Agent).Single().Sub2, changes.Of(ChangeKind.Agent).Single().A, changes.Of(ChangeKind.Agent).Single().Index));
        runtime.StartMission("agent-dune-2", "aurel", "spy");
    }

    [Fact]
    public void RecruitingChecksItsArguments()
    {
        var runtime = World(root => root["limits"]!["maxAgents"] = 7);
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Recruit("nowhere", "a", 5)));
        Assert.Equal(DipCodes.Format, Fixture.Refusal(() => runtime.Recruit("dune", "Bad Id", 5)));
        Assert.Equal(DipCodes.Format, Fixture.Refusal(() => runtime.Recruit("dune", "", 5)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Recruit("dune", "new", -1)));
        Assert.Equal(DipCodes.Terms, Fixture.Refusal(() => runtime.Recruit("dune", "new", 1001)));
        Assert.Equal(DipCodes.AlreadyActive, Fixture.Refusal(() => runtime.Recruit("dune", "agent-dune-1", 5)));
        runtime.Recruit("dune", "new", 0);
        Assert.Equal(DipCodes.Capacity, Fixture.Refusal(() => runtime.Recruit("dune", "another", 1000)));
    }

    // ---- outcomes -------------------------------------------------------------------------------------------------------------------

    /// <summary>The first seed on which the mission has this outcome.</summary>
    private static (DipRuntime Runtime, List<DipChange> Changes) Find(MissionChange wanted, string agent, string target, string mission, Action<JsonObject>? edit = null)
    {
        for (long seed = 1; seed < 4000; seed++)
        {
            var runtime = World(edit, seed);
            runtime.StartMission(agent, target, mission);
            var changes = Fixture.Live(runtime, 12);
            var outcome = changes.Of(ChangeKind.Mission).Where(c => c.Sub2 != (int)MissionChange.Started).Select(c => (MissionChange)c.Sub2).Single();
            if (outcome == wanted) return (runtime, changes);
        }
        throw new Xunit.Sdk.XunitException("no seed gives " + wanted);
    }

    [Fact]
    public void OutcomesHappenAtTheFrequenciesOfTheOdds()
    {
        int success = 0, exposed = 0;
        const int Runs = 600;
        for (long seed = 1; seed <= Runs; seed++)
        {
            var runtime = World(null, seed);
            runtime.StartMission("agent-brass-1", "aurel", "spy");
            var outcome = Fixture.Live(runtime, 6).Of(ChangeKind.Mission).Select(c => (MissionChange)c.Sub2).Single(c => c != MissionChange.Started);
            if (outcome is MissionChange.Succeeded or MissionChange.SucceededExposed) success++;
            if (outcome is MissionChange.SucceededExposed or MissionChange.FailedExposed) exposed++;
        }
        Assert.InRange(success, 420, 540);                      // 80 per cent of 600 is 480, a standard deviation of about 10
        Assert.InRange(exposed, 15, 60);                        // 0.8 x 3.9 per cent and 0.2 x 13 per cent: 5.7 per cent
    }

    [Fact]
    public void TheSameSeedAlwaysGivesTheSameOutcome()
    {
        MissionChange Outcome(long seed)
        {
            var runtime = World(null, seed);
            runtime.StartMission("agent-brass-1", "aurel", "spy");
            return Fixture.Live(runtime, 6).Of(ChangeKind.Mission).Select(c => (MissionChange)c.Sub2).Single(c => c != MissionChange.Started);
        }
        var first = Enumerable.Range(1, 80).Select(s => Outcome(s)).ToList();
        Assert.Equal(first, Enumerable.Range(1, 80).Select(s => Outcome(s)).ToList());
        Assert.Contains(MissionChange.Failed, first);
        Assert.Contains(MissionChange.Succeeded, first);
    }

    [Fact]
    public void TheCasusBelliACapturedAgentGrantsMayBeTheFirstOfTheDocument()
    {
        var (runtime, _) = Find(MissionChange.FailedExposed, "agent-cinder-1", "aurel", "sabotage", root => Fixture.First(root, "casusBelli", "spied"));
        Assert.Equal(0, runtime.Set.CbById["spied"].Index);
        Assert.True(runtime.Holds("aurel", "cinder", "spied"));
        Assert.Contains(runtime.Grants, g => g.Holder == 0 && g.Target == 2 && g.Cb == 0);
    }

    [Fact]
    public void ASuccessfulSpyingMissionRaisesWhatTheOwnerKnowsOfTheTarget()
    {
        var (runtime, changes) = Find(MissionChange.Succeeded, "agent-brass-1", "aurel", "spy");
        Assert.Equal(IntelLevel.Report, runtime.IntelOf("brass", "aurel"));           // 400,000 passes the line of 300,000 but not 600,000
        Assert.Equal(400000, runtime.State.Intel[1 * 5 + 0]);
        Assert.Equal(0, runtime.State.Intel[0 * 5 + 1]);
        var intel = changes.Of(ChangeKind.Intel).Single();
        Assert.Equal((IntelLevel.Report, IntelLevel.None, 1, 0), ((IntelLevel)intel.Index, (IntelLevel)intel.Sub, intel.A, intel.B));
        Assert.Equal(AgentState.Idle, (AgentState)runtime.Agents[1].State);
        Assert.Equal(-1, runtime.Agents[1].Mission);
    }

    [Fact]
    public void KnowledgeFadesAndALevelLostIsAnnounced()
    {
        var runtime = World(root => Fixture.Rules(root, "espionage")["intelDecayPpm"] = 100000);
        runtime.State.Intel[1 * 5 + 0] = 400000;
        var changes = Fixture.Live(runtime, 2).Of(ChangeKind.Intel);
        Assert.Equal(291600, runtime.State.Intel[1 * 5 + 0]);
        var lost = Assert.Single(changes);
        Assert.Equal((2L, IntelLevel.Rumour, IntelLevel.Report), (lost.Step, (IntelLevel)lost.Index, (IntelLevel)lost.Sub));
    }

    [Fact]
    public void ASuccessfulSabotageAsksTheEconomyForAShockAndShakesTheTarget()
    {
        var (runtime, changes) = Find(MissionChange.Succeeded, "agent-aurel-1", "brass", "sabotage");
        var shock = changes.Of(ChangeKind.Effect).Single();
        Assert.Equal(((int)EffectKind.Shock, "sabotage", 1, 0, 1000L, "mission:sabotage"), (shock.Index, shock.Name, shock.A, shock.B, shock.Amount, shock.Source));
        Assert.Equal(650 - 100, runtime.Realms[1].Stability);
    }

    [Fact]
    public void ASuccessfulIncitementShakesTheTargetAndNeverBelowNothing()
    {
        var (runtime, _) = Find(MissionChange.Succeeded, "agent-aurel-1", "brass", "unrest");
        Assert.Equal(650 - 150, runtime.Realms[1].Stability);
        var low = Find(MissionChange.Succeeded, "agent-aurel-1", "brass", "unrest", root => Fixture.Realm(root, "brass")["stability"] = 100);
        Assert.Equal(0, low.Runtime.Realms[1].Stability);
    }

    [Fact]
    public void ASuccessfulGuardRaisesCounterIntelligenceWhichMakesLaterMissionsAgainstTheRealmHarder()
    {
        var (runtime, _) = Find(MissionChange.Succeeded, "agent-aurel-1", "aurel", "guard");
        Assert.Equal(300, runtime.Realms[0].CounterIntel);
        Assert.Equal(800 - 150, runtime.OddsOf("agent-brass-1", "aurel", "spy").Success);        // 300 x 500 / 1000 = 150 less
    }

    [Fact]
    public void CounterIntelligenceFades()
    {
        var runtime = World(root => Fixture.Rules(root, "espionage")["counterDecayPpm"] = 500000);
        runtime.State.Realms[0].CounterIntel = 300;
        Fixture.Live(runtime, 1);
        Assert.Equal(75, runtime.Realms[0].CounterIntel);
    }

    [Theory]
    [InlineData(MissionChange.FailedExposed)]
    [InlineData(MissionChange.SucceededExposed)]
    public void AnExposedAgentIsCapturedAndTheOwnerPaysForIt(MissionChange outcome)
    {
        var (runtime, changes) = Find(outcome, "agent-cinder-1", "aurel", "sabotage");
        var agent = runtime.Agents[3];
        Assert.Equal(AgentState.Captured, (AgentState)agent.State);
        Assert.Equal(-1, agent.Mission);
        var exposedStep = changes.Of(ChangeKind.Agent).Single(c => c.Sub2 == (int)AgentChange.Captured).Step;
        Assert.Equal(exposedStep + 20, agent.FreeAt);
        Assert.Equal(-100, Fixture.Standing(runtime, "cinder", "honour"));
        Assert.Equal(-400 * (100 - (runtime.Step - exposedStep)) / 100, runtime.RegardOf("aurel", "cinder"));      // the deed of spying, fading since
        Assert.True(runtime.Holds("aurel", "cinder", "spied"));
        Assert.Equal([550, 550 - 80], runtime.Realms[2].Satisfaction);
        var incident = changes.Of(ChangeKind.Incident).Single();
        Assert.Equal((IncidentKind.SpyExposed, 2, 0), ((IncidentKind)incident.Sub, incident.A, incident.B));
    }

    [Fact]
    public void ACapturedAgentIsReleasedWhenTheDaysAreOverAndCannotBeSentMeanwhile()
    {
        var (runtime, changes) = Find(MissionChange.FailedExposed, "agent-cinder-1", "aurel", "sabotage");
        var agent = runtime.Agents[3];
        Assert.Equal(DipCodes.Unavailable, Fixture.Refusal(() => runtime.StartMission("agent-cinder-1", "aurel", "spy")));
        var free = agent.FreeAt;
        Assert.Empty(Fixture.Live(runtime, free - 1).Of(ChangeKind.Agent));
        var released = Fixture.Live(runtime, free).Of(ChangeKind.Agent).Single();
        Assert.Equal((AgentChange.Released, free), ((AgentChange)released.Sub2, released.Step));
        Assert.Equal(AgentState.Idle, (AgentState)agent.State);
        runtime.StartMission("agent-cinder-1", "aurel", "spy");
    }

    [Fact]
    public void ACapturedAgentCanBeReleasedEarlyAndOnlyIfItIsHeld()
    {
        var (runtime, _) = Find(MissionChange.FailedExposed, "agent-cinder-1", "aurel", "spy");
        var changes = runtime.Release("agent-cinder-1");
        var released = changes.Of(ChangeKind.Agent).Single();
        Assert.Equal((AgentChange.Released, 2, 3), ((AgentChange)released.Sub2, released.A, released.Index));
        Assert.Equal(DipCodes.Unavailable, Fixture.Refusal(() => runtime.Release("agent-cinder-1")));
        Assert.Equal(DipCodes.UnknownAgent, Fixture.Refusal(() => runtime.Release("nobody")));
        Assert.Equal(DipCodes.Unavailable, Fixture.Refusal(() => runtime.Release("agent-aurel-1")));
    }

    [Fact]
    public void AnAgentNotExposedComesHomeAtOnceAndNothingIsHeld()
    {
        var (runtime, changes) = Find(MissionChange.Failed, "agent-brass-1", "aurel", "spy");
        Assert.Equal(AgentState.Idle, (AgentState)runtime.Agents[1].State);
        Assert.Empty(changes.Of(ChangeKind.Incident));
        Assert.Empty(runtime.Grants);
        Assert.Equal(0, runtime.RegardOf("aurel", "brass"));
    }

    // ---- what a realm believes ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, IntelLevel.None)]
    [InlineData(99999, IntelLevel.None)]
    [InlineData(100000, IntelLevel.Rumour)]
    [InlineData(299999, IntelLevel.Rumour)]
    [InlineData(300000, IntelLevel.Report)]
    [InlineData(600000, IntelLevel.Detailed)]
    [InlineData(899999, IntelLevel.Detailed)]
    [InlineData(900000, IntelLevel.Complete)]
    [InlineData(1000000, IntelLevel.Complete)]
    public void ALevelOfKnowledgeBeginsAtItsLine(int intel, IntelLevel level)
    {
        var runtime = World();
        runtime.State.Intel[1 * 5 + 0] = intel;
        Assert.Equal(level, runtime.IntelOf("brass", "aurel"));
    }

    [Fact]
    public void WithCompleteKnowledgeTheEstimateIsExactAndWithNoneTheBandIsTheWholeOfTheValueAndMore()
    {
        var runtime = World();
        runtime.State.Intel[1 * 5 + 0] = 1_000_000;
        var exact = runtime.Estimate("brass", "aurel");
        Assert.Equal((IntelLevel.Complete, 500000L, 500000L, 500000L), (exact.Level, exact.Low, exact.Estimate, exact.High));
        runtime.State.Intel[1 * 5 + 0] = 0;
        var blind = runtime.Estimate("brass", "aurel");
        Assert.Equal(IntelLevel.None, blind.Level);
        Assert.Equal(0, blind.Low);
        Assert.Equal(blind.Estimate * 2, blind.High);
    }

    [Theory]
    [InlineData(100000, 500)]
    [InlineData(300000, 250)]
    [InlineData(600000, 100)]
    public void TheBandAroundAnEstimateIsTheDocumentsErrorForTheLevel(int intel, int error)
    {
        var runtime = World();
        runtime.State.Intel[1 * 5 + 0] = intel;
        var estimate = runtime.Estimate("brass", "aurel");
        Assert.Equal(estimate.Estimate * (1000 - error) / 1000, estimate.Low);
        Assert.Equal(estimate.Estimate * (1000 + error) / 1000, estimate.High);
        Assert.InRange(estimate.Estimate, 500000L * (1000 - error) / 1000, 500000L * (1000 + error) / 1000);
    }

    [Fact]
    public void TheEstimateIsTheSameUntilTheEpochTurnsAndDiffersBetweenEpochs()
    {
        var runtime = World();
        var seen = new HashSet<long>();
        for (var epoch = 0; epoch < 12; epoch++)
        {
            Fixture.Live(runtime, epoch * 30);
            var first = runtime.Estimate("brass", "aurel");
            Fixture.Live(runtime, epoch * 30 + 29);
            Assert.Equal(first, runtime.Estimate("brass", "aurel"));
            seen.Add(first.Estimate);
        }
        Assert.True(seen.Count >= 6, "the estimate should move from epoch to epoch");
    }

    [Fact]
    public void ARealmCannotEstimateItself()
    {
        var runtime = World();
        Assert.Equal(DipCodes.SameRealm, Fixture.Refusal(() => runtime.Estimate("aurel", "aurel")));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.Estimate("aurel", "nobody")));
        Assert.Equal(DipCodes.UnknownRealm, Fixture.Refusal(() => runtime.IntelOf("nobody", "aurel")));
    }
}
