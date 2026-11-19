using System.Text.Json.Nodes;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>The acceptance score: the evaluator as a pure function of its inputs, each raw input measured from a state set up to give a known number, and the chance bands.</summary>
public sealed class AcceptanceTests
{
    private static readonly AcceptanceRules Rules = new(0, -200, 1000);

    private static IReadOnlyList<long> Raws(params (FactorSource Source, long Value)[] values)
    {
        var raw = new long[Enum.GetValues<FactorSource>().Length];
        foreach (var (source, value) in values) raw[(int)source] = value;
        return raw;
    }

    private static TreatyTypeDef Type(int bias, params (FactorSource Source, int Weight)[] weights)
    {
        var table = new int[Enum.GetValues<FactorSource>().Length];
        foreach (var (source, weight) in weights) table[(int)source] = weight;
        return Fixture.Isles.Types[0] with { Bias = bias, Weights = table };
    }

    [Fact]
    public void TheScoreIsTheBiasPlusTheTraitsPlusEachRawTimesItsWeightOverAThousandPlusTheModifiersThatHold()
    {
        var type = Type(-100, (FactorSource.Regard, 600), (FactorSource.Fear, -250));
        var verdict = Acceptance.Evaluate(type, Rules, new AcceptanceInputs(Raws((FactorSource.Regard, 500), (FactorSource.Fear, 400)), 70, [("friends", 30), ("rivals", -10)]));
        Assert.Equal(-100 + 70 + 300 - 100 + 30 - 10, verdict.Score);
        Assert.Equal(["bias", "traits", "regard", "fear", "modifier friends", "modifier rivals"], verdict.Lines.Select(l => l.Name).ToArray());
        Assert.Equal([-100, 70, 300, -100, 30, -10], verdict.Lines.Select(l => l.Contribution).ToArray());
        Assert.Equal(1000, verdict.Chance);
    }

    [Fact]
    public void AProductIsDividedOnceAndTruncatedTowardsZero()
    {
        var type = Type(0, (FactorSource.Regard, 500));
        Assert.Equal(-3, Acceptance.Evaluate(type, Rules, new AcceptanceInputs(Raws((FactorSource.Regard, -7)), 0, [])).Lines[1].Contribution);
        Assert.Equal(3, Acceptance.Evaluate(type, Rules, new AcceptanceInputs(Raws((FactorSource.Regard, 7)), 0, [])).Lines[1].Contribution);
    }

    [Fact]
    public void AZeroWeightAndAZeroTraitBiasLeaveNoLine()
    {
        var verdict = Acceptance.Evaluate(Type(5, (FactorSource.Trust, 100)), Rules, new AcceptanceInputs(Raws((FactorSource.Trust, 40), (FactorSource.Honour, 900)), 0, []));
        Assert.Equal(["bias", "trust"], verdict.Lines.Select(l => l.Name).ToArray());
        Assert.Equal(9, verdict.Score);
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(0, 1000)]
    [InlineData(-1, 995)]
    [InlineData(-100, 500)]
    [InlineData(-199, 5)]
    [InlineData(-200, 0)]
    [InlineData(-201, 0)]
    [InlineData(-5000, 0)]
    public void TheChanceIsCertainAtTheAcceptLineNoneBelowTheRejectLineAndInProportionBetween(long score, int chance) => Assert.Equal(chance, Acceptance.ChanceOf(score, Rules));

    [Fact]
    public void WhenTheTwoLinesAreTheSameThereIsNoBand()
    {
        var rules = new AcceptanceRules(50, 50, 1000);
        Assert.Equal(1000, Acceptance.ChanceOf(50, rules));
        Assert.Equal(0, Acceptance.ChanceOf(49, rules));
    }

    [Fact]
    public void TheVerdictCarriesTheChanceOfItsScore()
    {
        var verdict = Acceptance.Evaluate(Type(-100), Rules, new AcceptanceInputs(Raws(), 0, []));
        Assert.Equal(500, verdict.Chance);
        Assert.Equal(-100, verdict.Score);
    }

    [Fact]
    public void DescribeSaysEveryLineTheScoreAndTheLines()
    {
        var verdict = Acceptance.Evaluate(Type(-100, (FactorSource.Regard, 600)), Rules, new AcceptanceInputs(Raws((FactorSource.Regard, 500)), 0, []));
        var text = Acceptance.Describe(verdict, Rules);
        Assert.Equal("  bias: 1000 x -100 / 1000 = -100\n  regard: 500 x 600 / 1000 = 300\n  score 200 (accepts at 0, refuses below -200): chance 1000 per mille", text);
    }

