using EconomyEngine;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;
using TutorialEngine;
using Xunit;

namespace GameRuntime.Tutorial.Tests;

/// <summary>The economy and the relationships and the tutorial: a step that says "make a trade" or "make a friend" is completed by the market really trading and the people really becoming friends, never by a button.</summary>
public sealed class EngineEventsIntegrationTests
{
    private static readonly EconomySet Town = EconomyLoader.Load(Samples.Read("town.economy.json"));
    private static readonly RelationshipSet Village = RelationshipLoader.Load(Samples.Read("village.relationship.json"));

    private sealed class Rig
    {
        public RuntimeSession Session { get; } = new(new RuntimeClock(50, 8), new Reporter());
        public TutorialComponent Tutorial { get; }
        public List<EconomyTradeExecuted> Trades { get; } = new();
        public List<RelationshipInteractionPerformed> Interactions { get; } = new();
        public List<EconomyRefused> TradeRefusals { get; } = new();
        public TutorialEconomyObserver? Economy { get; }
        public TutorialRelationshipObserver? Relationships { get; }

        public Rig(IEnumerable<TutorialTradeRule>? trades = null, IEnumerable<TutorialRelationshipRule>? bonds = null, string economyChannel = "", string tutorialChannel = "")
        {
            var tutorialChannels = new TutorialChannels();
            tutorialChannels.Install(Session);
            var economyChannels = new EconomyChannels();
            economyChannels.Install(Session);
            var relationshipChannels = new RelationshipChannels();
            relationshipChannels.Install(Session);
            var runtime = Samples.Done(Samples.Small);
            runtime.Start();
            Tutorial = new TutorialComponent(runtime, new TickTutorialClock(), () => Samples.Flag(), tutorialChannels, channel: tutorialChannel);
            var economyFacts = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["cal.season"] = FactValue.From("spring"), ["story.plague"] = FactValue.From(0L) };
            var economy = new EconomyComponent(EconomyRuntime.Create(Town), new TickEconomyClock(1_000_000_000), () => economyFacts, economyChannels, channel: economyChannel);
            var relationshipFacts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
            {
                ["cal.season"] = FactValue.From("spring"), ["cal.is_day"] = FactValue.From(true), ["cal.is_holiday"] = FactValue.From(false), ["weather.raining"] = FactValue.From(false),
            };
            var relationships = new RelationshipComponent(RelationshipRuntime.Create(Village), new TickRelationshipClock(1_000_000_000), () => relationshipFacts, relationshipChannels);
            Session.AddComponent(economy, 10);
            Session.AddComponent(relationships, 11);
            Session.AddComponent(Tutorial, 30);
            if (trades is not null) { Economy = new TutorialEconomyObserver("eco", trades, Samples.Small, economyChannel, tutorialChannel); Session.AddComponent(Economy, 40); }
            if (bonds is not null) { Relationships = new TutorialRelationshipObserver("rel", bonds, Samples.Small, "", tutorialChannel); Session.AddComponent(Relationships, 41); }
            Session.Subscribe<EconomyTradeExecuted>(Trades.Add);
            Session.Subscribe<EconomyRefused>(TradeRefusals.Add);
            Session.Subscribe<RelationshipInteractionPerformed>(Interactions.Add);
            Session.Init();
            Session.Submit(new RelationshipActorAddRequested("a1", "alice", ""));
            Session.Submit(new RelationshipActorAddRequested("a2", "dan", ""));
        }

