using System.Reflection;
using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>Every refusal code of the engine is reached by at least one input, and each input is refused with the code it is listed under. The set of codes the cases reach must equal the constants of <see cref="EconomyCodes"/>.</summary>
public sealed class RefusalMatrixTests
{
    private sealed record Case(string Name, string Code, Func<string> Run);

    private static Case Doc(string name, string code, Action<JsonObject> edit) => new(name, code, () => Fixture.Refusal(() => EconomyLoader.Load(Edit.Doc(edit)), name));

    private static Case Text(string name, string code, Func<string> text) => new(name, code, () => Fixture.Refusal(() => EconomyLoader.Load(text()), name));

    private static Case Call(string name, string code, Action<EconomyRuntime> call) => new(name, code, () => Fixture.Refusal(() => call(Fixture.Runtime()), name));

    private static JsonObject Effect(string kind, string good = "", string tag = "", string route = "", long amount = 100) => new() { ["kind"] = kind, ["good"] = good, ["tag"] = tag, ["route"] = route, ["amount"] = amount };

    private static Case Shock(string name, params JsonObject[] effects) => Doc(name, EconomyCodes.Shock, root => Edit.ById(root, "shocks", "drought")["effects"] = new JsonArray(effects.Select(e => (JsonNode)e).ToArray()));

