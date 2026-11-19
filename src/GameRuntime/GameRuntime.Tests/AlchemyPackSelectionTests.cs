using GameRuntime.Alchemy.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>The headless alchemy game picks a catalog pack by id from JSON at run time.</summary>
public sealed class AlchemyPackSelectionTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("alchemy-pack-");

    public void Dispose() => _dir.Delete(true);

    private (int Exit, string Out, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = AlchemyHeadlessGame.Run([.. args, "--audit", System.IO.Path.Combine(_dir.FullName, "audit.jsonl")], output, error, null);
        return (exit, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("alchemy")]
    [InlineData("survival")]
    [InlineData("space")]
    public void EachPackIsSelectableByIdAndPlaysDeterministically(string pack)
    {
        string[] args = ["--pack", pack, "--frame-ms", "500", "--seconds", "60", "--player", "methodical", "--seed", "5"];
        var first = Run(args);
        var second = Run(args);
        Assert.Equal(0, first.Exit);
        Assert.Contains("source=pack:" + pack + "@1.0.0", first.Out);
        Assert.Equal(first.Out, second.Out);
    }


    public static TheoryData<string> EveryShippedPack()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(AlchemyContentSource.DefaultPacksPath).Order(StringComparer.Ordinal))
            if (Path.GetFileName(directory) != "gear") data.Add(Path.GetFileName(directory));   // the gear pack is a vocabulary: no materials to play
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryShippedPack))]
    public void EveryShippedPackPlaysInTheHeadlessGameStraightFromItsJson(string pack)
    {
        var run = Run("--pack", pack, "--frame-ms", "500", "--seconds", "20", "--player", "curious", "--seed", "2");
        Assert.Equal(0, run.Exit);
        Assert.Contains("source=pack:" + pack + "@", run.Out);
    }

    [Fact]
    public void AVocabularyPackHasNothingToPlayAndIsRefusedClearly()
    {
        var run = Run("--pack", "gear", "--frame-ms", "500", "--seconds", "5");
        Assert.Equal(2, run.Exit);
        Assert.NotEmpty(run.Error);
    }
    [Fact]
    public void DifferentPacksGiveDifferentContent()
    {
        var a = Run("--pack", "alchemy", "--frame-ms", "500", "--seconds", "5").Out;
        var b = Run("--pack", "factory", "--frame-ms", "500", "--seconds", "5").Out;
        Assert.NotEqual(a.Split(' ').First(p => p.StartsWith("content=")), b.Split(' ').First(p => p.StartsWith("content=")));
    }

    [Fact]
    public void ANewPackOnDiskPlaysWithoutARebuild()
    {
        var packs = System.IO.Path.Combine(_dir.FullName, "packs");
        Directory.CreateDirectory(System.IO.Path.Combine(packs, "mini"));
        File.WriteAllText(System.IO.Path.Combine(packs, "mini", "pack.json"),
            """{ "format": 1, "id": "mini", "version": "2.0.0", "title": "Mini", "language": "en", "world": "timeless", "genres": ["test"], "dependencies": [], "license": { "spdx": "CC0-1.0", "provenance": "original", "author": "tests" }, "locales": [], "documents": ["d.json"] }""");
        File.WriteAllText(System.IO.Path.Combine(packs, "mini", "d.json"),
            """
            { "format": 1, "pack": "mini", "resources": [
              { "id": "mini.a", "name": "A", "class": "raw", "unit": "kg", "origin": "natural", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": {} },
              { "id": "mini.b", "name": "B", "class": "raw", "unit": "kg", "origin": "natural", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": {} },
              { "id": "mini.c", "name": "C", "class": "product", "unit": "kg", "origin": "crafted", "stack": { "max": 10 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": null, "tags": [], "properties": {} } ],
              "recipes": [ { "id": "mini.mix", "name": "Mix", "kind": "combine", "inputs": [{ "resource": "mini.a", "amount": 1 }, { "resource": "mini.b", "amount": 1 }], "catalysts": [], "tools": [], "outputs": [{ "resource": "mini.c", "amount": 1 }], "byproducts": [], "drops": [], "station": null, "skill": null, "environment": [], "duration": { "value": 5, "unit": "s" }, "quality": null, "reverse": null } ] }
            """);
        var run = Run("--pack", "mini", "--packs", packs, "--frame-ms", "500", "--seconds", "30", "--player", "methodical");
        Assert.Equal(0, run.Exit);
        Assert.Contains("source=pack:mini@2.0.0", run.Out);
        Assert.Contains("mini.a+mini.b", run.Out);
    }

    [Fact]
    public void AnUnknownOrInvalidPackIsRefusedWithAnExitCodeAndItsCode()
    {
        var unknown = Run("--pack", "nope", "--frame-ms", "500", "--seconds", "5");
        Assert.Equal(2, unknown.Exit);
        Assert.Contains("CATALOG_FILE_MISSING", unknown.Error);
        Assert.Equal(2, Run("--pack", "alchemy", "--content", "x.json").Exit);
        Assert.Equal(2, Run("--packs", "somewhere").Exit);
    }
}
