using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlchemyEngine;
using GameRuntime.Alchemy.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The pure alchemy library: content validation, the combine function, the recipe queries and the discovery book.</summary>
public sealed class AlchemyEngineTests
{
    private static readonly string[] NoEnvironment = [];

    private static AlchemyContent Demo() => AlchemyContentLoader.Load(File.ReadAllText(AlchemyContentSource.DefaultContentPath));

    private static CombineContext Context(long attempt = 0, params string[] environment) => new(7, attempt, environment);

    private static string Element(string id, bool starting = true, string properties = "") =>
        "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"properties\": [" + properties + "], \"starting\": " + (starting ? "true" : "false") + " }";

    private static string Recipe(string id, string inputs, string outputs, string catalysts = "", string environment = "", bool ordered = false, int numerator = 1, int denominator = 1) =>
        "{ \"id\": \"" + id + "\", \"inputs\": [" + inputs + "], \"catalysts\": [" + catalysts + "], \"environment\": [" + environment + "], \"outputs\": [" + outputs +
        "], \"ordered\": " + (ordered ? "true" : "false") + ", \"chance\": { \"numerator\": " + numerator + ", \"denominator\": " + denominator + " } }";

    private static string Content(string elements, string recipes) =>
        "{ \"format\": 1, \"elements\": [" + elements + "], \"recipes\": [" + recipes + "] }";

    private static string Refusal(string json) => Assert.Throws<AlchemyRefusedException>(() => AlchemyContentLoader.Load(json)).Code;

    // ---- validation: every refusal has its own code ------------------------------------------------------------------

    [Fact]
    public void TheDemoContentLoadsAndEveryElementIsReachable()
    {
        var content = Demo();
        Assert.Equal(17, content.Elements.Count);
        Assert.Equal(12, content.Recipes.Count);
        Assert.Equal(64, content.Digest.Length);
        Assert.Equal(content.Digest, Demo().Digest);
    }

    [Fact]
    public void ContentThatIsMalformedOrIncompleteIsRefusedWithACode()
    {
        var water = Element("a") + "," + Element("b", false);
        Assert.Equal(AlchemyCodes.ContentFormat, Refusal("{ not json"));
        Assert.Equal(AlchemyCodes.ContentFormat, Refusal("{ \"format\": 2, \"elements\": [], \"recipes\": [] }"));
        Assert.Equal(AlchemyCodes.ContentMissingField, Refusal("{ \"format\": 1, \"elements\": [] }"));
        Assert.Equal(AlchemyCodes.ContentUnknownField, Refusal("{ \"format\": 1, \"elements\": [], \"recipes\": [], \"extra\": 1 }"));
        Assert.Equal(AlchemyCodes.ContentMissingField, Refusal(Content("{ \"id\": \"a\", \"name\": \"a\", \"starting\": true }", "")));
        Assert.Equal(AlchemyCodes.ContentMissingField, Refusal(Content(water, "{ \"id\": \"r\", \"inputs\": [\"a\"], \"catalysts\": [], \"environment\": [], \"outputs\": [\"b\"], \"ordered\": false }")));
        Assert.Equal(AlchemyCodes.ContentBadId, Refusal(Content(Element("Upper"), "")));
        Assert.Equal(AlchemyCodes.ContentBadId, Refusal(Content(Element("a+b"), "")));
    }

    [Fact]
    public void ReferencesDuplicatesAndShapeAreChecked()
    {
        var two = Element("a") + "," + Element("b", false);
        Assert.Equal(AlchemyCodes.ContentDuplicateElement, Refusal(Content(Element("a") + "," + Element("a"), "")));
        Assert.Equal(AlchemyCodes.ContentNoStartingElement, Refusal(Content(Element("a", false), "")));
        Assert.Equal(AlchemyCodes.ContentDuplicateRecipe, Refusal(Content(two, Recipe("r", "\"a\"", "\"b\"") + "," + Recipe("r", "\"a\",\"a\"", "\"b\""))));
        Assert.Equal(AlchemyCodes.ContentUnknownElement, Refusal(Content(two, Recipe("r", "\"a\"", "\"zzz\""))));
        Assert.Equal(AlchemyCodes.ContentEmptyInputs, Refusal(Content(two, Recipe("r", "", "\"b\""))));
        Assert.Equal(AlchemyCodes.ContentNoOutputs, Refusal(Content(two, Recipe("r", "", "", catalysts: "\"a\""))));
        Assert.Equal(AlchemyCodes.ContentCatalystIsInput, Refusal(Content(two, Recipe("r", "\"a\"", "\"b\"", catalysts: "\"a\""))));
    }

