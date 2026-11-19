namespace GameRuntime.Field.Headless;

/// <summary>
/// Reads the stock of every squad and depot from the world and compares the squads with what the other owners say: the squad headcount for people,
/// the combat groups for rounds. A group that is destroyed or routed is out of the fight and holds nothing. The stock layer keeps a ledger of facts
/// that two other owners also hold, so this is how a drift between them shows up.
/// </summary>
public sealed record FieldLedger(long PeopleA, long RoundsA, long PeopleB, long RoundsB, long DepotPeopleA, long DepotRoundsA, long DepotPeopleB, long DepotRoundsB, bool Reconciled)
{
    private const string AmmoPrefix = "stock.ammo.";

    public static FieldLedger Read(FieldSession session, FieldForces forces)
    {
        var state = session.Commander.State;
        var world = session.World;
        var ammunition = forces.Groups.SelectMany(StockContent.AmmoResources).Distinct(StringComparer.Ordinal).ToList();

        (long People, long Rounds, bool Agrees) Side(IReadOnlyList<string> groups)
        {
            long people = 0, rounds = 0;
            var agrees = true;
            foreach (var group in groups)
            {
                if (!state.SquadOf.TryGetValue(group, out var squad)) continue;
                var heldPeople = world.Held(squad, StockResources.People);
                var heldRounds = ammunition.Sum(a => world.Held(squad, a));
                var combat = session.Combat.Groups[group];
                var withdrawn = combat.Destroyed || combat.Routed;
                var expectedPeople = withdrawn ? 0 : state.Headcount[squad];
                var expectedRounds = withdrawn ? 0 : combat.Batteries.Where(b => b.Ammo is not null).Sum(b => b.Rounds);
                var heldPlatforms = world.Held(squad, StockResources.Platforms);
                var heldFuel = combat.FuelType is { } fuelType ? world.Held(squad, StockResources.Fuel(fuelType)) : 0;
                agrees &= heldPeople == expectedPeople && heldRounds == expectedRounds
                    && heldPlatforms == (withdrawn ? 0 : combat.Platforms) && heldFuel == (withdrawn ? 0 : combat.Fuel);
                people += heldPeople;
                rounds += heldRounds;
            }
            return (people, rounds, agrees);
        }

        (long People, long Rounds) Depot(int entity) => (world.Held(entity, StockResources.People), ammunition.Sum(a => world.Held(entity, a)));

        var a = Side(forces.SideA);
        var b = Side(forces.SideB);
        var depotA = Depot(world.Depots[0]);
        var depotB = Depot(world.Depots[1]);
        return new FieldLedger(a.People, a.Rounds, b.People, b.Rounds, depotA.People, depotA.Rounds, depotB.People, depotB.Rounds, a.Agrees && b.Agrees);
    }
}
