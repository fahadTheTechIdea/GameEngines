using AIEngine;
using GameRuntime.Field.Headless;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The field game rules live in <c>field-ai.json</c>. These tests drive its two behaviours directly, with no game around them, and pin each threshold, each cadence and each
/// amount: what the hand-written quartermaster and commander did, written as cases.
/// </summary>
public sealed class FieldAiBehaviourTests
{
    private const long Budget = 100_000_000_000;

    private static AiRuntime Supply(bool ready = true)
    {
        var runtime = new AiRuntime(FieldAi.Set, 1);
        runtime.Add("q", "quartermaster", 0);
        runtime.SetKey("q", "ready", FactValue.From(ready));
        return runtime;
    }

    private static Dictionary<string, FactValue> Attrs(params (string Name, FactValue Value)[] values) => values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    private static FactValue N(long n) => FactValue.From(n);

    private static void Unit(AiRuntime r, string id, bool supplyable = true, long members = 100, long initial = 100, long fuel = 0, long fuelRoom = 0, string fuelStock = "", string peopleStock = "1:people") =>
        r.Perceive("q", "unit", id, "", Attrs(("supplyable", FactValue.From(supplyable)), ("members", N(members)), ("initial", N(initial)), ("people-stock", FactValue.From(peopleStock)),
            ("fuel", N(fuel)), ("fuel-room", N(fuelRoom)), ("fuel-stock", FactValue.From(fuelStock))), 0);

    private static void Battery(AiRuntime r, string group, int place, string name, long rounds, long capacity, string stock = "1:ammo", bool ammo = true) =>
        r.Perceive("q", "battery", group + "/" + place.ToString("D4"), group, Attrs(("group", FactValue.From(group)), ("battery", FactValue.From(name)), ("ammo", FactValue.From(ammo)),
            ("rounds", N(rounds)), ("capacity", N(capacity)), ("stock", FactValue.From(stock))), 0);

    private static void Stock(AiRuntime r, string id, long held, long tick = 0) => r.Perceive("q", "stock", id, "", Attrs(("held", N(held))), tick);

