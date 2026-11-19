using System.Text;
using AIEngine;
using EconomyEngine;
using EngineCommon;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The AI engine and the economy: a merchant agent decides by the price it knows, but only knows it when it is told, as a percept (knowledge, not truth).</summary>
public sealed class AiIntegrationTests
{
    private static readonly string[] Goods = ["grain", "cloth", "iron", "ale"];

    /// <summary>A merchant that sells when it believes grain is more than 30 percent over its reference price, and otherwise holds.</summary>
    private static string Merchant(Func<string, string>? attribute = null, bool owned = false, string kind = "market", IEnumerable<string>? skip = null)
    {
        attribute ??= a => a;
        var attrs = new StringBuilder();
        foreach (var good in Goods)
        {
            if (skip?.Contains(good) == true) continue;
            if (attrs.Length > 0) attrs.Append(',');
            attrs.Append($"{{ \"id\": \"{attribute("ratio_" + good)}\", \"type\": \"int\", \"min\": 0, \"max\": 1000000, \"unknown\": 0 }}, {{ \"id\": \"{attribute("short_" + good)}\", \"type\": \"bool\", \"unknown\": false }}, {{ \"id\": \"{attribute("reach_" + good)}\", \"type\": \"int\", \"min\": 0, \"max\": 1000000, \"unknown\": 1000 }}");
        }
        return $$"""
        { "format": 1, "id": "eco.ai",
          "keys": [ { "id": "grain", "type": "int", "initial": 0, "min": 0, "max": 1000000, "source": "agent" }, { "id": "baseline", "type": "int", "initial": 300, "min": 0, "max": 1000, "source": "agent" } ],
          "percepts": [ { "kind": "{{kind}}", "capacity": 1, "memory": 100000, "owned": {{(owned ? "true, \"ownerCapacity\": 1" : "false")}}, "attrs": [ {{attrs}} ] } ],
          "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
          "behaviors": [ { "id": "merchant", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
              { "id": "see-grain", "kind": "let", "key": "grain", "value": { "known": { "from": "market", "id": "town", "attr": "ratio_grain" } } },
              { "id": "choose", "kind": "utility", "momentum": 0, "options": [
                { "id": "sell", "weight": 1000, "considerations": [ { "input": { "key": "grain" }, "min": 1000, "max": 2000, "curve": { "kind": "linear" } } ], "child": { "id": "do-sell", "kind": "act", "intent": "do", "args": { "what": "sell" } } },
                { "id": "hold", "weight": 1000, "considerations": [ { "input": { "key": "baseline" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-hold", "kind": "act", "intent": "do", "args": { "what": "hold" } } } ] } ] } } ] }
        """;
    }

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public EconomyComponent Economy { get; }
        public EconomyAiBridge Bridge { get; }
        public List<EconomyPerceiver> Perceivers { get; } = new();
        public List<(long Tick, string Args)> Intents { get; } = new();
        public List<string> AiRefused { get; } = new();