    [Fact]
    public void ACatalystOnlyReactionAndAConsumingReactionWithoutOutputsAreValid()
    {
        var elements = Element("pick") + "," + Element("ore", false) + "," + Element("bread");
        var content = AlchemyContentLoader.Load(Content(elements,
            Recipe("mine", "", "\"ore\"", catalysts: "\"pick\"", environment: "\"at-mine\"") + "," + Recipe("eat", "\"bread\"", "")));
        var rules = new AlchemyRules(content);
        Assert.Equal(CombineOutcome.NoReaction, rules.WhatDoes(["pick"], []).Outcome);
        var mined = rules.WhatDoes(["pick"], ["at-mine"]);
        Assert.Equal(CombineOutcome.Reacted, mined.Outcome);
        Assert.Equal(["ore"], mined.Produced);
        Assert.Equal(["pick"], mined.Kept);
        var eaten = rules.WhatDoes(["bread"], []);
        Assert.Equal(CombineOutcome.Reacted, eaten.Outcome);
        Assert.Empty(eaten.Produced);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1_000_001)]
    public void AChanceMustBeAProperFractionAndIsNeverDefaulted(int numerator, int denominator)
    {
        var two = Element("a") + "," + Element("b", false);
        Assert.Equal(AlchemyCodes.ContentBadChance, Refusal(Content(two, Recipe("r", "\"a\"", "\"b\"", numerator: numerator, denominator: denominator))));
    }

    [Fact]
    public void RecipesThatCouldBothMatchOneCombinationAreRefusedButDistinctOnesAreNot()
    {
        var elements = Element("a") + "," + Element("b") + "," + Element("c", false) + "," + Element("d", false);
        // Same inputs, no environment on either: ambiguous.
        Assert.Equal(AlchemyCodes.ContentAmbiguousRecipe, Refusal(Content(elements, Recipe("r1", "\"a\",\"b\"", "\"c\"") + "," + Recipe("r2", "\"b\",\"a\"", "\"d\""))));
        // One environment contains the other: the larger environment offers both.
        Assert.Equal(AlchemyCodes.ContentAmbiguousRecipe, Refusal(Content(elements, Recipe("r1", "\"a\",\"b\"", "\"c\"", environment: "\"heat\"") + "," + Recipe("r2", "\"a\",\"b\"", "\"d\"", environment: "\"heat\",\"cold\""))));
        // Unordered against ordered of the same items can both match the ordered sequence.
        Assert.Equal(AlchemyCodes.ContentAmbiguousRecipe, Refusal(Content(elements, Recipe("r1", "\"a\",\"b\"", "\"c\"", ordered: true) + "," + Recipe("r2", "\"b\",\"a\"", "\"d\""))));
        // Two ordered recipes with opposite sequences never match the same items; unrelated environments are separate recipes.
        AlchemyContentLoader.Load(Content(elements, Recipe("r1", "\"a\",\"b\"", "\"c\"", ordered: true) + "," + Recipe("r2", "\"b\",\"a\"", "\"d\"", ordered: true)));
        AlchemyContentLoader.Load(Content(elements, Recipe("r1", "\"a\",\"b\"", "\"c\"", environment: "\"heat\"") + "," + Recipe("r2", "\"a\",\"b\"", "\"d\"", environment: "\"cold\"")));
    }

    [Fact]
    public void AnElementThatCanNeverBeMadeIsRefused()
    {
        var elements = Element("a") + "," + Element("b", false) + "," + Element("island", false);
        Assert.Equal(AlchemyCodes.ContentUnreachableElement, Refusal(Content(elements, Recipe("r", "\"a\"", "\"b\""))));
        // A recipe whose own input is the unmade element does not make it reachable.
        Assert.Equal(AlchemyCodes.ContentUnreachableElement, Refusal(Content(elements, Recipe("r", "\"a\"", "\"b\"") + "," + Recipe("loop", "\"island\"", "\"island\""))));
    }

    // ---- combine: pure, deterministic, every refusal named ---------------------------------------------------------------

    [Fact]
    public void CombineIsDeterministicAndOrderFreeForUnorderedRecipes()
    {
        var rules = new AlchemyRules(Demo());
        var one = rules.Combine(["fire", "water"], Context());
        var two = rules.Combine(["water", "fire"], Context());
        Assert.Equal(CombineOutcome.Reacted, one.Outcome);
        Assert.Equal("make-steam", one.RecipeId);
        Assert.Equal(new[] { "steam" }, one.Produced);
        Assert.Equal(AlchemyCodes.Reacted, one.Code);
        Assert.Equal(one.RecipeId, two.RecipeId);
        Assert.Equal(one.Produced, two.Produced);
        // The same inputs and context give the same result, again and again.
        Assert.Equal(JsonSerializer.Serialize(rules.Combine(["fire", "water"], Context(5))), JsonSerializer.Serialize(rules.Combine(["fire", "water"], Context(5))));
    }

    [Fact]
    public void OrderCatalystsAndEnvironmentDecideWhetherARecipeFits()
    {
        var rules = new AlchemyRules(Demo());
        // Ordered: mud then fire makes brick, fire then mud does not.
        Assert.Equal(CombineOutcome.Reacted, rules.Combine(["mud", "fire"], Context()).Outcome);
        Assert.Equal(CombineOutcome.NoReaction, rules.Combine(["fire", "mud"], Context()).Outcome);
        // Catalyst: air must be in the vessel, wherever it sits, and it stays.
        var plant = rules.Combine(["air", "water", "mud"], Context());
        Assert.Equal("grow-plant", plant.RecipeId);
        Assert.Equal(["air"], plant.Kept);
        Assert.Equal(CombineOutcome.NoReaction, rules.Combine(["water", "mud"], Context()).Outcome);
        // Environment: smelting needs heat on offer.
        Assert.Equal(CombineOutcome.NoReaction, rules.Combine(["stone", "fire"], Context()).Outcome);
        var smelted = rules.Combine(["stone", "fire"], Context(0, "heat"));
        Assert.Equal("smelt-metal", smelted.RecipeId);
        Assert.Equal(["heat"], smelted.Environment);
        // A recipe may make several elements.
        Assert.Equal(["lightning", "ash"], rules.Combine(["cloud", "fire"], Context()).Produced);
    }

    [Fact]
    public void AChanceRecipeIsDecidedOnlyBySeedAndAttemptAndFizzlesProduceNothing()
    {
        var rules = new AlchemyRules(Demo());
        var first = Enumerable.Range(0, 200).Select(a => rules.Combine(["lava", "water"], new CombineContext(1, a, NoEnvironment)).Outcome).ToArray();
        var again = Enumerable.Range(0, 200).Select(a => rules.Combine(["lava", "water"], new CombineContext(1, a, NoEnvironment)).Outcome).ToArray();
        var other = Enumerable.Range(0, 200).Select(a => rules.Combine(["lava", "water"], new CombineContext(2, a, NoEnvironment)).Outcome).ToArray();
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Contains(CombineOutcome.Reacted, first);
        Assert.Contains(CombineOutcome.Fizzled, first);
        var fizzle = rules.Combine(["lava", "water"], new CombineContext(1, Array.IndexOf(first, CombineOutcome.Fizzled), NoEnvironment));
        Assert.Equal("cool-lava", fizzle.RecipeId);
        Assert.Empty(fizzle.Produced);
        Assert.Equal(AlchemyCodes.Fizzled, fizzle.Code);
        // Roughly half of 200 rolls succeed for a 1/2 chance.
        Assert.InRange(first.Count(o => o == CombineOutcome.Reacted), 70, 130);
    }

    [Fact]
    public void InvalidRequestsAreRefusedWithTheirOwnCode()
    {
        var rules = new AlchemyRules(Demo());
        Assert.Equal(AlchemyCodes.CombineEmpty, rules.Combine([], Context()).Code);
        Assert.Equal(AlchemyCodes.CombineTooMany, rules.Combine(Enumerable.Repeat("fire", AlchemyRules.MaxItems + 1).ToArray(), Context()).Code);
        Assert.Equal(AlchemyCodes.CombineUnknownElement, rules.Combine(["fire", "unobtainium"], Context()).Code);
        Assert.Equal(AlchemyCodes.CombineBadEnvironment, rules.Combine(["fire", "water"], Context(0, "Not A Tag")).Code);
        Assert.All(new[] { rules.Combine([], Context()), rules.Combine(["x"], Context()) }, r => Assert.Equal(CombineOutcome.Refused, r.Outcome));
    }

    [Fact]
    public void TwoRecipesWhoseEnvironmentsAreBothOnOfferAreRefusedAtCombineTime()
    {
        var elements = Element("a") + "," + Element("b") + "," + Element("c", false) + "," + Element("d", false);
        var rules = new AlchemyRules(AlchemyContentLoader.Load(Content(elements,
            Recipe("r1", "\"a\",\"b\"", "\"c\"", environment: "\"heat\"") + "," + Recipe("r2", "\"a\",\"b\"", "\"d\"", environment: "\"cold\""))));
        Assert.Equal("r1", rules.Combine(["a", "b"], Context(0, "heat")).RecipeId);
        Assert.Equal("r2", rules.Combine(["a", "b"], Context(0, "cold")).RecipeId);
        Assert.Equal(AlchemyCodes.CombineAmbiguous, rules.Combine(["a", "b"], Context(0, "heat", "cold")).Code);
    }

    // ---- queries -------------------------------------------------------------------------------------------------------

    [Fact]
    public void QueriesSayWhatMakesAnElementAndWhatACombinationWouldDoWithoutRollingOrChangingAnything()
    {
        var rules = new AlchemyRules(Demo());
        Assert.Equal(["make-steam"], rules.WhatMakes("steam").Select(r => r.Id));
        Assert.Equal(["storm"], rules.WhatMakes("ash").Select(r => r.Id));
        Assert.Empty(rules.WhatMakes("fire"));
        Assert.Equal(AlchemyCodes.CombineUnknownElement, Assert.Throws<AlchemyRefusedException>(() => rules.WhatMakes("nothing")).Code);
        // The chance recipe is previewed as the reaction it is, whatever a roll would say.
        var preview = rules.WhatDoes(["water", "lava"], NoEnvironment);
        Assert.Equal(CombineOutcome.Reacted, preview.Outcome);
        Assert.Equal(["stone"], preview.Produced);
        Assert.Equal(CombineOutcome.NoReaction, rules.WhatDoes(["fire", "fire"], NoEnvironment).Outcome);
        Assert.Equal(AlchemyCodes.CombineEmpty, rules.WhatDoes([], NoEnvironment).Code);
    }

    // ---- the discovery book ----------------------------------------------------------------------------------------------

    [Fact]
    public void ABookStartsWithTheStartingElementsAndLearnsFromResults()
    {
        var content = Demo();
        var rules = new AlchemyRules(content);
        var book = new DiscoveryBook(content);
        Assert.Equal(["air", "earth", "fire", "water"], book.Elements);
        Assert.Equal(AlchemyCodes.CombineNotDiscovered, book.CheckUsable(["fire", "steam"]));
        Assert.Null(book.CheckUsable(["fire", "water"]));

        var learned = book.Record(rules.Combine(["fire", "water"], Context(book.Attempts)));
        Assert.Equal(["steam"], learned.NewElements);
        Assert.Equal("make-steam", learned.NewRecipe);
        Assert.False(learned.AlreadyTried);
        Assert.True(book.Knows("steam"));
        Assert.Equal(1, book.Attempts);

        var again = book.Record(rules.Combine(["fire", "water"], Context(book.Attempts)));
        Assert.Empty(again.NewElements);
        Assert.Null(again.NewRecipe);
        Assert.True(again.AlreadyTried);
        Assert.True(book.HasTried(["fire", "water"], NoEnvironment));

        // A refusal teaches nothing and costs no attempt.
        var before = book.Hash();
        book.Record(rules.Combine([], Context()));
        Assert.Equal(before, book.Hash());
        Assert.Equal(2, book.Attempts);
    }

    [Fact]
    public void ARecipeLearnedFromOutsideTeachesItsOutputsWithoutCountingAnAttempt()
    {
        var content = Demo();
        var book = new DiscoveryBook(content);
        var taught = book.LearnRecipe("storm");
        Assert.Equal("storm", taught.NewRecipe);
        Assert.Equal(["lightning", "ash"], taught.NewElements);
        Assert.Equal(0, book.Attempts);
        Assert.Empty(book.Tried);
        Assert.Contains("storm", book.Recipes);
        // Learning it again teaches nothing new; an unknown recipe is refused with its code.
        var again = book.LearnRecipe("storm");
        Assert.Null(again.NewRecipe);
        Assert.Empty(again.NewElements);
        Assert.Equal(AlchemyCodes.BookUnknownRecipe, Assert.Throws<AlchemyRefusedException>(() => book.LearnRecipe("nothing")).Code);
        // And it survives a save and load.
        Assert.Equal(book.Hash(), DiscoveryBook.Load(content, book.Save()).Hash());
    }

    [Fact]
    public void AFizzleCostsAnAttemptButStaysOpenToAnotherTry()
    {
        var content = Demo();
        var rules = new AlchemyRules(content);
        var book = new DiscoveryBook(content);
        foreach (var pair in new[] { new[] { "fire", "earth" } }) book.Record(rules.Combine(pair, Context()));
        var attempt = Enumerable.Range(0, 50).First(a => rules.Combine(["lava", "water"], new CombineContext(1, a, NoEnvironment)).Outcome == CombineOutcome.Fizzled);
        var result = rules.Combine(["lava", "water"], new CombineContext(1, attempt, NoEnvironment));
        var attemptsBefore = book.Attempts;
        var learned = book.Record(result);
        Assert.Empty(learned.NewElements);
        Assert.False(book.HasTried(["lava", "water"], NoEnvironment));
        Assert.Equal(attemptsBefore + 1, book.Attempts);
    }

    private static DiscoveryBook PlayedBook(AlchemyContent content)
    {
        var rules = new AlchemyRules(content);
        var book = new DiscoveryBook(content);
        foreach (var pair in new[] { new[] { "fire", "water" }, ["air", "steam"], ["earth", "water"], ["mud", "fire"] })
            book.Record(rules.Combine(pair, new CombineContext(3, book.Attempts, NoEnvironment)));
        return book;
    }

    [Fact]
    public void ASavedBookLoadsBackIdentically()
    {
        var content = Demo();
        var book = PlayedBook(content);
        var loaded = DiscoveryBook.Load(content, book.Save());
        Assert.Equal(book.Hash(), loaded.Hash());
        Assert.Equal(book.Attempts, loaded.Attempts);
        Assert.Equal(book.Elements, loaded.Elements);
        Assert.Equal(book.Recipes, loaded.Recipes);
        Assert.Equal(book.Tried, loaded.Tried);
        Assert.Equal(book.Save(), loaded.Save());
    }

    private static string RefusedBook(AlchemyContent content, byte[] bytes) => Assert.Throws<AlchemyRefusedException>(() => DiscoveryBook.Load(content, bytes)).Code;

    [Fact]
    public void ATamperedOrMismatchedBookIsRefusedAndNeverRepaired()
    {
        var content = Demo();
        var saved = PlayedBook(content).Save();

        Assert.Equal(AlchemyCodes.BookFormat, RefusedBook(content, Encoding.UTF8.GetBytes("not a book")));
        Assert.Equal(AlchemyCodes.BookFormat, RefusedBook(content, Encoding.UTF8.GetBytes("{\"Format\":9,\"Content\":\"\",\"Digest\":\"\",\"Body\":\"\"}")));

        // Change one character of the body: the digest no longer fits.
        using var document = JsonDocument.Parse(saved);
        var body = document.RootElement.GetProperty("Body").GetString()!;
        var flipped = (body[10] == 'A' ? 'B' : 'A');
        var damaged = Encoding.UTF8.GetString(saved).Replace(body, body[..10] + flipped + body[11..], StringComparison.Ordinal);
        Assert.Equal(AlchemyCodes.BookDigest, RefusedBook(content, Encoding.UTF8.GetBytes(damaged)));

        // The same bytes against other content are refused: a book belongs to its content.
        var other = AlchemyContentLoader.Load(Content(Element("a") + "," + Element("b", false), Recipe("r", "\"a\"", "\"b\"")));
        Assert.Equal(AlchemyCodes.BookContent, RefusedBook(other, saved));
    }

    [Fact]
    public void ABookWithAMatchingDigestButUnknownOrInconsistentPartsIsStillRefused()
    {
        var content = Demo();
        byte[] Seal(string bodyJson)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
            return JsonSerializer.SerializeToUtf8Bytes(new { Format = 1, Content = content.Digest, Digest = Convert.ToHexString(SHA256.HashData(bodyBytes)), Body = Convert.ToBase64String(bodyBytes) });
        }
        Assert.Equal(AlchemyCodes.BookUnknownElement, RefusedBook(content, Seal("{\"Attempts\":1,\"Elements\":[\"air\",\"earth\",\"fire\",\"water\",\"zzz\"],\"Recipes\":[],\"Tried\":[]}")));
        Assert.Equal(AlchemyCodes.BookUnknownRecipe, RefusedBook(content, Seal("{\"Attempts\":1,\"Elements\":[\"air\",\"earth\",\"fire\",\"water\"],\"Recipes\":[\"zzz\"],\"Tried\":[]}")));
        Assert.Equal(AlchemyCodes.BookFormat, RefusedBook(content, Seal("{\"Attempts\":1,\"Elements\":[\"air\"],\"Recipes\":[],\"Tried\":[]}")));
        Assert.Equal(AlchemyCodes.BookFormat, RefusedBook(content, Seal("{\"Attempts\":1,\"Elements\":[\"air\",\"earth\",\"fire\",\"water\"],\"Recipes\":[\"make-steam\"],\"Tried\":[]}")));
        Assert.Equal(AlchemyCodes.BookFormat, RefusedBook(content, Seal("{\"Attempts\":-1,\"Elements\":[\"air\",\"earth\",\"fire\",\"water\"],\"Recipes\":[],\"Tried\":[]}")));
    }
}
