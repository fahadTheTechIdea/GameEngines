using EngineCommon.TestSupport;
using Xunit;

namespace EngineCommon.Tests;

/// <summary>The shared source rules that arrived with the economy engine, each run on synthetic sources so it is shown to fail: no throw hidden at the end of a coalesce, and no use of a name a layer must stay away from.</summary>
public sealed class SyntaxRuleTests
{
    [Theory]
    [InlineData("class C { object M(object a) { return a ?? throw new System.Exception(); } }", 1)]
    [InlineData("class C { object f; void M(object a) { f ??= throw new System.Exception(); } }", 1)]
    [InlineData("class C { object M(object a, object b) { return (a ?? throw new System.Exception()) ?? (b ?? throw new System.Exception()); } }", 2)]
    [InlineData("class C { object M(object a, object b) { return a ?? b; } }", 0)]
    [InlineData("class C { object f; void M(object a) { f ??= new object(); } }", 0)]
    [InlineData("class C { object M(object a) { if (a is null) throw new System.Exception(); return a; } }", 0)]
    [InlineData("class C { object M(bool a, object b) { return a ? b : throw new System.Exception(); } }", 0)]
    [InlineData("class C { string M() { // a ?? throw new Exception()\n return \"a ?? throw x\"; } }", 0)]
    public void TheThrowingCoalesceRuleItselfCanFail(string source, int findings) => Assert.Equal(findings, SyntaxGuards.ThrowingCoalesces("t.cs", source).Count);

    [Theory]
    [InlineData("class C { void M(IResourceTransaction t) { } }", 1)]
    [InlineData("class C { void M(object t) { t.CommitInventory(x); } }", 1)]
    [InlineData("class C { void M(object t) { t.CommitInventory(x); t.EvaluateInventory(x); } }", 2)]
    [InlineData("class C { void M(object w) { w.Services.Get<IResourceTransaction>(); } }", 1)]
    [InlineData("class C { void M(object t) { t.Commit(x); } }", 0)]
    [InlineData("class C { string M() { // CommitInventory\n return \"IResourceTransaction\"; } }", 0)]
    public void TheForbiddenNameRuleItselfCanFail(string source, int findings) =>
        Assert.Equal(findings, SyntaxGuards.ForbiddenNames("t.cs", source, "IResourceTransaction", "CommitInventory", "EvaluateInventory").Count);
}