    // ---- the raw inputs, each measured from a state set up to give a known number -------------------------------------------------

    /// <summary>A world where a treaty type weighs only one input at a thousand per mille, so the line of that input is the raw number.</summary>
    private static (DipRuntime Runtime, Func<string, string, long> Raw) Only(FactorSource source, Action<JsonObject>? edit = null, string type = "nap")
    {
        var runtime = Fixture.PlainWorld(root =>
        {
            Fixture.Realm(root, "cinder")["control"] = "ai";
            var def = Fixture.Type(root, type);
            def["bias"] = 0;
            def["modifiers"] = new JsonArray();
            def["weights"] = new JsonObject { [EngineCommon.EnumNames.Name(source)] = 1000 };
            for (var i = 0; i < 5; i++) Fixture.Realm(root, new[] { "aurel", "brass", "cinder", "dune", "ember" }[i])["traits"] = new JsonArray();
            edit?.Invoke(root);
        });
        long Raw(string from, string to) => runtime.Evaluate(from, to, type, type == "nap" ? 50 : type == "peace" ? 60 : 0, 0).Lines.First(l => l.Name == EngineCommon.EnumNames.Name(source)).Raw;
        return (runtime, Raw);
    }

    [Fact]
    public void RegardIsTheTargetsRegardOfTheProposer()
    {
        var (runtime, raw) = Only(FactorSource.Regard);
        Assert.Equal(0, raw("aurel", "brass"));
        runtime.Remember("aurel", "brass", "gift", 1000);
        Assert.Equal(300, raw("aurel", "brass"));
        Assert.Equal(0, raw("brass", "aurel"));
    }

    [Theory]
    [InlineData(FactorSource.Trust, 100)]
    [InlineData(FactorSource.Fear, 50)]
    [InlineData(FactorSource.Honour, 100)]
    public void TrustFearAndHonourAreTheProposersOwnInThousandths(FactorSource source, int expected)
    {
        var (_, raw) = Only(source);
        Assert.Equal(expected, raw("aurel", "brass"));
        Assert.Equal(0, raw("brass", "aurel"));
    }

    [Fact]
    public void PowerRatioIsTheProposersShareOfTheTwoMightsStretchedToMinusAThousandToAThousand()
    {
        var (runtime, raw) = Only(FactorSource.PowerRatio);
        Assert.Equal(110, raw("aurel", "brass"));      // 500,000 of 900,000 is 555 per mille
        Assert.Equal(-112, raw("brass", "aurel"));     // 444 per mille
        runtime.SetMight("brass", 0);
        Assert.Equal(1000, raw("aurel", "brass"));
        runtime.SetMight("aurel", 0);
        Assert.Equal(0, raw("aurel", "brass"));        // both nothing: even
    }

    [Fact]
    public void SharedEnemyCountsTheRealmsBothFightOrBalanceAgainstUpToThree()
    {
        var (runtime, raw) = Only(FactorSource.SharedEnemy);
        Assert.Equal(0, raw("aurel", "brass"));
        runtime.DeclareWar("aurel", "ember", "");
        Assert.Equal(0, raw("aurel", "brass"));
        runtime.DeclareWar("brass", "ember", "");
        Assert.Equal(333, raw("aurel", "brass"));
        runtime.DeclareWar("aurel", "dune", "");
        runtime.DeclareWar("brass", "dune", "");
        Assert.Equal(666, raw("aurel", "brass"));
        runtime.DeclareWar("aurel", "cinder", "");
        runtime.DeclareWar("brass", "cinder", "");
        Assert.Equal(999, raw("aurel", "brass"));
    }

    [Fact]
    public void IdeologyIsTheTargetsAffinityForTheProposersIdeology()
    {
        var (_, raw) = Only(FactorSource.Ideology);
        Assert.Equal(-100, raw("aurel", "brass"));     // guild for crown
        Assert.Equal(-100, raw("brass", "aurel"));     // crown for guild
        Assert.Equal(600, raw("aurel", "cinder"));     // crown for crown
        Assert.Equal(-300, raw("aurel", "ember"));     // zeal for crown
        Assert.Equal(-200, raw("brass", "ember"));
    }

