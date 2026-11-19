using EngineCommon.TestSupport;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace EngineCommon.Graph.Tests;

/// <summary>
/// The graph library's source rules on the shared Roslyn guards: no clock, no random source, no floating point, no file access; no catch fails to report (there is none); no throwing catch-all arm; nothing throws from a
/// coalesce; and, stricter than the shared rules, nothing throws at all and no hash-ordered collection is used (a refusal is a code, and iteration order never depends on a hash).
/// </summary>
public sealed class SourceGuardTests
{
    private const string Project = "EngineCommon/EngineCommon.Graph";

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    private static List<(string File, string Text)> Library()
    {
        var sources = SyntaxGuards.Sources([Project]);
        Assert.True(sources.Count >= 12, "found only " + sources.Count + " source files");
        return sources;
    }

    [Fact]
    public void TheLibraryHasNoClockNoRandomSourceNoFloatingPointAndNoFileAccess() =>
        Clean(Library().SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));

    [Fact]
    public void NoCatchNoThrowingCatchAllNoOutOfRangeExceptionAndNoThrowingCoalesce()
    {
        var sources = Library();
        Assert.Equal(0, sources.Sum(s => SyntaxGuards.CatchCount(s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCoalesces(s.File, s.Text)));
    }

    [Fact]
    public void NothingThrowsAndNothingUsesAHashOrderedCollection()
    {
        var sources = Library();
        Clean(sources.SelectMany(s => SyntaxGuards.Parse(s.Text).DescendantNodes().Where(n => n is ThrowStatementSyntax or ThrowExpressionSyntax).Select(n => SyntaxGuards.Where(s.File, n) + ": throw")));
        Clean(sources.SelectMany(s => SyntaxGuards.ForbiddenNames(s.File, s.Text, "HashSet", "Dictionary", "Hashtable", "SortedSet", "ConcurrentDictionary")));
    }

    [Fact]
    public void TheRulesCanFailOnTheLibraryWithAForbiddenWordAdded()
    {
        var core = Library().First(s => s.File.EndsWith("Dijkstra.cs", StringComparison.Ordinal));
        Assert.Empty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text.Replace("public static class Dijkstra", "public static class Dijkstra { static double x = 0.5; }\npublic static class Spare", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations(core.File, core.Text.Replace("var heap = scratch.Heap;", "var heap = scratch.Heap; var when = DateTime.UtcNow;", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ThrowingCoalesces(core.File, core.Text.Replace("var heap = scratch.Heap;", "var heap = scratch.Heap ?? throw new System.Exception();", StringComparison.Ordinal)));
        Assert.NotEmpty(SyntaxGuards.ForbiddenNames(core.File, core.Text.Replace("var heap = scratch.Heap;", "var heap = scratch.Heap; var seen = new HashSet<int>();", StringComparison.Ordinal), "HashSet"));
        Assert.Contains(SyntaxGuards.Parse(core.Text.Replace("var heap = scratch.Heap;", "var heap = scratch.Heap; throw new System.Exception();", StringComparison.Ordinal)).DescendantNodes(), n => n is ThrowStatementSyntax);
    }
}
