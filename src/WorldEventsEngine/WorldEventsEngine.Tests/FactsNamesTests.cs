using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The facts the world offers to conditions and to the other layers, the modifiers, the words, the change lines and the explanations.</summary>
public sealed class FactsNamesTests
{
    private static void Within(FactDefinition definition, FactValue value, string id)
    {
        Assert.True(definition.Kind == value.Kind, id + " is " + value.Kind + ", declared " + definition.Kind);
        if (definition.Kind == FactKind.Integer) Assert.True(value.Integer >= definition.Minimum && value.Integer <= definition.Maximum, id + " = " + value.Integer + " outside " + definition.Minimum + " to " + definition.Maximum);
    }

    [Fact]
    public void EveryFactTheWorldGivesIsOfTheDeclaredTypeAndInTheDeclaredRangeThroughAYear()
    {
        var runtime = WorldRuntime.Create(Fixture.Valley);
        var offered = WorldFacts.Offered(Fixture.Valley);
        var host = Fixture.ValleyHost();
        for (var step = 0; step < 365; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, host);
            if (step % 7 != 0) continue;
            foreach (var region in Fixture.Valley.Regions)
                foreach (var (id, value) in runtime.Facts("", region.Id)) Within(offered[id], value, id);
        }
    }

    [Fact]
    public void TheLocalFactsAConditionReadsAreOfTheDeclaredTypeAndRange()
    {
        var set = Fixture.Valley;
        var runtime = WorldRuntime.Create(set);
        var host = Fixture.ValleyHost();
        for (var step = 0; step < 200; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, host);
            if (step % 11 != 0) continue;
            for (var region = 0; region < set.Regions.Count; region++)
                foreach (var (id, value) in runtime.LocalFactsFor(region, host)) Within(set.LocalFacts[id], value, id);
        }
    }

    [Fact]
    public void TheOfferedFactsNameTheWorldAndEachRegionUnderThePrefix()
    {
        var offered = WorldFacts.Offered(Fixture.Valley, "ext.");
        Assert.Contains("ext.world.season", offered.Keys);
        Assert.Contains("ext.world.riverton.temp", offered.Keys);
        Assert.Contains("ext.world.hillfort.hazard.flood", offered.Keys);
        Assert.Contains("ext.world.seaport.disease.plague.prevalence", offered.Keys);
        Assert.Contains("ext.world.seaport.mod.visibility", offered.Keys);
        Assert.DoesNotContain("ext.world.game.garrison", offered.Keys);                  // the host's own are the host's
        var facts = WorldRuntime.Create(Fixture.Valley).Facts("ext.", "riverton");
        Assert.Contains("ext.world.stress", facts.Keys);
        Assert.Contains("ext.world.riverton.river", facts.Keys);
        Assert.DoesNotContain("ext.world.hillfort.river", facts.Keys);
        Assert.Equal(WorldFacts.GlobalNames.Count, WorldRuntime.Create(Fixture.Valley).Facts("ext.", "").Count);
    }

    [Fact]
    public void AHazardFactIsTheRungNumberAndZeroForNone()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        Assert.Equal(0, runtime.Facts("", "a")["world.a.hazard.h"].Integer);
        runtime.BeginHazard("a", "h", "high");
        Assert.Equal(2, runtime.Facts("", "a")["world.a.hazard.h"].Integer);
        Assert.Equal(2, runtime.Facts("", "b")["world.b.hazard.h"].Integer);          // reached at the same rung, weaker but the same rung
    }

    [Fact]
    public void TheHostsFactsReachTheConditionsAndAHostFactMayNotTakeTheEnginesNames()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Card(root, "c-good")["when"] = Tiny.Cond("ext.alert", "gt", 5));
        Assert.Contains("ext.alert", runtime.Set.LocalFacts.Keys);
        Assert.Contains("ext.alert", runtime.Set.GlobalFacts.Keys);
        Assert.True(WorldFacts.IsReserved("temp") && WorldFacts.IsReserved("hazard.x") && WorldFacts.IsReserved("disease.y.infected") && WorldFacts.IsReserved("mod.z") && WorldFacts.IsReserved("stress"));
        Assert.False(WorldFacts.IsReserved("ext.alert"));
    }

    [Fact]
    public void AModifierIsItsBasePlusWeatherHazardsAndOutbreaksAndStaysInItsRange()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Card(root, "c-global")["effects"]!.AsArray().Add(new JsonObject { ["modifier"] = "vis", ["delta"] = -3000, ["duration"] = 3 }));
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var dry = runtime.Regions[0].Weather == 0;
        var baseline = dry ? 1000 : 700;                         // a wet weather takes 300 off visibility
        Assert.Equal(baseline, runtime.Modifier(0, 0));
        runtime.BeginHazard("a", "h", "low");                    // visibility -500 at the first onset step (half intensity)
        Assert.Equal(baseline - 250, runtime.Modifier(0, 0));
        runtime.PlayCard("c-global", "");                        // -3000: far below its range
        Assert.Equal(0, runtime.Modifier(0, 0));
        Assert.Equal(0, runtime.Modifier(1, 0));
    }

    [Fact]
    public void RequiredKeysAreEveryNameOnceAndTheSampleTableCoversThemAll()
    {
        var keys = WorldNames.RequiredKeys(Fixture.Valley);
        Assert.Equal(keys.Distinct().Count(), keys.Count);
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), keys);
        Assert.Contains("rung.tempest", keys);
        Assert.Contains("region.seaport", keys);
        Assert.Contains("card.festival", keys);
        var table = LocaleTable.Load(Fixture.Text("valley.world.en.json"));
        var report = WorldNames.Coverage(Fixture.Valley, table);
        Assert.True(report.IsComplete);
        Assert.Equal(keys.Count, report.Total);
    }

    [Fact]
    public void ACoverageReportNamesAMissingAnUnknownAndAPlaceholderMessage()
    {
        var text = Fixture.Text("valley.world.en.json").Replace("\"region.seaport\": \"Seaport\"", "\"region.seaport\": \"Sea {port}\"", StringComparison.Ordinal).Replace("\"card.festival\": \"Festival\",", "\"card.extra\": \"Extra\",", StringComparison.Ordinal);
        var report = WorldNames.Coverage(Fixture.Valley, LocaleTable.Load(text));
        Assert.False(report.IsComplete);
        Assert.Contains("card.festival", report.Missing);
        Assert.Contains("card.extra", report.Unknown);
        Assert.Contains(report.Problems, p => p.Key == "region.seaport");
    }

    [Fact]
    public void NamesAreRenderedFromTheLocaleChain()
    {
        var chain = LocaleChain.Create("en", [LocaleTable.Load(Fixture.Text("valley.world.en.json"))], "en");
        var set = Fixture.Valley;
        Assert.Equal("Riverton", WorldNames.Region(set, chain, 0));
        Assert.Equal("Spring", WorldNames.Season(set, chain, 0));
        Assert.Equal("Storm", WorldNames.Weather(set, chain, 4));
        Assert.Equal("Drought", WorldNames.Hazard(set, chain, 0));
        Assert.Equal("Severe drought", WorldNames.Rung(set, chain, 0, 1));
        Assert.Equal("Plague", WorldNames.Disease(set, chain, 0));
        Assert.Equal("Quarantine", WorldNames.Intervention(set, chain, 0));
        Assert.Equal("Raiders on the road", WorldNames.Card(set, chain, 0));
        Assert.Equal("Cassandra", WorldNames.Storyteller(set, chain, 0));
        Assert.Equal("Settled", WorldNames.Regime(set, chain, 0));
        Assert.Equal("Labour", WorldNames.Modifier(set, chain, 2));
    }

    [Fact]
    public void EveryKindOfChangeHasALineThatNamesWhatHappened()
    {
        var set = Fixture.Mini();
        var runtime = WorldRuntime.Create(set);
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        var lines = new List<string>();
        void Add(IEnumerable<WorldChange> changes) => lines.AddRange(changes.Select(c => WorldChangeText.Line(set, c)));
        Add(runtime.BeginHazard("a", "h", "high").Items);
        Add(runtime.PlayCard("c-bad", "b").Items);
        Add(runtime.SeedOutbreak("a", "d", 30).Items);
        Add(runtime.Intervene("a", "quar", true).Items);
        var log = Fixture.Live(runtime, 60);
        Add(log);
        Add(new[] { new WorldChange(ChangeKind.Season, 1, -1, 1, -1, 0, 0, 0, "", ""), new WorldChange(ChangeKind.Regime, 1, -1, 0, 0, 0, 0, 0, "", "") });
        Assert.Contains(lines, l => l.StartsWith("0 hazard h in a started at high power 1000", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("card c-bad in b forced", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("disease d in a introduced infected 30", StringComparison.Ordinal));
        Assert.Contains(lines, l => l == "0 intervention quar in a started");
        Assert.Contains(lines, l => l.Contains("effect shock sh1 in a scale 700 from hazard:h", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("effect signal sig.bad in b from card:c-bad", StringComparison.Ordinal));
        Assert.Contains(lines, l => l == "1 season - -> cold");
        Assert.Contains(lines, l => l == "1 regime r1 -> r1");
        Assert.Contains(lines, l => l.Contains(" weather ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(" director ", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplainingAHazardSaysWhyItDidNotStartAndWhatItIsDoing()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Hazard(root)["when"] = Tiny.Cond("temp", "gt", 9000));
        Fixture.Live(runtime, 3);
        var off = WorldExplain.Hazard(runtime, "h", "a", Fixture.Host());
        Assert.Contains("hazard h in a is not going", off);
        Assert.Contains("exposure 1000 per mille", off);
        Assert.Contains("chance a step in warm: 0 ppm (rate 0", off);
        Assert.Contains("when temp gt 9000", off);
        Assert.Contains("= false", off);
        runtime.BeginHazard("a", "h", "low");
        var on = WorldExplain.Hazard(runtime, "h", "a", Fixture.Host());
        Assert.Contains("is going: rung low, onset since step 3 (2 steps), power 1000, intensity now 500", on);
        Assert.Equal(WorldCodes.UnknownRegion, Fixture.Refusal(() => WorldExplain.Hazard(runtime, "h", "zz", Fixture.Host())));
        Assert.Equal(WorldCodes.UnknownHazard, Fixture.Refusal(() => WorldExplain.Hazard(runtime, "zz", "a", Fixture.Host())));
        Assert.Equal(WorldCodes.FactMissing, Fixture.Refusal(() => WorldExplain.Hazard(runtime, "h", "a", new Dictionary<string, FactValue>())));
    }

    [Fact]
    public void ExplainingAHazardSaysWhenItIsHeldBackByTheDirectorAndWhenTheRegionIsNeverHit()
    {
        var runtime = Fixture.MiniWorld(root => { Tiny.Hazard(root)["paced"] = true; Tiny.Region(root, "b")["exposure"] = new JsonArray(); });
        Fixture.Live(runtime, 3);
        Assert.Contains("paced: the director is build, so it may start", WorldExplain.Hazard(runtime, "h", "a", Fixture.Host()));
        Assert.Contains("never hit by this hazard on its own", WorldExplain.Hazard(runtime, "h", "b", Fixture.Host()));
    }

    [Fact]
    public void ExplainingACardSaysItsCooldownGroupTierAndCondition()
    {
        var runtime = Fixture.MiniWorld();
        Fixture.Live(runtime, 2);
        runtime.PlayCard("c-bad", "a");
        var text = WorldExplain.Card(runtime, "c-bad", "", Fixture.Host());
        Assert.Contains("card c-bad (bad, regional, weight 100)", text);
        Assert.Contains("cooldown until step", text);
        Assert.Contains("group g is held until step", text);
        Assert.Contains("storyteller t gives the tier 1000 per mille", text);
        Assert.Contains("bad cards come only while building", text);
        Assert.Contains("no condition", text);
        var good = WorldExplain.Card(runtime, "c-good", "b", Fixture.Host());
        Assert.Contains("when in b: season eq \"warm\" [\"warm\"] = true", good);
        var global = WorldExplain.Card(runtime, "c-global", "", Fixture.Host());
        Assert.Contains("card c-global (neutral, global, weight 50)", global);
        Assert.Equal(WorldCodes.UnknownCard, Fixture.Refusal(() => WorldExplain.Card(runtime, "zz", "", Fixture.Host())));
    }

    [Fact]
    public void ExplainingADiseaseGivesItsCompartmentsAndTheReproductionNumber()
    {
        var runtime = Fixture.MiniWorld();
        runtime.Advance(0, runtime.CycleSeasonAt, Fixture.Host());
        runtime.SeedOutbreak("a", "d", 100);
        runtime.Intervene("a", "quar", true);
        var text = WorldExplain.Disease(runtime, "d", "a");
        Assert.Contains("disease d (seir) in a: introduced", text);
        Assert.Contains("susceptible 9900, exposed 100, infectious 0, recovered 0, dead 0", text);
        Assert.Contains("reproduction number about 1600 per mille in warm, effective about 1584", text);        // 0.4 times 4 steps; times the 9900 of 10000 still susceptible
        Assert.Contains("interventions on: quar", text);
        Assert.Equal(WorldCodes.UnknownDisease, Fixture.Refusal(() => WorldExplain.Disease(runtime, "zz", "a")));
    }

    [Fact]
    public void ExplainingTheDirectorGivesItsPhaseStressAndCounts()
    {
        var runtime = Fixture.MiniWorld();
        Fixture.Live(runtime, 30);
        var text = WorldExplain.Director(runtime);
        Assert.StartsWith("director t on normal: ", text);
        Assert.Contains("peaks at 500, holds 2 steps, relaxes below 200 after at least 3 steps, decays 100 per mille a step", text);
        Assert.Contains("cards " + runtime.Played + " played", text);
        Assert.Contains("gap 1 to 5 steps", text);
    }
}