        public Rig(string? aiDocument = null, string kind = "market", Action<System.Text.Json.Nodes.JsonObject>? economyEdit = null, Func<EconomyComponent, AiSet, EconomyAiBridge>? makeBridge = null)
        {
            var set = AiLoader.Load(aiDocument ?? Merchant());
            AiEventCatalog.Declare(Session);
            var channels = new EconomyChannels();
            channels.Install(Session);
            var host = Samples.Host();
            Economy = new EconomyComponent(EconomyRuntime.Create(Samples.Fast(root =>
            {
                root["shocks"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "panic", ["key"] = "econ.shock.drought", ["duration"] = 1, ["effects"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["kind"] = "price", ["good"] = "grain", ["tag"] = "", ["route"] = "", ["amount"] = 600 } } });
                economyEdit?.Invoke(root);
            })), new TickEconomyClock(1_000_000), () => host, channels);
            Bridge = makeBridge is null ? new EconomyAiBridge("eco-ai", Economy, "ai", set, kind, "town", () => Perceivers, Goods) : makeBridge(Economy, set);
            Session.AddComponent(Economy, 10);
            Session.AddComponent(Bridge, 11);
            Session.AddComponent(new AiComponent(new AiRuntime(set, 1), 1000), 12);
            Session.Subscribe<AiIntentRaised>(e => Intents.Add((e.Tick, e.Args)));
            Session.Subscribe<AiRefused>(e => AiRefused.Add(e.Code));
            Session.Init();
            Session.Submit(new AiAgentAddRequested("ai", "agent", "merchant-1", "merchant"));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        public string Latest => Intents.Count == 0 ? "" : Intents[^1].Args;
    }

    [Fact]
    public void AnAgentThatIsNotToldAnythingKnowsNothingAndOneThatIsToldChoosesByThePriceItKnows()
    {
        var rig = new Rig();
        rig.Tick(3);
        Assert.Equal("what=hold", rig.Latest);
        rig.Economy.Runtime.ApplyShock("panic", "riverton", 1000);        // the truth: grain is 60 percent over its reference
        rig.Tick(3);
        Assert.Equal(0, rig.Bridge.Posted);
        Assert.Equal("what=hold", rig.Latest);                              // it does not know, so it cannot act on it
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Tick(4);
        Assert.Equal(1, rig.Bridge.Posted);
        Assert.Equal("what=sell", rig.Latest);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void WhatTheAgentIsToldIsThePriceRatioTheShortageAndTheBestReachOfEveryGood()
    {
        var rig = new Rig();
        var posts = new List<string>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add(e.Attrs));
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Tick(3);
        Assert.Equal("ratio_grain=1000;short_grain=false;reach_grain=1000;ratio_cloth=1000;short_cloth=false;reach_cloth=1000;ratio_iron=1000;short_iron=false;reach_iron=1000;ratio_ale=1000;short_ale=false;reach_ale=1000", posts[0]);
        Assert.Equal("what=hold", rig.Latest);
        rig.Economy.Runtime.ApplyShock("panic", "riverton", 1000);
        rig.Economy.Runtime.Markets[0].Pairs[1].Short = true;
        rig.Tick(2);
        Assert.StartsWith("ratio_grain=1600;short_grain=false;reach_grain=1000;ratio_cloth=1000;short_cloth=true;", posts[1], StringComparison.Ordinal);
        Assert.Equal("what=sell", rig.Latest);
    }

    [Fact]
    public void AGoodTheMarketDoesNotTradeIsToldAsNothingNotAsAPrice()
    {
        var rig = new Rig(economyEdit: root =>
        {
            var pairs = root["markets"]!.AsArray().Select(n => n!.AsObject()).First(m => m["id"]!.GetValue<string>() == "riverton")["pairs"]!.AsArray();
            pairs.Remove(pairs.First(n => n!["good"]!.GetValue<string>() == "ale"));
        });
        var posts = new List<string>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add(e.Attrs));
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Tick(2);
        Assert.Contains("ratio_ale=0;short_ale=false;reach_ale=0", posts[0], StringComparison.Ordinal);
        Assert.Contains("reach_grain=1000;", posts[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AnAgentInAMarketThatDoesNotPayIsToldWhatAFarMarketWouldPayLessWhatTheWayCosts()
    {
        var rig = new Rig();
        var posts = new List<string>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add(e.Attrs));
        rig.Economy.Runtime.ApplyShock("panic", "riverton", 1000);        // grain is 60 percent over its reference in riverton
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "hillfort"));
        rig.Tick(2);
        var runtime = rig.Economy.Runtime;
        var hillfort = runtime.MarketIndex("hillfort");
        var slot = runtime.PairIndex(hillfort, runtime.GoodIndex("grain"));
        var reach = runtime.Reach(hillfort, slot);
        Assert.True(reach.Route >= 0 && reach.Hops >= 1, "grain should be worth sending from hillfort to riverton");
        var told = posts[0].Split(';').Single(a => a.StartsWith("reach_grain=", StringComparison.Ordinal));
        var expected = reach.Value * 1000 / runtime.Markets[hillfort].Pairs[slot].Price;
        Assert.Equal("reach_grain=" + expected, told);
        Assert.InRange(expected, 1001, 1600);        // better than selling at home, and never more than riverton's own price over this one
        Assert.Contains("ratio_grain=1000;", posts[0], StringComparison.Ordinal);       // hillfort itself is not short and not dear
    }

    [Fact]
    public void WhenTheMarketChangesTheAgentIsToldAgainAndNotBefore()
    {
        var rig = new Rig();
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Tick(3);
        var told = rig.Bridge.Posted;
        rig.Tick(5);
        Assert.Equal(told, rig.Bridge.Posted);                          // nothing changed, nothing to tell
        rig.Economy.Runtime.ApplyShock("panic", "riverton", 1000);
        rig.Tick(2);
        Assert.Equal(told + 1, rig.Bridge.Posted);
        rig.Bridge.Notify();
        rig.Tick(2);
        Assert.Equal(told + 2, rig.Bridge.Posted);                     // a Notify tells again even though nothing changed
    }

    [Fact]
    public void AnAgentActsOnWhatItLastKnewWhenItIsNoLongerToldAndTheTruthMovesOn()
    {
        var rig = new Rig();
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Economy.Runtime.ApplyShock("panic", "riverton", 1000);
        rig.Tick(4);
        Assert.Equal("what=sell", rig.Latest);
        rig.Perceivers.Clear();                                         // the merchant leaves town
        rig.Economy.Runtime.Markets[0].Pairs[0].Price = 10_000;          // the price falls back to its reference
        rig.Tick(4);
        Assert.Equal("what=sell", rig.Latest);                           // it still believes what it last heard
    }

    [Fact]
    public void AnAgentInAMarketThatIsNotOneIsSkippedAndAGoodTheMarketDoesNotTradeReadsAsNothing()
    {
        var rig = new Rig();
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "nowhere"));
        rig.Tick(3);
        Assert.Equal(0, rig.Bridge.Posted);
        Assert.Empty(rig.AiRefused);
    }

    [Fact]
    public void ThePerceptKindIsCheckedAgainstBothDocumentsWhenTheGameIsAssembled()
    {
        void Refused(string document, string fragment, string kind = "market")
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => new Rig(document, kind));
            Assert.Equal(EconomyAiBridge.BadKind, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Refused(Merchant(), "no percept kind 'prices'", "prices");
        Refused(Merchant(owned: true), "is owned");
        Refused(Merchant(skip: ["ale"]), "must declare exactly");
        Refused(Merchant(attribute: a => a == "ratio_iron" ? "price_iron" : a), "must declare 'ratio_iron'");
        Refused(Merchant().Replace("\"id\": \"short_cloth\", \"type\": \"bool\", \"unknown\": false", "\"id\": \"short_cloth\", \"type\": \"int\", \"min\": 0, \"max\": 1, \"unknown\": 0", StringComparison.Ordinal), "'short_cloth' as Boolean");
        Refused(Merchant().Replace("\"id\": \"ratio_grain\", \"type\": \"int\", \"min\": 0, \"max\": 1000000", "\"id\": \"ratio_grain\", \"type\": \"int\", \"min\": 0, \"max\": 5000", StringComparison.Ordinal), "must allow 0 to 1000000");
        Refused(Merchant(attribute: a => a == "reach_iron" ? "far_iron" : a), "must declare 'reach_iron'");
        Refused(Merchant().Replace("\"id\": \"reach_grain\", \"type\": \"int\", \"min\": 0, \"max\": 1000000", "\"id\": \"reach_grain\", \"type\": \"int\", \"min\": 0, \"max\": 5000", StringComparison.Ordinal), "must allow 0 to 1000000");
        Assert.Equal("reach_ale", EconomyAiBridge.ReachAttribute(Samples.Town.Goods[3]));
        Assert.Equal("ratio_iron", EconomyAiBridge.RatioAttribute(Samples.Town.Goods[2]));
        Assert.Equal("short_ale", EconomyAiBridge.ShortAttribute(Samples.Town.Goods[3]));
        Assert.Throws<ArgumentNullException>(() => new EconomyAiBridge("x", null!, "ai", AiLoader.Load(Merchant()), "market", "town", () => []));
    }

    [Fact]
    public void TheGoodsTheMerchantsAreToldAboutAreNamedByTheGameAndCheckedAgainstTheDocuments()
    {
        EconomyComponent Economy()
        {
            var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
            var channels = new EconomyChannels();
            channels.Install(session);
            var host = Samples.Host();
            return new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => host, channels);
        }
        var ai = AiLoader.Load(Merchant());
        // The town has ten goods and a percept kind holds sixteen attributes, three a good: telling about all ten cannot match this document, and the bridge says so.
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => new EconomyAiBridge("b", Economy(), "ai", ai, "market", "town", () => [])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => new EconomyAiBridge("b", Economy(), "ai", ai, "market", "town", () => [], ["grain", "cloth", "iron", "nothing"])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => new EconomyAiBridge("b", Economy(), "ai", ai, "market", "town", () => [], ["grain", "grain", "iron", "ale"])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => new EconomyAiBridge("b", Economy(), "ai", ai, "market", "town", () => [], ["grain", "cloth", "iron"])));
        // Named in any order, the four goods match the document.
        Assert.Equal(0, new EconomyAiBridge("b", Economy(), "ai", ai, "market", "town", () => [], ["ale", "iron", "cloth", "grain"]).Posted);
    }

    // ---- more goods than one percept kind holds, and the production margin ---------------------------------------------------------------

    private static string Attr(string id, string type, string unknown = "0") => type == "bool"
        ? $"{{ \"id\": \"{id}\", \"type\": \"bool\", \"unknown\": false }}"
        : $"{{ \"id\": \"{id}\", \"type\": \"int\", \"min\": 0, \"max\": 1000000, \"unknown\": {unknown} }}";

    private static string Kind(string kind, IEnumerable<string> attrs) => $"{{ \"kind\": \"{kind}\", \"capacity\": 1, \"memory\": 100000, \"owned\": false, \"attrs\": [ {string.Join(", ", attrs)} ] }}";

    private static IEnumerable<string> GoodAttrs(params string[] goods) => goods.SelectMany(g => new[] { Attr("ratio_" + g, "int"), Attr("short_" + g, "bool"), Attr("reach_" + g, "int", "1000") });

    /// <summary>An AI document with the given percept kinds; its merchant reads one known value, <paramref name="kind"/>.<paramref name="attr"/>, and sells when it is above 1,300 per mille, else holds.</summary>
    private static string Document(IEnumerable<string> kinds, string kind, string attr) => $$"""
        { "format": 1, "id": "eco.ai2",
          "keys": [ { "id": "seen", "type": "int", "initial": 0, "min": 0, "max": 1000000, "source": "agent" }, { "id": "baseline", "type": "int", "initial": 300, "min": 0, "max": 1000, "source": "agent" } ],
          "percepts": [ {{string.Join(", ", kinds)}} ],
          "intents": [ { "id": "do", "args": [ { "id": "what", "type": "string" } ] } ],
          "behaviors": [ { "id": "merchant", "every": 1, "root": { "id": "top", "kind": "sequence", "resume": false, "children": [
              { "id": "see", "kind": "let", "key": "seen", "value": { "known": { "from": "{{kind}}", "id": "town", "attr": "{{attr}}" } } },
              { "id": "choose", "kind": "utility", "momentum": 0, "options": [
                { "id": "sell", "weight": 1000, "considerations": [ { "input": { "key": "seen" }, "min": 1300, "max": 2000, "curve": { "kind": "linear" } } ], "child": { "id": "do-sell", "kind": "act", "intent": "do", "args": { "what": "sell" } } },
                { "id": "hold", "weight": 1000, "considerations": [ { "input": { "key": "baseline" }, "min": 0, "max": 1000, "curve": { "kind": "linear" } } ], "child": { "id": "do-hold", "kind": "act", "intent": "do", "args": { "what": "hold" } } } ] } ] } } ] }
        """;

    private static readonly string[] AllGoods = ["grain", "cloth", "iron", "ale", "flour", "bread", "ore", "coal", "steel", "tools"];
    private static readonly string[] AllRecipes = ["mill", "bakery", "smelt-steel", "smith-tools", "alloy-steel"];

    private static string TenGoodsAndFiveMargins(string kind, string attr) =>
        Document([Kind("market", GoodAttrs("grain", "cloth", "iron", "ale", "flour")), Kind("market-2", GoodAttrs("bread", "ore", "coal", "steel", "tools")), Kind("margins", AllRecipes.Select(r => Attr("margin_" + r.Replace('-', '_'), "int")))], kind, attr);

    [Fact]
    public void AnEconomyOfTenGoodsIsToldInFullInTwoKindsAndNothingIsCutOff()
    {
        var goods = EconomyAiBridge.PlanGoods(Samples.Town, "market");
        Assert.Equal(["market", "market-2"], goods.Select(g => g.Kind).ToArray());
        Assert.Equal(AllGoods, goods.SelectMany(g => g.Goods).ToArray());                       // every good of the document, in document order
        Assert.All(goods, g => Assert.InRange(g.Goods.Count, 1, EconomyAiBridge.GoodsPerKind));
        var margins = EconomyAiBridge.PlanMargins(Samples.Town, "margins");
        Assert.Equal(["margins"], margins.Select(m => m.Kind).ToArray());
        Assert.Equal(AllRecipes, margins.SelectMany(m => m.Recipes).ToArray());

        Rig? rig = null;
        rig = new Rig(TenGoodsAndFiveMargins("market-2", "ratio_bread"), makeBridge: (economy, ai) => new EconomyAiBridge("eco-ai", economy, "ai", ai, "town", () => rig!.Perceivers, goods, margins));
        var posts = new List<(string Kind, string Attrs)>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add((e.Kind, e.Attrs)));
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        rig.Tick(3);
        Assert.Equal(["market", "market-2", "margins"], posts.Select(p => p.Kind).ToArray());
        Assert.Empty(rig.AiRefused);
        var told = posts.SelectMany(p => p.Attrs.Split(';')).Select(a => a.Split('=')[0]).ToArray();
        foreach (var good in AllGoods) Assert.Contains("ratio_" + good, told);
        foreach (var recipe in AllRecipes) Assert.Contains("margin_" + recipe.Replace('-', '_'), told);
        Assert.Equal(AllGoods.Length * 3 + AllRecipes.Length, told.Length);                      // no attribute dropped
        Assert.Equal(3, rig.Bridge.Posted);
    }

    [Fact]
    public void TheMarginPerceptIsWhatTheLinesEarnAtTheMarketsPricesAndAgentsActOnTheKnowledge()
    {
        var goods = EconomyAiBridge.PlanGoods(Samples.Town, "market");
        var margins = EconomyAiBridge.PlanMargins(Samples.Town, "margins");
        Rig? rig = null;
        rig = new Rig(TenGoodsAndFiveMargins("margins", "margin_bakery"), makeBridge: (economy, ai) => new EconomyAiBridge("eco-ai", economy, "ai", ai, "town", () => rig!.Perceivers, goods, margins));
        var posts = new List<(string Agent, string Kind, string Attrs)>();
        rig.Session.Subscribe<AiPerceptPosted>(e => posts.Add((e.Agent, e.Kind, e.Attrs)));
        rig.Perceivers.Add(new EconomyPerceiver("merchant-1", "riverton"));
        var runtime = rig.Economy.Runtime;
        var riverton = runtime.MarketIndex("riverton");
        var bread = runtime.PairIndex(riverton, runtime.GoodIndex("bread"));
        runtime.Markets[riverton].Pairs[bread].Price = 1;   // bread is worth nothing: the bakery loses on every run
        rig.Tick(2);
        long Value(string attrs, string name) => long.Parse(attrs.Split(';').Single(a => a.StartsWith(name + "=", StringComparison.Ordinal)).Split('=')[1], System.Globalization.CultureInfo.InvariantCulture);
        var told = posts.Last(p => p.Kind == "margins").Attrs;
        Assert.Equal(runtime.MarginOfLine(riverton, runtime.Set.RecipeById["bakery"].Index).Ratio, Value(told, "margin_bakery"));
        Assert.Equal(runtime.MarginOfLine(riverton, runtime.Set.RecipeById["mill"].Index).Ratio, Value(told, "margin_mill"));
        Assert.Equal(0, Value(told, "margin_smelt_steel"));                                       // riverton has no smelter: no line reads 0
        Assert.True(Value(told, "margin_bakery") < 1000, "bread priced at nothing cannot pay for the flour");
        // The merchant sells only when it is told the bakery pays (above 1,300 per mille): not now.
        Assert.Equal("what=hold", rig.Latest);
        runtime.Markets[riverton].Pairs[bread].Price = 10_000_000;   // bread is dear: the bakery earns a great deal
        rig.Tick(3);
        Assert.True(runtime.MarginOfLine(riverton, runtime.Set.RecipeById["bakery"].Index).Ratio > 1300);
        Assert.Equal("what=sell", rig.Latest);

        // An agent in a market that runs no line is told zeros, not the margins of somewhere else.
        rig.Perceivers.Add(new EconomyPerceiver("merchant-2", "seaport"));
        rig.Tick(2);
        Assert.All(posts.Where(p => p.Agent == "merchant-2" && p.Kind == "margins"), p => Assert.All(p.Attrs.Split(';'), a => Assert.EndsWith("=0", a, StringComparison.Ordinal)));
        Assert.Contains(posts, p => p.Agent == "merchant-2" && p.Kind == "margins");
    }

    [Fact]
    public void APerceptKindOverTheEnginesLimitIsRefusedByTheAiEngineAndTheBridgeRefusesWhatWouldNotFit()
    {
        // The limit is the AI engine's own: a percept kind of 17 attributes does not load, 16 does.
        Assert.Throws<EngineRefusedException>(() => AiLoader.Load(Document([Kind("market", Enumerable.Range(0, 17).Select(i => Attr("a" + i, "int")))], "market", "a0")));
        Assert.NotNull(AiLoader.Load(Document([Kind("market", Enumerable.Range(0, 16).Select(i => Attr("a" + i, "int")))], "market", "a0")));
        // The bridge refuses a group that would need more, a good or recipe named twice, two groups with one kind and a kind the AI document lacks or declares differently.
        var ai = AiLoader.Load(TenGoodsAndFiveMargins("market", "ratio_grain"));
        EconomyAiBridge Make(IReadOnlyList<EconomyGoodsPercept> goods, IReadOnlyList<EconomyMarginPercept>? margins = null)
        {
            var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
            var channels = new EconomyChannels();
            channels.Install(session);
            var host = Samples.Host();
            return new EconomyAiBridge("b", new EconomyComponent(EconomyRuntime.Create(Samples.Fast()), new TickEconomyClock(1), () => host, channels), "ai", ai, "town", () => [], goods, margins);
        }
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([new EconomyGoodsPercept("market", AllGoods.Take(6).ToList())])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([new EconomyGoodsPercept("market", ["grain"]), new EconomyGoodsPercept("market-2", ["grain"])])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([new EconomyGoodsPercept("market", ["grain"]), new EconomyGoodsPercept("market", ["cloth"])])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([], [new EconomyMarginPercept("margins", ["mill", "mill", "bakery", "smelt-steel", "smith-tools"])])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([], [new EconomyMarginPercept("margins", ["mill", "nothing", "bakery", "smelt-steel", "smith-tools"])])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([], [new EconomyMarginPercept("margins", ["mill", "bakery"])])));                       // the document declares five
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([], [new EconomyMarginPercept("absent", AllRecipes)])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make([], [new EconomyMarginPercept("margins", Enumerable.Repeat("mill", 17).ToList())])));
        Assert.Equal(EconomyAiBridge.BadKind, Samples.Code(() => Make(EconomyAiBridge.PlanGoods(Samples.Town, "market").Take(1).ToList().Select(g => new EconomyGoodsPercept("market-2", g.Goods)).ToList())));   // the kind named is declared with other goods
        Assert.Equal("margin_smelt_steel", EconomyAiBridge.MarginAttribute(Samples.Town.Recipes[2]));
        Assert.Equal(0, Make(EconomyAiBridge.PlanGoods(Samples.Town, "market"), EconomyAiBridge.PlanMargins(Samples.Town, "margins")).Posted);   // the whole document fits
    }
}
