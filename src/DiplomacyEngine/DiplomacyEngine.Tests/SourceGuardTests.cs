using System.Text.RegularExpressions;
using EngineCommon.TestSupport;
using Xunit;

namespace DiplomacyEngine.Tests;

/// <summary>
/// The diplomacy engine's source rules, on the shared Roslyn guards (<see cref="SyntaxGuards"/>): the core has no clock, no random source, no floating point and no file access; no catch fails to report; no switch
/// has a throwing catch-all arm and nothing throws from the end of a coalesce; every refusal code is named by a test. Each rule is shown to fail on synthetic sources in the shared guard's own tests (EngineCommon.Tests,
/// DialogueEngine.Tests) and, here, on the core with a forbidden word added.
/// </summary>
public sealed class SourceGuardTests
{
    private static readonly string[] Projects = ["DiplomacyEngine/DiplomacyEngine.Core", "DiplomacyEngine/DiplomacyEngine.Cli"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheCoreHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess()
    {
        var sources = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Core"]);
        Assert.True(sources.Count >= 20, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 5, "the scan found too few catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArmAndNoArgumentOutOfRangeExceptionIsNamed()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
    }

    [Fact]
    public void NothingThrowsFromTheEndOfACoalesce()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Count >= 20);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void EveryRefusalCodeIsNamedByATest()
    {
        var owner = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Core"]).First(s => s.File.EndsWith("DipCodes.cs", StringComparison.Ordinal));
        var declared = SyntaxGuards.DeclaredCodes(owner.Text, new Regex("^DIP_[A-Z0-9_]+$", RegexOptions.CultureInvariant));
        Assert.True(declared.Count >= 30);
        var tests = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Tests"]);
        Clean(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }

    [Fact]
    public void TheCoreAndTheGuardsAgreeAboutWhatTheyAreLookingAt()
    {
        // A guard that cannot fail says nothing: the same rules, run on the core with a forbidden word added, must find it.
        var core = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Core"]).First(s => s.File.EndsWith("Words.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text));
        var spare = core.Text.Replace("namespace DiplomacyEngine;", "namespace DiplomacyEngine;\nstatic class Spare { static double x = 0.5; static object F(object a) => a ?? throw new System.Exception(); static void G() { try { } catch (System.Exception) { } } }", StringComparison.Ordinal);
        Assert.NotEqual(core.Text, spare);
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, spare));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(core.File, spare));
        Assert.NotEmpty(SyntaxGuards.SilentCatches(core.File, spare));
        var codes = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Core"]).First(s => s.File.EndsWith("DipCodes.cs", StringComparison.Ordinal));
        var withNew = codes.Text.Replace("public const string Capacity = \"DIP_CAPACITY\";", "public const string Capacity = \"DIP_CAPACITY\";\n    public const string Unnamed = \"DIP_NOBODY_NAMES_THIS\";", StringComparison.Ordinal);
        var declared = SyntaxGuards.DeclaredCodes(withNew, new Regex("^DIP_[A-Z0-9_]+$", RegexOptions.CultureInvariant));
        var tests = SyntaxGuards.Sources(["DiplomacyEngine/DiplomacyEngine.Tests"]).Where(t => !t.File.EndsWith("SourceGuardTests.cs", StringComparison.Ordinal));
        Assert.Single(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }
}
