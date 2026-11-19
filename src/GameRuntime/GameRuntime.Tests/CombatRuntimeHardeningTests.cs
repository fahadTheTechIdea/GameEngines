using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using CombatEngine;
using GameRuntime.Combat.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The second pass over the combat game layer: the deferred-Update rule observed from a component that runs before combat, request kinds
/// beyond the duel (march, resupply, n-way rounds), attempt numbers, tampered checkpoints, journals across eras, culture, the command line's
/// failure paths, three-group games and a bounded real-time run.
/// </summary>
public sealed class CombatRuntimeHardeningTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("combat-hardening-");

    public void Dispose() => _dir.Delete(true);

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    private sealed record Run(int Exit, string[] Lines, string Error)
    {
        public string Result => Lines.Single(l => l.StartsWith("result ", StringComparison.Ordinal));
        public string Hash => Lines.Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal));
    }

    private Run Play(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = SkirmishHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, error);
        return new Run(exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), error.ToString());
    }

    private static readonly string[] Duel = ["--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband"];
    private static readonly string[] ThreeWay = ["--era", "modern", "--side-a", "arms-modern.rifle-platoon", "--side-b", "arms-modern.tank-platoon", "--side-c", "arms-modern.rifle-company", "--teams", "1,2,1"];

    private static SkirmishSession Session(string[] args, ulong seed = 1)
    {
        var forces = SkirmishForceSource.Load(SkirmishOptions.Parse(args));
        return SkirmishSession.Create(forces, seed, new Reporter());
    }

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Codes { get; } = new();
        public void Report(string code, string message, Exception? exception) => Codes.Add(code);
    }

    private static void Ticks(SkirmishSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++) session.Runtime.Update((RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond);
    }

    /// <summary>A component that runs before combat and writes down what combat looked like when it ran.</summary>
    private sealed class Observer(CombatComponent combat) : IGameComponent
    {
        public List<(long Tick, string Fingerprint, long Health)> Seen { get; } = new();
        public string Id => "observer";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context) => Seen.Add((context.Tick, combat.Fingerprint(), combat.Groups.Values.Sum(g => g.Health)));
    }

    // ---- the deferred rule, seen from outside --------------------------------------------------------------------------------

    [Fact]
    public void ARequestIsNotResolvedWhenItIsDeliveredButWhenCombatRunsSoAnEarlierComponentNeverSeesItsResult()
    {
        var session = Session([.. Duel, "--distance", "2"]);
        var observer = new Observer(session.Combat);
        session.Runtime.AddComponent(observer, 50);   // runs before combat (100) in every tick
        session.Runtime.Init();

        session.Runtime.Submit(new EngagementRequested("host-1", "a", "b", 2, 10, "open", "open", true));
        Ticks(session, 1);
        var afterTick = session.Combat.Groups.Values.Sum(g => g.Health);

        // The request was delivered at the start of tick 1, but combat answers it in its own Update later in that tick: the observer, which ran
        // between delivery and combat's Update, saw the state from before the fight. Resolving in the delivery handler would show it the result.
        Assert.Single(observer.Seen);
        Assert.NotEqual(observer.Seen[0].Health, afterTick);
        Assert.Equal(session.Combat.Groups.Values.Sum(g => g.MemberHealth * g.InitialMembers), observer.Seen[0].Health);
    }

    [Fact]
    public void RefusedRequestsConsumeNoAttemptAndEveryResolvedOrderConsumesExactlyOne()
    {
        var session = Session([.. Duel]);
        session.Runtime.Init();
        Assert.Equal(1, session.Combat.Attempt);
        session.Runtime.Submit(new EngagementRequested("r1", "a", "zzz", 2, 5, "open", "open", false));          // unknown group
        session.Runtime.Submit(new EngagementRequested("r2", "a", "b", 2, 5, "no-such-cover", "open", false));   // unknown cover
        Ticks(session, 1);
        Assert.Equal(1, session.Combat.Attempt);
        Assert.Equal(0, session.Combat.Engagements);

        session.Runtime.Submit(new EngagementRequested("one-way", "a", "b", 2, 5, "open", "open", false));
        Ticks(session, 1);
        Assert.Equal(2, session.Combat.Attempt);
        session.Runtime.Submit(new EngagementRequested("mutual", "a", "b", 2, 5, "open", "open", true));
        Ticks(session, 1);
        Assert.Equal(4, session.Combat.Attempt);
        Assert.Equal(3, session.Combat.Engagements);
    }

    // ---- march, resupply, rounds ---------------------------------------------------------------------------------------------

    [Fact]
    public void AMarchClosesTheDistanceAtTheGroupsSpeedOverTheTerrainAndAResupplyFillsOnlyTheRoom()
    {
        var session = Session(["--era", "modern", "--side-a", "arms-modern.rifle-platoon", "--side-b", "arms-modern.rifle-platoon"]);
        var moved = new List<Moved>();
        var supplied = new List<Resupplied>();
        var refused = new List<EngagementRefused>();
        session.Runtime.Subscribe<Moved>(moved.Add);
        session.Runtime.Subscribe<Resupplied>(supplied.Add);
        session.Runtime.Subscribe<EngagementRefused>(refused.Add);
        session.Runtime.Init();

        session.Runtime.Submit(new MoveRequested("m1", "a", 500, 10, "open"));
        session.Runtime.Submit(new MoveRequested("m2", "a", 500, 10, "road"));
        session.Runtime.Submit(new MoveRequested("m3", "a", 5, 10, "open"));
        session.Runtime.Submit(new MoveRequested("m4", "a", 500, 10, "lava"));
        Ticks(session, 1);
        var step = session.Combat.Groups["a"].SpeedMilli * 10 / 1000;
        Assert.Equal(500 - (int)step, moved[0].ToMetres);
        Assert.Equal((500_000 - session.Combat.Groups["a"].SpeedMilli * 10 * 130 / 100) / 1000, moved[1].ToMetres);
        Assert.Equal(0, moved[2].ToMetres);
        Assert.Equal(CombatCodes.UnknownTerrain, Assert.Single(refused).Code);

        var rifle = session.Combat.Groups["a"].Batteries.First(b => b.Ammo is not null);
        session.Runtime.Submit(new ResupplyRequested("s1", "a", rifle.Id, 50));
        Ticks(session, 1);
        Assert.Equal(0, supplied[0].Accepted);   // a full group has no room
        Assert.Equal(rifle.Capacity, supplied[0].Left);
    }

    [Fact]
    public void AReinforcementBringsAHurtGroupBackToItsStartingStrengthAndARefusalChangesNothing()
    {
        var session = Session(["--era", "modern", "--side-a", "arms-modern.rifle-platoon", "--side-b", "arms-modern.rifle-platoon"]);
        var reinforced = new List<Reinforced>();
        var refused = new List<EngagementRefused>();
        var engagements = new List<Engagement>();
        session.Runtime.Subscribe<Reinforced>(reinforced.Add);
        session.Runtime.Subscribe<EngagementRefused>(refused.Add);
        session.Runtime.Subscribe<Engagement>(engagements.Add);
        session.Runtime.Init();
        var start = session.Combat.Groups["a"].Members;
        // The skirmish ends with the fight, so the three requests are queued together and answered in the order they came.
        session.Runtime.Submit(new EngagementRequested("e1", "b", "a", 800, 1, "fortified", "open", false));
        session.Runtime.Submit(new ReinforceRequested("r1", "a", 1000));
        session.Runtime.Submit(new ReinforceRequested("r2", "nobody", 5));
        Ticks(session, 1);
        Assert.True(reinforced.Count > 0, string.Join(",", refused.Select(r => r.RequestId + ":" + r.Code)) + " " + session.Combat.Groups["a"].Standing + " " + session.Combat.Groups["a"].Health);
        var hurt = start - reinforced[0].Accepted;
        Assert.InRange(reinforced[0].Accepted, 1, start - 1);   // the engagement cost it some members, and no more than it had
        Assert.Equal(start, reinforced[0].Members);
        Assert.Equal(start, session.Combat.Groups["a"].Members);
        Assert.True(engagements[0].DefenderHealth < start * session.Combat.Groups["a"].MemberHealth, $"hurt {hurt}");
        Assert.Equal(CombatComponent.UnknownGroup, Assert.Single(refused).Code);
        Assert.Single(reinforced);   // the refused request answered nothing else
    }

    [Fact]
    public void ANWayRoundGivesEveryOrderItsOwnEngagementAndAddsTheDamage()
    {
        var session = Session(ThreeWay);
        var engagements = new List<Engagement>();
        var refused = new List<EngagementRefused>();
        session.Runtime.Subscribe<Engagement>(engagements.Add);
        session.Runtime.Subscribe<EngagementRefused>(refused.Add);
        session.Runtime.Init();
        var before = session.Combat.Groups.ToDictionary(g => g.Key, g => g.Value.Health);

        session.Runtime.Submit(new RoundRequested("round-1", "a>b|300|open|open;c>b|300|open|open;b>a|300|open|open", 2));
        Ticks(session, 1);
        Assert.Empty(refused);
        Assert.Equal(3, engagements.Count);
        Assert.All(engagements, e => Assert.Equal("round-1", e.RequestId));
        Assert.Equal(3, session.Combat.Engagements);
        Assert.Equal(4, session.Combat.Attempt);
        Assert.True(session.Combat.Groups["b"].Health < before["b"], "two groups fired at b");

        // Friends cannot fire at each other, and a malformed order is a refusal, not a guess.
        session.Runtime.Submit(new RoundRequested("friends", "a>c|100|open|open", 2));
        session.Runtime.Submit(new RoundRequested("garbage", "a-b-c", 2));
        session.Runtime.Submit(new RoundRequested("twice", "a>b|100|open|open;a>b|100|open|open", 2));
        Ticks(session, 1);
        Assert.Equal([CombatCodes.SameSide, CombatCodes.BadRequest, CombatCodes.BadRequest], refused.Select(r => r.Code).ToArray());
        Assert.Equal(3, session.Combat.Engagements);
    }

    // ---- checkpoints: versioned, tamper-proof, bound to their forces -------------------------------------------------------------

    private static byte[] Edit(byte[] bytes, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        change(node);
        return System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    [Fact]
    public void AMarchBurnsTheFuelOfAGroupThatNeedsItAndARefuellingFillsOnlyTheRoom()
    {
        var session = Session(ThreeWay);
        var burned = new List<FuelBurned>();
        var moved = new List<Moved>();
        var refuelled = new List<Refuelled>();
        var refused = new List<EngagementRefused>();
        session.Runtime.Subscribe<FuelBurned>(burned.Add);
        session.Runtime.Subscribe<Moved>(moved.Add);
        session.Runtime.Subscribe<Refuelled>(refuelled.Add);
        session.Runtime.Subscribe<EngagementRefused>(refused.Add);
        session.Runtime.Init();
        var tanks = session.Combat.Groups["b"];
        Assert.Equal(tanks.FuelCapacity, tanks.Fuel);

        session.Runtime.Submit(new MoveRequested("m1", "b", 900, 30, "open"));   // the tank platoon
        session.Runtime.Submit(new MoveRequested("m2", "a", 900, 30, "open"));   // the rifle platoon burns nothing
        Ticks(session, 1);
        var spent = tanks.BurnPerHourMilli * 30 / 3600;
        Assert.Equal(spent, Assert.Single(burned).BurnedMilli);
        Assert.Equal("b", burned[0].Group);
        Assert.Equal(tanks.Fuel - spent, session.Combat.Groups["b"].Fuel);
        Assert.Equal(2, moved.Count);

        session.Runtime.Submit(new RefuelRequested("f1", "b", 10 * spent));
        session.Runtime.Submit(new RefuelRequested("f2", "a", 5));
        Ticks(session, 1);
        Assert.Equal(spent, refuelled[0].AcceptedMilli);                         // only the room there was
        Assert.Equal(tanks.FuelCapacity, session.Combat.Groups["b"].Fuel);
        Assert.Equal(CombatCodes.BadRequest, Assert.Single(refused).Code);        // the rifle platoon burns no fuel
    }

    [Fact]
    public void ACheckpointCarriesThePlatformPoolAndTheFuel()
    {
        var session = Session(ThreeWay);
        session.Runtime.Init();
        // The tank platoon is group b: the rifle platoon (a) and company (c) shoot at it from close range for a while.
        // The skirmish ends with the fight, so the march that burns fuel is queued with the round; the checkpoint carries what is left.
        session.Runtime.Submit(new MoveRequested("m1", "b", 900, 30, "open"));
        session.Runtime.Submit(new RoundRequested("r1", "a>b|120|open|open;c>b|120|open|open", 20));
        Ticks(session, 1);
        var tanks = session.Combat.Groups["b"];
        Assert.InRange(tanks.PlatformHealth, 1, tanks.InitialPlatforms * tanks.PlatformUnitHealth - 1);   // hurt, not untouched and not gone
        var driven = tanks;
        Assert.InRange(driven.Fuel, 1, driven.FuelCapacity - 1);
        var bytes = CombatCheckpoint.Capture(session.Clock.Tick, session.Combat);
        var forces = SkirmishForceSource.Load(SkirmishOptions.Parse(ThreeWay)).Groups;
        Assert.Equal(tanks.PlatformHealth, CombatCheckpoint.Open(forces, bytes).Groups["b"].PlatformHealth);
        Assert.Equal(driven.Fuel, CombatCheckpoint.Open(forces, bytes).Groups["b"].Fuel);
        string Code(byte[] damaged) => Assert.Throws<CombatRefusedException>(() => CombatCheckpoint.Open(forces, damaged)).Code;
        // The pool is in the digest, so changing it alone is a digest failure; a pool or tank the forces cannot hold is refused before the digest is looked at, so a forged digest does not help.
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Groups"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == "b")!["PlatformHealth"] = tanks.PlatformHealth + 1)));
        var cap = tanks.InitialPlatforms * tanks.PlatformUnitHealth;
        Assert.Equal(CombatCheckpoint.ForcesCode, Code(Edit(bytes, n => n["Groups"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == "b")!["PlatformHealth"] = cap + 1)));
        Assert.Equal(CombatCheckpoint.ForcesCode, Code(Edit(bytes, n => n["Groups"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == "b")!["PlatformHealth"] = -1)));
        Assert.Equal(CombatCheckpoint.ForcesCode, Code(Edit(bytes, n => n["Groups"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == "b")!["Fuel"] = tanks.FuelCapacity + 1)));
        Assert.Equal(CombatCheckpoint.ForcesCode, Code(Edit(bytes, n => n["Groups"]!.AsArray().Single(g => g!["Id"]!.GetValue<string>() == "b")!["Fuel"] = -5)));
    }

    [Fact]
    public void EveryKindOfDamageToACheckpointIsRefusedWithItsOwnCode()
    {
        var session = Session(Duel);
        session.Runtime.Init();
        session.Runtime.Submit(new EngagementRequested("e1", "a", "b", 2, 5, "open", "open", true));
        Ticks(session, 1);
        var bytes = CombatCheckpoint.Capture(session.Clock.Tick, session.Combat);
        var forces = SkirmishForceSource.Load(SkirmishOptions.Parse(Duel)).Groups;
        Assert.Equal(session.Clock.Tick, CombatCheckpoint.Open(forces, bytes).Tick);

        string Code(byte[] damaged) => Assert.Throws<CombatRefusedException>(() => CombatCheckpoint.Open(forces, damaged)).Code;
        Assert.Equal(CombatCheckpoint.FormatCode, Code(bytes[..(bytes.Length / 2)]));                                          // truncated
        Assert.Equal(CombatCheckpoint.FormatCode, Code(System.Text.Encoding.UTF8.GetBytes("[]")));                             // not an envelope
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Format"] = 1)));                                    // another version is never migrated
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Format"] = 2)));
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Format"] = 3)));   // the version before battery crews is refused too
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Format"] = 5)));
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Tick"] = -1)));
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Attempt"] = 0)));
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Engagements"] = -1)));
        Assert.Equal(CombatCheckpoint.FormatCode, Code(Edit(bytes, n => n["Distance"] = -2)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Tick"] = 999)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Attempt"] = 99)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Groups"]![0]!["Health"] = 1)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Groups"]![0]!["Morale"] = 1000)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Groups"]![1]!["Batteries"]![0]!["Rounds"] = 5)));
        Assert.Equal(CombatCheckpoint.DigestCode, Code(Edit(bytes, n => n["Digest"] = "00")));

        var otherForces = SkirmishForceSource.Load(SkirmishOptions.Parse(["--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.hunting-band"])).Groups;
        Assert.Equal(CombatCheckpoint.ForcesCode, Assert.Throws<CombatRefusedException>(() => CombatCheckpoint.Open(otherForces, bytes)).Code);
    }

    [Fact]
    public void ACheckpointWithAnOrderStillWaitingIsRefusedAsNotQuiet()
    {
        var session = Session(Duel);
        session.Runtime.Init();
        session.Input.Press(SkirmishActions.Engage, "a>b|2|5|open|open|one-way");
        session.Input.Release(SkirmishActions.Engage);
        Ticks(session, 1);   // the commander raised the order after combat ran: it waits for the next Update
        Assert.False(session.Combat.IsQuiet);
        Assert.False(session.IsSettled);
        Assert.Equal(CombatCheckpoint.NotQuietCode, Assert.Throws<CombatRefusedException>(() => CombatCheckpoint.Capture(session.Clock.Tick, session.Combat)).Code);
        Ticks(session, 1);
        Assert.True(session.Combat.IsQuiet);
        Assert.Equal(1, CombatCheckpoint.Open(SkirmishForceSource.Load(SkirmishOptions.Parse(Duel)).Groups, CombatCheckpoint.Capture(session.Clock.Tick, session.Combat)).Engagements);
    }

    // ---- headless game: eras, culture, failure paths, three groups, real time -----------------------------------------------------

    [Fact]
    public void AJournalOfACrossEraFightReplaysTickForTick()
    {
        string[] cross = ["--pack-a", "arms-modern", "--pack-b", "arms-ancient", "--side-a", "arms-modern.rifle-platoon", "--side-b", "arms-ancient.iron-warband", "--frame-ms", "33"];
        var journal = Path("cross.journal");
        var recorded = Play([.. cross, "--seconds", "7200", "--player", "skirmisher", "--distance", "1500", "--round-seconds", "600", "--journal", journal]);
        Assert.Equal(0, recorded.Exit);
        Assert.Contains(recorded.Lines, l => l.Contains(" moved a "));
        var replay = Play([.. cross, "--replay", journal, "--seconds", "7200"]);
        Assert.Equal(0, replay.Exit);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay.Lines);
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void TheWholeGameOutputIsTheSameUnderAnotherCulture(string culture)
    {
        string[] args = [.. ThreeWay, "--frame-ms", "100", "--seconds", "30", "--player", "melee", "--distance", "400"];
        var expected = Play(args).Hash;
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(expected, Play(args).Hash);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void AThreeGroupGameWithAlliesPlaysToAnEndingWithoutFriendlyFire()
    {
        var run = Play([.. ThreeWay, "--frame-ms", "33", "--seconds", "120", "--player", "melee", "--distance", "400"]);
        Assert.Equal(0, run.Exit);
        Assert.Contains("a=arms-modern.rifle-platoon@arms-modern#1 b=arms-modern.tank-platoon@arms-modern#2 c=arms-modern.rifle-company@arms-modern#1", run.Lines[0]);
        Assert.Contains(run.Lines, l => l.Contains("ended "));
        Assert.DoesNotContain(run.Lines, l => l.Contains("engagement") && (l.Contains(" a>c ") || l.Contains(" c>a ")));
        Assert.DoesNotContain(run.Lines, l => l.Contains("COMBAT_SAME_SIDE"));
        Assert.Equal(3, run.Lines.Count(l => l.StartsWith("final ", StringComparison.Ordinal)));
    }

    [Fact]
    public void AThreeGroupGameSavesAndLoadsLikeAnUninterruptedRun()
    {
        string[] game = [.. ThreeWay, "--frame-ms", "100", "--player", "melee", "--distance", "1200", "--round-seconds", "5", "--seed", "4"];
        var whole = Play([.. game, "--seconds", "300"]);
        Assert.Contains(whole.Lines, l => l.Contains(" ended "));
        var save = Path("three.save");
        var first = Play([.. game, "--seconds", "1", "--save", save]);
        Assert.Equal(0, first.Exit);
        Assert.DoesNotContain("state=Ended", first.Result);
        var second = Play([.. game, "--seconds", "300", "--load", save]);
        Assert.Equal(0, second.Exit);
        Assert.Equal(whole.Result, second.Result);
    }

    [Fact]
    public void ScriptedMarchesResupplyAndRoundsRunThroughTheInputState()
    {
        var script = Path("human.txt");
        File.WriteAllText(script, "at 1 march a|300|10|road\nat 3 resupply a|arms-modern.repeating-rifle|10\nat 5 round 3@a>b|200|open|open;b>a|200|open|open\n");
        var run = Play(["--era", "modern", "--side-a", "arms-modern.rifle-company", "--side-b", "arms-modern.rifle-platoon", "--frame-ms", "100", "--seconds", "8", "--script", script]);
        Assert.Equal(0, run.Exit);
        Assert.Contains(run.Lines, l => l.Contains(" moved a 300m -> "));
        Assert.Contains(run.Lines, l => l.Contains(" resupplied a "));
        Assert.Equal(2, run.Lines.Count(l => l.Contains(" engagement e")));
    }

    [Fact]
    public void EveryFailurePathHasItsExitCodeAndIsReportedThroughTheOneReporter()
    {
        var badRules = Path("bad-rules.json");
        File.WriteAllText(badRules, "{ \"format\": 2 }");
        var cases = new (string[] Args, int Exit, string Code)[]
        {
            ([.. Duel, "--player", "wizard"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
            ([.. Duel, "--bogus", "1"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
            ([.. Duel, "--seconds"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
            ([.. Duel, "--terrain", "lava"], SkirmishHeadlessGame.Refused, "COMBAT_UNKNOWN_TERRAIN"),
            ([.. Duel, "--rules", badRules], SkirmishHeadlessGame.Refused, "COMBAT_RULES_MISSING_FIELD"),
            ([.. Duel, "--rules", Path("missing.json")], SkirmishHeadlessGame.Refused, "COMBAT_FILE"),
            ([.. Duel, "--script", Path("missing.txt")], SkirmishHeadlessGame.Refused, "COMBAT_FILE"),
            ([.. Duel, "--load", Path("missing.save")], SkirmishHeadlessGame.Refused, "COMBAT_FILE"),
            ([.. Duel, "--packs", Path("no-packs")], SkirmishHeadlessGame.Refused, ""),
            (["--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-b", "arms-ancient.iron-warband", "--side-c", "arms-ancient.iron-warband", "--player", "aggressor"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
            (["--era", "ancient", "--side-a", "arms-ancient.iron-warband", "--side-c", "arms-ancient.iron-warband"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
            ([.. Duel, "--teams", "1"], SkirmishHeadlessGame.Refused, "COMBAT_USAGE"),
        };
        foreach (var (args, exit, code) in cases)
        {
            var reporter = new CollectingEngineReporter();
            var error = new StringWriter();
            var actual = SkirmishHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], new StringWriter(), error, null, reporter);
            Assert.True(actual == exit, string.Join(' ', args) + " exited " + actual + ": " + error);
            var notice = Assert.Single(reporter.Notices);
            Assert.Equal(ScenarioEngine.Contracts.Severity.Error, notice.Severity);
            if (code.Length > 0) Assert.Equal(code, notice.Code);
            Assert.NotEmpty(notice.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.ToString()), "the user was told nothing for " + string.Join(' ', args));
        }
    }

    private sealed class CollectingEngineReporter : ScenarioEngine.Contracts.IEngineReporter
    {
        public List<ScenarioEngine.Contracts.EngineNotice> Notices { get; } = new();
        public void Report(ScenarioEngine.Contracts.EngineNotice notice) => Notices.Add(notice);
        public void Audit(ScenarioEngine.Contracts.AuditRecord record) { }
    }

    [Fact]
    public void ABoundedRealTimeRunPlaysThreeGroupsOnTheWallClock()
    {
        var watch = Stopwatch.StartNew();
        var run = Play([.. ThreeWay, "--seconds", "2", "--player", "melee", "--distance", "3000"]);
        watch.Stop();
        Assert.Equal(0, run.Exit);
        Assert.Contains("mode=real-time", run.Lines[0]);
        var ticks = long.Parse(run.Result[(run.Result.IndexOf(" tick=", StringComparison.Ordinal) + 6)..].Split(' ')[0], CultureInfo.InvariantCulture);
        Assert.InRange(ticks, 20, 90);                                   // the groups start out of range so the fight cannot end the run early: about two seconds at 30 ticks a second, with room for a slow machine
        Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 20);             // it really waited for the clock, and did not hang
    }
}
