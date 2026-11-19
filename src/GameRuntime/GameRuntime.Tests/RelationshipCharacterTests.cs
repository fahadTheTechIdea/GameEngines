using Beep.SimEngine;
using Beep.SimEngine.Standard;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The people of the character factory are the actors of the relationships (R15): their ids come from the factory, their trait ids are the factory's, and what a trait does comes from the relationship document.</summary>
public sealed class RelationshipCharacterTests
{
    private static readonly PlayerId Company = new(0);

    private const string People = """
    { "format": 3, "id": "people", "seed": 1, "time": { "stepsPerDay": 4 },
      "limits": { "maxActors": 200, "maxEdgesPerActor": 8, "maxMemoriesPerEdge": 4, "maxMoodlets": 4 },
      "axes": [ { "id": "respect", "key": "k.respect", "min": -100, "max": 100, "baseline": 0, "driftPerDay": 1 } ],
      "needs": [ { "id": "morale", "key": "k.morale", "max": 100, "initial": 80, "decayPerDay": 20, "critical": 20, "rearm": 40, "urgency": [ [0, 1000], [100, 0] ], "decayModifiers": [] } ],
      "sources": [],
      "mood": { "min": -100, "max": 100, "base": 0, "hysteresis": 5, "needWeights": [ { "need": "morale", "weight": 100, "neutral": 50 } ], "states": [ { "id": "low", "key": "k.low", "from": -100 }, { "id": "high", "key": "k.high", "from": 0 } ] },
      "moodlets": [], "memories": [],
      "traits": [
        { "id": "chars.trait.brave", "moodBase": 10, "needDecay": [ { "need": "morale", "permille": 500 } ], "moodScale": [], "axisScale": [] },
        { "id": "chars.trait.careful", "moodBase": 0, "needDecay": [], "moodScale": [], "axisScale": [] },
        { "id": "chars.trait.reckless", "moodBase": -10, "needDecay": [ { "need": "morale", "permille": 1500 } ], "moodScale": [], "axisScale": [] },
        { "id": "chars.trait.steady", "moodBase": 5, "needDecay": [], "moodScale": [], "axisScale": [] } ],
      "ladders": [], "bonds": [], "gifts": [], "tastes": [ 0, 0, 1000, 1000, 1000 ],
      "gossip": { "trustAxis": "respect", "minTrust": 0, "maxTellings": 0, "maxListeners": 0 },
      "social": { "communities": { "enabled": false, "axis": "respect", "above": 10, "maxRounds": 4 }, "standing": { "enabled": false, "axis": "respect", "above": 0, "alphaPermille": 150, "iterations": 4, "preTrustTrait": "", "eventDelta": 100 },
                  "balance": { "enabled": false, "axis": "respect", "like": 20, "dislike": -20, "memory": "", "strengthPermille": 500, "topK": 2, "maxPerActor": 1, "chancePermille": 1000 }, "schedule": "atomic" },
      "interactions": [], "facts": [] }
    """;

    private static (CharacterHarness Harness, IReadOnlyList<(string Id, string[] Traits)> People, IReadOnlyList<string> Vocabulary) Spawn(ulong seed, int count = 40)
    {
        var h = CharacterHarness.Create();
        var ids = h.Do(w => h.FactoryOf(w).SpawnIndividuals(new SpawnIndividualsRequest("chars.rifleman", count, Company, seed, SpawnLayout.Grid(new TileCoord(2, 2), 10, 2))));
        var people = h.Do(w => ids.Select(id => w.Entities.Get(id)!.Get<ICharacter>()!).Select(c => (RelationshipCharacters.ActorId(c), c.Traits.ToArray())).ToList());
        var vocabulary = h.Do(w => w.Content.All<CharacterTraitDef>().Select(t => t.Id).ToList());
        return (h, people, vocabulary);
    }

    /// <summary>The characters of the world, read inside a tick of the world (the only way in) and handed over; the relationships are then filled outside it.</summary>
    private static List<ICharacter> Characters(CharacterHarness h) => h.Do(w => w.Entities.All.Select(e => e.Get<ICharacter>()).Where(c => c is { Archetype.Length: > 0 }).Select(c => c!).OrderBy(c => c.Index).ToList());

    private static RelationshipRuntime Populate(CharacterHarness h, RelationshipSet set, out IReadOnlyList<string> ids)
    {
        var runtime = RelationshipRuntime.Create(set);
        ids = RelationshipCharacters.AddActors(runtime, Characters(h));
        return runtime;
    }