    private static readonly List<Case> Cases = new()
    {
        // ---- the document ----
        Doc("the old format 1", EconomyCodes.Format, root => root["format"] = 1),
        Doc("the retired format 2", EconomyCodes.Format2, root => root["format"] = 2),
        Doc("the retired format 3", EconomyCodes.Format3, root => root["format"] = 3),
        Doc("the retired format 4", EconomyCodes.Format4, root => root["format"] = 4),
        Doc("the retired format 5", EconomyCodes.Format5, root => root["format"] = 5),
        Doc("a format from the future", EconomyCodes.Format, root => root["format"] = 7),
        Doc("a routing mode that does not exist", EconomyCodes.Format, root => root["routing"]!["mode"] = "teleport"),
        Doc("a routing mode of the wrong type", EconomyCodes.Format, root => root["routing"]!["mode"] = 5),
        Doc("recipes that are not an array", EconomyCodes.Format, root => root["recipes"] = "bread"),
        Doc("an empty object as a recipe", EconomyCodes.MissingField, root => root["recipes"]!.AsArray().Add(new JsonObject())),
        Doc("a recipe naming a good twice on one side", EconomyCodes.Recipe, root => Edit.ById(root, "recipes", "mill")["inputs"]!.AsArray().Add(new JsonObject { ["good"] = "grain", ["amount"] = 1 })),
        Doc("a recipe that consumes nothing", EconomyCodes.Range, root => Edit.ById(root, "recipes", "mill")["inputs"] = new JsonArray()),
        Doc("two recipes that feed each other", EconomyCodes.RecipeCycle, root => root["recipes"]!.AsArray().Add(new JsonObject { ["id"] = "unmill", ["key"] = "econ.recipe.mill", ["inputs"] = new JsonArray(new JsonObject { ["good"] = "flour", ["amount"] = 2 }), ["outputs"] = new JsonArray(new JsonObject { ["good"] = "grain", ["amount"] = 3 }), ["duration"] = 1, ["locked"] = false })),
        Doc("a line in a market that does not trade the recipe goods", EconomyCodes.Line, root => Edit.Market(root, "seaport")["lines"]!.AsArray().Add(new JsonObject { ["recipe"] = "mill", ["capacity"] = 5 })),
        Doc("a line for a recipe that does not exist", EconomyCodes.Reference, root => Edit.Market(root, "riverton")["lines"]!.AsArray().Add(new JsonObject { ["recipe"] = "nothing", ["capacity"] = 5 })),
        Doc("a recipe run twice in one market", EconomyCodes.Duplicate, root => Edit.Market(root, "riverton")["lines"]!.AsArray().Add(new JsonObject { ["recipe"] = "mill", ["capacity"] = 5 })),
        Doc("no routing", EconomyCodes.MissingField, root => root.Remove("routing")),
        Doc("no recipes", EconomyCodes.MissingField, root => root.Remove("recipes")),
        Doc("an unknown field in routing", EconomyCodes.UnknownField, root => root["routing"]!["maxHops"] = 3),
        Text("not JSON", EconomyCodes.Format, () => "{"),
        Text("an array", EconomyCodes.Format, () => "[]"),
        Doc("an unknown policy kind", EconomyCodes.Format, root => Edit.Obj(root, "policies", 0)["kind"] = "bogus"),
        Doc("a text where a number belongs", EconomyCodes.Format, root => root["seed"] = "seven"),
        Doc("an upper case id", EconomyCodes.Format, root => Edit.Obj(root, "goods", 0)["id"] = "Grain"),
        Doc("a condition that is not one", EconomyCodes.UnknownField, root => Edit.Obj(root, "modifiers", 0)["when"] = new JsonObject { ["maybe"] = 1 }),
        Doc("an unknown field", EconomyCodes.UnknownField, root => root["surprise"] = 1),
        Doc("an unknown field in a market", EconomyCodes.UnknownField, root => Edit.Obj(root, "markets", 0)["surprise"] = 1),
        Doc("a missing top level field", EconomyCodes.MissingField, root => root.Remove("limits")),
        Doc("a missing field in a good", EconomyCodes.MissingField, root => Edit.Obj(root, "goods", 0).Remove("speed")),
        Doc("an int fact without a range", EconomyCodes.MissingField, root => Edit.ById(root, "facts", "story.plague").Remove("max")),
        Doc("steps per day zero", EconomyCodes.Range, root => root["time"]!["stepsPerDay"] = 0),
        Doc("a floor above a thousand", EconomyCodes.Range, root => Edit.Obj(root, "goods", 0)["floor"] = 1001),
        Doc("stock above capacity", EconomyCodes.Range, root => Edit.Pair(root, "riverton", "grain")["stock"] = 1_000_000),
        Doc("more loans than the limit", EconomyCodes.Range, root => root["limits"]!["maxLoans"] = 0),
        Doc("a negative treasury", EconomyCodes.Range, root => Edit.Market(root, "riverton")["treasury"] = -1),
        Doc("an anchor of zero", EconomyCodes.Range, root => Edit.Obj(root, "currencies", 1)["anchor"] = 0),
        Text("a repeated property", EconomyCodes.Duplicate, () => Fixture.TownText.Replace("\"format\": 6", "\"format\": 6, \"format\": 6", StringComparison.Ordinal)),
        Doc("two goods with one id", EconomyCodes.Duplicate, root => root["goods"]!.AsArray().Add(Edit.Copy(root["goods"]![0]!))),
        Doc("two markets with one id", EconomyCodes.Duplicate, root => root["markets"]!.AsArray().Add(Edit.Copy(root["markets"]![0]!))),
        Doc("a good traded twice in a market", EconomyCodes.Duplicate, root => Edit.Market(root, "riverton")["pairs"]!.AsArray().Add(Edit.Copy(Edit.Pair(root, "riverton", "grain")))),
        Doc("a policy listed twice", EconomyCodes.Duplicate, root => Edit.Market(root, "riverton")["policies"]!.AsArray().Add("sales-tax")),
        Doc("a tag listed twice", EconomyCodes.Duplicate, root => Edit.Obj(root, "goods", 0)["tags"] = new JsonArray("food", "food")),
        Doc("a fact declared twice", EconomyCodes.Duplicate, root => root["facts"]!.AsArray().Add(Edit.Copy(root["facts"]![0]!))),
        Doc("a pair of an undeclared good", EconomyCodes.Reference, root => Edit.Pair(root, "riverton", "grain")["good"] = "nothing"),
        Doc("a market with an undeclared currency", EconomyCodes.Reference, root => Edit.Market(root, "riverton")["currency"] = "gold"),
        Doc("a route to an undeclared market", EconomyCodes.Reference, root => Edit.Obj(root, "routes", 0)["to"] = "nowhere"),
        Doc("a policy that is not declared", EconomyCodes.Reference, root => Edit.Market(root, "riverton")["policies"]!.AsArray().Add("no-such")),
        Doc("a loan from an undeclared lender", EconomyCodes.Reference, root => Edit.Obj(root, "loans", 0)["lender"] = "nowhere"),
        Doc("a condition on an undeclared fact", EconomyCodes.Reference, root => Edit.Obj(root, "modifiers", 0)["when"]!["fact"] = "cal.nothing"),
        Doc("a host fact in the engine's namespace", EconomyCodes.Reserved, root => root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "market.treasury", ["type"] = "int", ["min"] = 0, ["max"] = 1 })),
        Doc("a host fact in the currency namespace", EconomyCodes.Reserved, root => root["facts"]!.AsArray().Add(new JsonObject { ["id"] = "currency.x", ["type"] = "bool" })),
        Doc("no reference currency", EconomyCodes.Currency, root => Edit.Obj(root, "currencies", 0)["reference"] = false),
        Doc("two reference currencies", EconomyCodes.Currency, root => { Edit.Obj(root, "currencies", 1)["reference"] = true; Edit.Obj(root, "currencies", 1)["anchor"] = 1_000_000; }),
        Doc("a reference currency with another anchor", EconomyCodes.Currency, root => Edit.Obj(root, "currencies", 0)["anchor"] = 900_000),
        Doc("a good worth nothing in a currency", EconomyCodes.Price, root => { Edit.Obj(root, "currencies", 1)["anchor"] = 1_000_000_000_000; Edit.Obj(root, "goods", 0)["base"] = 1000; }),
        Doc("the first band not at zero", EconomyCodes.Band, root => Edit.Obj(root, "bands", 0)["from"] = 5),
        Doc("bands not ascending", EconomyCodes.Band, root => Edit.Obj(root, "bands", 2)["from"] = 700),
        Doc("an offset past every", EconomyCodes.Schedule, root => Edit.Obj(root, "schedules", 0)["offset"] = 7),
        Doc("a sales tax with no schedule", EconomyCodes.Policy, root => Edit.ById(root, "policies", "sales-tax")["schedule"] = ""),
        Doc("a sales tax with a rate of zero", EconomyCodes.Policy, root => Edit.ById(root, "policies", "sales-tax")["rate"] = 0),
        Doc("a subsidy with a rate", EconomyCodes.Policy, root => Edit.ById(root, "policies", "farm-subsidy")["rate"] = 10),
        Doc("a subsidy that pays nothing", EconomyCodes.Policy, root => Edit.ById(root, "policies", "farm-subsidy")["perUnit"] = 0),
        Doc("a budget line that pays nothing", EconomyCodes.Budget, root => { Edit.Market(root, "riverton")["budget"]![0]!["perHead"] = 0; Edit.Market(root, "riverton")["budget"]![0]!["amount"] = 0; }),
        Doc("a route from a market to itself", EconomyCodes.Route, root => Edit.Obj(root, "routes", 0)["to"] = "riverton"),
        Doc("a loan from a market to itself", EconomyCodes.Loan, root => Edit.Obj(root, "loans", 0)["borrower"] = "riverton"),
        Doc("a loan across currencies", EconomyCodes.Loan, root => Edit.Obj(root, "loans", 0)["borrower"] = "seaport"),
        Shock("a shock effect naming a good and a tag", Effect("supply", good: "grain", tag: "food")),
        Shock("a shock effect about goods naming a route", Effect("supply", route: "river-sea")),
        Shock("a route effect naming a good", Effect("route", good: "grain")),
        Shock("a rate effect naming a tag", Effect("rate", tag: "food")),
        Shock("a growth effect naming a route", Effect("growth", route: "river-sea")),

        // ---- the calls ----
        Call("a trade in an unknown market", EconomyCodes.UnknownMarket, r => r.Trade("nowhere", "grain", 1)),
        Call("a trade of an unknown good", EconomyCodes.UnknownGood, r => r.Trade("riverton", "gold", 1)),
        Call("growth of an unknown currency", EconomyCodes.UnknownCurrency, r => r.SetGrowth("gold", 1)),
        Call("an unknown route", EconomyCodes.UnknownRoute, r => r.RouteIndex("no-such-road")),
        Call("an unknown shock", EconomyCodes.UnknownShock, r => r.ApplyShock("rain", "", 1000)),
        Call("settling an unknown schedule", EconomyCodes.UnknownSchedule, r => r.Settle("never")),
        Call("explaining an unknown loan", EconomyCodes.UnknownLoan, r => EconomyExplain.Loan(r, "no-such")),
        Call("a loan with an id already used", EconomyCodes.DuplicateLoan, r => r.Lend("levy-loan", "world", "hillfort", 10, 0, 2, LoanKind.Bullet, "taxday", 2)),
        new Case("a good the market does not trade", EconomyCodes.NotTraded, () => Fixture.Refusal(() =>
            EconomyRuntime.Create(EconomyLoader.Load(Edit.Doc(root => { var pairs = Edit.Market(root, "hillfort")["pairs"]!.AsArray(); pairs.Remove(pairs.Last(p => p!["good"]!.GetValue<string>() == "ale")); }))).Trade("hillfort", "ale", 1), "not traded")),
        Call("more than the market holds", EconomyCodes.Stock, r => r.Trade("riverton", "grain", 1_000_000)),
        Call("a grant that empties below zero", EconomyCodes.Funds, r => r.Fund("riverton", -1_000_000)),
        Call("a sale the warehouse cannot take", EconomyCodes.Capacity, r => r.Trade("riverton", "grain", -1_000_000)),
        Call("a step in the past", EconomyCodes.StepOrder, r => { r.Advance(10, Fixture.Spring); r.Advance(5, Fixture.Spring); }),
        Call("a missing host fact", EconomyCodes.FactMissing, r => r.Advance(24, Fixture.NoFacts)),
        Call("a host fact of the wrong type", EconomyCodes.FactInvalid, r => r.Advance(24, new Dictionary<string, ScenarioEngine.Contracts.FactValue> { ["cal.season"] = ScenarioEngine.Contracts.FactValue.From(1L), ["story.plague"] = ScenarioEngine.Contracts.FactValue.From(0L) })),
        Call("an id with capitals", EconomyCodes.UnknownName, r => r.Trade("River Town", "grain", 1)),
        Call("a trade of nothing", EconomyCodes.Range, r => r.Trade("riverton", "grain", 0)),
        Call("a shock scale of zero", EconomyCodes.Range, r => r.ApplyShock("drought", "", 0)),
        Call("settling a schedule that runs by itself", EconomyCodes.Schedule, r => r.Settle("payday")),
        Call("lending to the lender", EconomyCodes.Loan, r => r.Lend("x", "riverton", "riverton", 10, 0, 2, LoanKind.Bullet, "taxday", 2)),
        Call("devaluing the reference currency", EconomyCodes.Shock, r => r.ApplyShock("devaluation", "riverton", 1000)),
        Call("unlocking a recipe that does not exist", EconomyCodes.UnknownRecipe, r => r.UnlockRecipe("nothing")),
        Call("unlocking a recipe the document never locked", EconomyCodes.RecipeOpen, r => r.UnlockRecipe("mill")),
        Call("explaining a line the market does not have", EconomyCodes.NoLine, r => EconomyExplain.Production(r, "seaport", "mill")),

        // ---- the saves ----
        new Case("a save that is not a save", EconomyCodes.SaveFormat, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, "{}"u8.ToArray()), "not a save")),
        new Case("a format 3 save", EconomyCodes.SaveFormat3, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, Tamper.Stale(EconomySave.Write(Fixture.Runtime()), i => i.Format = 3)), "format 3 save")),
        new Case("a format 4 save", EconomyCodes.SaveFormat4, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, Tamper.Stale(EconomySave.Write(Fixture.Runtime()), i => i.Format = 4)), "format 4 save")),
        new Case("a format 5 save", EconomyCodes.SaveFormat5, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, Tamper.Stale(EconomySave.Write(Fixture.Runtime()), i => i.Format = 5)), "format 5 save")),
        new Case("a save of another document", EconomyCodes.SaveDocument, () => Fixture.Refusal(() => EconomySave.Open(EconomyLoader.Load(Edit.Doc(root => root["seed"] = 1)), EconomySave.Write(Fixture.Runtime())), "another document")),
        new Case("a save with a stale digest", EconomyCodes.SaveDigest, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, Tamper.Stale(EconomySave.Write(Fixture.Runtime()), i => i.Step += 1)), "stale digest")),
        new Case("a save with a forged state", EconomyCodes.SaveState, () => Fixture.Refusal(() => EconomySave.Open(Fixture.Town, Tamper.Forge(EconomySave.Write(Fixture.Runtime()), i => i.Currencies[0].World = 5)), "forged state")),
    };

    public static IEnumerable<object[]> Indexes() => Cases.Select((c, i) => new object[] { i, c.Name });

    [Theory]
    [MemberData(nameof(Indexes))]
    public void EachInputIsRefusedWithTheCodeItIsListedUnder(int index, string name)
    {
        var entry = Cases[index];
        Assert.Equal(entry.Code, entry.Run());
        Assert.Equal(name, entry.Name);
    }

    [Fact]
    public void TheCodesTheCasesReachAreExactlyTheCodesTheEngineDeclares()
    {
        var declared = typeof(EconomyCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var reached = Cases.Select(c => c.Code).ToHashSet();
        Assert.Empty(declared.Except(reached));
        Assert.Empty(reached.Except(declared));
        Assert.True(declared.Count >= 35, "found " + declared.Count + " codes");
        Assert.All(declared, code => Assert.StartsWith("ECON_", code));
    }

    [Fact]
    public void ARefusedCallLeavesTheEconomyExactlyAsItWas()
    {
        var runtime = Tamper.Played();
        var digest = runtime.StateDigest();
        foreach (var call in new Action<EconomyRuntime>[]
        {
            r => r.Trade("riverton", "grain", 1_000_000), r => r.Trade("riverton", "grain", -1_000_000), r => r.Trade("nowhere", "grain", 1), r => r.Fund("riverton", -1_000_000_000),
            r => r.ApplyShock("drought", "", 0), r => r.ApplyShock("nothing", "", 1000), r => r.Settle("payday"), r => r.SetGrowth("crown", 999_999_999),
            r => r.Lend("levy-loan", "world", "hillfort", 10, 0, 2, LoanKind.Bullet, "taxday", 2), r => r.Advance(r.Step - 1, Fixture.Spring), r => r.Advance(r.Step + 48, Fixture.NoFacts),
        })
        {
            Assert.Throws<EngineRefusedException>(() => call(runtime));
            Assert.Equal(digest, runtime.StateDigest());
        }
    }
}
