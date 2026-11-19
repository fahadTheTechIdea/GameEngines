using Beep.SimEngine;
using Beep.SimEngine.Standard;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>Reinforcing a squad through the character factory: new slots after the old ones, a fresh casualty order, refusals that change nothing, persistence.</summary>
public sealed class SquadReinforcementTests
{
    private static readonly PlayerId Company = new(0);

    private static EntityId Squad(CharacterHarness h, string formation = "chars.platoon", ulong seed = 5) =>
        h.Do(w => h.FactoryOf(w).SpawnFormation(new SpawnFormationRequest(formation, 1, Company, seed, SpawnLayout.Grid(new TileCoord(2, 2), 10, 2))))[0];

    private static (int Total, int Lost, int Headcount, int Aggregate, int Waves) State(CharacterHarness h, EntityId id) =>
        h.Do(w => { var s = w.Entities.Get(id)!.Get<SquadComponent>()!; return (s.Total, s.Lost, s.Headcount, s.Aggregate, s.Waves); });

    private static int[] LostSlots(CharacterHarness h, EntityId id) =>
        h.Do(w => { var s = w.Entities.Get(id)!.Get<SquadComponent>()!; return Enumerable.Range(0, s.Total).Where(s.IsLost).ToArray(); });

    private static void Reinforce(CharacterHarness h, EntityId id, int entry, int count) => h.Do(w => { h.FactoryOf(w).Reinforce(id, entry, count); return 0; });

    private static void Casualties(CharacterHarness h, EntityId id, int count) => h.Do(w => { h.FactoryOf(w).Casualties(id, count); return 0; });

