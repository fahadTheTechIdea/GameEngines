using System.Text.RegularExpressions;
using EngineCommon.TestSupport;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// The source guards of the dialogue projects, on Roslyn syntax trees (see <see cref="SyntaxGuards"/>): no catch that does not report, no throwing catch-all switch arm, no
/// ArgumentOutOfRangeException, and no refusal code that no test refers to. Each rule is also run on synthetic sources so that it is shown to fail.
/// </summary>
public sealed class SyntaxGuardTests
{
    // The guard's own files hold synthetic sources full of code texts; they must not count as tests that refer to a code.
    private static readonly string[] GuardFiles = ["SyntaxGuardTests.cs"];

    /// <summary>The projects whose C# the rules read: the dialogue engine, the locale machinery it shares, and the game layer that serves it.</summary>
    private static readonly string[] Projects =
    [
        "DialogueEngine/DialogueEngine.Core", "DialogueEngine/DialogueEngine.Cli", "EngineCommon/EngineCommon.Core", "GameRuntime/GameRuntime.Dialogue", "GameRuntime/GameRuntime.Channels", "GameRuntime/GameRuntime.Briefing.Headless",
    ];

    private static readonly string[] TestProjects = ["DialogueEngine/DialogueEngine.Tests", "GameRuntime/GameRuntime.Tests"];

    private static readonly Regex CodeText = new(@"^(DLG|DIALOGUE)_[A-Z0-9_]+$", RegexOptions.CultureInvariant);

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    // ---- catch ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void NoCatchInTheDialogueProjectsSwallowsAnException()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Assert.True(sources.Count > 70, "the scan found only " + sources.Count + " files; it may be looking in the wrong place");
        var catches = sources.Sum(s => SyntaxGuards.CatchCount(s.Text));
        Assert.True(catches >= 20, "the scan found only " + catches + " catches; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SilentCatches(s.File, s.Text)));
    }

