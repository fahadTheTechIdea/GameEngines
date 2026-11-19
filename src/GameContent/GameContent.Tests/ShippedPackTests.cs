using System.Text.Json.Nodes;
using AlchemyEngine;
using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>Every pack shipped under <c>src/GameContent/Packs</c> validates, round-trips, converts to Alchemy and composes into a SimEngine world.</summary>
public sealed class ShippedPackTests
{
    public static readonly string PacksRoot = Path.Combine(AppContext.BaseDirectory, "Packs");

    public static TheoryData<string> GenrePacks()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(PacksRoot).Order(StringComparer.Ordinal))
            if (Path.GetFileName(directory) != "core") data.Add(Path.GetFileName(directory));
        return data;
    }

    public static TheoryData<string> AllPacks()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(PacksRoot).Order(StringComparer.Ordinal)) data.Add(Path.GetFileName(directory));
        return data;
    }


    /// <summary>Every pack that has materials to convert; the gear vocabulary defines stats and skills only.</summary>
    public static TheoryData<string> ConvertiblePacks()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(PacksRoot).Order(StringComparer.Ordinal))
            if (Path.GetFileName(directory) != "gear") data.Add(Path.GetFileName(directory));
        return data;
    }
    private static CatalogSet Load(string id) => CatalogLoader.Load(new DirectoryCatalogSource(PacksRoot), id).Required();

    [Fact]
    public void TheGenreEquipmentAndEraPacksAndTheCorePackAreShipped()
    {
        var ids = Directory.GetDirectories(PacksRoot).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "alchemy", "city", "colony", "core", "arms-ancient", "arms-fantasy", "arms-future", "arms-medieval", "arms-modern", "ages", "electronics", "factory", "farming", "gear", "horizons", "kingdom", "kitchen", "matter", "military", "mining", "naval", "pharmacy", "rpg", "space", "survival", "tycoon", "trading", "wasteland" }.Order(StringComparer.Ordinal).ToArray(), ids);
    }

    [Theory]
    [MemberData(nameof(AllPacks))]
    public void EveryPackValidatesIsOriginalAndBuildsOnCore(string id)
    {
        var set = Load(id);
        Assert.Equal(id, set.Root.Id);
        Assert.Equal("original", set.Root.License.Provenance);
        if (id == "core") Assert.Empty(set.Root.Dependencies);
        else if (id != "gear") Assert.Contains(set.Packs, p => p.Id == "core");   // a vocabulary needs nothing; every other pack builds on the shared materials
    }

    private static readonly string[] InfrastructureGenres = ["equipment", "eras"];

    [Theory]
    [MemberData(nameof(GenrePacks))]
    public void EveryGenrePackIsBroadNotATokenExample(string id)
    {
        var pack = Load(id).Root;
        if (pack.Genres.Any(g => InfrastructureGenres.Contains(g))) return;   // vocabularies, eras and materials are checked by their own tests
        if (pack.Items.Count > 0)
        {
            Assert.True(pack.Items.Count >= 12, id + " has dozens of items");
            Assert.True(pack.Items.Select(i => i.Class).Distinct().Count() >= 3, id + " covers several item classes");
            Assert.Equal(pack.Items.Count, pack.Items.Select(i => i.Resource).Distinct().Count());
            return;
        }
        Assert.InRange(pack.Resources.Count, 12, 200);
        Assert.InRange(pack.Recipes.Count, 20, 200);
        Assert.True(pack.Stations.Count >= 2, id + " defines its stations");
        Assert.True(pack.Recipes.Select(r => r.Kind).Distinct().Count() >= 6, id + " uses a variety of recipe kinds");
        Assert.True(pack.Resources.Select(r => r.Class).Distinct().Count() >= 3, id + " uses a variety of resource classes");
        Assert.Equal(pack.Resources.Count, pack.Resources.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void NoIdIsUsedTwiceAcrossTheShippedPacks()
    {
        var ids = new List<string>();
        foreach (var directory in Directory.GetDirectories(PacksRoot))
        {
            var root = Load(Path.GetFileName(directory)).Root;
            ids.AddRange(root.Resources.Select(r => r.Id).Concat(root.Recipes.Select(r => r.Id)).Concat(root.Stations.Select(s => s.Id)).Concat(root.Skills.Select(s => s.Id)));
        }
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AllPacks))]
    public void EveryPackHasNoFreeResourceCycleAndNoUnreachableResource(string id)
    {
        var report = CatalogValidator.Validate(Load(id));
        Assert.False(report.Has(CatalogCodes.FreeCycle));
        Assert.False(report.Has(CatalogCodes.Unreachable));
        Assert.True(report.IsValid, report.ToString());
    }

    [Theory]
    [MemberData(nameof(AllPacks))]
    public void EveryPackRoundTripsToTheSameBytes(string id)
    {
        var set = Load(id);
        var first = Canonical(set);
        var again = CatalogLoader.Load(new MemoryCatalogSource(first), id).Required();
        var second = Canonical(again);
        Assert.Equal(first.Keys.Order().ToArray(), second.Keys.Order().ToArray());
        foreach (var (path, text) in first) Assert.Equal(text, second[path]);
        // And the canonical form means the same as the authored files.
        foreach (var pack in set.Packs)
            Assert.Equal(pack.Resources.Count, again.Packs.Single(p => p.Id == pack.Id).Resources.Count);
    }

    /// <summary>Every pack of a set as a pack.json plus one canonical document.</summary>
    private static Dictionary<string, string> Canonical(CatalogSet set)
    {
        var files = new Dictionary<string, string>();
        foreach (var pack in set.Packs)
        {
            var manifest = new JsonObject
            {
                ["format"] = 1, ["id"] = pack.Id, ["version"] = pack.Version, ["title"] = pack.Title, ["language"] = pack.Language, ["world"] = Names<WorldKind>.Of(pack.World),
                ["locales"] = new JsonArray(),
                ["genres"] = new JsonArray(pack.Genres.Select(g => (JsonNode)g).ToArray()),
                ["dependencies"] = new JsonArray(pack.Dependencies.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id, ["version"] = d.Version }).ToArray()),
                ["license"] = new JsonObject { ["spdx"] = pack.License.Spdx, ["provenance"] = pack.License.Provenance, ["author"] = pack.License.Author },
                ["documents"] = new JsonArray("all.json"),
            };
            files[pack.Id + "/pack.json"] = manifest.ToJsonString();
            files[pack.Id + "/all.json"] = CatalogWriter.Document(pack);
        }
        return files;
    }

    [Theory]
    [MemberData(nameof(ConvertiblePacks))]
    public void EveryPackConvertsToAlchemyAndEveryConvertedRecipeReactsInTheAlchemyRules(string id)
    {
        var conversion = CatalogToAlchemy.Convert(Load(id));
        Assert.True(conversion.RecipeCount >= 5, id + " keeps a usable number of recipes");
        var rules = new AlchemyRules(conversion.Content);
        foreach (var recipe in conversion.Content.Recipes)
        {
            var answer = rules.WhatDoes(recipe.Inputs.Concat(recipe.Catalysts).ToArray(), recipe.Environment);
            Assert.Equal(CombineOutcome.Reacted, answer.Outcome);
            Assert.Equal(recipe.Id, answer.RecipeId);
        }
        // Nothing was lost without a note: every pack recipe is either converted or has a refusal.
        var converted = conversion.Content.Recipes.Select(r => r.Id).ToHashSet();
        var refused = conversion.Report.Refusals.Select(n => n.Subject).ToHashSet();
        foreach (var recipe in Load(id).Recipes)
            Assert.True(converted.Contains(recipe.Id) || converted.Any(c => c.StartsWith(recipe.Id + ".drop-", StringComparison.Ordinal)) || refused.Contains(recipe.Id), recipe.Id + " is neither converted nor refused");
    }

    [Theory]
    [MemberData(nameof(ConvertiblePacks))]
    public void AConversionIsDeterministic(string id)
    {
        var first = CatalogToAlchemy.Convert(Load(id));
        var second = CatalogToAlchemy.Convert(Load(id));
        Assert.Equal(first.Content.Digest, second.Content.Digest);
        var simFirst = CatalogToSim.Convert(Load(id));
        var simSecond = CatalogToSim.Convert(Load(id));
        Assert.Equal(simFirst.Files.OrderBy(f => f.Key).Select(f => f.Value), simSecond.Files.OrderBy(f => f.Key).Select(f => f.Value));
    }

    [Theory]
    [MemberData(nameof(ConvertiblePacks))]
    public void NoShippedPackRecipeIsRefusedByAlchemy(string id)
    {
        var refusals = CatalogToAlchemy.Convert(Load(id)).Report.Refusals.ToList();
        Assert.True(refusals.Count == 0, string.Join("\n", refusals.Select(n => n.Code + " " + n.Subject)));
    }

    [Theory]
    [MemberData(nameof(ConvertiblePacks))]
    public void EveryPackComposesIntoASimEngineWorldThroughItsOwnReaders(string id)
    {
        var set = Load(id);
        var conversion = CatalogToSim.Convert(set);
        Assert.True(conversion.Transformations.Count >= 5, id + " keeps a usable number of transformations");
        var game = SimWorld.Compose(conversion);
        Assert.NotNull(game);
        // Every recipe is either a transformation or has a refusal code; none vanish.
        var refused = conversion.Report.Refusals.Select(n => n.Subject).ToHashSet();
        foreach (var recipe in set.Recipes) Assert.True(conversion.Transformations.Contains(recipe.Id) || refused.Contains(recipe.Id), recipe.Id + " is neither converted nor refused");
    }
}
