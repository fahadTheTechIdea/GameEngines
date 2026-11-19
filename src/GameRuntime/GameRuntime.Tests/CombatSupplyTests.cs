using System.Text.Json;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The supply rule of the field game end to end: a depot per side feeds the groups still in the fight with rounds and people, the combat
/// engine says what fitted, the world records exactly that, and the ledger, the squads and combat still agree.
/// </summary>
public sealed class CombatSupplyTests
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <summary>Notes the tick of every round the commander asks for, and whether the stock books were settled just before it did.</summary>
    private sealed class RoundSpy(Func<bool> booksSettled) : IGameComponent
    {
        private long _tick;
        public string Id => "spy";
        public List<long> RoundTicks { get; } = new();
        public int RoundsWithOpenBooks { get; private set; }

        public void Init(in ComponentContext context) =>
            context.Events.Connect<RoundRequested>(_ => { RoundTicks.Add(_tick); if (!booksSettled()) RoundsWithOpenBooks++; });

        public void Update(in UpdateContext context) => _tick = context.Tick;
    }

    private sealed class Run
    {
        public FieldSession Session { get; }
        public FieldForces Forces { get; }
        public Reporter Reporter { get; } = new();
        public List<(string Kind, JsonElement Payload)> Seen { get; } = new();
        /// <summary>What happened to groups and squads, in the order it was heard: "routed:group" and "reinforced:squad".</summary>
        public List<string> Order { get; } = new();
        /// <summary>How many members joined squads in each tick.</summary>
        public SortedDictionary<long, int> ReinforcementsPerTick { get; } = new();
        public RoundSpy Spy { get; }
        public List<PlatformsLost> PlatformLosses { get; } = new();
        public List<FuelBurned> Burned { get; } = new();

        public Run(string[] args, Func<FieldForces, FieldForces>? shape = null)
        {
            var options = FieldOptions.Parse(args);
            Forces = FieldForceSource.Load(options);
            if (shape is not null) Forces = shape(Forces);
            Session = FieldSession.Create(options, Forces, FieldWorld.Create(options, Forces, null), Reporter);
            Spy = new RoundSpy(() => Session.Stock.Settled);
            Session.Runtime.AddComponent(Spy, 240);   // before the commander, so it knows the tick of the round the commander then asks for
            Session.Runtime.Subscribe<ObservationPosted>(e =>
            {
                var payload = JsonDocument.Parse(e.PayloadJson).RootElement.Clone();
                Seen.Add((e.Kind, payload));
                if (e.Kind == "sim.event.squad-reinforced")
                {
                    Order.Add("reinforced:" + payload.GetProperty("squad").GetInt32());
                    ReinforcementsPerTick[e.Tick] = ReinforcementsPerTick.GetValueOrDefault(e.Tick) + 1;
                }
            });
            Session.Runtime.Subscribe<PlatformsLost>(PlatformLosses.Add);
            Session.Runtime.Subscribe<FuelBurned>(Burned.Add);
            Session.Runtime.Subscribe<Standing>(e => { if (e.State == "routed") Order.Add("routed:" + e.Group); });
            Session.Runtime.Init();
        }

        public void Ticks(int count)
        {
            var oneTick = (RuntimeClock.MicrosecondsPerSecond + Session.Runtime.Clock.TicksPerSecond - 1) / Session.Runtime.Clock.TicksPerSecond;
            for (var i = 0; i < count && Session.Runtime.State == RuntimeState.Running; i++) Session.Runtime.Update(oneTick);
        }

        public FieldLedger Ledger => FieldLedger.Read(Session, Forces);
        public long Added => Seen.Where(s => s.Kind == "sim.event.squad-reinforced").Sum(s => (long)s.Payload.GetProperty("added").GetInt32());
    }

    private static string[] Args(int squads, params string[] more) =>
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", squads.ToString(System.Globalization.CultureInfo.InvariantCulture),
         "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", squads.ToString(System.Globalization.CultureInfo.InvariantCulture),
         "--frame-ms", "33", .. more];

    private static string[] TanksAgainstRifles(int tankSquads, int rifleSquads, params string[] more) =>
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.tank-platoon", "--squads-a", tankSquads.ToString(System.Globalization.CultureInfo.InvariantCulture),
         "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", rifleSquads.ToString(System.Globalization.CultureInfo.InvariantCulture),
         "--frame-ms", "33", .. more];

    /// <summary>The tank squads start with a little over half a tank of fuel, so a few marches take them under the line where the quartermaster offers more.</summary>
    private static FieldForces HalfTanks(FieldForces forces) => forces with
    {
        Groups = [.. forces.Groups.Select(g => g.FuelCapacity > 0 ? g with { Fuel = g.FuelCapacity * 55 / 100 } : g)],
    };

    private static long Fuel(Run run, string group) => run.Session.World.Held(run.Session.Commander.State.SquadOf[group], StockResources.Fuel(run.Forces.Groups.Single(g => g.Id == group).FuelType!));

    /// <summary>Every weapon starts at three tenths of a full load, so the supply rule has something to do.</summary>
    private static FieldForces Thin(FieldForces forces) => forces with
    {
        Groups = [.. forces.Groups.Select(g => g with { Batteries = [.. g.Batteries.Select(b => b.Ammo is null ? b : b with { Rounds = b.Capacity * 3 / 10 })] })],
    };

    private static long Rounds(Run run) => run.Session.Combat.Groups.Values.Sum(g => g.Batteries.Where(b => b.Ammo is not null).Sum(b => b.Rounds));

    [Fact]
    public void ADepotTopsUpThinBatteriesWithOnlyWhatItHoldsAndNoRoundIsMadeOrLost()
    {
        var run = new Run(Args(3, "--distance", "1000"), Thin);   // out of range: nothing is fired, so rounds only move
        var issued = Rounds(run);
        run.Ticks(5);
        var l0 = run.Ledger;
        var inField = l0.RoundsA + l0.RoundsB;
        var inDepots = l0.DepotRoundsA + l0.DepotRoundsB;
        Assert.Equal(issued, inField);
        Assert.InRange(inDepots, 1, issued);   // a quarter of what the squads took out
        run.Ticks(200);
        var l1 = run.Ledger;
        Assert.True(l1.Reconciled, "ledger and combat disagree after the supply: " + l1);
        Assert.Equal(inField + inDepots, l1.RoundsA + l1.RoundsB + l1.DepotRoundsA + l1.DepotRoundsB);   // rounds only moved
        Assert.Equal(issued + inDepots - (l1.DepotRoundsA + l1.DepotRoundsB), Rounds(run));              // and combat holds what the ledger says
        Assert.True(l1.DepotRoundsA < l0.DepotRoundsA && l1.DepotRoundsB < l0.DepotRoundsB, "the depots gave nothing");
        Assert.Equal(RuntimeState.Running, run.Session.Runtime.State);
        Assert.Empty(run.Reporter.Reports);
    }

    [Fact]
    public void ABatteryAboveHalfItsLoadAndAGroupAbovePlentyOfStrengthAreLeftAlone()
    {
        // Seven tenths of a full load is not low enough to ask the depot for rounds, and nobody is missing, so nobody is asked for either.
        var run = new Run(Args(3, "--distance", "1000"), forces => forces with
        {
            Groups = [.. forces.Groups.Select(g => g with { Batteries = [.. g.Batteries.Select(b => b.Ammo is null ? b : b with { Rounds = b.Capacity * 7 / 10 })] })],
        });
        run.Ticks(5);
        var before = run.Ledger;
        run.Ticks(200);
        var after = run.Ledger;
        Assert.Equal(before, after);
        Assert.Equal(0, run.Added);
    }

    [Fact]
    public void AReinforcementCostsEightLedgerLinesOfATicksWorkSoASmallBudgetSpreadsThemOut()
    {
        var spread = new Run(Args(10, "--distance", "250", "--seconds", "120", "--stock-lines", "16"));
        spread.Ticks(400);
        Assert.InRange(spread.ReinforcementsPerTick.Values.Max(), 1, 2);                           // sixteen units: two reinforcements a tick at most
        Assert.True(spread.Ledger.Reconciled);
        var wide = new Run(Args(10, "--distance", "250", "--seconds", "120", "--stock-lines", "1000"));
        wide.Ticks(400);
        Assert.True(wide.ReinforcementsPerTick.Values.Max() > 2, "with a big budget the reinforcements arrive together; the small budget is what spread them");
        Assert.True(wide.Ledger.Reconciled);
    }

    [Fact]
    public void ARoundThatFallsDueWhileTheBooksAreOpenGoesAsSoonAsTheyAreSettledAndNeverBeforeTheyAre()
    {
        // Sixty squads need a hundred and eighty units of issuing at eight a tick: the books are open long past the first round due at tick 15.
        var run = new Run(Args(30, "--distance", "250", "--seconds", "120", "--stock-lines", "8"));
        run.Ticks(300);
        Assert.NotEmpty(run.Spy.RoundTicks);
        Assert.True(run.Spy.RoundTicks[0] > 15 && run.Spy.RoundTicks[0] % 15 != 0, "the first round was due at 15 and went at " + run.Spy.RoundTicks[0] + ", the first tick the books were settled");
        Assert.Equal(0, run.Spy.RoundsWithOpenBooks);   // and no round was ever asked for with the books open
        Assert.True(run.Ledger.Reconciled);
        Assert.Empty(run.Reporter.Reports);
    }

    [Fact]
    public void MarchingBurnsFuelThatTheLedgerRecordsAndTheQuartermasterRefillsFromTheDepotWithoutMakingOrLosingAny()
    {
        var run = new Run(TanksAgainstRifles(3, 3, "--distance", "5000"), HalfTanks);
        run.Ticks(5);
        var tanks = new[] { "a1", "a2", "a3" };
        long InTanks() => tanks.Sum(g => Fuel(run, g));
        long InDepot() => run.Session.World.Held(run.Session.World.Depots[0], StockResources.Fuel(run.Forces.Groups.First(g => g.Id == "a1").FuelType!));
        var before = InTanks() + InDepot();
        var depotBefore = InDepot();
        Assert.Equal(tanks.Sum(g => run.Session.Combat.Groups[g].Fuel), InTanks());
        for (var march = 0; march < 4; march++)
        {
            foreach (var g in tanks) run.Session.Runtime.Submit(new MoveRequested("march" + march + g, g, 5000, 600, "open"));
            run.Ticks(3);
        }
        Assert.NotEmpty(run.Burned);
        run.Ticks(200);   // long enough for the quartermaster cycle, the refuelling and the books
        var burned = run.Burned.Sum(b => b.BurnedMilli);
        Assert.True(burned > 0);
        Assert.True(run.Ledger.Reconciled, run.Ledger.ToString());
        Assert.Equal(tanks.Sum(g => run.Session.Combat.Groups[g].Fuel), InTanks());                 // the ledger says what the tanks hold
        Assert.Equal(before - burned, InTanks() + InDepot());                                       // and fuel only left by being burned
        Assert.True(InDepot() < depotBefore, "the depot gave the tanks no fuel");
        Assert.Empty(run.Reporter.Reports);
    }

    [Fact]
    public void PlatformsAndTheFuelTheyCarriedThatAFightDestroysAreRecordedAndAgreeWithCombat()
    {
        // Tanks against tanks: a rifle company cannot hurt armour, another tank can.
        string[] args = ["--pack-a", "arms-modern", "--formation-a", "arms-modern.tank-platoon", "--squads-a", "3", "--pack-b", "arms-modern", "--formation-b", "arms-modern.tank-platoon", "--squads-b", "3",
            "--frame-ms", "33", "--distance", "250", "--seconds", "120"];
        var run = new Run(args);
        run.Ticks(400);
        Assert.NotEmpty(run.PlatformLosses);
        Assert.True(run.PlatformLosses.Sum(l => l.FuelLostMilli) > 0, "tanks went down and their fuel with them");
        Assert.True(run.Ledger.Reconciled, run.Ledger.ToString());
        foreach (var group in run.Forces.SideA)
        {
            var squad = run.Session.Commander.State.SquadOf[group];
            var combat = run.Session.Combat.Groups[group];
            var routedOrDead = combat.Routed || combat.Destroyed;
            Assert.Equal(routedOrDead ? 0 : combat.Platforms, run.Session.World.Held(squad, StockResources.Platforms));
        }
        Assert.Empty(run.Reporter.Reports);
    }

    [Fact]
    public void ADepotWithNothingInItSuppliesNothing()
    {
        var run = new Run(Args(3, "--distance", "1000", "--depot-percent", "0"), Thin);
        var before = Rounds(run);
        run.Ticks(200);
        Assert.Equal(before, Rounds(run));
        Assert.Equal(0, run.Ledger.DepotRoundsA + run.Ledger.DepotRoundsB + run.Ledger.DepotPeopleA + run.Ledger.DepotPeopleB);
        Assert.True(run.Ledger.Reconciled);
    }

    [Fact]
    public void ReinforcementsAreExactlyThePeopleTheDepotsGaveUpAndEverythingStillAgrees()
    {
        var run = new Run(Args(5, "--distance", "250", "--seconds", "120"));
        var reserve = run.Ledger.DepotPeopleA + run.Ledger.DepotPeopleB;
        Assert.Equal(0, reserve);   // nothing is issued before the squads are raised
        run.Ticks(300);
        var ledger = run.Ledger;
        Assert.True(ledger.Reconciled, ledger.ToString());
        Assert.InRange(run.Added, 1, 250);
        Assert.Equal(250 - run.Added, ledger.DepotPeopleA + ledger.DepotPeopleB);   // a quarter of 2 x 500 raised, less what the squads took in
        Assert.Empty(run.Reporter.Reports);
    }

    [Fact]
    public void AGroupThatRoutedHoldsNothingInTheLedgerButItsSquadStillExists()
    {
        var run = new Run(Args(5, "--distance", "250", "--seconds", "120"));
        run.Ticks(300);
        var routed = run.Session.Combat.Groups.Values.Where(g => g.Routed && !g.Destroyed).ToList();
        Assert.NotEmpty(routed);   // the default fight routs groups; if it stops doing so this test needs another scene
        var state = run.Session.Commander.State;
        foreach (var group in routed)
        {
            var squad = state.SquadOf[group.Id];
            Assert.Equal(0, run.Session.World.Held(squad, StockResources.People));
            Assert.True(state.Headcount[squad] > 0);
        }
        Assert.True(run.Ledger.Reconciled);
    }

    [Fact]
    public void ARoutedGroupIsNeverReinforcedAfterItRouted()
    {
        var run = new Run(Args(5, "--distance", "250", "--seconds", "120"));
        run.Ticks(300);
        var state = run.Session.Commander.State;
        var routedGroups = run.Order.Where(o => o.StartsWith("routed:", StringComparison.Ordinal)).Select(o => o[7..]).ToList();
        Assert.NotEmpty(routedGroups);
        foreach (var group in routedGroups)
        {
            var after = run.Order.SkipWhile(o => o != "routed:" + group).Skip(1);
            Assert.DoesNotContain("reinforced:" + state.SquadOf[group], after);
        }
        Assert.Contains(run.Order, o => o.StartsWith("reinforced:", StringComparison.Ordinal));   // and others were reinforced, so the check could fail
    }
}
