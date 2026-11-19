using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>The command line tool and the pack scaffold, including their error paths.</summary>
public sealed class CliTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("catalog-cli-");

    public void Dispose() => _dir.Delete(true);

    private static (int Exit, string Out, string Error) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        return (CatalogCli.Run(args, output, error), output.ToString(), error.ToString());
    }

    /// <summary>A packs folder holding a copy of core, into which a test can scaffold.</summary>
    private string Library()
    {
        var packs = Path.Combine(_dir.FullName, "packs");
        foreach (var file in Directory.GetFiles(Path.Combine(ShippedPackTests.PacksRoot, "core")))
        {
            var target = Path.Combine(packs, "core", Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return packs;
    }

    [Fact]
    public void UsageErrorsExitTwoAndPrintTheUsage()
    {
        foreach (var args in new[] { Array.Empty<string>(), ["list"], ["frobnicate", "x"], ["validate", ShippedPackTests.PacksRoot], ["new-pack", "x", "y"] })
        {
            var result = Cli(args);
            Assert.Equal(2, result.Exit);
            Assert.Contains("usage:", result.Error);
        }
        Assert.Equal(2, Cli("list", Path.Combine(_dir.FullName, "missing")).Exit);
        Assert.Contains("not found", Cli("validate", Path.Combine(_dir.FullName, "missing"), "core").Error);
    }

    [Fact]
    public void AnUnknownOrBrokenPackExitsOneWithItsFindings()
    {
        var unknown = Cli("validate", ShippedPackTests.PacksRoot, "nope");
        Assert.Equal(1, unknown.Exit);
        Assert.Contains(CatalogCodes.FileMissing, unknown.Out);
        var packs = Library();
        Directory.CreateDirectory(Path.Combine(packs, "broken"));
        File.WriteAllText(Path.Combine(packs, "broken", "pack.json"), "{ not json");
        var broken = Cli("validate", packs, "broken");
        Assert.Equal(1, broken.Exit);
        Assert.Contains(CatalogCodes.NotJson, broken.Out);
        Assert.Equal(1, Cli("validate-all", packs).Exit);
    }

    [Fact]
    public void ListValidateStatsAndTheConvertersWorkOnAShippedPack()
    {
        Assert.Contains("survival", Cli("list", ShippedPackTests.PacksRoot).Out.Split('\n', StringSplitOptions.TrimEntries));
        var valid = Cli("validate", ShippedPackTests.PacksRoot, "survival");
        Assert.Equal(0, valid.Exit);
        Assert.Contains("valid pack=survival", valid.Out);
        Assert.Contains("warnings=0", valid.Out);
        Assert.Contains("pack survival:", Cli("stats", ShippedPackTests.PacksRoot, "survival").Out);
        var alchemy = Cli("alchemy", ShippedPackTests.PacksRoot, "survival", "--notes");
        Assert.Equal(0, alchemy.Exit);
        Assert.Contains("refused=0", alchemy.Out);
        Assert.Contains("reduced CATALOG_ALCHEMY_", alchemy.Out);
        var output = Path.Combine(_dir.FullName, "sim");
        var sim = Cli("sim", ShippedPackTests.PacksRoot, "survival", "--out", output);
        Assert.Equal(0, sim.Exit);
        Assert.True(File.Exists(Path.Combine(output, "recipes.json")));
        Assert.True(File.Exists(Path.Combine(output, "manifest.json")));
    }

    [Fact]
    public void ValidateAllCoversEveryShippedPackAndTheLibraryRules()
    {
        var result = Cli("validate-all", ShippedPackTests.PacksRoot);
        Assert.Equal(0, result.Exit);
        Assert.Contains("valid packs=28", result.Out);
    }

    [Fact]
    public void AScaffoldedPackValidatesConvertsAndCannotBeScaffoldedTwice()
    {
        var packs = Library();
        var made = Cli("new-pack", packs, "harbor", "naval", "Test Author");
        Assert.Equal(0, made.Exit);
        Assert.Contains("harbor", made.Out);
        var validate = Cli("validate", packs, "harbor");
        Assert.Equal(0, validate.Exit);
        Assert.Contains("warnings=0", validate.Out);
        Assert.Equal(0, Cli("alchemy", packs, "harbor").Exit);
        Assert.Equal(0, Cli("sim", packs, "harbor").Exit);
        Assert.Equal(0, Cli("validate-all", packs).Exit);
        var again = Cli("new-pack", packs, "harbor", "naval", "Test Author");
        Assert.Equal(2, again.Exit);
        Assert.Contains("already exists", again.Error);
    }

    [Fact]
    public void AScaffoldRefusesBadIdsGenresAndAuthorsWithoutWritingAnything()
    {
        var packs = Library();
        foreach (var (id, genre, author) in new[] { ("Bad Id", "naval", "A"), ("ok", "Not A Slug", "A"), ("ok", "naval", ""), ("ok", "naval", "say \"hi\""), ("9lives", "naval", "A") })
        {
            Assert.Equal(2, Cli("new-pack", packs, id, genre, author).Exit);
            Assert.False(Directory.Exists(Path.Combine(packs, id)));
        }
    }
}