    [Fact]
    public void OnlyOneCatchRelyOnTheOutcomeMarkerAndItIsTheReloadThatClosesTheConversation()
    {
        var marked = SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.MarkedCatches(s.File, s.Text)).ToList();
        var only = Assert.Single(marked);
        Assert.Contains("DialogueRuntime.cs", only, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) { } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch { var x = 1; } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (IOException e) { _ = e; } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { if (e is null) throw; } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { Action a = () => throw e; } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { void L() { rejected.Add(e); } } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { foreach (var x in y) rejected.Add(x); } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) { /* outcome: a block comment is not the marker */ } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) {\n // a note, not an outcome: nothing is reported\n } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) { var s = \"// outcome: in a string\"; } } }", 1)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) { try { Run(); } catch (Exception) { } } } }", 2)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { throw; } } }", 0)]
    [InlineData("class C { int M() { try { Run(); } catch (Exception e) when (e is null) { return 2; } return 1; } }", 0)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { rejected.Add(e.Message); } } }", 0)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { events.Emit(new Refused(e)); } } }", 0)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception e) { if (e is null) throw; else Console.Error.WriteLine(e); } } }", 0)]
    [InlineData("class C { void M() { try { Run(); } catch (Exception) {\n // outcome: the report names it\n var x = 1; } } }", 0)]
    [InlineData("class C { void M() { // try { } catch (Exception) { }\n } }", 0)]
    public void TheCatchRuleItselfCanFail(string source, int silent) => Assert.Equal(silent, SyntaxGuards.SilentCatches("t.cs", source).Count);

    // ---- switch arms and ArgumentOutOfRangeException --------------------------------------------------------------------------------

    [Fact]
    public void NoSwitchInTheDialogueProjectsThrowsFromACatchAllArm()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Clean(sources.SelectMany(s => SyntaxGuards.ThrowingCatchAlls(s.File, s.Text)));
    }

    [Fact]
    public void NoDialogueProjectNamesArgumentOutOfRangeException()
    {
        var sources = SyntaxGuards.Sources(Projects);
        Clean(sources.SelectMany(s => SyntaxGuards.OutOfRangeExceptions(s.File, s.Text)));
    }

    [Theory]
    [InlineData("class C { int M(int k) { switch (k) { case 1: return 1; default: throw new Exception(); } } }", 1)]
    [InlineData("class C { int M(int k) { switch (k) { case 1: return 1; default: { Log(); throw new Exception(); } } } }", 1)]
    [InlineData("class C { int M(int k) { switch (k) { case 1: return 1; case _: throw new Exception(); } } }", 1)]
    [InlineData("class C { int M(int k) => k switch { 1 => 1, _ => throw new Exception() }; }", 1)]
    [InlineData("class C { int M(int k) => k switch { 1 => 1, _ => throw new ArgumentOutOfRangeException(nameof(k)) }; }", 1)]
    [InlineData("class C { int M(int k) { switch (k) { case 1: return 1; case 2: throw new Exception(); default: return 0; } } }", 0)]
    [InlineData("class C { int M(int k) => k switch { 1 => 1, _ => 0 }; }", 0)]
    [InlineData("class C { int M(int k) { switch (k) { default: Action a = () => throw new Exception(); return 0; } } }", 0)]
    [InlineData("class C { int M(Kind k) => k switch { Kind.A => 1, Kind.B => throw new Exception() }; }", 0)]
    public void TheCatchAllRuleItselfCanFail(string source, int throwing) => Assert.Equal(throwing, SyntaxGuards.ThrowingCatchAlls("t.cs", source).Count);

    [Theory]
    [InlineData("class C { void M() { throw new ArgumentOutOfRangeException(); } }", 1)]
    [InlineData("class C { void M() { try { } catch (ArgumentOutOfRangeException) { throw; } } }", 1)]
    [InlineData("class C { void M() { var s = \"ArgumentOutOfRangeException\"; /* ArgumentOutOfRangeException */ } }", 0)]
    [InlineData("class C { void M() { // ArgumentOutOfRangeException\n } }", 0)]
    public void TheOutOfRangeRuleItselfCanFail(string source, int uses) => Assert.Equal(uses, SyntaxGuards.OutOfRangeExceptions("t.cs", source).Count);

    // ---- refusal codes -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryRefusalCodeTheDialogueProjectsDeclareIsReferencedFromATest()
    {
        var declared = SyntaxGuards.Sources(Projects).SelectMany(s => SyntaxGuards.DeclaredCodes(s.Text, CodeText)).ToList();
        Assert.True(declared.Count > 55, "the scan found only " + declared.Count + " codes; it may be looking in the wrong place");
        Assert.Contains(declared, c => c.Owner == "DialogueCodes" && c.Text == "DLG_BUSY");
        Assert.Contains(declared, c => c.Owner == "DialogueChannels" && c.Text == "DLG_CHANNEL_UNKNOWN");
        var tests = SyntaxGuards.Sources(TestProjects, GuardFiles);
        Assert.True(tests.Count > 40, "the scan found only " + tests.Count + " test sources; it may be looking in the wrong place");
        Clean(SyntaxGuards.UnreferencedCodes(declared, tests.Select(t => t.Text)));
    }

    private const string Owner = "class DialogueCodes { public const string Busy = \"DLG_BUSY\"; public const string Other = \"DIALOGUE_OTHER\"; public const string NotACode = \"plain\"; private const int N = 1; }";

    [Fact]
    public void TheCodeScanFindsTheConstantsWhoseTextHasTheShapeOfACode()
    {
        Assert.Equal(["DialogueCodes.Busy (DLG_BUSY)", "DialogueCodes.Other (DIALOGUE_OTHER)"], SyntaxGuards.DeclaredCodes(Owner, CodeText).Select(c => c.ToString()));
    }

    [Theory]
    [InlineData("class T { void M() { Assert.Equal(DialogueCodes.Busy, x); Assert.Equal(DialogueCodes.Other, y); } }", 0)]
    [InlineData("class T { void M() { Assert.Equal(\"DLG_BUSY\", x); Assert.Equal(\"DIALOGUE_OTHER\", y); } }", 0)]
    [InlineData("class T { void M() { Assert.Equal(DialogueCodes.Busy, x); } }", 1)]
    [InlineData("class T { /* DialogueCodes.Busy DialogueCodes.Other */ /// DLG_BUSY DIALOGUE_OTHER\n void M() { } }", 2)]
    [InlineData("class T { void M() { Assert.Equal(DialogueCodes.BusyNot, x); Assert.Equal(Other.Busy, y); Assert.Equal(DialogueCodes.Other, z); } }", 1)]
    [InlineData("class T { void M() { var s = \"DLG_BUSY and more\"; Assert.Equal(DialogueCodes.Other, z); } }", 1)]
    public void TheCodeRuleItselfCanFail(string test, int unreferenced) =>
        Assert.Equal(unreferenced, SyntaxGuards.UnreferencedCodes(SyntaxGuards.DeclaredCodes(Owner, CodeText), [test]).Count);
}
