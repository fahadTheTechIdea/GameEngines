using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>Every way a document can be wrong, each refused whole with its own code and nothing repaired.</summary>
public sealed class RefusalMatrixTests
{
    private static JsonObject Type(JsonObject root, string id) => Fixture.Type(root, id);

    private static JsonArray Clauses(JsonObject root, string id) => Type(root, id)["clauses"]!.AsArray();

    public static readonly Dictionary<string, (string Code, Action<JsonObject> Edit)> Cases = new()
    {
        // format
        ["format other than 1"] = (DipCodes.Format, r => r["format"] = 2),
        ["id with a capital"] = (DipCodes.Format, r => r["id"] = "Isles"),
        ["seasons not an array"] = (DipCodes.Format, r => r["seasons"] = 5),
        ["time not an object"] = (DipCodes.Format, r => r["time"] = "daily"),
        ["a number that is text"] = (DipCodes.Format, r => r["seed"] = "seven"),
        ["a consent that is none of them"] = (DipCodes.Format, r => Type(r, "nap")["consent"] = "forced"),
        ["a clause kind that is none of them"] = (DipCodes.Format, r => Clauses(r, "nap")[0]!["kind"] = "friendship"),
        ["a fact type that is none of them"] = (DipCodes.Format, r => r["facts"]![0]!["type"] = "float"),
        ["a fact range on a string"] = (DipCodes.Format, r => { r["facts"] = new JsonArray(new JsonObject { ["id"] = "x.y", ["type"] = "string", ["min"] = 1 }); }),
        ["an affinity that is text"] = (DipCodes.Format, r => r["ideologies"]![0]!["affinity"]![0] = "high"),
        ["a mission kind that is none of them"] = (DipCodes.Format, r => Fixture.Mission(r, "spy")["kind"] = "poison"),
        ["a payment effect declared"] = (DipCodes.Format, r => Type(r, "nap")["onSign"]![0]!["kind"] = "payment"),
        ["a faction event that is none of them"] = (DipCodes.Format, r => Fixture.Realm(r, "aurel")["factions"]![0]!["likes"]![0]!["event"] = "earthquake"),
        ["a control that is none of them"] = (DipCodes.Format, r => Fixture.Realm(r, "aurel")["control"] = "robot"),
        ["a weights table that is not an object"] = (DipCodes.Format, r => Type(r, "nap")["weights"] = 5),
        ["a condition that is not a condition"] = (DipCodes.UnknownField, r => Fixture.Cb(r, "rivalry")["requires"] = new JsonObject { ["often"] = true }),
        ["an excluded type that is not text"] = (DipCodes.Reference, r => Type(r, "trade")["excludes"] = new JsonArray(5)),
        ["a trait that is not text"] = (DipCodes.Reference, r => Fixture.Realm(r, "aurel")["traits"] = new JsonArray(5)),

        // fields
        ["an unknown field in the document"] = (DipCodes.UnknownField, r => r["extra"] = 1),
        ["an unknown field in a type"] = (DipCodes.UnknownField, r => Type(r, "nap")["extra"] = 1),
        ["an unknown field in a realm"] = (DipCodes.UnknownField, r => Fixture.Realm(r, "aurel")["extra"] = 1),
        ["an unknown field in a rule"] = (DipCodes.UnknownField, r => Fixture.Rules(r, "war")["extra"] = 1),
        ["an unknown weight"] = (DipCodes.UnknownField, r => Type(r, "nap")["weights"]!["charm"] = 5),
        ["a clause with a field of another kind"] = (DipCodes.Clause, r => Clauses(r, "nap")[0]!["graceDays"] = 3),
        ["a missing section"] = (DipCodes.MissingField, r => r.Remove("rules")),
        ["a missing limit"] = (DipCodes.MissingField, r => r["limits"]!.AsObject().Remove("maxWars")),
        ["a type with no breach"] = (DipCodes.MissingField, r => Type(r, "nap").Remove("breach")),
        ["a realm with no standing"] = (DipCodes.MissingField, r => Fixture.Realm(r, "aurel").Remove("standing")),
        ["a whole-number fact with no range"] = (DipCodes.MissingField, r => r["facts"]![0]!.AsObject().Remove("max")),
        ["a standing casus belli with no condition"] = (DipCodes.MissingField, r => Fixture.Cb(r, "rivalry").Remove("requires")),
        ["a granted casus belli with a condition"] = (DipCodes.Format, r => Fixture.Cb(r, "border")["requires"] = Fixture.Cond("regard", "lt", 0)),

        // ranges
        ["steps per day zero"] = (DipCodes.Range, r => r["time"]!["stepsPerDay"] = 0),
        ["one realm only"] = (DipCodes.Range, r => { var realms = r["realms"]!.AsArray(); while (realms.Count > 1) realms.RemoveAt(1); }),
        ["a deed lasting no days"] = (DipCodes.Range, r => Fixture.Deed(r, "gift")["days"] = 0),
        ["a deed worth nothing"] = (DipCodes.Range, r => Fixture.Deed(r, "gift")["value"] = 0),
        ["a regard weight past two thousand"] = (DipCodes.Range, r => Fixture.Rules(r, "regard")["ideology"] = 2001),
        ["a term with a maximum under its minimum"] = (DipCodes.Range, r => Type(r, "nap")["maxDays"] = 5),
        ["a type with a term that cannot be zero and a minimum of zero"] = (DipCodes.Range, r => Type(r, "nap")["minDays"] = 0),
        ["a mutual type that waits no days to expire"] = (DipCodes.Range, r => Type(r, "nap")["expireDays"] = 0),
        ["a consideration after the expiry"] = (DipCodes.Range, r => Type(r, "nap")["considerDays"] = 11),
        ["a stability past a thousand"] = (DipCodes.Range, r => Fixture.Realm(r, "aurel")["stability"] = 1001),
        ["an affinity list of the wrong length"] = (DipCodes.Range, r => r["ideologies"]![0]!["affinity"]!.AsArray().RemoveAt(0)),
        ["an affinity past a thousand"] = (DipCodes.Range, r => r["ideologies"]![0]!["affinity"]![0] = 1001),
        ["more agents than the limit"] = (DipCodes.Range, r => r["limits"]!["maxAgents"] = 3),
        ["a faction with no share"] = (DipCodes.Range, r => Fixture.Realm(r, "aurel")["factions"]![0]!["share"] = 0),
        ["a realm with no factions"] = (DipCodes.Range, r => Fixture.Realm(r, "aurel")["factions"] = new JsonArray()),
        ["a skill past a thousand"] = (DipCodes.Range, r => r["agents"]![0]!["skill"] = 1001),
        ["a seed below zero"] = (DipCodes.Range, r => r["seed"] = -1),
        ["a mission of no days"] = (DipCodes.Range, r => Fixture.Mission(r, "spy")["days"] = 0),
        ["a bias past the limit"] = (DipCodes.Range, r => Type(r, "nap")["bias"] = 100001),
        ["a weight past five thousand"] = (DipCodes.Range, r => Type(r, "nap")["weights"]!["regard"] = 5001),

        // duplicates
        ["a realm declared twice"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "brass")["id"] = "aurel"),
        ["a type declared twice"] = (DipCodes.Duplicate, r => Type(r, "trade")["id"] = "nap"),
        ["a deed declared twice"] = (DipCodes.Duplicate, r => Fixture.Deed(r, "insult")["id"] = "gift"),
        ["a mission declared twice"] = (DipCodes.Duplicate, r => Fixture.Mission(r, "unrest")["id"] = "spy"),
        ["an agent declared twice"] = (DipCodes.Duplicate, r => r["agents"]![1]!["id"] = "agent-aurel-1"),
        ["a season declared twice"] = (DipCodes.Duplicate, r => r["seasons"]![1]!["id"] = "spring"),
        ["a trait given twice to one realm"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "aurel")["traits"] = new JsonArray("loyal", "loyal")),
        ["a market shared by two realms"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "brass")["market"] = "aurel-market"),
        ["a leader leading two realms"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "brass")["leader"] = "king-aurel"),
        ["a clause repeated"] = (DipCodes.Duplicate, r => Clauses(r, "nap").Add(Fixture.Copy(Clauses(r, "nap")[0]!))),
        ["a type excluding another twice"] = (DipCodes.Duplicate, r => Type(r, "trade")["excludes"] = new JsonArray("embargo", "embargo")),
        ["a faction event repeated"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "aurel")["factions"]![0]!["likes"]!.AsArray().Add(Fixture.Copy(Fixture.Realm(r, "aurel")["factions"]![0]!["likes"]![0]!))),
        ["a faction declared twice in a realm"] = (DipCodes.Duplicate, r => Fixture.Realm(r, "aurel")["factions"]![1]!["id"] = "court"),
        ["a fact declared twice"] = (DipCodes.Duplicate, r => r["facts"]!.AsArray().Add(Fixture.Copy(r["facts"]![0]!))),
        ["a modifier declared twice"] = (DipCodes.Duplicate, r => { var m = Type(r, "nap")["modifiers"]!.AsArray(); m.Add(Fixture.Copy(m[0]!)); }),

        // references
        ["an ideology that is not declared"] = (DipCodes.Reference, r => Fixture.Realm(r, "aurel")["ideology"] = "mystics"),
        ["a trait that is not declared"] = (DipCodes.Reference, r => Fixture.Realm(r, "aurel")["traits"] = new JsonArray("brave")),
        ["a type excluding one that is not declared"] = (DipCodes.Reference, r => Type(r, "trade")["excludes"] = new JsonArray("treaty")),
        ["a type excluding itself"] = (DipCodes.Clause, r => Type(r, "trade")["excludes"] = new JsonArray("trade")),
        ["a breach deed that is not declared"] = (DipCodes.Reference, r => Type(r, "nap")["breach"]!["deed"] = "grudge"),
        ["a sign deed that is not declared"] = (DipCodes.Reference, r => Type(r, "nap")["signDeed"] = "grudge"),
        ["a breach casus belli that is not declared"] = (DipCodes.Reference, r => Type(r, "nap")["breach"]!["casusBelli"] = "grudge"),
        ["a breach casus belli that is never granted"] = (DipCodes.Reference, r => Type(r, "nap")["breach"]!["casusBelli"] = "rivalry"),
        ["a breach casus belli for no days"] = (DipCodes.Range, r => Type(r, "nap")["breach"]!["cbDays"] = 0),
        ["a balance casus belli that is not declared"] = (DipCodes.Reference, r => Fixture.Rules(r, "balance")["casusBelli"] = "grudge"),
        ["a succession casus belli that is not declared"] = (DipCodes.Reference, r => Fixture.Rules(r, "politics")["succession"]!["casusBelli"] = "grudge"),
        ["a witness deed that is not declared"] = (DipCodes.Reference, r => Fixture.Rules(r, "standing")["witnessDeed"] = "grudge"),
        ["a declared-war deed that is not declared"] = (DipCodes.Reference, r => Fixture.Rules(r, "war")["declaredDeed"] = "grudge"),
        ["an agent of a realm that is not declared"] = (DipCodes.Reference, r => r["agents"]![0]!["realm"] = "nowhere"),
        ["a mission deed that is not declared"] = (DipCodes.Reference, r => Fixture.Mission(r, "spy")["deed"] = "grudge"),
        ["a mission casus belli that is not granted"] = (DipCodes.Reference, r => Fixture.Mission(r, "sabotage")["casusBelli"] = "rivalry"),
        ["a tribute season that is not declared"] = (DipCodes.Reference, r => Clauses(r, "vassalage")[1]!["dueSeason"] = "monsoon"),
        ["a condition reading a fact that is not there"] = (DipCodes.Reference, r => Fixture.Cb(r, "rivalry")["requires"] = Fixture.Cond("prestige", "lt", 0)),
        ["a condition reading a treaty that is not declared"] = (DipCodes.Reference, r => Fixture.Cb(r, "rivalry")["requires"] = Fixture.Cond("treaty.charter", "eq", true)),
        ["a standing casus belli reading a granted one of its own kind"] = (DipCodes.Reference, r => Fixture.Cb(r, "rivalry")["requires"] = Fixture.Cond("cb.rivalry", "eq", true)),
        ["a modifier reading a fact that is not there"] = (DipCodes.Reference, r => Type(r, "nap")["modifiers"]![0]!["when"] = Fixture.Cond("crisis", "lt", 5)),

        // reserved
        ["a host fact named like a pair fact"] = (DipCodes.Reserved, r => r["facts"]![0]!["id"] = "regard"),
        ["a host fact under first"] = (DipCodes.Reserved, r => r["facts"]![0]!["id"] = "first.mood"),
        ["a host fact under treaty"] = (DipCodes.Reserved, r => r["facts"]![0]!["id"] = "treaty.mood"),
        ["a host fact under cb"] = (DipCodes.Reserved, r => r["facts"]![0]!["id"] = "cb.mood"),

        // clauses
        ["a truce that may not be made in a war"] = (DipCodes.Clause, r => Type(r, "ceasefire")["duringWar"] = false),
        ["a peace that may not be made in a war"] = (DipCodes.Clause, r => Type(r, "peace")["duringWar"] = false),
        ["a treaty that pauses and ends a war"] = (DipCodes.Clause, r => Type(r, "ceasefire")["endsWar"] = true),
        ["a peace that is imposed"] = (DipCodes.Clause, r => { Type(r, "peace")["consent"] = "imposed"; Type(r, "peace")["considerDays"] = 0; Type(r, "peace")["expireDays"] = 0; }),
        ["an imposed treaty that is considered"] = (DipCodes.Clause, r => Type(r, "embargo")["considerDays"] = 1),
        ["an imposed treaty that expires"] = (DipCodes.Clause, r => Type(r, "embargo")["expireDays"] = 1),
        ["a tribute without its fields"] = (DipCodes.Clause, r => Clauses(r, "vassalage")[1]!.AsObject().Remove("graceDays")),
        ["a defend without mutual"] = (DipCodes.Clause, r => Clauses(r, "alliance")[0]!.AsObject().Remove("mutual")),
        ["a tribute with a field of another kind"] = (DipCodes.Clause, r => Clauses(r, "vassalage")[1]!["mutual"] = true),

        // rules
        ["a reject line above the accept line"] = (DipCodes.Rules, r => Fixture.Rules(r, "acceptance")["rejectAt"] = 5),
        ["a relax line above the threat line"] = (DipCodes.Rules, r => Fixture.Rules(r, "balance")["relaxLine"] = 451),
        ["politics: the revolt line at the unrest line"] = (DipCodes.Politics, r => Fixture.Rules(r, "politics")["revoltLine"] = 400),
        ["politics: the unrest line and its margin past a thousand"] = (DipCodes.Politics, r => { Fixture.Rules(r, "politics")["unrestLine"] = 600; Fixture.Rules(r, "politics")["hysteresis"] = 500; }),
        ["espionage: levels that do not rise"] = (DipCodes.Espionage, r => Fixture.Rules(r, "espionage")["levels"] = new JsonArray(100, 100, 600, 900)),
        ["espionage: errors that rise"] = (DipCodes.Espionage, r => Fixture.Rules(r, "espionage")["errors"] = new JsonArray(1000, 500, 600, 100, 0)),
        ["espionage: a shock on a mission that is not a sabotage"] = (DipCodes.Espionage, r => Fixture.Mission(r, "spy")["shock"] = "shock"),
        ["espionage: damage to stability from a spying mission"] = (DipCodes.Espionage, r => Fixture.Mission(r, "spy")["stabilityHit"] = 100),
    };

    public static IEnumerable<object[]> CaseNames() => Cases.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void ADocumentWrongInOnePlaceIsRefusedWithTheCodeOfThatPlace(string name)
    {
        var root = Fixture.Root();
        var (code, edit) = Cases[name];
        edit(root);
        Assert.Equal(code, Fixture.Refusal(() => DipLoader.Load(root.ToJsonString()), name));
    }

    [Fact]
    public void ThereAreAboutAHundredCasesAndEveryDocumentCodeIsReachedByOne()
    {
        Assert.True(Cases.Count >= 95, "only " + Cases.Count);
        foreach (var code in new[] { DipCodes.Format, DipCodes.UnknownField, DipCodes.MissingField, DipCodes.Range, DipCodes.Duplicate, DipCodes.Reference, DipCodes.Reserved, DipCodes.Clause, DipCodes.Rules, DipCodes.Politics, DipCodes.Espionage })
            Assert.Contains(Cases.Values, c => c.Code == code);
    }

    [Fact]
    public void ARepeatedFieldInTheTextOfTheDocumentIsRefused()
    {
        var text = Fixture.IslesText.Replace("\"seed\": 20261029,", "\"seed\": 20261029, \"seed\": 5,", StringComparison.Ordinal);
        Assert.NotEqual(Fixture.IslesText, text);
        Assert.Equal(DipCodes.Duplicate, Fixture.Refusal(() => DipLoader.Load(text)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"format\":1}")]
    public void TextThatIsNotADocumentIsRefused(string text) => Assert.NotNull(Fixture.Refusal(() => DipLoader.Load(text)));

    [Fact]
    public void ADocumentOverTheSizeLimitIsRefusedAtOnce()
    {
        Assert.Equal(DipCodes.Range, Fixture.Refusal(() => DipLoader.Load(new string(' ', DipLoader.MaxCharacters + 1))));
        Assert.Throws<ArgumentNullException>(() => DipLoader.Load(null!));
    }

    [Fact]
    public void TheDocumentOfThePlainTestsLoadsAndSoDoesAnEmptyListOfOptionalThings()
    {
        var set = Fixture.Plain(r =>
        {
            r["missions"] = new JsonArray();
            r["traits"] = new JsonArray();
            r["agents"] = new JsonArray();
            r["facts"] = new JsonArray();
            foreach (var realm in r["realms"]!.AsArray()) realm!["traits"] = new JsonArray();
            foreach (var type in r["types"]!.AsArray()) type!["modifiers"] = new JsonArray();
        });
        Assert.Empty(set.Missions);
        Assert.Empty(set.Agents);
        Assert.Empty(set.HostFacts);
        Assert.Equal(5, set.Realms.Count);
    }

    [Fact]
    public void ALoadedSetIsDescribedByItsDeclarations()
    {
        var set = Fixture.Isles;
        Assert.Equal(("isles", 20261029UL, 1), (set.Id, set.Seed, set.StepsPerDay));
        Assert.Equal(64, set.Digest.Length);
        Assert.Equal(Digest.Of(Fixture.IslesText), set.Digest);
        Assert.Equal(["alliance-broken", "alliance-formed", "ceasefire-broken", "ceasefire-signed", "marriage-made", "peace-broken", "peace-signed", "realm-calm", "realm-revolt", "realm-unrest", "succession", "succession-contested", "treaty-broken", "treaty-signed", "vassal-defied", "vassalage-sworn"], set.Signals.ToArray());
        Assert.Equal(["embargo", "embargo-lifted", "sabotage", "trade-boom", "trade-bust"], set.Shocks.ToArray());
        Assert.Equal(9 + 18 + 8 + 5 + 1, set.PairFacts.Count);
    }
}