        public void Tick(int n = 1) { for (var i = 0; i < n; i++) Assert.Equal(1, Session.Update(20_000)); }
    }

    [Fact]
    public void ARealTradeCompletesTheStepAndAnotherTradersDoesNot()
    {
        var rig = new Rig(trades: [new TutorialTradeRule("player", "grain", "e2")], economyChannel: "");
        rig.Tick();
        rig.Session.Submit(new EconomyTradeRequested("t1", "someone-else", "riverton", "grain", 5));
        rig.Tick(3);
        Assert.Equal(0, rig.Tutorial.Runtime.EventCounts[1]);
        rig.Session.Submit(new EconomyTradeRequested("t2", "player", "riverton", "grain", 5));
        rig.Tick(3);
        Assert.Equal(2, rig.Trades.Count);
        Assert.Equal(1, rig.Tutorial.Runtime.EventCounts[1]);
        Assert.Equal(1, rig.Economy!.Observed);
    }

    [Fact]
    public void ARefusedTradeIsNotATrade()
    {
        var rig = new Rig(trades: [new TutorialTradeRule("", "", "e2")]);
        rig.Tick();
        rig.Session.Submit(new EconomyTradeRequested("t1", "player", "riverton", "grain", 1_000_000_000));
        rig.Tick(3);
        Assert.NotEmpty(rig.TradeRefusals);
        Assert.Equal(0, rig.Tutorial.Runtime.EventCounts[1]);
        Assert.Equal(0, rig.Economy!.Observed);
    }

    [Fact]
    public void ARuleWithNothingNamedMatchesAnyTradeAndSeveralRulesAreAllTold()
    {
        var rig = new Rig(trades: [new TutorialTradeRule("", "", "e2"), new TutorialTradeRule("player", "", "e3"), new TutorialTradeRule("nobody", "", "e1")]);
        rig.Tick();
        rig.Session.Submit(new EconomyTradeRequested("t1", "player", "riverton", "grain", 3));
        rig.Tick(3);
        Assert.Equal([0L, 1L, 1L], rig.Tutorial.Runtime.EventCounts.Take(3));
        Assert.Equal(2, rig.Economy!.Observed);
    }

    [Fact]
    public void ARealBondCompletesTheStepAndAClearedOneDoesNot()
    {
        // The bond is symmetric: it is set on both people, so a rule for either person hears of it; a bond that is cleared is not a friend made.
        var rig = new Rig(bonds: [new TutorialRelationshipRule(TutorialRelationshipSignal.BondSet, "alice", "spouse", "e2"), new TutorialRelationshipRule(TutorialRelationshipSignal.BondSet, "carol", "spouse", "e3")]);
        rig.Tick();
        rig.Session.Submit(new RelationshipBondRequested("b1", "dan", "alice", "spouse", true));
        rig.Tick(3);
        Assert.Equal([0L, 1L, 0L], rig.Tutorial.Runtime.EventCounts.Take(3));                       // alice's side of it; nobody named carol
        rig.Session.Submit(new RelationshipBondRequested("b3", "alice", "dan", "spouse", false));
        rig.Tick(3);
        Assert.Equal([0L, 1L, 0L], rig.Tutorial.Runtime.EventCounts.Take(3));
        Assert.Equal(1, rig.Relationships!.Observed);
    }

    [Fact]
    public void AnAcceptedInteractionIsToldAndARefusedOneIsNot()
    {
        var rig = new Rig(bonds: [new TutorialRelationshipRule(TutorialRelationshipSignal.InteractionAccepted, "", "chat", "e3")]);
        rig.Tick();
        for (var i = 0; i < 12; i++)
        {
            rig.Session.Submit(new RelationshipInteractRequested("c" + i, "chat", i % 2 == 0 ? "alice" : "dan", i % 2 == 0 ? "dan" : "alice"));
            rig.Tick(30);
        }
        var accepted = rig.Interactions.Count(i => i.Accepted);
        Assert.NotEmpty(rig.Interactions);
        Assert.Equal(accepted, rig.Tutorial.Runtime.EventCounts[2]);
        Assert.Equal(accepted, rig.Relationships!.Observed);
    }

    [Fact]
    public void ARuleNamingAnEventTheTutorialLacksIsRefusedWhenTheGameIsAssembled()
    {
        Assert.Equal(TutorialEconomyObserver.BadRule, Samples.Code(() => new TutorialEconomyObserver("e", [new TutorialTradeRule("", "", "nope")], Samples.Small)));
        Assert.Equal(TutorialRelationshipObserver.BadRule, Samples.Code(() => new TutorialRelationshipObserver("r", [new TutorialRelationshipRule(TutorialRelationshipSignal.BondSet, "", "", "nope")], Samples.Small)));
        Assert.Throws<ArgumentNullException>(() => new TutorialEconomyObserver("e", null!, Samples.Small));
        Assert.Throws<ArgumentNullException>(() => new TutorialRelationshipObserver("r", null!, Samples.Small));
    }

    [Fact]
    public void AnObserverListensOnlyToTheChannelItWasGiven()
    {
        var rig = new Rig(trades: [new TutorialTradeRule("", "", "e2")], economyChannel: "town");
        rig.Tick();
        rig.Session.Submit(new EconomyTradeRequested("t1", "player", "riverton", "grain", 3, "town"));
        rig.Tick(3);
        Assert.Equal(1, rig.Tutorial.Runtime.EventCounts[1]);
        var other = new Rig(trades: [new TutorialTradeRule("", "", "e2")], economyChannel: "");
        other.Tick();
        other.Session.Submit(new EconomyTradeRequested("t1", "player", "riverton", "grain", 3, "elsewhere"));
        other.Tick(3);
        Assert.Equal(0, other.Tutorial.Runtime.EventCounts[1]);
    }
}