    [Fact]
    public void TiesIsTwoHundredAndFiftyForEachTreatyBetweenThemUpToFour()
    {
        var (runtime, raw) = Only(FactorSource.Ties, root =>
        {
            foreach (var id in new[] { "trade", "marriage", "alliance", "vassalage" }) Fixture.Decided(root, id, true);
            Fixture.Type(root, "nap")["considerDays"] = 0;
        });
        Assert.Equal(0, raw("aurel", "brass"));
        runtime.Propose("aurel", "brass", "marriage", 0, 0); Fixture.Live(runtime, 6);
        Assert.Equal(250, raw("aurel", "brass"));
        runtime.Propose("aurel", "brass", "trade", 0, 0); Fixture.Live(runtime, 8);
        runtime.Propose("aurel", "brass", "alliance", 0, 0); Fixture.Live(runtime, 12);
        Assert.Equal(750, raw("aurel", "brass"));
        runtime.Propose("aurel", "brass", "vassalage", 0, 100); Fixture.Live(runtime, 16);
        Assert.Equal(1000, raw("aurel", "brass"));
        Assert.Equal(4, runtime.Treaties.Count);
    }

    [Fact]
    public void ThreatIsTheProposersShareOfAllMightGrownByItsInfamy()
    {
        var (runtime, raw) = Only(FactorSource.Threat);
        Assert.Equal(500000L * 1000 / 1650000, raw("aurel", "brass"));   // 303 per mille, no infamy
    }

    [Fact]
    public void ThreatCountsHalfTheMightOfARealmItProtectsAndIsKeptWithinAThousand()
    {
        var (runtime, raw) = Only(FactorSource.Threat, root => Fixture.Decided(root, "alliance", true));
        var before = raw("aurel", "dune");
        Fixture.Treaty(runtime, "aurel", "dune", "alliance");
        // aurel (500,000) now also counts half of dune (125,000): 625,000 of 1,650,000
        Assert.Equal(625000L * 1000 / 1650000, raw("aurel", "brass"));
        Assert.True(raw("aurel", "brass") > before);
    }

    [Fact]
    public void ThreatIsGrownByInfamyAndKeptWithinAThousand()
    {
        var (runtime, raw) = Only(FactorSource.Threat, root => Fixture.Realm(root, "aurel")["standing"]!["infamy"] = 500);
        Assert.Equal(500000L * 1000 / 1650000 * 1500 / 1000, raw("aurel", "brass"));
        runtime.SetMight("aurel", 1_000_000_000);
        Assert.Equal(1000, raw("aurel", "brass"));
    }

    [Fact]
    public void StabilityIsHowFarTheTargetIsBelowTheMiddleDoubled()
    {
        var (runtime, raw) = Only(FactorSource.Stability);
        Assert.Equal((500 - 650) * 2, raw("aurel", "brass"));
        Assert.Equal((500 - 700) * 2, raw("brass", "aurel"));
        Assert.Equal((500 - 550) * 2, raw("aurel", "ember"));
    }

    [Fact]
    public void TermsIsMinusTheBurdenWhenTheTargetPaysAndPlusItWhenTheTargetReceives()
    {
        var (runtime, _) = Only(FactorSource.Terms, null, "peace");
        long Terms(long amount, int days) => runtime.Evaluate("aurel", "brass", "peace", days, amount).Lines.First(l => l.Name == "terms").Raw;
        Assert.Equal(-600, Terms(1000, 60));          // 1,000 for 60 days over a unit of 100,000 is 600
        Assert.Equal(600, Terms(-1000, 60));
        Assert.Equal(-1000, Terms(500000, 60));        // more than a whole unit is capped at 1000
        Assert.Equal(-1000, Terms(100000, 60));
        Assert.Equal(-30, Terms(100, 30));
    }

    [Fact]
    public void WarScoreAndExhaustionAreTheTargetsOwnInTheWarBetweenThem()
    {
        var (runtime, raw) = Only(FactorSource.WarScore, root => Fixture.Rules(root, "war")["exhaustPpm"] = 10000, "peace");
        runtime.DeclareWar("aurel", "brass", "");
        runtime.Score(0, "aurel", 1000, "event");       // 400 for the attacker
        Assert.Equal(-400, runtime.Evaluate("aurel", "brass", "peace", 60, 0).Lines.First(l => l.Name == "war_score").Raw);   // brass is losing by 400
        Assert.Equal(400, runtime.Evaluate("brass", "aurel", "peace", 60, 0).Lines.First(l => l.Name == "war_score").Raw);
        Fixture.Live(runtime, 20);
        var war = runtime.Wars[0];
        Assert.True(war.DefenderExhaustion > war.AttackerExhaustion);   // the loser tires faster
    }
}
