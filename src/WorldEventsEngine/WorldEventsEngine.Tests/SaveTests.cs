using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The save: an exact copy of the state that resumes exactly, the order of the checks (ranges before the digest), every forgery a rule of its own refuses, and replay, jump and resume equalities.</summary>
public sealed class SaveTests
{
    private static string Describe(IEnumerable<WorldChange> changes, WorldSet set) => string.Join('\n', changes.Select(c => WorldChangeText.Line(set, c)));

    [Fact]
    public void ASaveReopensToTheSameStateAndTheSameFutureAsTheOriginal()
    {
        var original = Tamper.Played(out var set);
        var bytes = WorldSave.Write(original);
        var reopened = WorldSave.Open(set, bytes);
        Assert.Equal(original.StateDigest(), reopened.StateDigest());
        Assert.Equal(bytes, WorldSave.Write(reopened));
        var futureOriginal = Fixture.Live(original, 80);
        var futureReopened = Fixture.Live(reopened, 80);
        Assert.Equal(Describe(futureOriginal, set), Describe(futureReopened, set));
        Assert.Equal(original.StateDigest(), reopened.StateDigest());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void SavingAndResumingAtAnyStepEqualsPlayingStraightThrough(int seed)
    {
        WorldSet Load() => Fixture.Mini(root =>
        {
            root["seed"] = seed;
            Tiny.Hazard(root)["ratePpm"] = 80_000;
            Tiny.Disease(root)["seedPpm"] = 40_000;
            Tiny.Disease(root)["seedCases"] = 3;
            Tiny.Teller(root, "t")["gapMax"] = 4;
        });
        var set = Load();
        var straight = WorldRuntime.Create(set);
        var straightChanges = Fixture.Live(straight, 200);
        var cut = 20 + seed * 13;
        var first = WorldRuntime.Create(set);
        var head = Fixture.Live(first, cut);
        var resumed = WorldSave.Open(set, WorldSave.Write(first));
        var tail = Fixture.Live(resumed, 200);
        Assert.Equal(straight.StateDigest(), resumed.StateDigest());
        Assert.Equal(Describe(straightChanges, set), Describe(head.Concat(tail), set));
    }

    [Fact]
    public void LivingManyStepsInOneCallEqualsLivingThemOneByOne()
    {
        var set = Fixture.Mini(root => { Tiny.Hazard(root)["ratePpm"] = 80_000; Tiny.Disease(root)["seedPpm"] = 40_000; Tiny.Disease(root)["seedCases"] = 3; });
        var one = WorldRuntime.Create(set);
        var many = WorldRuntime.Create(set);
        var oneByOne = Fixture.Live(one, 150);
        var jump = many.Advance(150, many.CycleSeasonAt, Fixture.Host()).Items;
        Assert.Equal(one.StateDigest(), many.StateDigest());
        Assert.Equal(Describe(oneByOne, set), Describe(jump, set));
    }

    [Fact]
    public void TheDigestIsCheckedAfterTheRangesAndTheStateAgainstTheDocumentLast()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        // A forged value out of range is refused as a state even though the forger recomputed the digest; a stale digest on a possible state is refused as a digest.
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Stress = 5000))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Stale(bytes, i => i.Stress = 5000))));       // out of range wins over the stale digest
        Assert.Equal(WorldCodes.SaveDigest, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Stale(bytes, i => i.Stress = i.Stress == 7 ? 8 : 7))));
    }

    [Fact]
    public void ABrokenOrAlienSaveIsRefusedByItsFormat()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, Encoding.UTF8.GetBytes("not json"))));
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Format = 3))));
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Format = 1))));
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, Encoding.UTF8.GetBytes("{\"Format\":1}"))));
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, new byte[WorldSave.MaxBytes + 1])));
        var text = Encoding.UTF8.GetString(bytes).Replace("\"Format\":2", "\"Format\":2,\"Extra\":3", StringComparison.Ordinal);
        Assert.Contains("\"Extra\":3", text, StringComparison.Ordinal);
        Assert.Equal(WorldCodes.SaveFormat, Fixture.Refusal(() => WorldSave.Open(set, Encoding.UTF8.GetBytes(text))));
    }

    [Fact]
    public void ASaveOfAnotherDocumentIsRefusedEvenWhenTheIdIsTheSame()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        var edited = Fixture.Mini(root => Tiny.Hazard(root)["cooldown"] = 4);
        Assert.Equal(set.Id, edited.Id);
        Assert.Equal(WorldCodes.SaveDocument, Fixture.Refusal(() => WorldSave.Open(edited, bytes)));
    }

    /// <summary>Forgeries the rules about the state catch: each is a state the engine can never make, with the digest recomputed so only the rule can refuse it.</summary>
    private static IEnumerable<(string Name, Action<WorldImage> Edit)> Forgeries()
    {
        yield return ("people appear", i => i.Regions[0].Susceptible[0] += 1);
        yield return ("people vanish", i => i.Regions[0].Susceptible[0] -= 1);
        yield return ("a negative compartment", i => { i.Regions[0].Susceptible[0] = -1; });
        yield return ("a compartment beyond a billion", i => i.Regions[0].Dead[0] = 2_000_000_000);
        yield return ("a step before the beginning", i => i.Step = -2);
        yield return ("a season that does not exist", i => i.Season = 9);
        yield return ("a regime that does not exist", i => i.Regime = 3);
        yield return ("a storyteller that does not exist", i => i.Storyteller = 5);
        yield return ("a difficulty that does not exist", i => i.Difficulty = 5);
        yield return ("a stress above a thousand", i => i.Stress = 1001);
        yield return ("a negative stress", i => i.Stress = -1);
        yield return ("a director phase that does not exist", i => i.Phase = 3);
        yield return ("a director phase that began in the future", i => i.PhaseStart = i.Step + 5);
        yield return ("a last card in the future", i => i.LastCard = i.Step + 5);
        yield return ("a negative draw count", i => i.Draws = -1);
        yield return ("too few card cooldowns", i => i.CardReadyAt.RemoveAt(0));
        yield return ("too many hazard cooldowns", i => i.HazardReadyAt.Add(0));
        yield return ("a negative cooldown", i => i.CardReadyAt[0] = -1);
        yield return ("a region fewer", i => i.Regions.RemoveAt(1));
        yield return ("a weather that does not exist", i => i.Regions[0].Weather = 7);
        yield return ("a temperature beyond a hundred degrees", i => i.Regions[0].Temp = 20_000);
        yield return ("more water than the soil holds", i => i.Regions[0].SoilTenths = 5000);
        yield return ("a compartment list of the wrong length", i => i.Regions[0].Exposed.Add(0));
        yield return ("an outbreak state that does not exist", i => i.Regions[0].Outbreak[0] = 3);
        yield return ("intervention flags beyond two bits", i => i.Regions[0].Interventions[0] = 4);
        yield return ("a hazard that does not exist", i => i.Hazards.Add(new ImageHazard { Hazard = 9, Region = 0, Rung = 0, Phase = 0, PhaseStart = 0, PhaseLength = 1, PeakLength = 1, Power = 1000 }));
        yield return ("a hazard in a region that does not exist", i => i.Hazards.Add(new ImageHazard { Hazard = 0, Region = 9, Rung = 0, Phase = 0, PhaseStart = 0, PhaseLength = 1, PeakLength = 1, Power = 1000 }));
        yield return ("a power of nothing", i => i.Hazards[0].Power = 0);
        yield return ("a hazard phase that does not exist", i => i.Hazards[0].Phase = 5);
        yield return ("a hazard twice in one region", i => i.Hazards.Add(new ImageHazard { Hazard = i.Hazards[0].Hazard, Region = i.Hazards[0].Region, Rung = i.Hazards[0].Rung, Phase = i.Hazards[0].Phase, PhaseStart = i.Hazards[0].PhaseStart, PhaseLength = i.Hazards[0].PhaseLength, PeakLength = i.Hazards[0].PeakLength, Power = i.Hazards[0].Power }));
        yield return ("a peak longer than its rung allows", i => { i.Hazards[0].PeakLength = 50; i.Hazards[0].PhaseLength = 50; });
        yield return ("a phase of the wrong length for its rung", i => i.Hazards[0].PhaseLength += 1);
        yield return ("a hazard that has gone on past its phase", i => i.Hazards[0].PhaseStart = -1);
        yield return ("a timed modifier that has ended", i => i.Timed.Add(new ImageTimed { Card = 0, Region = 0, Modifier = 0, Delta = -1, Until = i.Step }));
        yield return ("a timed modifier of a card that does not exist", i => i.Timed.Add(new ImageTimed { Card = 9, Region = 0, Modifier = 0, Delta = -1, Until = i.Step + 3 }));
        yield return ("an agenda entry that is not its chain entry", i => i.Agenda.Add(new ImageAgenda { Due = i.Step + 1, Card = 2, Region = 0, Parent = 0, Link = 0 }));
        yield return ("an agenda entry of a link that does not exist", i => i.Agenda.Add(new ImageAgenda { Due = i.Step + 1, Card = 1, Region = 0, Parent = 0, Link = 4 }));
        yield return ("an intervention marked automatic that has no trigger", i => i.Regions[0].Interventions[1] = 2);
        yield return ("a peak below those infectious now", i => i.Regions[0].Peak[0] = 0);
        yield return ("more hazards than the list holds", i => { for (var n = 0; n < 9; n++) i.Hazards.Add(new ImageHazard { Hazard = 0, Region = 0, Rung = 0, Phase = 0, PhaseStart = 0, PhaseLength = 1, PeakLength = 3, Power = 1000 }); });
    }

    [Fact]
    public void EveryForgeryTheStateRulesCatchIsRefusedAsAState()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        var image = Tamper.Image(bytes);
        Assert.NotEmpty(image.Hazards);                    // the played world has what the forgeries edit
        Assert.NotEmpty(image.Timed);
        Assert.NotEmpty(image.Agenda);
        Assert.True(image.Regions[0].Infectious[0] > 0);
        var count = 0;
        foreach (var (name, edit) in Forgeries())
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => WorldSave.Open(set, Tamper.Forge(bytes, edit)));
            Assert.True(WorldCodes.SaveState == refusal.Code, name + " was refused with " + refusal.Code + ": " + refusal.Message);
            count++;
        }
        Assert.True(count >= 40);
    }

    [Fact]
    public void ASirDiseaseWithExposedPeopleAndAnUnrelatedOutbreakStateAreRefused()
    {
        var set = Fixture.Mini(root => { Tiny.Disease(root)["model"] = "sir"; Tiny.Disease(root)["incubation"] = 0; });
        var runtime = WorldRuntime.Create(set);
        Fixture.Live(runtime, 3);
        var bytes = WorldSave.Write(runtime);
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => { i.Regions[0].Susceptible[0] -= 5; i.Regions[0].Exposed[0] += 5; i.Regions[0].Outbreak[0] = 1; }))));
        Assert.Equal(WorldCodes.SaveState, Fixture.Refusal(() => WorldSave.Open(set, Tamper.Forge(bytes, i => { i.Regions[0].Susceptible[0] -= 5; i.Regions[0].Infectious[0] += 5; i.Regions[0].Peak[0] += 5; }))));      // infected people with no outbreak state
    }

    [Fact]
    public void TheDigestOfTheStateIsAFunctionOfTheStateAlone()
    {
        var a = Fixture.MiniWorld();
        var b = Fixture.MiniWorld();
        Assert.Equal(a.StateDigest(), b.StateDigest());
        Fixture.Live(a, 5);
        Assert.NotEqual(a.StateDigest(), b.StateDigest());
        Fixture.Live(b, 5);
        Assert.Equal(a.StateDigest(), b.StateDigest());
        a.PlayCard("c-global", "");
        Assert.NotEqual(a.StateDigest(), b.StateDigest());
    }

    [Fact]
    public void TheSavedWorldsOfAYearOfTheSampleReopenAndKeepGoingIdentically()
    {
        var set = Fixture.Valley;
        var straight = WorldRuntime.Create(set);
        Fixture.Live(straight, 365, Fixture.ValleyHost());
        var half = WorldRuntime.Create(set);
        Fixture.Live(half, 180, Fixture.ValleyHost());
        var resumed = WorldSave.Open(set, WorldSave.Write(half));
        Fixture.Live(resumed, 365, Fixture.ValleyHost());
        Assert.Equal(straight.StateDigest(), resumed.StateDigest());
    }
}
