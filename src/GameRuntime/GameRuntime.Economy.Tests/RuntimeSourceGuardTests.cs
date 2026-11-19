using EngineCommon.TestSupport;
using Xunit;

namespace GameRuntime.Economy.Tests;

/// <summary>
/// The same Roslyn rules as the engine, on the game layer: no clock, no random, no floating point in the component and the bridges; every catch hands the failure on or says it as a refusal event; no throwing
/// catch-all arm and nothing thrown from the end of a coalesce; and the bridge to the SimEngine never touches the inventory's own transaction interface, so the ledger is written through its one effect.
/// </summary>
public sealed class RuntimeSourceGuardTests
{
    private static readonly string[] Projects = ["GameRuntime/GameRuntime.Economy", "GameRuntime/GameRuntime.Economy.Bridges", "GameRuntime/GameRuntime.Economy.Headless"];

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Theory]
    [InlineData("GameRuntime/GameRuntime.Economy")]
    [InlineData("GameRuntime/GameRuntime.Economy.Bridges")]
    public void TheComponentAndTheBridgesHaveNoClockNoRandomAndNoFloatingPoint(string project)
    {
        var sources = SyntaxGuards.Sources([project]);
        Assert.NotEmpty(sources);
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 2, "the scan found too few catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchHasAThrowingCatchAllArm() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));

    [Fact]
    public void NoProjectNamesArgumentOutOfRangeException() => Clean(SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));

    [Fact]
    public void NothingThrowsFromTheEndOfACoalesce()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Count >= 20, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void TheSimBridgeNeverTouchesTheInventorysTransactionInterface()
    {
        var sources = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Economy", "GameRuntime/GameRuntime.Economy.Bridges"]);
        Assert.Contains(sources, s => s.File.EndsWith("EconomySimLedger.cs", StringComparison.Ordinal));
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "IResourceTransaction", "CommitInventory", "EvaluateInventory", "ResourceInventoryIntent", "IInventory")));
        // The ledger bridge does name the one writer it goes through, so the rule is looking at the right file.
        var ledger = sources.First(s => s.File.EndsWith("EconomySimLedger.cs", StringComparison.Ordinal));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(ledger.File, ledger.Text, "StockFlowEffect"));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(ledger.File, ledger.Text.Replace("EffectRequested(", "EffectRequested(), t.CommitInventory(", StringComparison.Ordinal), "CommitInventory"));
    }
}