    private static List<string> Offers(AiRuntime r, long tick = 8) =>
        r.Step(tick, Budget).Intents.Select(i => i.Name + " " + string.Join(' ', i.Args.Select(a => a.Name + "=" + (a.Value.Kind == FactKind.String ? a.Value.Text : a.Value.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture))))).ToList();

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    [InlineData(0, true)]
    public void ABatteryBelowHalfItsLoadIsOfferedWhatIsMissingAndNoOtherBatteryIs(long rounds, bool offered)
    {
        var r = Supply();
        Unit(r, "a1");
        Battery(r, "a1", 0, "gun", rounds, 100);
        Stock(r, "1:ammo", 1000);
        Assert.Equal(offered ? [$"resupply battery=gun group=a1 rounds={100 - rounds}"] : [], Offers(r));
    }

    [Fact]
    public void TheQuartermasterLooksOnlyOnItsBeatAndOnlyWhenTheHostSaysReady()
    {
        List<string> At(long tick, bool ready)
        {
            var r = Supply(ready);
            Unit(r, "a1");
            Battery(r, "a1", 0, "gun", 10, 100);
            Stock(r, "1:ammo", 1000);
            return Offers(r, tick);
        }
        Assert.Single(At(8, true));
        Assert.Single(At(38, true));
        Assert.Single(At(68, true));
        Assert.Empty(At(7, true));
        Assert.Empty(At(9, true));
        Assert.Empty(At(30, true));
        Assert.Empty(At(8, false));
    }

    [Fact]
    public void AnOfferIsNeverMoreThanTheDepotStillHoldsAfterTheEarlierOffersOfTheSameLook()
    {
        var r = Supply();
        Unit(r, "a1");
        Unit(r, "a2");
        Battery(r, "a1", 0, "gun", 0, 25);
        Battery(r, "a1", 1, "mg", 0, 25);
        Battery(r, "a2", 0, "gun", 0, 25);
        Stock(r, "1:ammo", 60);
        Assert.Equal(["resupply battery=gun group=a1 rounds=25", "resupply battery=mg group=a1 rounds=25", "resupply battery=gun group=a2 rounds=10"], Offers(r));
        // The depot is empty now, in the agent belief too: the next look offers nothing until the host tells it otherwise.
        Assert.Empty(Offers(r, 38));
        Stock(r, "1:ammo", 5, tick: 38);
        Assert.Equal(["resupply battery=gun group=a1 rounds=5"], Offers(r, 68));
    }

    [Fact]
    public void BatteriesWithNoAmmunitionAndGroupsOutOfTheFightAreLeftAlone()
    {
        var r = Supply();
        Unit(r, "a1");
        Unit(r, "a2", supplyable: false);
        Battery(r, "a1", 0, "sword", 0, 100, ammo: false);
        Battery(r, "a2", 0, "gun", 0, 100);
        Stock(r, "1:ammo", 1000);
        Assert.Empty(Offers(r));
    }

    [Theory]
    [InlineData(499, "refuel amount=501 group=a1")]
    [InlineData(500, null)]
    public void ATankBelowHalfWhatItsStandingPlatformsHoldIsOfferedFuel(long fuel, string? expected)
    {
        var r = Supply();
        Unit(r, "a1", fuel: fuel, fuelRoom: 1000, fuelStock: "1:fuel");
        Stock(r, "1:fuel", 10_000);
        Stock(r, "1:people", 0);
        Assert.Equal(expected is null ? [] : [expected], Offers(r));
    }

    [Fact]
    public void NoTankMeansNoFuelAndTheFuelOfferIsCappedByTheDepot()
    {
        var r = Supply();
        Unit(r, "a1", fuel: 0, fuelRoom: 0, fuelStock: "");
        Unit(r, "a2", fuel: 0, fuelRoom: 1000, fuelStock: "1:fuel");
        Stock(r, "1:fuel", 300);
        Assert.Equal(["refuel amount=300 group=a2"], Offers(r));
    }

    [Theory]
    [InlineData(74, 100, "reinforce group=a1 members=20")]     // a visit is a fifth of the strength
    [InlineData(75, 100, null)]
    [InlineData(10, 100, "reinforce group=a1 members=20")]
    [InlineData(80, 100, null)]
    [InlineData(1, 4, "reinforce group=a1 members=1")]         // a visit is never less than one person
    public void AGroupBelowThreeQuartersOfItsStrengthIsOfferedAVisitOfNewPeople(long members, long initial, string? expected)
    {
        var r = Supply();
        Unit(r, "a1", members: members, initial: initial);
        Stock(r, "1:people", 10_000);
        Assert.Equal(expected is null ? [] : [expected], Offers(r));
    }

    [Fact]
    public void ThePeopleOfferIsTheSmallestOfAVisitWhatIsMissingAndWhatTheDepotHolds()
    {
        List<string> With(long members, long held)
        {
            var r = Supply();
            Unit(r, "a1", members: members, initial: 100);
            Stock(r, "1:people", held);
            return Offers(r);
        }
        Assert.Equal(["reinforce group=a1 members=20"], With(50, 500));
        Assert.Equal(["reinforce group=a1 members=7"], With(50, 7));
        Assert.Empty(With(50, 0));
        Assert.Equal(["reinforce group=a1 members=3"], With(97 - 23, 3));
    }

    [Fact]
    public void FuelAndPeopleDrawnByTwoGroupsFromOneDepotAreCountedOffAsTheyAreOffered()
    {
        var r = Supply();
        Unit(r, "a1", members: 10, initial: 100, fuel: 0, fuelRoom: 100, fuelStock: "1:fuel");
        Unit(r, "a2", members: 10, initial: 100, fuel: 0, fuelRoom: 100, fuelStock: "1:fuel");
        Stock(r, "1:fuel", 150);
        Stock(r, "1:people", 25);
        Assert.Equal(["refuel amount=100 group=a1", "reinforce group=a1 members=20", "refuel amount=50 group=a2", "reinforce group=a2 members=5"], Offers(r));
    }

    [Fact]
    public void AGroupIsServedBatteriesFirstThenFuelThenPeopleAndGroupsInIdOrder()
    {
        var r = Supply();
        Unit(r, "b1", members: 10, initial: 100, fuel: 0, fuelRoom: 100, fuelStock: "2:fuel", peopleStock: "2:people");
        Unit(r, "a1", members: 10, initial: 100, fuel: 0, fuelRoom: 100, fuelStock: "1:fuel", peopleStock: "1:people");
        Battery(r, "a1", 0, "gun", 0, 10, "1:ammo");
        Battery(r, "b1", 0, "gun", 0, 10, "2:ammo");
        foreach (var depot in new[] { "1", "2" }) { Stock(r, depot + ":ammo", 100); Stock(r, depot + ":fuel", 1000); Stock(r, depot + ":people", 1000); }
        Assert.Equal(
        [
            "resupply battery=gun group=a1 rounds=10", "refuel amount=100 group=a1", "reinforce group=a1 members=20",
            "resupply battery=gun group=b1 rounds=10", "refuel amount=100 group=b1", "reinforce group=b1 members=20",
        ], Offers(r));
    }

    // ---- the commander -------------------------------------------------------------------------------------------------------------------

    private static AiRuntime Command(int side = 1, bool ready = true)
    {
        var r = new AiRuntime(FieldAi.Set, 1);
        r.Add("c", "commander", 0);
        r.SetKey("c", "ready", FactValue.From(ready));
        r.SetKey("c", "distance", N(250));
        r.SetKey("c", "terrain", FactValue.From("hills"));
        foreach (var (id, hostile, standing) in new[] { ("a1", false, true), ("a2", false, true), ("a3", false, false), ("a4", false, true), ("b1", true, true), ("b2", true, false), ("b3", true, true) })
            r.Perceive("c", "group", id, "", Attrs(("hostile", FactValue.From(hostile)), ("standing", FactValue.From(standing))), 0);
        return r;
    }

    [Fact]
    public void EveryStandingOwnGroupFiresAtTheNthStandingFoeRoundTheEndOverTheHeldDistanceAndGround()
    {
        var r = Command();
        var shots = new List<string>();
        for (var tick = 1; tick <= 15; tick++) shots.AddRange(r.Step(tick, Budget).Intents.Select(i => $"{tick} {i.Arg("attacker").Text}>{i.Arg("defender").Text}|{i.Arg("metres").Integer}|{i.Arg("cover").Text}|{i.Arg("terrain").Text}"));
        Assert.Equal(["15 a1>b1|250|open|hills", "15 a2>b3|250|open|hills", "15 a4>b1|250|open|hills"], shots);
    }

    [Fact]
    public void ABeatThatPassedWhileNotReadyIsOrderedAtTheFirstReadyTickAndOnlyOnce()
    {
        var r = Command(ready: false);
        var ticks = new List<long>();
        for (var tick = 1; tick <= 50; tick++)
        {
            if (tick == 20) r.SetKey("c", "ready", FactValue.From(true));
            if (tick == 21) r.SetKey("c", "ready", FactValue.From(false));
            if (tick == 33) r.SetKey("c", "ready", FactValue.From(true));
            ticks.AddRange(r.Step(tick, Budget).Intents.Select(_ => (long)tick).Distinct());
        }
        Assert.Equal([20, 33, 45], ticks);   // the beat at 15 is ordered at 20; the beat at 30 waits for 33; the one at 45 is ordered at 45
        Assert.Equal(FactValue.From(false), r.Key("c", "due"));
    }

    [Fact]
    public void NobodyFiresWhenNoFoeStandsAndTheBeatIsStillSpent()
    {
        var r = Command();
        foreach (var foe in new[] { "b1", "b3" }) r.Perceive("c", "group", foe, "", Attrs(("hostile", FactValue.From(true)), ("standing", FactValue.From(false))), 14);
        var shots = 0;
        for (var tick = 15; tick <= 29; tick++) shots += r.Step(tick, Budget).Intents.Count;
        Assert.Equal(0, shots);
        Assert.Equal(FactValue.From(false), r.Key("c", "due"));   // the beat was taken, not kept for later
    }
}
