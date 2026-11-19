using System.Text.Json.Nodes;
using AlchemyEngine;
using Beep.SimEngine;
using Beep.SimEngine.Hosting;
using GameRuntime.AlchemySim.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The SimEngine to alchemy reader, over the engine's own Planet X production content and variants of it.</summary>
public sealed class AlchemySimReaderTests
{
    private const string Smelt = "space.smelt.planetx";
    private const string Assemble = "space.assemble.parts";

    private static SimRecipeView Bridge(Action<Dictionary<string, JsonNode>>? patch = null)
    {
        var documents = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var file in new[] { "planet-x-recipes.json", "workshop-gates.json", "workshop-world.json" })
            documents[file] = JsonNode.Parse(File.ReadAllText(Path.Combine(WorkshopWorld.ContentDirectory, file)))!;
        patch?.Invoke(documents);
        var files = new PreloadedFileSource();
        foreach (var file in Directory.GetFiles(WorkshopWorld.ContentDirectory, "*.json"))
        {
            var name = Path.GetFileName(file);
            files.Add(name, documents.TryGetValue(name, out var patched) ? patched.ToJsonString() : File.ReadAllText(file));
        }
        var holder = new SimRecipeViewHolder();
        var factory = WorkshopWorld.Factory(WorkshopWorld.Definition(holder, new DirectorTickets(), new SimEventOutbox()), files);
        WorkshopWorld.NewGame(factory, 1);
        return holder.View;
    }

    private static JsonObject Batch(string id, string inputs, string tools, string outputs, string? entityOutput = null)
    {
        var json = $$"""
        { "id": "{{id}}", "mode": "batch", "cancel_policy": "return_inputs", "category": "manufacturing",
          "inputs": {{inputs}}, "tools": {{tools}}, "tool_consumption": {}, "outputs": {{outputs}}, "operating_inputs": [],
          "input_ports": {{Ports(inputs, "space.feed")}}, "tool_ports": {{Ports(tools, "space.tool")}}, "output_ports": {{Ports(outputs, "space.product")}},
          "operating_ports": {},
          "duration": { "numerator": 30, "denominator": 1, "unit": "second" } }
        """;
        var node = (JsonObject)JsonNode.Parse(json)!;
        if (entityOutput is not null)
        {
            node["entity_output"] = entityOutput;
            node["output_home"] = "unassigned";
        }
        return node;
    }

    private static string Ports(string bill, string port)
    {
        var resources = ((JsonObject)JsonNode.Parse(bill)!).Select(p => "\"" + p.Key + "\": \"" + port + "\"");
        return "{ " + string.Join(", ", resources) + " }";
    }

    private static void Add(Dictionary<string, JsonNode> documents, JsonNode transformation) =>
        ((JsonArray)documents["planet-x-recipes.json"]["transformations"]!).Add(transformation);

    private static string ExcludedCode(SimRecipeView view, string id) => view.Excluded.Single(e => e.TransformationId == id).Code;

    // ---- the real content ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReaderBridgesTheEnginesOwnRecipesWithoutCopyingTheirFacts()
    {
        var view = Bridge();
        Assert.Equal([Assemble, Smelt], view.Recipes.Select(r => r.Id).Order().ToArray());
        Assert.Empty(view.Excluded);

        // Alchemy sees resources only: which are consumed, held and made. No quantity is in its content.
        var smelt = view.Alchemy.Recipes.Single(r => r.Id == Smelt);
        Assert.Equal(["space.planetx.ore"], smelt.Inputs);
        Assert.Equal(["space.crucible"], smelt.Catalysts);
        Assert.Equal(["space.alloy", "space.slag"], smelt.Outputs);
        Assert.Empty(smelt.Environment);

        // The quantities, the tool and the duration stay the engine's, read-only.
        var facts = view.Recipes.Single(r => r.Id == Smelt).Facts;
        Assert.Equal(Fix.FromInt(10), facts.Inputs.Lines.Single().Amount);
        Assert.Equal(Fix.FromInt(1), facts.Tools.Lines.Single().Amount);
        Assert.Equal([Fix.FromInt(6), Fix.FromInt(4)], facts.Outputs.Lines.Select(l => l.Amount).ToArray());
        Assert.False(facts.Duration.IsZero);
        Assert.Equal("manufacturing", facts.Category);
        Assert.Equal(Fix.Zero, facts.Cash);
        Assert.Single(facts.InputPorts);
        Assert.Equal(2, facts.OutputPorts.Count);

        var assemble = view.Recipes.Single(r => r.Id == Assemble);
        Assert.Equal(Fix.FromInt(3), assemble.Facts.Inputs.Lines.Single().Amount);
        Assert.Equal([new SimProcessor("space.assembler", "space.operation")], assemble.Processors);
        Assert.Equal("space.discover.assemble", assemble.TechnologyId);
    }

    [Fact]
    public void ElementsAreTheEnginesResourcesAndOnlyUnmadeOnesStartKnown()
    {
        var view = Bridge();
        Assert.Equal(7, view.Alchemy.Elements.Count);
        Assert.Equal(["space.blank", "space.crucible", "space.fixture", "space.planetx.ore"], view.Alchemy.Elements.Where(e => e.Starting).Select(e => e.Id).Order().ToArray());
        Assert.Equal(["space.alloy", "space.finished", "space.slag"], view.Alchemy.Elements.Where(e => !e.Starting).Select(e => e.Id).Order().ToArray());
        Assert.All(view.Alchemy.Elements, e => Assert.Contains("material", e.Properties));
    }

    [Fact]
    public void TheAlchemyQueriesAndDiscoveryBookRunOverTheEnginesRecipes()
    {
        var view = Bridge();
        var rules = new AlchemyRules(view.Alchemy);
        Assert.Equal([Smelt], rules.WhatMakes("space.slag").Select(r => r.Id).ToArray());
        var preview = rules.WhatDoes(["space.planetx.ore", "space.crucible"], []);
        Assert.Equal(CombineOutcome.Reacted, preview.Outcome);
        Assert.Equal(Smelt, preview.RecipeId);
        Assert.Equal(["space.crucible"], preview.Kept);

        var book = new DiscoveryBook(view.Alchemy);
        var learned = book.Record(rules.Combine(["space.blank", "space.fixture"], new CombineContext(1, book.Attempts, [])));
        Assert.Equal(Assemble, learned.NewRecipe);
        Assert.Equal(["space.finished"], learned.NewElements);
    }

    [Fact]
    public void ReadingTwiceGivesTheSameView()
    {
        var first = Bridge();
        var second = Bridge();
        Assert.Equal(first.Alchemy.Digest, second.Alchemy.Digest);
        Assert.Equal(first.Recipes.Select(r => r.Id + r.TechnologyId), second.Recipes.Select(r => r.Id + r.TechnologyId));
    }

    // ---- what alchemy cannot represent is excluded with a code, never half bridged ------------------------------------------

    [Fact]
    public void ATransformationNoTechnologyUnlocksIsExcludedAsNotGated()
    {
        var view = Bridge(d => Add(d, Batch("space.extra", """{ "space.slag": 2 }""", """{ "space.fixture": 1 }""", """{ "space.finished": 1 }""")));
        Assert.Equal(SimRecipeCodes.NotGated, ExcludedCode(view, "space.extra"));
        Assert.Equal(2, view.Recipes.Count);
    }

    [Fact]
    public void AToolThatIsAlsoAnInputIsExcluded()
    {
        var view = Bridge(d => Add(d, Batch("space.extra", """{ "space.blank": 2 }""", """{ "space.blank": 1 }""", """{ "space.finished": 1 }""")));
        Assert.Equal(SimRecipeCodes.ToolIsInput, ExcludedCode(view, "space.extra"));
    }

    [Fact]
    public void ATransformationThatConsumesNothingIsExcluded()
    {
        var view = Bridge(d => Add(d, Batch("space.extra", "{}", """{ "space.fixture": 1 }""", """{ "space.alloy": 1 }""")));
        Assert.Equal(SimRecipeCodes.NoInputs, ExcludedCode(view, "space.extra"));
    }

    [Fact]
    public void ATransformationThatMakesAnEntityIsExcluded()
    {
        var view = Bridge(d =>
        {
            ((JsonArray)d["workshop-world.json"]["entities"]!).Add(JsonNode.Parse("""{ "id": "space.token", "kind": "entity", "components": {} }"""));
            Add(d, Batch("space.extra", """{ "space.blank": 1 }""", "{}", "{}", entityOutput: "space.token"));
        });
        Assert.Equal(SimRecipeCodes.EntityOutput, ExcludedCode(view, "space.extra"));
    }

    [Fact]
    public void AContinuousTransformationIsExcluded()
    {
        var view = Bridge(d => Add(d, JsonNode.Parse("""
            { "id": "space.extra", "mode": "continuous", "cancel_policy": "stop", "category": "manufacturing", "duration": null,
              "inputs": [ { "resource": "space.planetx.ore", "quantity": 1, "period": { "numerator": 1, "denominator": 1, "unit": "minute" } } ],
              "tools": {}, "tool_consumption": [], "input_ports": { "space.planetx.ore": "space.feed" }, "tool_ports": {}, "operating_inputs": [], "operating_ports": {},
              "outputs": [ { "resource": "space.alloy", "quantity": 1, "period": { "numerator": 1, "denominator": 1, "unit": "minute" } } ],
              "output_ports": { "space.alloy": "space.product" } }
            """)!));
        Assert.Equal(SimRecipeCodes.Continuous, ExcludedCode(view, "space.extra"));
    }

    [Fact]
    public void AGateWithPrerequisitesIsExcludedAsAmbiguous()
    {
        var view = Bridge(d =>
        {
            Add(d, Batch("space.extra", """{ "space.slag": 2 }""", """{ "space.fixture": 1 }""", """{ "space.finished": 1 }"""));
            ((JsonArray)d["workshop-gates.json"]["technology_transformation_unlocks"]!).Add(JsonNode.Parse("""{ "id": "space.discover.extra.unlock", "definition": "space.extra" }"""));
            var technology = JsonNode.Parse("""
                { "id": "space.discover.extra", "prerequisites": ["space.discover.smelt"], "unlocks": ["space.discover.extra.unlock"],
                  "player_orderable": true, "priority": 30, "automatic_supply": false, "on_complete": "space.discover.complete",
                  "nodes": [ { "id": "study", "executor": "work.timed", "duration": { "numerator": 1, "denominator": 1, "unit": "minute" } } ] }
                """)!;
            ((JsonArray)d["workshop-gates.json"]["technologies"]!).Add(technology);
        });
        Assert.Equal(SimRecipeCodes.GateAmbiguous, ExcludedCode(view, "space.extra"));
        Assert.Equal(2, view.Recipes.Count);
    }

    [Fact]
    public void TwoTransformationsThatCouldBothMatchOneCombinationAreRefusedByTheAlchemyLoader()
    {
        // A second gated recipe over the same resources would be an ambiguous alchemy recipe: the loader's own refusal applies.
        var exception = Assert.Throws<AlchemyRefusedException>(() => Bridge(d =>
        {
            Add(d, Batch("space.extra", """{ "space.blank": 4 }""", """{ "space.fixture": 1 }""", """{ "space.finished": 2 }"""));
            ((JsonArray)d["workshop-gates.json"]["technology_transformation_unlocks"]!).Add(JsonNode.Parse("""{ "id": "space.discover.extra.unlock", "definition": "space.extra" }"""));
            ((JsonArray)d["workshop-gates.json"]["technologies"]!).Add(JsonNode.Parse("""
                { "id": "space.discover.extra", "prerequisites": [], "unlocks": ["space.discover.extra.unlock"],
                  "player_orderable": true, "priority": 30, "automatic_supply": false, "on_complete": "space.discover.complete",
                  "nodes": [ { "id": "study", "executor": "work.timed", "duration": { "numerator": 1, "denominator": 1, "unit": "minute" } } ] }
                """)!);
        }));
        Assert.Equal(AlchemyCodes.ContentAmbiguousRecipe, exception.Code);
    }
}
