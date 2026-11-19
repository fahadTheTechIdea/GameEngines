using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>Every refusal code is reached by at least one input, and the set of codes the matrix reaches is exactly the constants of <see cref="WorldCodes"/>.</summary>
public sealed class RefusalMatrixTests
{
    private static JsonNode Deep(int levels)
    {
        JsonNode node = Tiny.Cond("season", "eq", "warm");
        for (var i = 0; i < levels; i++) node = new JsonObject { ["not"] = node };
        return node;
    }

    private static JsonObject Effect(string kind, JsonNode value) => new() { [kind] = value };

    private static void Excite(JsonObject root, int jump, int decay, int cap) => Tiny.Hazard(root)["excite"] = new JsonObject { ["jumpPpm"] = jump, ["decayPermille"] = decay, ["capPpm"] = cap };

    /// <summary>Documents that are wrong in one place: the code each is refused with, a name for the failure message, and the document.</summary>
    private static IEnumerable<(string Code, string Name, Func<string> Text)> Documents()
    {
        string Doc(Action<JsonObject> edit) { var r = Tiny.Root(); edit(r); return r.ToJsonString(); }
        yield return (WorldCodes.Format, "format 3", () => Doc(r => r["format"] = 3));
        yield return (WorldCodes.Format, "format 0", () => Doc(r => r["format"] = 0));
        yield return (WorldCodes.FormatSuperseded, "format 1, which is no longer read", () => Doc(r => r["format"] = 1));
        yield return (WorldCodes.FormatSuperseded, "format 1 as the sample was before the upgrade", () => Doc(r => { r["format"] = 1; Tiny.Hazard(r).Remove("excite"); Tiny.Zone(r).Remove("spill"); }));
        yield return (WorldCodes.Nonstationary, "a branching ratio of exactly 1", () => Doc(r => Excite(r, 1_000_000, 500, 1_000_000)));
        yield return (WorldCodes.Nonstationary, "a branching ratio above 1", () => Doc(r => Excite(r, 600_000, 300, 1_000_000)));
        yield return (WorldCodes.Nonstationary, "a jump that never fades", () => Doc(r => Excite(r, 1002, 1, 1_000_000)));
        yield return (WorldCodes.Hazard, "a cap and no jump", () => Doc(r => Excite(r, 0, 500, 5)));
        yield return (WorldCodes.Hazard, "a cap below the jump", () => Doc(r => Excite(r, 100, 500, 99)));
        yield return (WorldCodes.Range, "a decay of 0", () => Doc(r => Excite(r, 100, 0, 1000)));
        yield return (WorldCodes.Range, "a decay above 1000", () => Doc(r => Excite(r, 100, 1001, 1000)));
        yield return (WorldCodes.Range, "a jump above certainty", () => Doc(r => Excite(r, 1_000_001, 1000, 1_000_001)));
        yield return (WorldCodes.Range, "a negative jump", () => Doc(r => Excite(r, -1, 500, 0)));
        yield return (WorldCodes.Range, "spill above 900", () => Doc(r => Tiny.Zone(r)["spill"] = 901));
        yield return (WorldCodes.Range, "negative spill", () => Doc(r => Tiny.Zone(r)["spill"] = -1));
        yield return (WorldCodes.MissingField, "a hazard without excite", () => Doc(r => Tiny.Hazard(r).Remove("excite")));
        yield return (WorldCodes.MissingField, "an excite without a cap", () => Doc(r => Tiny.Hazard(r)["excite"]!.AsObject().Remove("capPpm")));
        yield return (WorldCodes.MissingField, "a zone without spill", () => Doc(r => Tiny.Zone(r).Remove("spill")));
        yield return (WorldCodes.UnknownField, "an extra excite field", () => Doc(r => Tiny.Hazard(r)["excite"]!.AsObject()["halfLife"] = 3));
        yield return (WorldCodes.Format, "not JSON", () => "{ nope");
        yield return (WorldCodes.Format, "hazards is not an array", () => Doc(r => r["hazards"] = 5));
        yield return (WorldCodes.Format, "hazard kind unknown", () => Doc(r => Tiny.Hazard(r)["kind"] = "plague"));
        yield return (WorldCodes.Format, "disease model unknown", () => Doc(r => Tiny.Disease(r)["model"] = "sis"));
        yield return (WorldCodes.Format, "card tier unknown", () => Doc(r => Tiny.Card(r, "c-bad")["tier"] = "evil"));
        yield return (WorldCodes.Format, "card scope unknown", () => Doc(r => Tiny.Card(r, "c-bad")["scope"] = "town"));
        yield return (WorldCodes.Format, "condition operator", () => Doc(r => Tiny.Card(r, "c-good")["when"]!["op"] = "like"));
        yield return (WorldCodes.Format, "host fact type", () => Doc(r => Tiny.Array(r, "facts")[0]!["type"] = "float"));
        yield return (WorldCodes.Format, "hazards entry without an id", () => Doc(r => Tiny.Array(r, "hazards").Add(5)));
        yield return (WorldCodes.Format, "id with a capital", () => Doc(r => r["id"] = "Mini"));
        yield return (WorldCodes.Format, "frequency entry is text", () => Doc(r => Tiny.Zone(r)["seasons"]![0]!["frequency"]![0] = "x"));
        yield return (WorldCodes.UnknownField, "extra top-level field", () => Doc(r => r["extra"] = 1));
        yield return (WorldCodes.UnknownField, "extra hazard field", () => Doc(r => Tiny.Hazard(r)["colour"] = "red"));
        yield return (WorldCodes.UnknownField, "condition with an unknown shape", () => Doc(r => Tiny.Card(r, "c-good")["when"] = new JsonObject { ["fact"] = "season" }));
        yield return (WorldCodes.MissingField, "no start", () => Doc(r => r.Remove("start")));
        yield return (WorldCodes.MissingField, "hazard without a ladder", () => Doc(r => Tiny.Hazard(r).Remove("ladder")));
        yield return (WorldCodes.MissingField, "card without a chain", () => Doc(r => Tiny.Card(r, "c-bad").Remove("chain")));
        yield return (WorldCodes.MissingField, "int host fact without range", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "x", ["type"] = "int" })));
        yield return (WorldCodes.Range, "no steps in a day", () => Doc(r => r["time"]!["stepsPerDay"] = 0));
        yield return (WorldCodes.Range, "no seasons", () => Doc(r => r["seasons"] = new JsonArray()));
        yield return (WorldCodes.Range, "a season of no steps", () => Doc(r => Tiny.Array(r, "seasons")[0]!["steps"] = 0));
        yield return (WorldCodes.Range, "a rate above certainty", () => Doc(r => Tiny.Hazard(r)["ratePpm"] = 2_000_000));
        yield return (WorldCodes.Range, "a population beyond a billion", () => Doc(r => Tiny.Region(r, "a")["population"] = 2_000_000_000L));
        yield return (WorldCodes.Range, "a negative weight", () => Doc(r => Tiny.Zone(r)["seasons"]![0]!["frequency"]![0] = -1));
        yield return (WorldCodes.Range, "no hazards allowed in the list", () => Doc(r => r["limits"]!["maxHazards"] = 0));
        yield return (WorldCodes.Range, "soil of no size", () => Doc(r => r["hydrology"]!["soilCapacity"] = 0));
        yield return (WorldCodes.Range, "a document too long", () => new string(' ', WorldLoader.MaxCharacters + 1));
        yield return (WorldCodes.Range, "a link flow above one", () => Doc(r => Tiny.Array(r, "links")[0]!["flowPpm"] = 1_000_001));
        yield return (WorldCodes.Range, "a shock of scale 0", () => Doc(r => Tiny.Rung(r, 1)["effects"]![1]!["scale"] = 0));
        yield return (WorldCodes.Range, "a chain delay of 0", () => Doc(r => Tiny.Card(r, "c-bad")["chain"]![0]!["delay"] = 0));
        yield return (WorldCodes.Range, "a season longer than ten years", () => Doc(r => Tiny.Array(r, "seasons")[0]!["steps"] = 4000));
        yield return (WorldCodes.Range, "a peak the stress never reaches", () => Doc(r => Tiny.Teller(r, "t")["peakAt"] = 1002));
        yield return (WorldCodes.Range, "a condition nested too deep", () => Doc(r => Tiny.Card(r, "c-good")["when"] = Deep(8)));
        yield return (WorldCodes.Range, "a modifier base outside its range", () => Doc(r => Tiny.Array(r, "modifiers")[0]!["base"] = 2000));
        yield return (WorldCodes.Duplicate, "season twice", () => Doc(r => Tiny.Array(r, "seasons").Add(Tiny.Copy(Tiny.Array(r, "seasons")[0]!))));
        yield return (WorldCodes.Duplicate, "region twice", () => Doc(r => Tiny.Array(r, "regions").Add(Tiny.Copy(Tiny.Region(r, "a")))));
        yield return (WorldCodes.Duplicate, "hazard twice", () => Doc(r => Tiny.Array(r, "hazards").Add(Tiny.Copy(Tiny.Hazard(r)))));
        yield return (WorldCodes.Duplicate, "disease twice", () => Doc(r => Tiny.Array(r, "diseases").Add(Tiny.Copy(Tiny.Disease(r)))));
        yield return (WorldCodes.Duplicate, "card twice", () => Doc(r => Tiny.Array(r, "cards").Add(Tiny.Copy(Tiny.Card(r, "c-bad")))));
        yield return (WorldCodes.Duplicate, "modifier twice", () => Doc(r => Tiny.Array(r, "modifiers").Add(Tiny.Copy(Tiny.Array(r, "modifiers")[0]!))));
        yield return (WorldCodes.Duplicate, "weather twice", () => Doc(r => Tiny.Array(r, "weathers").Add(Tiny.Copy(Tiny.Array(r, "weathers")[0]!))));
        yield return (WorldCodes.Duplicate, "host fact twice", () => Doc(r => Tiny.Array(r, "facts").Add(Tiny.Copy(Tiny.Array(r, "facts")[0]!))));
        yield return (WorldCodes.Duplicate, "exposure to a hazard twice", () => Doc(r => Tiny.Region(r, "a")["exposure"]!.AsArray().Add(Tiny.Copy(Tiny.Region(r, "a")["exposure"]![0]!))));
        yield return (WorldCodes.Duplicate, "immunity to a disease twice", () => Doc(r => Tiny.Region(r, "b")["immune"]!.AsArray().Add(Tiny.Copy(Tiny.Region(r, "b")["immune"]![0]!))));
        yield return (WorldCodes.Duplicate, "rung twice", () => Doc(r => Tiny.Hazard(r)["ladder"]!.AsArray().Add(Tiny.Copy(Tiny.Rung(r, 0)))));
        yield return (WorldCodes.Duplicate, "a modifier changed twice by a weather", () => Doc(r => Tiny.Array(r, "weathers")[1]!["modifiers"]!.AsArray().Add(Tiny.Copy(Tiny.Array(r, "weathers")[1]!["modifiers"]![0]!))));
        yield return (WorldCodes.Duplicate, "excludes the same hazard twice", () => Doc(r =>
        {
            Tiny.Array(r, "hazards").Add(MiniHazard("h2", "h"));
            Tiny.Hazard(r)["excludes"] = new JsonArray("h2", "h2");
        }));
        yield return (WorldCodes.Reference, "a region in an unknown zone", () => Doc(r => Tiny.Region(r, "a")["zone"] = "nope"));
        yield return (WorldCodes.Reference, "exposure to an unknown hazard", () => Doc(r => Tiny.Region(r, "a")["exposure"]![0]!["hazard"] = "nope"));
        yield return (WorldCodes.Reference, "immunity to an unknown disease", () => Doc(r => Tiny.Region(r, "b")["immune"]![0]!["disease"] = "nope"));
        yield return (WorldCodes.Reference, "excludes an unknown hazard", () => Doc(r => Tiny.Hazard(r)["excludes"] = new JsonArray("nope")));
        yield return (WorldCodes.Reference, "an intervention for an unknown disease", () => Doc(r => Tiny.Array(r, "interventions")[0]!["disease"] = "nope"));
        yield return (WorldCodes.Reference, "a chain to an unknown card", () => Doc(r => Tiny.Card(r, "c-bad")["chain"]![0]!["card"] = "nope"));
        yield return (WorldCodes.Reference, "a hazard effect at an unknown rung", () => Doc(r => Tiny.Card(r, "c-bad")["effects"]!.AsArray().Add(new JsonObject { ["hazard"] = "h", ["severity"] = "nope" })));
        yield return (WorldCodes.Reference, "a weather changes an unknown modifier", () => Doc(r => Tiny.Array(r, "weathers")[1]!["modifiers"]![0]!["modifier"] = "nope"));
        yield return (WorldCodes.Reference, "starts with an unknown storyteller", () => Doc(r => r["start"]!["storyteller"] = "nope"));
        yield return (WorldCodes.Reference, "a condition reads an unknown fact", () => Doc(r => Tiny.Card(r, "c-good")["when"]!["fact"] = "nerve"));
        yield return (WorldCodes.Reference, "an outbreak effect for an unknown disease", () => Doc(r => Tiny.Card(r, "c-bad")["effects"]!.AsArray().Add(WithCases(Effect("outbreak", "nope")))));
        yield return (WorldCodes.Reference, "a link to an unknown region", () => Doc(r => Tiny.Array(r, "links")[0]!["b"] = "nope"));
        yield return (WorldCodes.Reference, "a region condition reads an unknown hazard", () => Doc(r => Tiny.Card(r, "c-good")["when"] = Tiny.Cond("hazard.nope", "eq", 0)));
        yield return (WorldCodes.Reserved, "a host fact named like a weather fact", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "temp", ["type"] = "bool" })));
        yield return (WorldCodes.Reserved, "a host fact in the hazard namespace", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "hazard.x", ["type"] = "bool" })));
        yield return (WorldCodes.Reserved, "a host fact named like a world fact", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "stress", ["type"] = "bool" })));
        yield return (WorldCodes.Climate, "a wet weather with no rain", () => Doc(r => Tiny.Array(r, "weathers")[1]!["precip"] = 0));
        yield return (WorldCodes.Climate, "a dry weather with rain", () => Doc(r => Tiny.Array(r, "weathers")[0]!["precip"] = 10));
        yield return (WorldCodes.Climate, "a zone season with no weather weighted", () => Doc(r => Tiny.Zone(r)["seasons"]![0]!["frequency"] = new JsonArray(0, 0)));
        yield return (WorldCodes.Climate, "no regime in a season", () => Doc(r => Tiny.Array(r, "regimes")[0]!["frequency"] = new JsonArray(1, 0)));
        yield return (WorldCodes.Climate, "a regime that leaves a zone no weather", () => Doc(r => Tiny.Array(r, "regimes")[0]!["bias"] = new JsonArray(0, 0)));
        yield return (WorldCodes.Hazard, "the first tail is not certain", () => Doc(r => Tiny.Rung(r, 0)["tailPpm"] = 900_000));
        yield return (WorldCodes.Hazard, "a worse rung that is not rarer", () => Doc(r => Tiny.Rung(r, 1)["tailPpm"] = 1_000_000));
        yield return (WorldCodes.Hazard, "a duration range reversed", () => Doc(r => Tiny.Rung(r, 0)["minDuration"] = 9));
        yield return (WorldCodes.Hazard, "the worst rung escalates", () => Doc(r => Tiny.Rung(r, 1)["escalatePpm"] = 5));
        yield return (WorldCodes.Hazard, "a hazard excludes itself", () => Doc(r => Tiny.Hazard(r)["excludes"] = new JsonArray("h")));
        yield return (WorldCodes.Hazard, "an exclusion only one way", () => Doc(r =>
        {
            Tiny.Array(r, "hazards").Add(MiniHazard("h2", ""));
            Tiny.Hazard(r)["excludes"] = new JsonArray("h2");
        }));
        yield return (WorldCodes.Disease, "an SIR disease with an incubation", () => Doc(r => { Tiny.Disease(r)["model"] = "sir"; }));
        yield return (WorldCodes.Disease, "an SEIR disease with no incubation", () => Doc(r => Tiny.Disease(r)["incubation"] = 0));
        yield return (WorldCodes.Disease, "seeding with no cases", () => Doc(r => Tiny.Disease(r)["seedPpm"] = 100));
        yield return (WorldCodes.Director, "relaxes at the peak", () => Doc(r => Tiny.Teller(r, "t")["relaxBelow"] = 500));
        yield return (WorldCodes.Director, "a gap that ends before it begins", () => Doc(r => Tiny.Teller(r, "t")["gapMin"] = 9));
        yield return (WorldCodes.Director, "a bad card that relieves stress", () => Doc(r => Tiny.Card(r, "c-bad")["intensity"] = -5));
        yield return (WorldCodes.Director, "a good card that adds stress", () => Doc(r => Tiny.Card(r, "c-good")["intensity"] = 5));
        yield return (WorldCodes.Effect, "an effect of two kinds", () => Doc(r => Tiny.Rung(r, 0)["effects"]!.AsArray().Add(new JsonObject { ["signal"] = "x", ["shock"] = "y" })));
        yield return (WorldCodes.Effect, "an effect of no kind", () => Doc(r => Tiny.Rung(r, 0)["effects"]!.AsArray().Add(new JsonObject())));
        yield return (WorldCodes.Effect, "a modifier in an outbreak's effects", () => Doc(r => Tiny.Disease(r)["onOutbreak"]!.AsArray().Add(new JsonObject { ["modifier"] = "vis", ["delta"] = -1 })));
        yield return (WorldCodes.Effect, "a global card that starts a hazard", () => Doc(r => Tiny.Card(r, "c-global")["effects"]!.AsArray().Add(new JsonObject { ["hazard"] = "h", ["severity"] = "low" })));
        yield return (WorldCodes.Effect, "a global card that starts an outbreak", () => Doc(r => Tiny.Card(r, "c-global")["effects"]!.AsArray().Add(WithCases(Effect("outbreak", "d")))));
        yield return (WorldCodes.Links, "a region linked to itself", () => Doc(r => Tiny.Array(r, "links").Add(new JsonObject { ["a"] = "a", ["b"] = "a", ["flowPpm"] = 1 })));
        yield return (WorldCodes.Links, "two regions linked twice", () => Doc(r => Tiny.Array(r, "links").Add(new JsonObject { ["a"] = "b", ["b"] = "a", ["flowPpm"] = 1 })));
    }

    private static JsonObject WithCases(JsonObject effect)
    {
        effect["cases"] = 1;
        return effect;
    }

    private static JsonObject MiniHazard(string id, string excludes)
    {
        var copy = Tiny.Copy(Tiny.Hazard(Tiny.Root())).AsObject();
        copy["id"] = id;
        copy["key"] = "h." + id;
        copy["excludes"] = excludes.Length == 0 ? new JsonArray() : new JsonArray(excludes);
        return copy;
    }

    /// <summary>Calls that are wrong: the code, a name, and the call. Each is made on a fresh runtime of the small document.</summary>
    private static IEnumerable<(string Code, string Name, Action<WorldRuntime> Call)> Calls()
    {
        yield return (WorldCodes.UnknownRegion, "intervene in an unknown region", r => r.Intervene("zz", "quar", true));
        yield return (WorldCodes.UnknownIntervention, "an unknown intervention", r => r.Intervene("a", "zz", true));
        yield return (WorldCodes.AlreadyActive, "start an intervention twice", r => { r.Intervene("a", "quar", true); r.Intervene("a", "quar", true); });
        yield return (WorldCodes.NotApplicable, "stop an intervention that was not started", r => r.Intervene("a", "quar", false));
        yield return (WorldCodes.UnknownProfile, "an unknown storyteller", r => r.UseStoryteller("zz"));
        yield return (WorldCodes.UnknownProfile, "an unknown difficulty", r => r.UseDifficulty("zz"));
        yield return (WorldCodes.UnknownHazard, "an unknown hazard", r => r.BeginHazard("a", "zz", "low"));
        yield return (WorldCodes.UnknownSeverity, "an unknown severity", r => r.BeginHazard("a", "h", "zz"));
        yield return (WorldCodes.AlreadyActive, "a hazard that is already going", r => { r.BeginHazard("a", "h", "low"); r.BeginHazard("a", "h", "low"); });
        yield return (WorldCodes.UnknownDisease, "an unknown disease", r => r.SeedOutbreak("a", "zz", 1));
        yield return (WorldCodes.Count, "no cases", r => r.SeedOutbreak("a", "d", 0));
        yield return (WorldCodes.NotApplicable, "an outbreak where no one is susceptible", r => { r.SeedOutbreak("b", "d", 4000); r.SeedOutbreak("b", "d", 1); });
        yield return (WorldCodes.UnknownCard, "an unknown card", r => r.PlayCard("zz", "a"));
        yield return (WorldCodes.NotApplicable, "a region for a global card", r => r.PlayCard("c-global", "a"));
        yield return (WorldCodes.UnknownRegion, "a regional card without a region", r => r.PlayCard("c-bad", ""));
        yield return (WorldCodes.UnknownSeason, "a season the document lacks", r => r.Advance(0, _ => "zz", Fixture.Host()));
        yield return (WorldCodes.StepOrder, "time going back", r => { Fixture.Live(r, 5); r.Advance(3, r.CycleSeasonAt, Fixture.Host()); });
        yield return (WorldCodes.StepOrder, "a jump beyond the limit", r => r.Advance(WorldRuntime.MaxAdvance + 1, r.CycleSeasonAt, Fixture.Host()));
        yield return (WorldCodes.FactMissing, "a host fact not given", r => r.Advance(0, r.CycleSeasonAt, new Dictionary<string, FactValue>()));
        yield return (WorldCodes.FactInvalid, "a host fact of the wrong type", r => r.Advance(0, r.CycleSeasonAt, new Dictionary<string, FactValue> { ["ext.alert"] = FactValue.From("x") }));
        yield return (WorldCodes.FactInvalid, "a host fact out of range", r => r.Advance(0, r.CycleSeasonAt, Fixture.Host(11)));
        yield return (WorldCodes.UnknownRegion, "facts of an unknown region", r => r.Facts("ext.", "zz"));
        yield return (WorldCodes.Capacity, "more hazards than the list holds", r =>
        {
            for (var i = 0; i < 9; i++) r.BeginHazard(i % 2 == 0 ? "a" : "b", "h", "low");
        });
    }

    private static readonly HashSet<string> Reached = new();

    [Fact]
    public void EveryDocumentRefusalIsReachedByItsCase()
    {
        foreach (var (code, name, text) in Documents())
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => WorldLoader.Load(text()));
            Assert.True(code == refusal.Code, name + " was refused with " + refusal.Code + ": " + refusal.Message + " (expected " + code + ")");
            lock (Reached) Reached.Add(code);
        }
    }

    [Fact]
    public void EveryCallRefusalIsReachedByItsCaseAndLeavesTheStateAsItWas()
    {
        foreach (var (code, name, call) in Calls())
        {
            var runtime = Fixture.MiniWorld(root =>
            {
                // The capacity case needs a list that is small: one hazard fills it only if it is the second region.
                if (name == "more hazards than the list holds") root["limits"]!["maxHazards"] = 1;
            });
            var before = runtime.StateDigest();
            var refusal = Assert.Throws<EngineRefusedException>(() => call(runtime));
            Assert.True(code == refusal.Code, name + " was refused with " + refusal.Code + ": " + refusal.Message + " (expected " + code + ")");
            lock (Reached) Reached.Add(code);
            if (name is "time going back" or "start an intervention twice" or "a hazard that is already going" or "an outbreak where no one is susceptible" or "more hazards than the list holds") continue;    // these cases change state before the refused call
            Assert.True(before == runtime.StateDigest(), name + " changed the state");
        }
    }

    [Fact]
    public void ARefusedAdvanceLeavesTheWorldAsItWasEvenAfterStepsWereLived()
    {
        var runtime = Fixture.MiniWorld();
        Fixture.Live(runtime, 12);
        var before = runtime.StateDigest();
        Assert.Equal(WorldCodes.FactMissing, Fixture.Refusal(() => runtime.Advance(20, runtime.CycleSeasonAt, new Dictionary<string, FactValue>())));
        Assert.Equal(WorldCodes.UnknownSeason, Fixture.Refusal(() => runtime.Advance(20, step => step < 15 ? "warm" : "zz", Fixture.Host())));
        Assert.Equal(before, runtime.StateDigest());
        Assert.Equal(12, runtime.Step);
    }

    [Fact]
    public void EverySaveRefusalIsReachedByItsCase()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = WorldSave.Write(runtime);
        void Expect(string code, Action open) { Assert.Equal(code, Fixture.Refusal(open)); lock (Reached) Reached.Add(code); }
        Expect(WorldCodes.SaveFormat, () => WorldSave.Open(set, Encoding.UTF8.GetBytes("{}")));
        Expect(WorldCodes.SaveFormat, () => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Format = 1)));
        Expect(WorldCodes.SaveDocument, () => WorldSave.Open(Fixture.Valley, bytes));
        Expect(WorldCodes.SaveDigest, () => WorldSave.Open(set, Tamper.Stale(bytes, i => i.Stress = i.Stress == 0 ? 1 : 0)));
        Expect(WorldCodes.SaveState, () => WorldSave.Open(set, Tamper.Forge(bytes, i => i.Step = -3)));
    }

    [Fact]
    public void TheBranchRefusalIsReachedByItsCase()
    {
        var runtime = Tamper.Played(out _);
        Assert.Equal(WorldCodes.BranchDocument, Fixture.Refusal(() => runtime.Branch(Fixture.Mini(root => root["id"] = runtime.Set.Id + "-other"))));
        lock (Reached) Reached.Add(WorldCodes.BranchDocument);
    }

    [Fact]
    public void TheForcedOnsetRefusalIsReachedByItsCase()
    {
        var runtime = Tamper.Played(out var set);
        Assert.Equal(WorldCodes.ForcedOnset, Fixture.Refusal(() => runtime.BranchForcingOnset(set, new ForcedOnset(0, 0, runtime.Step, 300_000))));
        lock (Reached) Reached.Add(WorldCodes.ForcedOnset);
    }

    [Fact]
    public void TheCodesTheMatrixReachesAreExactlyTheConstantsOfTheCodesClass()
    {
        EveryDocumentRefusalIsReachedByItsCase();
        EveryCallRefusalIsReachedByItsCaseAndLeavesTheStateAsItWas();
        EverySaveRefusalIsReachedByItsCase();
        TheBranchRefusalIsReachedByItsCase();
        TheForcedOnsetRefusalIsReachedByItsCase();
        var declared = typeof(WorldCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Assert.True(declared.Count >= 30);
        Assert.Empty(declared.Except(Reached));
        Assert.Empty(Reached.Except(declared));
        Assert.All(declared, code => Assert.StartsWith("WE_", code));
        Assert.Equal(declared.Count, declared.Distinct().Count());
    }
}
