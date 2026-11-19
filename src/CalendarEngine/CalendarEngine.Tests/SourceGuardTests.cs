using EngineCommon.TestSupport;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>
/// The calendar engine's source rules, on the shared Roslyn guards (<see cref="SyntaxGuards"/>): the core has no clock, no random source, no floating point and no file access; no catch fails to report;
/// no switch has a throwing catch-all arm. Each rule is also run on synthetic sources so that it is shown to fail.
/// </summary>
public sealed class SourceGuardTests
{
    private static List<(string File, string Text)> Core() => SyntaxGuards.Sources(["CalendarEngine/CalendarEngine.Core"]);

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    [Fact]
    public void TheCoreHasNoClockNoRandomNoFloatingPointAndNoFileAccess()
    {
        var sources = Core();
        Assert.True(sources.Count >= 15, "found only " + sources.Count + " source files");
        Clean(sources.SelectMany(s => SyntaxGuards.ClockAndFloatViolations(s.File, s.Text)));
    }

    [Fact]
    public void NoCatchOfTheCoreOrTheSimBridgeSwallowsAFailure()
    {
        var sources = SyntaxGuards.Sources(["CalendarEngine/CalendarEngine.Core", "CalendarEngine/CalendarEngine.Sim", "CalendarEngine/CalendarEngine.Cli"]);
        Assert.True(sources.Sum(s => SyntaxGuards.CatchCount(s.Text)) >= 1, "the scan found no catch; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void NoSwitchOfTheCalendarProjectsHasAThrowingCatchAllArm()
    {
        Clean(SyntaxGuards.Sources(["CalendarEngine/CalendarEngine.Core", "CalendarEngine/CalendarEngine.Sim", "CalendarEngine/CalendarEngine.Cli"]).SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
    }

    [Theory]
    [InlineData("class C { void M() { var now = DateTime.UtcNow; } }")]
    [InlineData("class C { void M() { var t = Stopwatch.GetTimestamp(); } }")]
    [InlineData("class C { void M() { double ratio = 0.5; } }")]
    [InlineData("class C { void M() { var x = Math.Floor(y); } }")]
    [InlineData("class C { void M() { var r = new Random(4); } }")]
    [InlineData("class C { void M() { var text = File.ReadAllText(path); } }")]
    [InlineData("class C { void M() { float f = 1; } }")]
    [InlineData("class C { void M() { var ratio = 0.5; } }")]
    [InlineData("class C { void M() { Thread.Sleep(1); } }")]
    [InlineData("class C { void M() { decimal d = 1; } }")]
    public void TheClockAndFloatGuardCatchesWhatItIsFor(string code) => Assert.NotEmpty(SyntaxGuards.ClockAndFloatViolations("t.cs", code));

    [Theory]
    [InlineData("class C { // DateTime in a comment\n }")]
    [InlineData("class C { /* double in a block comment */ }")]
    [InlineData("class C { string s = \"a message about DateTime and double\"; }")]
    [InlineData("class C { void M() { var s = $\"{x} a Random message\"; } }")]
    [InlineData("class C { string s = \"\"\"raw DateTime\"\"\"; }")]
    [InlineData("class C { void M() { var c = '\"'; var after = 1; } }")]
    [InlineData("class C { string s = @\"verbatim \"\"double\"\" text\"; }")]
    [InlineData("class C { long M(long a) => Math.Max(a, 1) + Math.Abs(a); }")]
    public void TheClockAndFloatGuardIgnoresCommentsAndText(string code) => Assert.Empty(SyntaxGuards.ClockAndFloatViolations("t.cs", code));

    [Theory]
    [InlineData("class C { void M() { try { a(); } catch (Exception) { b(); } } }", 1)]
    [InlineData("class C { void M() { try { a(); } catch { } } }", 1)]
    [InlineData("class C { void M() { try { a(); } catch (IOException e) when (x) { log(e); } catch (Exception) { throw; } } }", 1)]
    [InlineData("class C { void M() { try { a(); } catch (Exception e) { throw new X(e); } } }", 0)]
    [InlineData("class C { void M() { try { a(); } catch (Exception) { if (x) { y(); } throw; } } }", 0)]
    [InlineData("class C { void M() { var catchment = 1; // catch { }\n } }", 0)]
    [InlineData("class C { void M() { try { a(); } catch (EngineRefusedException r) { events.Emit(new CalendarRefused(Id, 1)); } } }", 0)]
    public void TheCatchGuardFindsACatchThatDoesNotReportAndAcceptsOneThatDoes(string code, int silent) => Assert.Equal(silent, SyntaxGuards.SilentCatches("t.cs", code).Count);

    [Theory]
    [InlineData("class C { int M(int x) { switch (x) { case 1: return 1; default: throw new Exception(); } } }", 1)]
    [InlineData("class C { int M(int x) => x switch { A => 1, _ => throw new Exception() }; }", 1)]
    [InlineData("class C { int M(int x) { switch (x) { default: { throw new Exception(); } } } }", 1)]
    [InlineData("class C { int M(int x) => x switch { A => 1, var other => throw new Exception() }; }", 1)]
    [InlineData("class C { int M(int x) { switch (x) { case 1: return 1; } throw new Exception(); } }", 0)]
    public void TheCatchAllGuardFindsEachShapeOfThrowingCatchAll(string code, int throwing) => Assert.Equal(throwing, SyntaxGuards.ThrowingCatchAlls("t.cs", code).Count);
}
