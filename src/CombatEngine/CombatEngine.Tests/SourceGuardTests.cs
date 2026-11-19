using Xunit;

namespace CombatEngine.Tests;

/// <summary>Layer rules kept as tests: one rule computes damage, and the engine owns no item numbers and reads no catalog.</summary>
public sealed class SourceGuardTests
{
    internal static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "CombatEngine.Core"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    internal static Dictionary<string, string> CoreSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "CombatEngine.Core"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Directory.GetFiles(Path.Combine(directory!.FullName, "CombatEngine.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetFileName(f), f => File.ReadAllText(f));
    }

    [Fact]
    public void OnlyTheDamageRuleUsesTheArmorCurve()
    {
        var users = CoreSources().Where(s => s.Value.Contains("ArmorConstant", StringComparison.Ordinal) || s.Value.Contains("NonPiercingCutPercent", StringComparison.Ordinal) || s.Value.Contains("MinimumDamage", StringComparison.Ordinal))
            .Select(s => s.Key).Order().ToArray();
        Assert.Equal(["CombatRules.cs", "DamageRule.cs"], users);
    }

    [Fact]
    public void EveryDamageComesFromTheOneRule()
    {
        var callers = CoreSources().Where(s => s.Value.Contains("DamageRule.Compute", StringComparison.Ordinal)).Select(s => s.Key).Order().ToArray();
        Assert.Equal(["CombatResolver.cs", "DamageRule.cs"], callers.Where(c => c != "DamageRule.cs").Append("DamageRule.cs").Order().ToArray());
        Assert.Single(CoreSources()["CombatResolver.cs"].Split("DamageRule.Compute").Skip(1));
    }

    [Fact]
    public void TheEngineKnowsNoCatalogStatNoItemAndNoWeaponByName()
    {
        foreach (var (file, text) in CoreSources())
        {
            Assert.DoesNotContain("gear.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("GameContent", text, StringComparison.Ordinal);
            foreach (var name in new[] { "rifle", "sword", "arrow", "musket", "cannon", "railgun", "longbow" })
                if (file != "CombatEngine.Core.csproj") Assert.False(text.Contains("\"" + name, StringComparison.OrdinalIgnoreCase), file + " names a weapon: " + name);
        }
        Assert.DoesNotContain("GameContent", File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CombatEngine.Core", "CombatEngine.Core.csproj")), StringComparison.Ordinal);
    }


    [Fact]
    public void NoFloatingPointAndNoCultureDependentFormattingInTheEngine()
    {
        foreach (var (file, text) in CoreSources())
        {
            foreach (var forbidden in new[] { " double ", " float ", " decimal ", "(double)", "(float)", "(decimal)", "Math.Sqrt", "Math.Pow", "Math.Round", "CurrentCulture", ".ToString(\"", "double.", "decimal." })
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal), file + " uses " + forbidden.Trim());
        }
        var sources = CoreSources();
        foreach (var file in new[] { "CombatRoll.cs", "StateHash.cs" })
            Assert.Contains("CultureInfo.InvariantCulture", sources[file], StringComparison.Ordinal);
    }

    [Fact]
    public void TheEngineKeepsCheckedArithmeticOn()
    {
        var project = File.ReadAllText(Path.Combine(ProjectRoot(), "CombatEngine.Core", "CombatEngine.Core.csproj"));
        Assert.Contains("<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("unchecked", string.Concat(CoreSources().Values), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheStandingRuleDecidesMoraleAndRout()
    {
        var users = CoreSources().Where(s => s.Value.Contains("Morale.", StringComparison.Ordinal) || s.Value.Contains("RoutStrengthPercent", StringComparison.Ordinal)).Select(s => s.Key).Order().ToArray();
        Assert.Equal(["CombatRules.cs", "StandingRule.cs"], users.Where(u => u != "CombatRulesLoader.cs").ToArray());
    }
    [Fact]
    public void TheEngineIsPure()
    {
        foreach (var (file, text) in CoreSources())
        {
            foreach (var forbidden in new[] { "DateTime", "Stopwatch", "Environment.TickCount", "new Random", "Random.Shared", "File.", "Console.", "static readonly Dictionary", "static Dictionary" })
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal) && file != "CombatRulesLoader.cs", file + " uses " + forbidden);
        }
    }
}