    [Fact]
    public void EveryTraitTheDocumentHasEffectsForIsOneOfTheGamesTraitsAndAMisspeltOneIsRefused()
    {
        var (_, _, vocabulary) = Spawn(9, 2);
        Assert.Contains("chars.trait.brave", vocabulary);
        var set = RelationshipLoader.Load(People);
        RelationshipCharacters.CheckTraits(set, vocabulary);
        var misspelt = RelationshipLoader.Load(People.Replace("chars.trait.careful", "chars.trait.carefull", StringComparison.Ordinal));
        var refusal = Assert.Throws<EngineRefusedException>(() => RelationshipCharacters.CheckTraits(misspelt, vocabulary));
        Assert.Equal(RelationshipCharacters.UnknownTrait, refusal.Code);
        Assert.Contains("chars.trait.carefull", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => RelationshipCharacters.CheckTraits(set, null!));
    }

    [Fact]
    public void ADocumentMayLeaveSomeOfTheGamesTraitsWithoutEffects()
    {
        var (_, _, vocabulary) = Spawn(9, 2);
        var node = System.Text.Json.Nodes.JsonNode.Parse(People)!.AsObject();
        node["traits"]!.AsArray().RemoveAt(1);
        var fewer = RelationshipLoader.Load(node.ToJsonString());
        Assert.Equal(3, fewer.Traits.Count);
        RelationshipCharacters.CheckTraits(fewer, vocabulary);
    }

    [Fact]
    public void ACharacterBecomesAnActorWithAnIdMadeOfItsArchetypeSeedAndIndexAndItsOwnTraits()
    {
        var (h, people, _) = Spawn(9);
        var runtime = Populate(h, RelationshipLoader.Load(People), out var ids);
        Assert.Equal(40, ids.Count);
        Assert.Equal(40, runtime.Actors.Count);
        Assert.StartsWith("chars.rifleman.9.", ids[0], StringComparison.Ordinal);
        Assert.Equal(people.Select(p => p.Id).OrderBy(i => i, StringComparer.Ordinal), runtime.Actors.Keys);
        foreach (var (id, traits) in people) Assert.Equal(traits.OrderBy(t => t, StringComparer.Ordinal), runtime.Actor(id).Traits);
        Assert.Contains(people, p => p.Traits.Length > 0);
    }

    [Fact]
    public void WhatATraitDoesComesFromTheRelationshipDocumentAndShowsInMoodAndInHowFastANeedFalls()
    {
        var (h, people, _) = Spawn(9);
        var set = RelationshipLoader.Load(People);
        var runtime = Populate(h, set, out _);
        runtime.Advance(4, new Dictionary<string, FactValue>());      // one day
        var seenTraitSets = new HashSet<string>();
        foreach (var (id, traits) in people)
        {
            var permille = 1000L;
            foreach (var trait in traits.OrderBy(t => t, StringComparer.Ordinal))
            {
                if (trait == "chars.trait.brave") permille = permille * 500 / 1000;
                if (trait == "chars.trait.reckless") permille = permille * 1500 / 1000;
            }
            var rate = 20 * permille * 1000 / 1_000_000;
            var actor = runtime.Actor(id);
            Assert.Equal(320 - 4 * rate, actor.Need[0]);
            var bias = (traits.Contains("chars.trait.brave") ? 10 : 0) + (traits.Contains("chars.trait.reckless") ? -10 : 0) + (traits.Contains("chars.trait.steady") ? 5 : 0);
            var level = actor.Need[0] / 4;
            Assert.Equal(bias + (level - 50) * 100 / 100, runtime.MoodValue(actor));
            seenTraitSets.Add(string.Join("+", traits.OrderBy(t => t, StringComparer.Ordinal)));
        }
        Assert.True(seenTraitSets.Count >= 4, "the 40 riflemen should show several different trait sets, saw " + seenTraitSets.Count);
    }

    [Fact]
    public void TheSameSeedGivesTheSameActorsAndTheSameRelationshipsAndAnotherSeedGivesOtherTraits()
    {
        string Digest(ulong seed)
        {
            var (h, _, _) = Spawn(seed);
            var runtime = Populate(h, RelationshipLoader.Load(People), out _);
            runtime.Advance(12, new Dictionary<string, FactValue>());
            return runtime.StateDigest();
        }
        Assert.Equal(Digest(9), Digest(9));
        Assert.NotEqual(Digest(9), Digest(10));
    }

    [Fact]
    public void AddingTheSameCharactersTwiceIsRefusedAndTheRelationshipsKeepWhatWasAdded()
    {
        var (h, _, _) = Spawn(9, 5);
        var runtime = Populate(h, RelationshipLoader.Load(People), out _);
        var refusal = Assert.Throws<EngineRefusedException>(() => RelationshipCharacters.AddActors(runtime, Characters(h)));
        Assert.Equal(RelationshipCodes.DuplicateActor, refusal.Code);
        Assert.Equal(5, runtime.Actors.Count);
        Assert.Throws<ArgumentNullException>(() => RelationshipCharacters.AddActors(null!, []));
        Assert.Throws<ArgumentNullException>(() => RelationshipCharacters.AddActors(runtime, null!));
        Assert.Throws<ArgumentNullException>(() => RelationshipCharacters.ActorId(null!));
    }

}
