using GameContent.Generator;
using Xunit;

namespace GameContent.Tests;

/// <summary>The generator's tables are the input and the pack JSON is the output: regenerating must give the committed files byte for byte.</summary>
public sealed class GeneratorTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("catalog-generator-");

    public void Dispose() => _dir.Delete(true);

    [Fact]
    public void RegeneratingGivesTheCommittedPacksByteForByte()
    {
        var generated = PackGenerator.Generate(_dir.FullName);
        Assert.True(generated.Count >= 9, "generated " + generated.Count + " packs");
        foreach (var pack in generated)
            foreach (var file in new[] { "pack.json", "resources.json", "recipes.json" })
            {
                var committed = Path.Combine(ShippedPackTests.PacksRoot, pack, file);
                Assert.True(File.Exists(committed), committed + " is not committed");
                Assert.Equal(File.ReadAllText(committed), File.ReadAllText(Path.Combine(_dir.FullName, pack, file)));
            }
    }

    [Fact]
    public void GeneratingTwiceGivesTheSameFiles()
    {
        var second = Directory.CreateTempSubdirectory("catalog-generator-b-");
        try
        {
            PackGenerator.Generate(_dir.FullName);
            PackGenerator.Generate(second.FullName);
            foreach (var path in Directory.GetFiles(_dir.FullName, "*.json", SearchOption.AllDirectories))
                Assert.Equal(File.ReadAllText(path), File.ReadAllText(Path.Combine(second.FullName, Path.GetRelativePath(_dir.FullName, path))));
        }
        finally { second.Delete(true); }
    }
}
