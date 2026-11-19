using System.Text.Json.Nodes;
using DialogueEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>The dialogue engine and the economy: market facts are external facts for dialogue conditions (a shopkeeper's line depends on the price band), and what a conversation did is written back as deferred requests (a trade, a shock, a settlement).</summary>
public sealed class DialogueIntegrationTests
{
    private sealed class Cast : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => id == "npc";
    }

    private const string Shop = """
    { "format": 3, "id": "eco.shop", "variables": [],
      "externalFacts": [ { "id": "market.band.grain", "type": "string" }, { "id": "market.stock.grain", "type": "int", "min": 0, "max": 1000000000 }, { "id": "currency.crown.rate", "type": "int", "min": 0, "max": 1000000000 } ],
      "speakers": [ { "id": "npc", "portraits": [ "calm" ] } ],
      "quests": [ { "id": "errand", "title": "quest.errand.title", "start": "go", "stages": [ { "id": "go", "journal": "quest.errand.go", "advance": [] } ] } ],
      "conversations": [ { "id": "shop", "start": "check", "nodes": [
        { "id": "check", "kind": "branch", "cases": [
            { "when": { "fact": "ext.market.band.grain", "op": "eq", "value": "scarce" }, "next": "scarce" },
            { "when": { "fact": "ext.market.band.grain", "op": "eq", "value": "dear" }, "next": "dear" },
            { "when": { "fact": "ext.market.band.grain", "op": "eq", "value": "cheap" }, "next": "cheap" } ], "otherwise": "fair" },
        { "id": "scarce", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "shop.scarce", "next": "ask" },
        { "id": "dear", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "shop.dear", "next": "ask" },
        { "id": "cheap", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "shop.cheap", "next": "ask" },
        { "id": "fair", "kind": "line", "speaker": "npc", "portrait": "calm", "line": "shop.fair", "next": "ask" },
        { "id": "ask", "kind": "choice", "pick": "player", "options": [
            { "id": "buy", "line": "shop.buy", "effects": [ { "signal": "buy-grain", "args": [ { "text": "player" }, { "text": "riverton" }, { "text": "grain" } ] } ], "next": "bye" },
            { "id": "sell", "line": "shop.sell", "effects": [ { "signal": "sell-grain", "args": [ { "text": "player" }, { "text": "riverton" }, { "text": "grain" } ] } ], "next": "bye" },
            { "id": "rumour", "line": "shop.rumour", "effects": [ { "signal": "rumour-of-drought", "args": [ { "text": "riverton" } ] } ], "next": "bye" },
            { "id": "help", "line": "shop.help", "effects": [ { "startQuest": "errand" }, { "finishQuest": "errand", "status": "completed" } ], "next": "bye" } ] },
        { "id": "bye", "kind": "end", "result": "done" } ] } ] }
    """;

    private static readonly EconomyOutcome[] Rules =
    [
        new("signal:buy-grain", EconomyOutcomeKind.Trade, "{0}", "{1}", "{2}", 20),
        new("signal:sell-grain", EconomyOutcomeKind.Trade, "{0}", "{1}", "{2}", -15),
        new("signal:rumour-of-drought", EconomyOutcomeKind.Shock, "drought", "{0}", "", 0, 500),
        new("quest:errand:completed", EconomyOutcomeKind.Settle, "decree"),
    ];

    private static EconomySet WithPriceShocks() => Samples.Fast(root =>
    {
        root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "panic", ["key"] = "econ.shock.drought", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "grain", ["tag"] = "", ["route"] = "", ["amount"] = 1500 } } });
        root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "worry", ["key"] = "econ.shock.drought", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "grain", ["tag"] = "", ["route"] = "", ["amount"] = 300 } } });
        root["shocks"]!.AsArray().Add(new JsonObject { ["id"] = "glut", ["key"] = "econ.shock.drought", ["duration"] = 1, ["effects"] = new JsonArray { new JsonObject { ["kind"] = "price", ["good"] = "grain", ["tag"] = "", ["route"] = "", ["amount"] = -500 } } });
    });

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public EconomyComponent Economy { get; }
        public EconomyDialogueFacts Facts { get; }
        public EconomyDialogueOutcomes Outcomes { get; }
        public List<string> Lines { get; } = new();
        public List<(long Tick, string Name)> Signals { get; } = new();
        public List<EconomyRefused> Refused { get; } = new();
        public List<EconomyTradeExecuted> Trades { get; } = new();

        public Rig(IEnumerable<EconomyOutcome>? rules = null, EconomySet? set = null, long ticksPerStep = 1_000_000)
        {
            var economyChannels = new EconomyChannels();
            economyChannels.Install(Session);
            var dialogueChannels = new DialogueChannels();
            dialogueChannels.Install(Session);
            var host = Samples.Host();
            Economy = new EconomyComponent(EconomyRuntime.Create(set ?? WithPriceShocks()), new TickEconomyClock(ticksPerStep), () => host, economyChannels);
            Facts = new EconomyDialogueFacts(Economy, "riverton");
            var cast = new Cast();
            var dialogue = new DialogueComponent(new DialogueRuntime(DialogueLoader.Load(Shop, cast), 1), cast, Facts.Facts, dialogueChannels);
            Outcomes = new EconomyDialogueOutcomes("outcomes", rules ?? Rules);
            Session.AddComponent(Economy, 30);
            Session.AddComponent(dialogue, 40);
            Session.AddComponent(Outcomes, 50);
            Session.Subscribe<DialogueLineShown>(e => Lines.Add(e.Key));
            Session.Subscribe<DialogueSignalRaised>(e => Signals.Add((e.Tick, e.Name)));
            Session.Subscribe<EconomyRefused>(Refused.Add);
            Session.Subscribe<EconomyTradeExecuted>(Trades.Add);
            Session.Init();
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Session.Update(20_000); }

        public void Talk(string option)
        {
            Session.Submit(new DialogueStartRequested("shop", "shop"));
            Tick();
            Session.Submit(new DialogueAdvanceRequested("a"));
            Tick();
            Session.Submit(new DialogueChooseRequested("c", option));
            Tick();
        }
    }

    private static string FirstLine(string? shock)
    {
        var rig = new Rig();
        if (shock is not null) rig.Economy.Runtime.ApplyShock(shock, "riverton", 1000);
        rig.Session.Submit(new DialogueStartRequested("shop", "shop"));
        rig.Tick(2);
        return Assert.Single(rig.Lines);
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AShopkeepersLineDependsOnThePriceBandOfTheGrain()
    {
        Assert.Equal("shop.fair", FirstLine(null));
        Assert.Equal("shop.dear", FirstLine("worry"));
        Assert.Equal("shop.scarce", FirstLine("panic"));
        Assert.Equal("shop.cheap", FirstLine("glut"));
    }

    [Fact]
    public void TheFactsAreTheMarketsAndCanBePointedAtAnotherShop()
    {
        var rig = new Rig();
        rig.Economy.Runtime.ApplyShock("glut", "seaport", 1000);
        var facts = rig.Facts.Facts();
        Assert.Equal("fair", facts["ext.market.band.grain"].Text);
        Assert.Equal(1800, facts["ext.market.stock.grain"].Integer);
        Assert.Equal(1_000_000, facts["ext.currency.crown.rate"].Integer);
        rig.Facts.At("seaport");
        Assert.Equal("seaport", rig.Facts.Market);
        Assert.Equal("cheap", rig.Facts.Facts()["ext.market.band.grain"].Text);
        Assert.Equal(1200, rig.Facts.Facts()["ext.market.stock.grain"].Integer);
        Assert.All(rig.Facts.Facts().Keys, k => Assert.StartsWith("ext.", k, StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownShopIsRefusedWhenTheFactsAreRead()
    {
        var rig = new Rig();
        rig.Facts.At("nowhere");
        Assert.Equal(EconomyCodes.UnknownMarket, Samples.Code(() => rig.Facts.Facts()));
        Assert.Throws<ArgumentNullException>(() => new EconomyDialogueFacts(null!, "riverton"));
    }

    // ---- the documents fit each other -----------------------------------------------------------------------------------------------

    [Fact]
    public void ADialogueDocumentThatReadsWhatTheEconomyOffersIsAcceptedAndOneThatDoesNotIsRefused()
    {
        var cast = new Cast();
        EconomyDialogueFacts.Check(DialogueLoader.Load(Shop, cast), Samples.Town);
        void Mismatch(string declaration, string fragment)
        {
            var set = DialogueLoader.Load(Shop.Replace("\"externalFacts\": [", "\"externalFacts\": [ " + declaration + ",", StringComparison.Ordinal), cast);
            var refusal = Assert.Throws<EngineRefusedException>(() => EconomyDialogueFacts.Check(set, Samples.Town));
            Assert.Equal(EconomyDialogueFacts.Mismatch, refusal.Code);
            Assert.Contains(fragment, refusal.Message, StringComparison.Ordinal);
        }
        Mismatch("{ \"id\": \"market.prices.grain\", \"type\": \"int\", \"min\": 0, \"max\": 9 }", "'market.prices.grain'");
        Mismatch("{ \"id\": \"market.treasury\", \"type\": \"bool\" }", "Boolean in the dialogue document");
        Mismatch("{ \"id\": \"market.treasury\", \"type\": \"int\", \"min\": 0, \"max\": 100 }", "outside the 0 to 100");
        EconomyDialogueFacts.Check(DialogueLoader.Load(Shop.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"market.treasury\", \"type\": \"int\", \"min\": 0, \"max\": 1000000000 },", StringComparison.Ordinal), cast), Samples.Town);
        EconomyDialogueFacts.Check(DialogueLoader.Load(Shop.Replace("\"externalFacts\": [", "\"externalFacts\": [ { \"id\": \"squads\", \"type\": \"int\", \"min\": 0, \"max\": 9 },", StringComparison.Ordinal), cast), Samples.Town);
        Assert.Throws<ArgumentNullException>(() => EconomyDialogueFacts.Check(null!, Samples.Town));
        Assert.Throws<ArgumentNullException>(() => EconomyDialogueFacts.Check(DialogueLoader.Load(Shop, cast), null!));
    }

    // ---- outcomes, written back as deferred requests ----------------------------------------------------------------------------------

    [Fact]
    public void ASignalOfAConversationBecomesATradeTheEconomysAnswerOneTickLater()
    {
        var rig = new Rig();
        var stock = rig.Economy.Runtime.Markets[0].Pairs[0].Stock;
        rig.Talk("buy");
        var signal = Assert.Single(rig.Signals);
        Assert.Equal("buy-grain", signal.Name);
        Assert.Equal(1, rig.Outcomes.Requested);
        Assert.Empty(rig.Trades);                                  // the signal came in this tick, after the economy had run: nothing has happened yet
        Assert.Equal(stock, rig.Economy.Runtime.Markets[0].Pairs[0].Stock);
        rig.Tick();
        var trade = Assert.Single(rig.Trades);
        Assert.Equal(("player", "riverton", "grain", 20L), (trade.Trader, trade.Market, trade.Good, trade.Quantity));
        Assert.Equal(stock - 20, rig.Economy.Runtime.Markets[0].Pairs[0].Stock);
        Assert.Equal(trade.Value + trade.Tax, trade.Total);
        Assert.Empty(rig.Refused);
        Assert.Equal(0, rig.Outcomes.Unmatched);
    }

    [Fact]
    public void ASaleIsTheSameRuleWithANegativeQuantity()
    {
        var rig = new Rig();
        rig.Talk("sell");
        rig.Tick();
        var trade = Assert.Single(rig.Trades);
        Assert.Equal(-15, trade.Quantity);
        Assert.Equal(trade.Value, trade.Total);
    }

    [Fact]
    public void ASignalCanStartAShockAndAQuestThatEndsCanSettleASchedule()
    {
        var rig = new Rig();
        var shocks = new List<EconomyShockChanged>();
        var settled = new List<EconomySettled>();
        rig.Session.Subscribe<EconomyShockChanged>(shocks.Add);
        rig.Session.Subscribe<EconomySettled>(settled.Add);
        rig.Talk("rumour");
        rig.Tick();
        Assert.Equal("drought", Assert.Single(shocks).Shock);
        Assert.Equal(500, rig.Economy.Runtime.ActiveShocks[0].Scale);
        var second = new Rig();
        second.Session.Subscribe<EconomySettled>(settled.Add);
        second.Talk("help");
        second.Tick(2);
        Assert.Contains(settled, s => s.Name == "court");
        Assert.Equal(1, second.Outcomes.Requested);
    }

    [Fact]
    public void ATriggerWithNoRuleIsCountedAndAnOutcomeWithBadIdsIsRefusedByTheEconomyNotByTheBridge()
    {
        var rig = new Rig([new EconomyOutcome("signal:buy-grain", EconomyOutcomeKind.Trade, "{0}", "nowhere", "{2}", 20)]);
        var digest = rig.Economy.Runtime.StateDigest();
        rig.Talk("help");                                              // the quest events have no rule here
        rig.Tick(2);
        Assert.Equal(2, rig.Outcomes.Unmatched);
        var buying = new Rig([new EconomyOutcome("signal:buy-grain", EconomyOutcomeKind.Trade, "{0}", "nowhere", "{2}", 20)]);
        var before = buying.Economy.Runtime.StateDigest();
        buying.Talk("buy");
        buying.Tick(2);
        Assert.Equal(EconomyCodes.UnknownMarket, Assert.Single(buying.Refused).Code);
        Assert.Equal(before, buying.Economy.Runtime.StateDigest());
        Assert.Equal(digest, rig.Economy.Runtime.StateDigest());
    }

    [Fact]
    public void RulesAreCheckedWhenTheGameIsAssembled()
    {
        Assert.Equal(EconomyDialogueOutcomes.BadRule, Samples.Code(() => new EconomyDialogueOutcomes("o", [new EconomyOutcome("nonsense", EconomyOutcomeKind.Settle, "decree")])));
        Assert.Equal(EconomyDialogueOutcomes.BadRule, Samples.Code(() => new EconomyDialogueOutcomes("o",
            [new EconomyOutcome("signal:x", EconomyOutcomeKind.Settle, "decree"), new EconomyOutcome("signal:x", EconomyOutcomeKind.Settle, "other")])));
        Assert.Throws<ArgumentNullException>(() => new EconomyDialogueOutcomes("o", null!));
    }

    [Fact]
    public void TheSharedTriggerVocabularyIsOneOwner()
    {
        Assert.Equal("signal:x", DialogueTriggers.ForSignal("x"));
        Assert.Equal("quest:q:completed", DialogueTriggers.ForQuest("q", "completed"));
        Assert.True(DialogueTriggers.IsTrigger("signal:x"));
        Assert.True(DialogueTriggers.IsTrigger("quest:q:done"));
        Assert.False(DialogueTriggers.IsTrigger("sign:x"));
        Assert.Equal(["a;b", "100%"], DialogueTriggers.TextArguments("s:a%3Bb;i:5;s:100%25"));
        Assert.Equal("x-b-a-{2}", DialogueTriggers.Fill("x-{1}-{0}-{2}", ["a", "b"]));
        Assert.Empty(DialogueTriggers.TextArguments(""));
    }
}