    [Fact]
    public void ReinforcementsAreNewSlotsAfterTheOldOnesOfTheChosenRosterEntry()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);
        var before = h.Do(w => Enumerable.Range(0, 32).Select(s => h.FactoryOf(w).Describe(id, s).Name).ToArray());
        Reinforce(h, id, 1, 5);   // five more sergeants
        Assert.Equal((37, 0, 37, 37, 1), State(h, id));
        var (names, rosterIndexes) = h.Do(w => (Enumerable.Range(0, 37).Select(s => h.FactoryOf(w).Describe(id, s).Name).ToArray(),
            Enumerable.Range(0, 37).Select(s => w.Entities.Get(id)!.Get<SquadComponent>()!.RosterIndexOf(s)).ToArray()));
        Assert.Equal(before, names.Take(32));   // the first thirty-two are who they were
        Assert.All(rosterIndexes.Skip(32), i => Assert.Equal(1, i));
        Assert.Equal(30, rosterIndexes.Count(i => i == 0));
        Assert.Equal(7, rosterIndexes.Count(i => i == 1));
        Assert.Equal(5, names.Skip(32).Distinct().Count());
        // A reinforcement can be made an individual like any member, and it is who Describe said it is.
        var hero = h.Do(w => h.FactoryOf(w).Materialize(id, 34));
        var record = h.Do(w => w.Entities.Get(hero)!.Get<CharacterComponent>()!.Archetype);
        Assert.Equal("chars.sergeant", record);
    }

    [Fact]
    public void ARefusedReinforcementChangesNothing()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h, "chars.patrol");   // six riflemen in a squad that holds at most forty
        Assert.Equal("characters.bad-count", h.Do(w => h.FactoryOf(w).CheckReinforce(id, 0, 0)).Id);
        Assert.Equal("characters.bad-roster-entry", h.Do(w => h.FactoryOf(w).CheckReinforce(id, 1, 1)).Id);
        Assert.Equal("characters.bad-roster-entry", h.Do(w => h.FactoryOf(w).CheckReinforce(id, -1, 1)).Id);
        Assert.Equal("characters.unknown-squad", h.Do(w => h.FactoryOf(w).CheckReinforce(new EntityId(999), 0, 1)).Id);
        Assert.Equal("characters.too-many", h.Do(w => h.FactoryOf(w).CheckReinforce(id, 0, 35)).Id);
        Assert.True(h.Do(w => h.FactoryOf(w).CheckReinforce(id, 0, 34)).IsNone);
        var hash = h.EntitiesHash;
        Assert.Contains("too-many", h.Do(w => { try { h.FactoryOf(w).Reinforce(id, 0, 35); return "added"; } catch (InvalidOperationException e) { return e.Message; } }));
        Assert.Equal((6, 0, 6, 6, 0), State(h, id));
        Assert.Equal(hash, h.EntitiesHash);
    }


    /// <summary>How many of the new slots (after the first thirty-two) belong to each of the platoon two roster entries: riflemen and sergeants.</summary>
    private static (int Riflemen, int Sergeants) Joined(CharacterHarness h, EntityId id, int from)
    {
        var entries = h.Do(w => { var s = w.Entities.Get(id)!.Get<SquadComponent>()!; return Enumerable.Range(from, s.Total - from).Select(s.RosterIndexOf).ToArray(); });
        return (entries.Count(e => e == 0), entries.Count(e => e == 1));
    }

    [Theory]
    [InlineData(1, 1, 0)]     // 0.94 riflemen, 0.06 sergeants: the larger remainder wins
    [InlineData(3, 3, 0)]
    [InlineData(10, 9, 1)]    // 9.375 and 0.625
    [InlineData(16, 15, 1)]
    [InlineData(32, 30, 2)]   // exactly the roster
    [InlineData(100, 94, 6)]  // 93.75 and 6.25
    [InlineData(8, 8, 0)]     // 7.5 and 0.5: a tie goes to the earlier entry
    public void ReinforcementsJoinTheRosterEntriesInTheProportionsTheSquadWasRaisedWith(int count, int riflemen, int sergeants)
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);   // thirty riflemen and two sergeants
        h.Do(w => { h.FactoryOf(w).ReinforceByRoster(id, count); return 0; });
        Assert.Equal((riflemen, sergeants), Joined(h, id, 32));
        Assert.Equal((32 + count, 0, 32 + count, 32 + count), (State(h, id).Total, State(h, id).Lost, State(h, id).Headcount, State(h, id).Aggregate));
    }

    [Fact]
    public void ReinforcingByTheRosterRefusesLikeReinforcingAnEntryAndChangesNothing()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h, "chars.patrol");
        Assert.Equal("characters.bad-count", h.Do(w => h.FactoryOf(w).CheckReinforceByRoster(id, 0)).Id);
        Assert.Equal("characters.unknown-squad", h.Do(w => h.FactoryOf(w).CheckReinforceByRoster(new EntityId(999), 1)).Id);
        Assert.Equal("characters.too-many", h.Do(w => h.FactoryOf(w).CheckReinforceByRoster(id, 35)).Id);
        var hash = h.EntitiesHash;
        Assert.Contains("too-many", h.Do(w => { try { h.FactoryOf(w).ReinforceByRoster(id, 35); return "added"; } catch (InvalidOperationException e) { return e.Message; } }));
        Assert.Equal(hash, h.EntitiesHash);
        h.Do(w => { h.FactoryOf(w).ReinforceByRoster(id, 34); return 0; });   // a one-entry roster takes them all
        Assert.Equal(40, State(h, id).Total);
    }
    [Fact]
    public void AfterReinforcementTheCasualtyOrderCoversEveryMemberAndNeverLosesASlotTwice()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);
        Casualties(h, id, 20);
        var first = LostSlots(h, id);
        Reinforce(h, id, 0, 10);
        Assert.Equal((42, 20, 22, 22, 1), State(h, id));
        Casualties(h, id, 22);   // everyone left, old and new
        Assert.Equal((42, 42, 0, 0, 1), State(h, id));
        Assert.Equal(Enumerable.Range(0, 42), LostSlots(h, id));
        Assert.Subset(LostSlots(h, id).ToHashSet(), first.ToHashSet());
        Assert.Equal("characters.not-enough-members", h.Do(w => h.FactoryOf(w).CheckCasualties(id, 1)).Id);
    }

    [Fact]
    public void ReinforcedMembersAreTakenByTheOrderNotOnlyTheOldOnes()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);
        Reinforce(h, id, 0, 32);   // doubles the squad
        Casualties(h, id, 32);
        var lost = LostSlots(h, id);
        Assert.InRange(lost.Count(s => s >= 32), 8, 24);   // about half of the losses fall on the new members
    }

    [Fact]
    public void AKilledIndividualDoesNotStopTheCasualtyOrderLaterOnItsSlot()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);
        var heroes = Enumerable.Range(0, 32).Select(s => h.Do(w => h.FactoryOf(w).Materialize(id, s))).Take(1).ToArray();
        // Kill the individual first; its slot is lost, and the order meets it later.
        h.Do(w => { h.FactoryOf(w).Kill(heroes[0]); return 0; });
        Casualties(h, id, 31);   // every other member, which walks the whole order including the slot already lost
        Assert.Equal((32, 32, 0, 0, 0), State(h, id));
    }

    [Fact]
    public void TwoGamesGiveTheSameReinforcedSquadTheSameLossesAndASaveContinuesIdentically()
    {
        List<int> Run(CharacterHarness h, EntityId id, bool saveBetween)
        {
            Casualties(h, id, 7);
            Reinforce(h, id, 1, 6);
            Casualties(h, id, 9);
            if (saveBetween) { h = h.Load(h.Game.Save()); }
            Reinforce(h, id, 0, 4);
            Casualties(h, id, 11);
            return LostSlots(h, id).ToList();
        }
        var a = CharacterHarness.Create();
        var b = CharacterHarness.Create();
        var c = CharacterHarness.Create();
        var one = Run(a, Squad(a), false);
        var two = Run(b, Squad(b), false);
        var three = Run(c, Squad(c), true);
        Assert.Equal(one, two);
        Assert.Equal(one, three);
        var other = CharacterHarness.Create();
        Assert.NotEqual(one, Run(other, Squad(other, seed: 6), false));
    }

    [Fact]
    public void ASavedReinforcedSquadLoadsWithTheSameHash()
    {
        var h = CharacterHarness.Create();
        var id = Squad(h);
        Reinforce(h, id, 1, 3);
        Casualties(h, id, 5);
        var loaded = h.Load(h.Game.Save());
        Assert.Equal(h.Hash, loaded.Hash);
        Assert.Equal(State(h, id), State(loaded, id));
    }
}
